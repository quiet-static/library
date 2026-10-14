using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ScreenOverlayTests
    {
        [Test]
        public void Channel_DeliversNamedVisibilityAndPulseCommands()
        {
            var channel = ScriptableObject.CreateInstance<ScreenOverlayChannel>();
            try
            {
                ScreenOverlayCommand last = default;
                channel.CommandRequested += command => last = command;
                channel.Pulse("spray");
                Assert.That(last.Id, Is.EqualTo("spray"));
                Assert.That(last.Visible && last.Timed, Is.True);
                channel.Hide("spray");
                Assert.That(last.Visible || last.Timed, Is.False);
                channel.Show("dim");
                Assert.That(last.Id, Is.EqualTo("dim"));
                Assert.That(last.Visible && !last.Timed, Is.True);
                channel.ClearAll();
                Assert.That(last.Id, Is.Null);
            }
            finally { Object.DestroyImmediate(channel); }
        }

        [Test]
        public void Controller_PulseExpires_ShowCancelsExpiry_AndDisableClears()
        {
            var root = new GameObject("Overlay Test");
            var channel = ScriptableObject.CreateInstance<ScreenOverlayChannel>();
            try
            {
                var controller = root.AddComponent<ScreenOverlayController>();
                var layer = new ScreenOverlayController.Layer { id = "spray", fadeSeconds = 0f, holdSeconds = 0f };
                typeof(ScreenOverlayController).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, new[] { layer });
                typeof(ScreenOverlayController).GetField("channel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, channel);
                Invoke(controller, "Awake");
                Invoke(controller, "OnEnable");
                var group = root.GetComponentInChildren<CanvasGroup>();
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.blocksRaycasts, Is.False);
                channel.Pulse("spray");
                Invoke(controller, "Update");
                Assert.That(group.alpha, Is.EqualTo(1f));
                Invoke(controller, "Update");
                Assert.That(group.alpha, Is.Zero);
                channel.Pulse("spray");
                channel.Show("spray");
                Invoke(controller, "Update");
                Invoke(controller, "Update");
                Assert.That(group.alpha, Is.EqualTo(1f));
                Invoke(controller, "OnDisable");
                Assert.That(group.alpha, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(channel); }
        }

        private static void Invoke(ScreenOverlayController controller, string method) =>
            typeof(ScreenOverlayController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
