using NUnit.Framework;
using QuietStatic.Toolkit.Input;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class TestInputBehaviour : MonoBehaviour
    {
    }

    public sealed class InputModeManagerTests
    {
        private GameObject managerObject;
        private GameObject inputObject;
        private InputModeManager manager;
        private TestInputBehaviour input;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Input Mode Manager");
            manager = managerObject.AddComponent<InputModeManager>();
            inputObject = new GameObject("Gameplay Input");
            input = inputObject.AddComponent<TestInputBehaviour>();
            manager.RegisterGameplayInput(input);
            manager.EnableGameplayInput();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(inputObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void GameplayClaim_DisablesAndRestoresDesiredGroup()
        {
            Assert.That(input.enabled, Is.True);

            InputBlockHandle claim = manager.AcquireInputBlock(
                InputBlockGroups.Gameplay,
                "Test");

            Assert.That(input.enabled, Is.False);
            Assert.That(manager.IsInputBlocked(InputBlockGroups.Gameplay), Is.True);

            claim.Dispose();

            Assert.That(input.enabled, Is.True);
            Assert.That(manager.BlockedGroups, Is.EqualTo(InputBlockGroups.None));
        }

        [Test]
        public void OverlappingClaims_RequireEveryOwnerToRelease()
        {
            InputBlockHandle first =
                manager.AcquireInputBlock(InputBlockGroups.Gameplay, "First");
            InputBlockHandle second =
                manager.AcquireInputBlock(InputBlockGroups.Gameplay, "Second");

            Assert.That(
                manager.GetInputBlockOwners(InputBlockGroups.Gameplay),
                Is.EquivalentTo(new[] { "First", "Second" }));

            first.Dispose();
            Assert.That(input.enabled, Is.False);

            second.Dispose();
            Assert.That(input.enabled, Is.True);
        }

        [Test]
        public void ClaimForOtherGroup_DoesNotSuppressGameplay()
        {
            InputBlockHandle claim =
                manager.AcquireInputBlock(InputBlockGroups.UI, "Menu Overlay");

            Assert.That(input.enabled, Is.True);
            Assert.That(manager.IsInputBlocked(InputBlockGroups.UI), Is.True);
            Assert.That(manager.IsInputBlocked(InputBlockGroups.Gameplay), Is.False);

            claim.Dispose();
        }

        [Test]
        public void ReleasingClaim_DoesNotOverrideNewDesiredMode()
        {
            InputBlockHandle claim =
                manager.AcquireInputBlock(InputBlockGroups.Gameplay, "Activity");

            manager.EnableUIInput();
            claim.Dispose();

            Assert.That(input.enabled, Is.False);
            Assert.That(manager.CurrentMode, Is.EqualTo("UI"));
        }

        [Test]
        public void DisposingHandleTwice_IsSafe()
        {
            InputBlockHandle claim =
                manager.AcquireInputBlock(InputBlockGroups.Gameplay);

            claim.Dispose();
            claim.Dispose();

            Assert.That(input.enabled, Is.True);
            Assert.That(manager.BlockedGroups, Is.EqualTo(InputBlockGroups.None));
        }
    }
}
