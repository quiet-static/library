using System.Collections;
using NUnit.Framework;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class ToolkitSingletonProbe :
        ToolkitSingleton<ToolkitSingletonProbe>
    {
        public static int EnableCount { get; private set; }

        public static void ResetEnableCount()
        {
            EnableCount = 0;
        }

        private void OnEnable()
        {
            EnableCount++;
        }
    }

    public sealed class ToolkitSingletonLifecycleTests
    {
        private GameObject primaryObject;
        private GameObject duplicateObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (duplicateObject != null)
            {
                Object.Destroy(duplicateObject);
            }

            if (primaryObject != null)
            {
                Object.Destroy(primaryObject);
            }

            yield return null;
            ToolkitSingletonProbe.ResetEnableCount();
        }

        [UnityTest]
        public IEnumerator DestroyableDuplicate_IsDisabledBeforeOnEnable()
        {
            ToolkitSingletonProbe.ResetEnableCount();
            primaryObject = new GameObject("Primary Singleton");
            ToolkitSingletonProbe primary =
                primaryObject.AddComponent<ToolkitSingletonProbe>();

            duplicateObject = new GameObject("Duplicate Singleton");
            ToolkitSingletonProbe duplicate =
                duplicateObject.AddComponent<ToolkitSingletonProbe>();

            Assert.That(ToolkitSingletonProbe.Instance, Is.SameAs(primary));
            Assert.That(primary.enabled, Is.True);
            Assert.That(duplicate.enabled, Is.False);
            Assert.That(ToolkitSingletonProbe.EnableCount, Is.EqualTo(1));

            yield return null;

            Assert.That(duplicate == null, Is.True);
            Assert.That(ToolkitSingletonProbe.Instance, Is.SameAs(primary));
        }
    }
}
