using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.State;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CompatibilityBoundaryTests
    {
        private const string CompatibilityAssemblyName =
            "QuietStatic.Compatibility.Runtime";

        [Test]
        public void CompatibilityAssembly_DependsOnCoreInOneDirection()
        {
            Assembly coreAssembly = typeof(GameStateManager).Assembly;
            Assembly compatibilityAssembly =
                Assembly.Load(CompatibilityAssemblyName);

            string[] compatibilityReferences = compatibilityAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            string[] coreReferences = coreAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(
                compatibilityReferences,
                Does.Contain(coreAssembly.GetName().Name));
            Assert.That(
                coreReferences,
                Does.Not.Contain(CompatibilityAssemblyName));
        }

        [Test]
        public void LegacyBridges_AreOwnedByCompatibilityAssembly()
        {
            Assembly compatibilityAssembly =
                Assembly.Load(CompatibilityAssemblyName);
            string[] legacyTypeNames =
            {
                "QuietStatic.Characters.AnimationController",
                "QuietStatic.Characters.CharacterMotor",
                "QuietStatic.Characters.MovementStateController"
            };

            foreach (string typeName in legacyTypeNames)
            {
                Assert.That(
                    compatibilityAssembly.GetType(typeName),
                    Is.Not.Null,
                    $"{typeName} should remain in {CompatibilityAssemblyName}.");
            }
        }
    }
}
