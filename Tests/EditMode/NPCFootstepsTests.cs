using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Audio;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NPCFootstepsTests
    {
        private GameObject actor;
        private NPCFootsteps footsteps;
        private AudioRequestChannel channel;
        private AudioClip clip;
        private readonly List<AudioCommand> commands = new();

        [SetUp]
        public void SetUp()
        {
            actor = new GameObject("Footstep test");
            footsteps = actor.AddComponent<NPCFootsteps>();
            EnableFootsteps();
            channel = ScriptableObject.CreateInstance<AudioRequestChannel>();
            clip = AudioClip.Create("Step", 100, 1, 1000, false);
            footsteps.SetRequestChannel(channel);
            channel.CommandRequested += commands.Add;
            var serialized = new SerializedObject(footsteps);
            var clips = serialized.FindProperty("footstepClips");
            clips.arraySize = 2;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = null;
            clips.GetArrayElementAtIndex(1).objectReferenceValue = clip;
            serialized.FindProperty("stepDistance").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            commands.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(channel);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void TravelEmitsPositionalStepsAndSkipsNullClips()
        {
            Move(0.5f);
            Assert.That(commands, Is.Empty);
            Move(0.5f);
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0].Type, Is.EqualTo(AudioCommandType.PlaySfxAtPosition));
            Assert.That(commands[0].Clip, Is.SameAs(clip));
            Assert.That(commands[0].Position, Is.EqualTo(actor.transform.position));
            Assert.That(commands[0].Value, Is.EqualTo(0.4f));
            Move(1f);
            Assert.That(commands, Has.Count.EqualTo(2));
        }

        [Test]
        public void StoppingPausingTeleportingAndReenablingResetStride()
        {
            Move(0.75f);
            Move(0f);
            Move(0.5f);
            Assert.That(commands, Is.Empty, "Standing still must reset the stride.");
            Move(0.5f, 0f);
            Move(0.5f);
            Assert.That(commands, Is.Empty, "Paused movement must not produce a step.");
            Move(10f);
            Move(0.5f);
            Assert.That(commands, Is.Empty, "Teleporting must reset the stride.");
            actor.SetActive(false);
            actor.transform.position += Vector3.right * 10f;
            actor.SetActive(true);
            EnableFootsteps();
            Move(0.5f);
            Assert.That(commands, Is.Empty, "Reactivation must reset the position and stride.");
            Move(0.5f);
            Assert.That(commands, Has.Count.EqualTo(1));
        }

        [Test]
        public void TurningAndVerticalMovementDoNotEmitSteps()
        {
            actor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            actor.transform.position += Vector3.up;
            Move(0f);
            Assert.That(commands, Is.Empty);
        }

        private void Move(float distance, float deltaTime = 0.25f)
        {
            actor.transform.position += Vector3.forward * distance;
            typeof(NPCFootsteps).GetMethod("TrackMovement", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(footsteps, new object[] { deltaTime });
        }

        private void EnableFootsteps()
        {
            // EditMode does not dispatch runtime OnEnable callbacks.
            typeof(NPCFootsteps).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(footsteps, null);
        }
    }
}
