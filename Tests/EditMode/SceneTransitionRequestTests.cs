using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;

namespace QuietStatic.Tests.EditMode
{
    public class SceneTransitionRequestTests
    {
        [Test]
        public void Constructor_NormalizesTargetAndSceneLists()
        {
            SceneTransitionRequest request =
                new SceneTransitionRequest(
                    "  Area1  ",
                    new[] { " Player ", "", "Player", null },
                    new[] { " Bootstrapper ", "Bootstrapper", " " }
                );

            Assert.That(request.TargetSceneName, Is.EqualTo("Area1"));
            Assert.That(
                request.AdditionalScenesToLoad,
                Is.EqualTo(new[] { "Player" })
            );
            Assert.That(
                request.AdditionalScenesToKeep,
                Is.EqualTo(new[] { "Bootstrapper" })
            );
        }

        [Test]
        public void KeepsScene_UsesExactSceneIdentity()
        {
            SceneTransitionRequest request =
                new SceneTransitionRequest(
                    "Area1",
                    additionalScenesToKeep: new[] { "Player" }
                );

            Assert.That(request.KeepsScene("Player"), Is.True);
            Assert.That(request.KeepsScene("player"), Is.False);
            Assert.That(request.KeepsScene(null), Is.False);
        }

        [Test]
        public void Constructor_PreservesLoadAndRetentionOrder()
        {
            SceneTransitionRequest request =
                new SceneTransitionRequest(
                    "Area1",
                    new[] { "Lighting", "Player" },
                    new[] { "Bootstrapper", "Player" },
                    unloadOtherScenes: false
                );

            Assert.That(
                request.AdditionalScenesToLoad,
                Is.EqualTo(new[] { "Lighting", "Player" })
            );
            Assert.That(
                request.AdditionalScenesToKeep,
                Is.EqualTo(new[] { "Bootstrapper", "Player" })
            );
            Assert.That(request.UnloadOtherScenes, Is.False);
        }
    }
}
