using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class CinematicTreePlaybackTests
    {
        private GameObject root;
        private CinematicTree tree;
        private CinematicTreePlayer player;
        private CinematicTextOverlay overlay;
        private TMP_Text label;
        private float originalTimeScale;
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [SetUp] public void Setup()
        {
            originalTimeScale = Time.timeScale;
            root = new GameObject("Cinematic test", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            var text = new GameObject("Text", typeof(RectTransform));
            text.transform.SetParent(root.transform);
            label = text.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            overlay = root.AddComponent<CinematicTextOverlay>();
            Set(overlay, "textLabel", label);
            Set(overlay, "canvasGroup", root.GetComponent<CanvasGroup>());
            player = root.AddComponent<CinematicTreePlayer>();
            Set(player, "overlay", overlay);
            Set(player, "autoAdvance", false);
            Set(player, "allowDefaultInput", false);
            tree = ScriptableObject.CreateInstance<CinematicTree>();
            Set(tree, "beats", new List<CinematicTree.Beat>
            {
                new() { id = "wide", text = "<b>Rain.</b>", charactersPerSecond = 1, holdSeconds = .05f },
                new() { id = "close", text = "Office.", charactersPerSecond = 0, holdSeconds = .05f },
            });
            player.SetTree(tree);
        }
        [TearDown] public void Cleanup()
        {
            Time.timeScale = originalTimeScale;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(tree);
        }

        [UnityTest] public IEnumerator ManualAdvance_RevealsBeforeAdvancingAndFinishesInOrder()
        {
            var ids = new List<string>();
            player.BeatStarted += beat => ids.Add(beat.id);
            var finished = new UnityEvent();
            int completions = 0;
            finished.AddListener(() => completions++);
            Set(player, "onFinished", finished);
            player.Play();
            Assert.That(overlay.IsRevealing, Is.True);
            Assert.That(label.textInfo.characterCount, Is.EqualTo(5));
            player.Advance();
            Assert.That(player.CurrentBeat.id, Is.EqualTo("wide"));
            Assert.That(overlay.IsRevealing, Is.False);
            player.Advance();
            Assert.That(player.CurrentBeat.id, Is.EqualTo("close"));
            player.Advance();
            Assert.That(player.IsRunning, Is.False);
            Assert.That(label.text, Is.Empty);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(ids, Is.EqualTo(new[] { "wide", "close" }));
            yield return null;
        }

        [UnityTest] public IEnumerator AutomaticPlayback_HoldsAfterRevealAndWorksWhilePaused()
        {
            Set(player, "autoAdvance", true);
            tree.Beats[0].charactersPerSecond = 200;
            tree.Beats[0].holdSeconds = .3f;
            Time.timeScale = 0;
            player.Play();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(player.CurrentBeat.id, Is.EqualTo("wide"));
            Assert.That(overlay.IsRevealing, Is.False);
            float deadline = Time.realtimeSinceStartup + 3;
            while (player.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.IsRunning, Is.False);
            Assert.That(label.text, Is.Empty);
        }

        [UnityTest] public IEnumerator Disable_ClearsTextWithoutNaturalCompletionAndSupportsReplay()
        {
            int completions = 0;
            var finished = new UnityEvent();
            finished.AddListener(() => completions++);
            Set(player, "onFinished", finished);
            player.Play();
            player.enabled = false;
            Assert.That(label.text, Is.Empty);
            Assert.That(player.IsRunning, Is.False);
            Assert.That(completions, Is.Zero);
            player.enabled = true;
            player.Play();
            Assert.That(player.CurrentBeat.id, Is.EqualTo("wide"));
            player.Stop();
            yield return null;
        }

        [UnityTest] public IEnumerator BeatCallback_CanCancelWithoutStartingAnotherBeat()
        {
            player.BeatStarted += _ => player.Stop();
            player.Play();
            Assert.That(player.IsRunning, Is.False);
            Assert.That(label.text, Is.Empty);
            yield return null;
        }

        [UnityTest] public IEnumerator StableBeatCues_ChangeShotsWhenTheMatchingTextBeatStarts()
        {
            var cameraObject = new GameObject("Cinematic camera");
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            var director = cameraObject.AddComponent<CinematicCutsceneCameraDirector>();
            var wide = new GameObject("Wide marker").transform;
            var close = new GameObject("Close marker").transform;
            wide.SetParent(root.transform);
            close.SetParent(root.transform);
            wide.position = new Vector3(2, 3, 4);
            close.position = new Vector3(5, 6, 7);
            Set(director, "cutsceneCamera", camera);
            Set(director, "shots", new List<CinematicCutsceneCameraDirector.CinematicShot>
            {
                new() { shotId = "shot.wide", cameraPositionMarker = wide },
                new() { shotId = "shot.close", cameraPositionMarker = close },
            });
            var cues = root.AddComponent<CinematicTreeShotCues>();
            cues.enabled = false;
            Set(cues, "player", player);
            Set(cues, "cues", new List<CinematicTreeShotCues.Cue>
            {
                new() { beatId = "wide", cameraDirector = director, cameraShotId = "shot.wide" },
                new() { beatId = "close", cameraDirector = director, cameraShotId = "shot.close" },
            });
            cues.enabled = true;
            player.Play();
            Assert.That(director.CurrentShotId, Is.EqualTo("shot.wide"));
            Assert.That(camera.transform.position, Is.EqualTo(wide.position));
            player.Advance();
            player.Advance();
            Assert.That(director.CurrentShotId, Is.EqualTo("shot.close"));
            Assert.That(camera.transform.position, Is.EqualTo(close.position));
            player.Stop();
            yield return null;
        }
    }
}
