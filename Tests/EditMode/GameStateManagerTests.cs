using System;
using System.Collections.Generic;
using NUnit.Framework;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    public sealed class GameStateManagerTests
    {
        private GameObject managerObject;
        private GameStateManager manager;
        private Action<string, string> stateChangedHandler;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Game State Manager");
            manager = managerObject.AddComponent<GameStateManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (stateChangedHandler != null)
            {
                GameStateManager.OnGameStateChanged -= stateChangedHandler;
            }

            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SetState_TrimsAndPublishesCanonicalEvent()
        {
            string reportedPreviousState = null;
            string reportedCurrentState = null;

            stateChangedHandler = (previousState, currentState) =>
            {
                reportedPreviousState = previousState;
                reportedCurrentState = currentState;
            };

            GameStateManager.OnGameStateChanged += stateChangedHandler;

            bool changed = manager.SetState("  Playing  ");

            Assert.That(changed, Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo("Playing"));
            Assert.That(reportedPreviousState, Is.EqualTo("Starting"));
            Assert.That(reportedCurrentState, Is.EqualTo("Playing"));
        }

        [Test]
        public void SetState_WithCurrentState_IsIdempotent()
        {
            manager.SetState("Playing");

            int notificationCount = 0;
            stateChangedHandler = (_, _) => notificationCount++;
            GameStateManager.OnGameStateChanged += stateChangedHandler;

            bool changed = manager.SetState("Playing");

            Assert.That(changed, Is.False);
            Assert.That(notificationCount, Is.Zero);
        }

        [Test]
        public void SetState_WithWhitespaceOnly_IsRejected()
        {
            LogAssert.Expect(
                LogType.Warning,
                "WARNING: Game State Manager from SetState: " +
                "GameStateManager cannot switch to an empty state."
            );

            bool changed = manager.SetState("   ");

            Assert.That(changed, Is.False);
            Assert.That(manager.CurrentState, Is.EqualTo("Starting"));
        }

        [Test]
        public void SetState_ReentrantTransitionPublishesEachStateInOrder()
        {
            List<string> managerStates = new List<string>();

            stateChangedHandler = (_, currentState) =>
            {
                managerStates.Add(currentState);

                if (currentState == "Playing")
                {
                    manager.SetState("Paused");
                }
            };

            GameStateManager.OnGameStateChanged += stateChangedHandler;

            bool changed = manager.SetState("Playing");

            Assert.That(changed, Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo("Paused"));
            Assert.That(
                managerStates,
                Is.EqualTo(new[] { "Playing", "Paused" })
            );
        }
    }
}
