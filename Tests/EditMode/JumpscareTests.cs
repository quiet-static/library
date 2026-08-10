using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Jumpscare;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Tests.EditMode
{
    public sealed class JumpscareTests
    {
        [Test]
        public void RevealAndCleanup_ControlAllConfiguredTargetsAndEvents()
        {
            GameObject root = new("Jumpscare");
            GameObject primary = new("Primary");
            GameObject secondary = new("Secondary");
            primary.SetActive(false);
            secondary.SetActive(false);
            Light revealLight = root.AddComponent<Light>();
            revealLight.enabled = false;
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();
            int revealed = 0;
            int cleaned = 0;
            try
            {
                Set(sequence, "scareObject", primary);
                Set(sequence, "additionalScareObjects", new[] { secondary });
                Set(sequence, "revealLights", new[] { revealLight });
                Set(sequence, "onRevealed", Event(() => revealed++));
                Set(sequence, "onCleanedUp", Event(() => cleaned++));
                Invoke(sequence, "BeginScare");
                Assert.That(primary.activeSelf && secondary.activeSelf && revealLight.enabled, Is.True);
                Assert.That(revealed, Is.EqualTo(1));
                Invoke(sequence, "CleanupScare");
                Assert.That(primary.activeSelf || secondary.activeSelf || revealLight.enabled, Is.False);
                Assert.That(cleaned, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(secondary);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TriggerAndReset_AllowManualReplay()
        {
            GameObject root = new("Jumpscare");
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();
            JumpscareTrigger trigger = root.AddComponent<JumpscareTrigger>();
            try
            {
                Set(trigger, "jumpscare", sequence);
                trigger.Trigger();
                Assert.That(Get<bool>(trigger, "triggered"), Is.True);
                trigger.ResetTrigger();
                Assert.That(Get<bool>(trigger, "triggered"), Is.False);
                Assert.That(Get<int>(trigger, "activationCount"), Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static UnityEvent Event(UnityAction action)
        {
            UnityEvent result = new();
            result.AddListener(action);
            return result;
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    }
}
