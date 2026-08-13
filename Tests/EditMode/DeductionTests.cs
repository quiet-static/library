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
            SetField(flags, "persistBetweenScenes", false);
            InvokeLifecycle(flags, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            InvokeLifecycle(flags, "OnDestroy");
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
            InvokeLifecycle(controller, "OnEnable");

            try
            {
                flags.SetFlag("suspect.sam");
                flags.SetFlag("suspect.coworker");

                Assert.That(flags.HasFlag("suspect.sam"), Is.False);
                Assert.That(flags.HasFlag("suspect.coworker"), Is.True);
            }
            finally
            {
                InvokeLifecycle(controller, "OnDisable");
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
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field == null)
                {
                    continue;
                }

                field.SetValue(target, value);
                return;
            }

            Assert.Fail($"Missing field '{name}'.");
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (method == null)
                {
                    continue;
                }

                method.Invoke(target, null);
                return;
            }

            Assert.Fail($"Missing lifecycle method '{methodName}'.");
        }
    }
}
