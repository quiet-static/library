using NUnit.Framework;
using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class InteractableUnityEventTests
    {
        [Test]
        public void Interact_UsesSuccessfulActorlessInteractionPath()
        {
            GameObject gameObject = new("UI Interaction");

            try
            {
                Interactable interactable =
                    gameObject.AddComponent<Interactable>();
                bool succeeded = false;
                interactable.InteractionSucceeded += (_, actor) =>
                {
                    Assert.That(actor, Is.Null);
                    succeeded = true;
                };

                interactable.Interact();

                Assert.That(succeeded, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
