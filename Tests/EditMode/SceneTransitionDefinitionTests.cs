using System;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SceneTransitionDefinitionTests
    {
        private GameObject root;
        private SceneTransitionDefinition definition;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Scene Transition Definition");
            definition = root.AddComponent<SceneTransitionDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void Apply_UsesFirstEligibleExactOrdinalResponse()
        {
            int blockedCount = 0;
            int firstEligibleCount = 0;
            int laterEligibleCount = 0;
            int caseVariantCount = 0;
            SetResponses(
                CreateResponse(
                    "Blocked",
                    "route.entry",
                    new FlagRequirement(
                        FlagRequirementMode.All,
                        new[] { "test.missing.requirement" }),
                    () => blockedCount++),
                CreateResponse(
                    "First eligible",
                    "route.entry",
                    new FlagRequirement(),
                    () => firstEligibleCount++),
                CreateResponse(
                    "Later eligible",
                    "route.entry",
                    new FlagRequirement(),
                    () => laterEligibleCount++),
                CreateResponse(
                    "Case variant",
                    "Route.Entry",
                    new FlagRequirement(),
                    () => caseVariantCount++));

            bool appliedExact = definition.Apply("route.entry");
            bool appliedCaseVariant = definition.Apply("Route.Entry");

            Assert.That(appliedExact, Is.True);
            Assert.That(appliedCaseVariant, Is.True);
            Assert.That(blockedCount, Is.Zero);
            Assert.That(firstEligibleCount, Is.EqualTo(1));
            Assert.That(laterEligibleCount, Is.Zero);
            Assert.That(caseVariantCount, Is.EqualTo(1));
        }

        [Test]
        public void Apply_ReturnsFalseWhenNoEligibleResponseExists()
        {
            int responseCount = 0;
            SetResponses(
                CreateResponse(
                    "Different route",
                    "route.other",
                    new FlagRequirement(),
                    () => responseCount++),
                CreateResponse(
                    "Blocked route",
                    "route.entry",
                    new FlagRequirement(
                        FlagRequirementMode.All,
                        new[] { "test.missing.requirement" }),
                    () => responseCount++));
            ExpectNoEligibleWarning("route.entry");

            bool applied = definition.Apply("route.entry");

            Assert.That(applied, Is.False);
            Assert.That(responseCount, Is.Zero);
        }

        [Test]
        public void Apply_AlwaysInvokesSceneEnteredEventWithoutAMatchingResponse()
        {
            int enteredCount = 0;
            UnityEvent entered = new UnityEvent();
            entered.AddListener(() => enteredCount++);
            SetField(definition, "onEntered", entered);
            SetResponses(
                CreateResponse(
                    "Different route",
                    "route.other",
                    new FlagRequirement(),
                    null));
            ExpectNoEligibleWarning("route.unknown");

            bool applied = definition.Apply("route.unknown");

            Assert.That(applied, Is.False);
            Assert.That(enteredCount, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Apply_UnconditionedTransitionIsANoOp(string conditionId)
        {
            int enteredCount = 0;
            int responseCount = 0;
            SetField(
                definition,
                "onEntered",
                CreateEvent(() => enteredCount++));
            SetResponses(
                CreateResponse(
                    "Blank response",
                    string.Empty,
                    new FlagRequirement(),
                    () => responseCount++));

            bool applied = definition.Apply(conditionId);

            Assert.That(applied, Is.False);
            Assert.That(enteredCount, Is.Zero);
            Assert.That(responseCount, Is.Zero);
        }

        [Test]
        public void FindInScene_FindsInactiveDescendantInLoadedScene()
        {
            GameObject child = new GameObject("Inactive Definition");
            child.transform.SetParent(root.transform);
            UnityEngine.Object.DestroyImmediate(definition);
            definition = child.AddComponent<SceneTransitionDefinition>();
            child.SetActive(false);

            Assert.That(
                SceneTransitionDefinition.FindInScene(root.scene),
                Is.SameAs(definition));
        }

        [Test]
        public void FindInScene_RejectsInvalidScene()
        {
            Assert.That(
                SceneTransitionDefinition.FindInScene(default),
                Is.Null);
        }

        private void SetResponses(
            params SceneTransitionDefinition.Response[] responses)
        {
            SetField(definition, "responses", responses);
            Assert.That(definition.Responses, Is.EqualTo(responses));
        }

        private void ExpectNoEligibleWarning(string conditionId)
        {
            LogAssert.Expect(
                LogType.Warning,
                $"WARNING: {definition.name} from Apply: " +
                $"SceneTransitionDefinition in scene '{root.scene.name}' " +
                $"has no eligible response for condition '{conditionId}'.");
        }

        private static SceneTransitionDefinition.Response CreateResponse(
            string label,
            string conditionId,
            FlagRequirement requirement,
            Action listener)
        {
            SceneTransitionDefinition.Response response = new();
            UnityEvent entered = new UnityEvent();
            if (listener != null)
            {
                entered.AddListener(listener.Invoke);
            }

            SetField(response, "label", label);
            SetField(response, "conditionId", conditionId);
            SetField(response, "requirement", requirement);
            SetField(response, "onEntered", entered);
            return response;
        }

        private static UnityEvent CreateEvent(Action listener)
        {
            UnityEvent result = new UnityEvent();
            result.AddListener(listener.Invoke);
            return result;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
