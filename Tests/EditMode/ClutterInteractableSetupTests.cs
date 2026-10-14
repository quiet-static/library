using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using QuietStatic.Toolkit.Interactions;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ClutterInteractableSetupTests
    {
        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void SetupAddsFlavorInteractionTargetingAndHighlighting()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(target.GetComponent<Collider>());

            FlavorTextInteraction flavor = ClutterInteractableWindow.SetUp(target);

            Assert.That(flavor, Is.Not.Null);
            Assert.That(target.GetComponent<Interactable>(), Is.Not.Null);
            Assert.That(target.GetComponent<InteractionHighlighter>(), Is.Not.Null);
            Assert.That(target.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(target.GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void SetupIsIdempotent()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);

            ClutterInteractableWindow.SetUp(target);
            ClutterInteractableWindow.SetUp(target);

            Assert.That(target.GetComponents<FlavorTextInteraction>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponents<Interactable>(), Has.Length.EqualTo(1));
            Assert.That(target.GetComponents<InteractionHighlighter>(), Has.Length.EqualTo(1));
        }
    }
}
