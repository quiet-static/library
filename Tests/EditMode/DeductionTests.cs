using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Deductions;
using QuietStatic.Toolkit.Flags;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class DeductionTests
    {
        private GameObject flagObject;
        private FlagManager flags;

        [SetUp]
        public void SetUp()
        {
            flagObject = new GameObject("Flags");
            flags = flagObject.AddComponent<FlagManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(flagObject);
        }

        [Test]
        public void CategoryController_KeepsOnlyLatestAnswer()
        {
            GameObject controllerObject = new("Deduction Categories");
            DeductionCategoryController controller =
                controllerObject.AddComponent<DeductionCategoryController>();
            DeductionCategoryController.Category category =
                new DeductionCategoryController.Category();
            SetField(category, "answerFlags", new[] { "suspect.sam", "suspect.coworker" });
            SetField(controller, "categories", new[] { category });

            try
            {
                flags.SetFlag("suspect.sam");
                flags.SetFlag("suspect.coworker");

                Assert.That(flags.HasFlag("suspect.sam"), Is.False);
                Assert.That(flags.HasFlag("suspect.coworker"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void Evaluator_SelectsHighestPriorityMatchingResult()
        {
            flags.SetFlag("suspect.coworker");
            DeductionResultDefinition fallback = CreateResult("fallback", 0);
            DeductionResultDefinition specific = CreateResult(
                "specific",
                10,
                new FlagRequirement(
                    FlagRequirementMode.All,
                    new[] { "suspect.coworker" }));

            try
            {
                Assert.That(
                    DeductionEvaluator.FindResult(
                        new[] { fallback, specific },
                        flags),
                    Is.SameAs(specific));
            }
            finally
            {
                Object.DestroyImmediate(fallback);
                Object.DestroyImmediate(specific);
            }
        }

        [Test]
        public void Result_CanRequireAndForbidAnswers()
        {
            DeductionResultDefinition result = CreateResult(
                "simple-theft",
                10,
                new FlagRequirement(
                    FlagRequirementMode.All,
                    new[] { "suspect.coworker" }),
                new FlagRequirement(
                    FlagRequirementMode.NotAny,
                    new[] { "motive.bait" }));

            try
            {
                flags.SetFlag("suspect.coworker");
                Assert.That(result.Matches(flags), Is.True);
                flags.SetFlag("motive.bait");
                Assert.That(result.Matches(flags), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(result);
            }
        }

        private static DeductionResultDefinition CreateResult(
            string id,
            int priority,
            params FlagRequirement[] requirements)
        {
            DeductionResultDefinition result =
                ScriptableObject.CreateInstance<DeductionResultDefinition>();
            SetField(result, "id", id);
            SetField(result, "priority", priority);
            SetField(result, "requirements", requirements);
            return result;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
