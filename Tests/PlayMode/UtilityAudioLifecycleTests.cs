using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Audio;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class UtilityAudioLifecycleTests
    {
        private readonly List<Object> created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    Object.Destroy(created[i]);
            }
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledTrigger_DoesNotConsumeEvent_AndStillFiresOnlyOnce()
        {
            GameObject owner = CreateObject("One-shot trigger");
            TriggerOnce trigger = owner.AddComponent<TriggerOnce>();
            owner.AddComponent<BoxCollider>().isTrigger = true;
            int calls = 0;
            var callback = new UnityEvent();
            callback.AddListener(() => calls++);
            SetField(trigger, "requiredTag", string.Empty);
            SetField(trigger, "onTriggered", callback);
            trigger.enabled = false;

            GameObject visitor = CreateObject("Visitor");
            visitor.AddComponent<BoxCollider>();
            visitor.AddComponent<Rigidbody>().useGravity = false;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(calls, Is.Zero);

            visitor.transform.position = Vector3.right * 10f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            trigger.enabled = true;
            visitor.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(calls, Is.EqualTo(1));

            visitor.transform.position = Vector3.right * 10f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            trigger.enabled = true;
            visitor.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RepeatedSfxPause_ResumesOwnedSounds_WithoutResumingExternalPauses()
        {
            SfxManager manager = CreateObject("SFX manager").AddComponent<SfxManager>();
            EventSound3D prefab = CreateObject("Sound prefab").AddComponent<EventSound3D>();
            SetField(manager, "eventSound3DPrefab", prefab);
            AudioClip clip = AudioClip.Create("Loop", 44100, 1, 44100, false);
            created.Add(clip);
            EventSound3D owned = manager.PlayAtPosition(clip, Vector3.zero, 1f, 10f, 1f, true);
            created.Add(owned.gameObject);
            EventSound3D external = manager.PlayAtPosition(clip, Vector3.zero, 1f, 10f, 1f, true);
            created.Add(external.gameObject);
            Assert.That(owned.IsPlaying, Is.True);
            Assert.That(external.IsPlaying, Is.True);

            external.Pause();
            manager.PauseSpawnedSounds();
            manager.PauseSpawnedSounds();
            Assert.That(owned.IsPaused, Is.True);
            manager.ResumeSpawnedSounds();
            Assert.That(owned.IsPaused, Is.False);
            Assert.That(owned.IsPlaying, Is.True);
            Assert.That(external.IsPaused, Is.True);
            yield return null;
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            created.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
