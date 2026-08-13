using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class CinematicWaitSourceSpy : MonoBehaviour, ICinematicWaitSource
    {
        public bool IsRunning { get; private set; }
        public int PlayCount { get; private set; }

        public void Play()
        {
            PlayCount++;
            IsRunning = true;
        }

        public void Complete()
        {
            IsRunning = false;
        }
    }

    public sealed class CinematicPlaybackTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();
        private Action sequenceStartedHandler;
        private Action sequenceEndedHandler;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CutsceneSequenceRunner.OnSequenceStarted -= sequenceStartedHandler;
            CutsceneSequenceRunner.OnSequenceEnded -= sequenceEndedHandler;
            sequenceStartedHandler = null;
            sequenceEndedHandler = null;

            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.Destroy(createdObjects[index]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sequence_PlayRaisesLifecycleInOrderAndResetsState()
        {
            CutsceneSequenceRunner runner = CreateSequenceRunner(
                new CutsceneSequenceRunner.Step(),
                new CutsceneSequenceRunner.Step());
            var lifecycle = new List<string>();
            sequenceStartedHandler = () => lifecycle.Add("started");
            sequenceEndedHandler = () => lifecycle.Add("ended");
            CutsceneSequenceRunner.OnSequenceStarted += sequenceStartedHandler;
            CutsceneSequenceRunner.OnSequenceEnded += sequenceEndedHandler;

            runner.Play();
            for (int frame = 0; frame < 10 && runner.IsRunning; frame++)
            {
                yield return null;
            }

            Assert.That(lifecycle, Is.EqualTo(new[] { "started", "ended" }));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator Sequence_StopDuringWaitRaisesEndedAndSkipsFinishedEvent()
        {
            GameObject waitObject = Track(new GameObject("Wait Source"));
            CinematicWaitSourceSpy waitSource =
                waitObject.AddComponent<CinematicWaitSourceSpy>();
            var step = new CutsceneSequenceRunner.Step
            {
                waitSource = waitSource,
            };
            CutsceneSequenceRunner runner = CreateSequenceRunner(step);
            var finished = new UnityEvent();
            int finishedCount = 0;
            int endedCount = 0;
            finished.AddListener(() => finishedCount++);
            SetField(runner, "onSequenceFinished", finished);
            sequenceEndedHandler = () => endedCount++;
            CutsceneSequenceRunner.OnSequenceEnded += sequenceEndedHandler;

            runner.Play();
            yield return null;
            Assert.That(waitSource.PlayCount, Is.EqualTo(1));
            Assert.That(runner.IsRunning, Is.True);

            runner.Stop();

            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(-1));
            Assert.That(endedCount, Is.EqualTo(1));
            Assert.That(finishedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Sequence_PlayStepRunsOnlyRequestedStep()
        {
            var firstStarted = new UnityEvent();
            var secondStarted = new UnityEvent();
            int firstCount = 0;
            int secondCount = 0;
            firstStarted.AddListener(() => firstCount++);
            secondStarted.AddListener(() => secondCount++);
            var first = new CutsceneSequenceRunner.Step
            {
                onStepStarted = firstStarted,
            };
            var second = new CutsceneSequenceRunner.Step
            {
                onStepStarted = secondStarted,
            };
            CutsceneSequenceRunner runner = CreateSequenceRunner(first, second);

            runner.PlayStep(1);
            yield return null;

            Assert.That(firstCount, Is.Zero);
            Assert.That(secondCount, Is.EqualTo(1));
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.CurrentStepIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator ScenePlayer_RunsBeatsInOrderAndAppliesShot()
        {
            CinematicDefinition definition = CreateDefinition(
                "opening",
                new CinematicDefinition.Beat
                {
                    id = "wide",
                    cameraShotId = "arrival",
                },
                new CinematicDefinition.Beat
                {
                    id = "close",
                });
            GameObject cameraObject = Track(new GameObject("Camera Rig"));
            GameObject poseObject = Track(new GameObject("Arrival Pose"));
            poseObject.transform.SetPositionAndRotation(
                new Vector3(3f, 4f, 5f),
                Quaternion.Euler(10f, 20f, 30f));
            CinematicScenePlayer player = CreateScenePlayer(definition);
            SetField(player, "shots", new List<CinematicScenePlayer.ShotBinding>
            {
                new CinematicScenePlayer.ShotBinding
                {
                    id = "arrival",
                    cameraTransform = cameraObject.transform,
                    pose = poseObject.transform,
                },
            });
            var started = new CinematicScenePlayer.StringEvent();
            var finished = new CinematicScenePlayer.StringEvent();
            var lifecycle = new List<string>();
            started.AddListener(id => lifecycle.Add($"start:{id}"));
            finished.AddListener(id => lifecycle.Add($"finish:{id}"));
            SetField(player, "onBeatStarted", started);
            SetField(player, "onBeatFinished", finished);

            player.Play();
            for (int frame = 0; frame < 10 && player.IsRunning; frame++)
            {
                yield return null;
            }

            Assert.That(lifecycle, Is.EqualTo(new[]
            {
                "start:wide",
                "finish:wide",
                "start:close",
                "finish:close",
            }));
            Assert.That(cameraObject.transform.position, Is.EqualTo(poseObject.transform.position));
            Assert.That(
                Quaternion.Angle(
                    cameraObject.transform.rotation,
                    poseObject.transform.rotation),
                Is.LessThan(0.001f));
            Assert.That(player.IsRunning, Is.False);
            Assert.That(player.CurrentBeatIndex, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator ScenePlayer_WaitsForWaitSourceBeforeFinishingBeat()
        {
            CinematicDefinition definition = CreateDefinition(
                "activity",
                new CinematicDefinition.Beat
                {
                    id = "wait",
                    activityId = "dialogue",
                });
            GameObject waitObject = Track(new GameObject("Wait Source"));
            CinematicWaitSourceSpy waitSource =
                waitObject.AddComponent<CinematicWaitSourceSpy>();
            CinematicScenePlayer player = CreateScenePlayer(definition);
            SetField(player, "activities", new List<CinematicScenePlayer.ActivityBinding>
            {
                new CinematicScenePlayer.ActivityBinding
                {
                    id = "dialogue",
                    component = waitSource,
                },
            });
            int finishedBeatCount = 0;
            var finished = new CinematicScenePlayer.StringEvent();
            finished.AddListener(_ => finishedBeatCount++);
            SetField(player, "onBeatFinished", finished);

            player.Play();
            yield return null;

            Assert.That(waitSource.PlayCount, Is.EqualTo(1));
            Assert.That(player.IsRunning, Is.True);
            Assert.That(finishedBeatCount, Is.Zero);

            waitSource.Complete();
            yield return null;

            Assert.That(player.IsRunning, Is.False);
            Assert.That(finishedBeatCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ScenePlayer_StopCancelsWithoutFinishedEvent()
        {
            CinematicDefinition definition = CreateDefinition(
                "activity",
                new CinematicDefinition.Beat
                {
                    id = "wait",
                    activityId = "activity",
                });
            GameObject waitObject = Track(new GameObject("Wait Source"));
            CinematicWaitSourceSpy waitSource =
                waitObject.AddComponent<CinematicWaitSourceSpy>();
            CinematicScenePlayer player = CreateScenePlayer(definition);
            SetField(player, "activities", new List<CinematicScenePlayer.ActivityBinding>
            {
                new CinematicScenePlayer.ActivityBinding
                {
                    id = "activity",
                    component = waitSource,
                },
            });
            var sequenceFinished = new UnityEvent();
            int finishedCount = 0;
            sequenceFinished.AddListener(() => finishedCount++);
            SetField(player, "onFinished", sequenceFinished);

            player.Play();
            yield return null;
            player.Stop();

            Assert.That(player.IsRunning, Is.False);
            Assert.That(player.CurrentBeatIndex, Is.EqualTo(-1));
            Assert.That(finishedCount, Is.Zero);
        }

        private CutsceneSequenceRunner CreateSequenceRunner(
            params CutsceneSequenceRunner.Step[] steps)
        {
            GameObject gameObject = Track(new GameObject("Sequence Runner"));
            CutsceneSequenceRunner runner =
                gameObject.AddComponent<CutsceneSequenceRunner>();
            SetField(runner, "steps", steps);
            return runner;
        }

        private CinematicScenePlayer CreateScenePlayer(
            CinematicDefinition definition)
        {
            GameObject gameObject = Track(new GameObject("Cinematic Player"));
            CinematicScenePlayer player =
                gameObject.AddComponent<CinematicScenePlayer>();
            SetField(player, "definition", definition);
            return player;
        }

        private CinematicDefinition CreateDefinition(
            string id,
            params CinematicDefinition.Beat[] beats)
        {
            CinematicDefinition definition = Track(
                ScriptableObject.CreateInstance<CinematicDefinition>());
            SetField(definition, "id", id);
            SetField(definition, "beats", new List<CinematicDefinition.Beat>(beats));
            return definition;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
