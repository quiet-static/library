using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SceneBootstrapProfileTests
    {
        private SceneBootstrapProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<SceneBootstrapProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void PersistentScenes_AreNormalizedDistinctAndOrdered()
        {
            SetField("persistentScenes", new[]
            {
                new SceneReference(" Systems "),
                new SceneReference("Player"),
                new SceneReference("Systems"),
                new SceneReference(string.Empty),
            });

            Assert.That(
                profile.PersistentSceneNames,
                Is.EqualTo(new[] { "Systems", "Player" }));
        }

        [Test]
        public void InitialRequest_UsesConfiguredSceneGroupsAndCleanup()
        {
            SetField("initialScene", new SceneReference("Title"));
            SetField("additionalInitialScenes", new[]
            {
                new SceneReference("Lighting"),
            });
            SetField("initialScenesToKeep", new[]
            {
                new SceneReference("Loading"),
            });
            SetField("unloadOtherScenes", false);

            SceneTransitionRequest request =
                profile.CreateInitialTransitionRequest();

            Assert.That(request.TargetSceneName, Is.EqualTo("Title"));
            Assert.That(request.AdditionalScenesToLoad, Is.EqualTo(new[] { "Lighting" }));
            Assert.That(request.AdditionalScenesToKeep, Is.EqualTo(new[] { "Loading" }));
            Assert.That(request.UnloadOtherScenes, Is.False);
        }

        [Test]
        public void ReferencedScenes_IncludeEveryProfileSceneOnce()
        {
            SetField("persistentScenes", new[] { new SceneReference("Systems") });
            SetField("initialScene", new SceneReference("Title"));
            SetField("additionalInitialScenes", new[] { new SceneReference("UI") });
            SetField("initialScenesToKeep", new[] { new SceneReference("Systems") });

            Assert.That(
                profile.ReferencedSceneNames,
                Is.EqualTo(new[] { "Systems", "Title", "UI" }));
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(SceneBootstrapProfile).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(profile, value);
        }
    }
}
