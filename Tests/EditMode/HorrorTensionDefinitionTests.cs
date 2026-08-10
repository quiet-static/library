using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Horror;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class HorrorTensionDefinitionTests
    {
        [Test]
        public void SelectState_UsesHighestPriorityMatchThenDefault()
        {
            HorrorTensionDefinition definition = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            GameObject flagsObject = new("Flags");
            FlagManager flags = flagsObject.AddComponent<FlagManager>();
            try
            {
                HorrorTensionDefinition.State calm = State("calm", 0, null);
                HorrorTensionDefinition.State uneasy = State("uneasy", 10, "ReadLetter");
                HorrorTensionDefinition.State threat = State("threat", 20, "PowerOut");
                Set(definition, "defaultStateId", "calm");
                Set(definition, "states", new[] { calm, uneasy, threat });

                Assert.That(definition.SelectState(flags), Is.SameAs(calm));
                flags.SetFlag("ReadLetter");
                Assert.That(definition.SelectState(flags), Is.SameAs(uneasy));
                flags.SetFlag("PowerOut");
                Assert.That(definition.SelectState(flags), Is.SameAs(threat));
            }
            finally
            {
                Object.DestroyImmediate(flagsObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void SelectState_EqualPriorityUsesAuthoredOrder()
        {
            HorrorTensionDefinition definition = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            GameObject flagsObject = new("Flags");
            FlagManager flags = flagsObject.AddComponent<FlagManager>();
            try
            {
                HorrorTensionDefinition.State first = State("first", 10, "active");
                HorrorTensionDefinition.State second = State("second", 10, "active");
                Set(definition, "states", new[] { first, second });
                flags.SetFlag("active");
                Assert.That(definition.SelectState(flags), Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(flagsObject);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FindState_NormalizesLookupButRemainsCaseSensitive()
        {
            HorrorTensionDefinition definition = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            try
            {
                HorrorTensionDefinition.State state = State("Threat", 1, null);
                Set(definition, "states", new[] { state });
                Assert.That(definition.FindState("  Threat "), Is.SameAs(state));
                Assert.That(definition.FindState("threat"), Is.Null);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SelectState_WithInvalidDefaultAndNoMatch_ReturnsNull()
        {
            HorrorTensionDefinition definition = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            try
            {
                Set(definition, "defaultStateId", "missing");
                Set(definition, "states", new[] { State("calm", 0, null) });
                Assert.That(definition.SelectState(), Is.Null);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static HorrorTensionDefinition.State State(string id, int priority, string flag)
        {
            HorrorTensionDefinition.State state = new();
            Set(state, "id", id);
            Set(state, "priority", priority);
            if (flag != null)
                Set(state, "activationRequirement", new FlagRequirement(
                    FlagRequirementMode.All, new[] { flag }));
            return state;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }
}
