using System;
using NUnit.Framework;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class PlayerManagerTests
    {
        private GameObject managerObject;
        private GameObject firstPlayer;
        private GameObject secondPlayer;
        private PlayerManager manager;
        private Action<GameObject, GameObject> playerChangedHandler;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Player Manager");
            manager = managerObject.AddComponent<PlayerManager>();
            firstPlayer = new GameObject("First Player");
            secondPlayer = new GameObject("Second Player");
        }

        [TearDown]
        public void TearDown()
        {
            if (playerChangedHandler != null)
            {
                manager.PlayerChanged -= playerChangedHandler;
            }

            UnityEngine.Object.DestroyImmediate(secondPlayer);
            UnityEngine.Object.DestroyImmediate(firstPlayer);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SetPlayer_ChangesPlayerAndReportsOldAndNewReferences()
        {
            manager.SetPlayer(firstPlayer);

            GameObject reportedOldPlayer = null;
            GameObject reportedNewPlayer = null;
            int notificationCount = 0;
            playerChangedHandler = (oldPlayer, newPlayer) =>
            {
                reportedOldPlayer = oldPlayer;
                reportedNewPlayer = newPlayer;
                notificationCount++;
            };
            manager.PlayerChanged += playerChangedHandler;

            manager.SetPlayer(secondPlayer);

            Assert.That(manager.Player, Is.SameAs(secondPlayer));
            Assert.That(reportedOldPlayer, Is.SameAs(firstPlayer));
            Assert.That(reportedNewPlayer, Is.SameAs(secondPlayer));
            Assert.That(notificationCount, Is.EqualTo(1));
        }

        [Test]
        public void SetPlayer_WithCurrentPlayer_DoesNotNotifyAgain()
        {
            manager.SetPlayer(firstPlayer);

            int notificationCount = 0;
            playerChangedHandler = (_, _) => notificationCount++;
            manager.PlayerChanged += playerChangedHandler;

            manager.SetPlayer(firstPlayer);

            Assert.That(manager.Player, Is.SameAs(firstPlayer));
            Assert.That(notificationCount, Is.Zero);
        }

        [Test]
        public void SetPlayer_WithNull_ClearsPlayerAndReportsChange()
        {
            manager.SetPlayer(firstPlayer);

            GameObject reportedOldPlayer = null;
            GameObject reportedNewPlayer = firstPlayer;
            int notificationCount = 0;
            playerChangedHandler = (oldPlayer, newPlayer) =>
            {
                reportedOldPlayer = oldPlayer;
                reportedNewPlayer = newPlayer;
                notificationCount++;
            };
            manager.PlayerChanged += playerChangedHandler;

            manager.SetPlayer(null);

            Assert.That(manager.Player, Is.Null);
            Assert.That(reportedOldPlayer, Is.SameAs(firstPlayer));
            Assert.That(reportedNewPlayer, Is.Null);
            Assert.That(notificationCount, Is.EqualTo(1));
        }

        [Test]
        public void SetPlayer_WithNull_ReplacesDestroyedUnityObjectReference()
        {
            manager.SetPlayer(firstPlayer);
            GameObject destroyedPlayer = firstPlayer;
            UnityEngine.Object.DestroyImmediate(firstPlayer);
            firstPlayer = null;

            Assert.That(destroyedPlayer == null, Is.True);
            Assert.That(ReferenceEquals(destroyedPlayer, null), Is.False);

            GameObject reportedOldPlayer = null;
            GameObject reportedNewPlayer = destroyedPlayer;
            int notificationCount = 0;
            playerChangedHandler = (oldPlayer, newPlayer) =>
            {
                reportedOldPlayer = oldPlayer;
                reportedNewPlayer = newPlayer;
                notificationCount++;
            };
            manager.PlayerChanged += playerChangedHandler;

            manager.SetPlayer(null);

            Assert.That(ReferenceEquals(manager.Player, null), Is.True);
            Assert.That(
                ReferenceEquals(reportedOldPlayer, destroyedPlayer),
                Is.True
            );
            Assert.That(ReferenceEquals(reportedNewPlayer, null), Is.True);
            Assert.That(notificationCount, Is.EqualTo(1));
        }
    }
}
