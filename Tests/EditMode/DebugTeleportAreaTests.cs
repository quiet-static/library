using NUnit.Framework;
using System.Linq;
using System.Reflection;
using QuietStatic.Toolkit.Characters.Player;
using QuietStatic.Toolkit.DebugTools;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class DebugTeleportAreaTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void TeleportMovesPlayerAndPreservesControllerState(bool enabled)
        {
            var group = new GameObject("Area");
            var player = new GameObject("Player");
            try
            {
                var destination = new GameObject("Destination").transform;
                destination.SetParent(group.transform);
                destination.SetPositionAndRotation(new Vector3(12, 3, -4), Quaternion.Euler(0, 90, 0));
                var area = group.AddComponent<DebugTeleportArea>();
                var controller = player.AddComponent<CharacterController>();
                var motor = player.AddComponent<CharacterMotor>();
                typeof(CharacterMotor).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(motor, Vector3.down * 30);
                controller.enabled = enabled;
                Assert.That(area.Teleport(player.transform, 0), Is.True);
                Assert.That(Vector3.Distance(player.transform.position, destination.position), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(player.transform.rotation, destination.rotation), Is.LessThan(0.001f));
                Assert.That(controller.enabled, Is.EqualTo(enabled));
                Assert.That(motor.VerticalVelocity, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(group);
            }
        }

        [Test]
        public void LifecycleCallbacksRegisterAndRemoveDestinations()
        {
            var group = new GameObject("Area");
            try
            {
                var area = group.AddComponent<DebugTeleportArea>();
                // EditMode does not dispatch MonoBehaviour enable callbacks automatically.
                typeof(DebugTeleportArea).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(area, null);
                Assert.That(DebugTeleportArea.ActiveAreas.Contains(area), Is.True);
                typeof(DebugTeleportArea).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(area, null);
                Assert.That(DebugTeleportArea.ActiveAreas.Contains(area), Is.False);
            }
            finally { Object.DestroyImmediate(group); }
        }
    }
}

