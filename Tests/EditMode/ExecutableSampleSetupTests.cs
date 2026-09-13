using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Samples;
using UnityEditor;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ExecutableSampleSetupTests
    {
        private const string Root =
            "Assets/Samples/Quiet Static Library/0.1.0/" +
            "Executable Toolkit Example";

        [Test]
        public void ResolveRequiredScenes_ReturnsDeterministicSampleOrder()
        {
            string[] candidates =
            {
                Root + "/Scenes/QuietStaticSampleContent.unity",
                "Assets/Other.unity",
                Root + "/Scenes/QuietStaticSampleBootstrap.unity",
                Root + "/Scenes/QuietStaticSampleSystems.unity"
            };

            bool success = ExecutableSampleSetup.TryResolveRequiredScenePaths(
                Root,
                candidates,
                out string[] paths,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(
                paths.Select(System.IO.Path.GetFileNameWithoutExtension),
                Is.EqualTo(ExecutableSampleSetup.RequiredSceneNames));
        }

        [Test]
        public void ResolveRequiredScenes_RejectsMissingOrDuplicateExactNames()
        {
            string[] missing =
            {
                Root + "/Scenes/QuietStaticSampleBootstrap.unity",
                Root + "/Scenes/QuietStaticSampleSystems.unity"
            };

            Assert.That(
                ExecutableSampleSetup.TryResolveRequiredScenePaths(
                    Root,
                    missing,
                    out _,
                    out string missingError),
                Is.False);
            StringAssert.Contains("QuietStaticSampleContent", missingError);
            StringAssert.Contains("found 0", missingError);

            string[] duplicate =
            {
                Root + "/Scenes/QuietStaticSampleBootstrap.unity",
                Root + "/Scenes/QuietStaticSampleSystems.unity",
                Root + "/Scenes/QuietStaticSampleContent.unity",
                "Assets/Other/QuietStaticSampleContent.unity"
            };

            Assert.That(
                ExecutableSampleSetup.TryResolveRequiredScenePaths(
                    Root,
                    duplicate,
                    out _,
                    out string duplicateError),
                Is.False);
            StringAssert.Contains("found 2", duplicateError);
        }

        [Test]
        public void OrderBuildSettings_PutsRequiredScenesFirstAndPreservesRest()
        {
            var existing = new EditorBuildSettingsScene(
                "Assets/Existing.unity",
                false);
            var duplicate = new EditorBuildSettingsScene(
                "Assets/Existing.unity",
                true);
            var unresolved = new EditorBuildSettingsScene(string.Empty, false);
            var second = new EditorBuildSettingsScene("Assets/Second.unity", true);
            var current = new[]
            {
                existing,
                new EditorBuildSettingsScene(
                    Root + "/Scenes/QuietStaticSampleSystems.unity",
                    false),
                duplicate,
                unresolved,
                second
            };
            string[] required = ExecutableSampleSetup.RequiredSceneNames
                .Select(name => $"{Root}/Scenes/{name}.unity")
                .ToArray();

            EditorBuildSettingsScene[] ordered =
                ExecutableSampleSetup.OrderBuildSettings(current, required);

            Assert.That(
                ordered.Take(3).Select(scene => scene.path),
                Is.EqualTo(required));
            Assert.That(
                ordered.Take(3).All(scene => scene.enabled),
                Is.True);
            Assert.That(ordered, Has.Length.EqualTo(7));
            Assert.That(ordered[3], Is.SameAs(existing));
            Assert.That(ordered[4], Is.SameAs(duplicate));
            Assert.That(ordered[5], Is.SameAs(unresolved));
            Assert.That(ordered[6], Is.SameAs(second));
        }
    }
}
