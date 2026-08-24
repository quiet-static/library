using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Setup;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SetupSerializedUtilityTests
    {
        private sealed class ReferenceHolder : ScriptableObject
        {
            [SerializeField] private Object reference;
        }

        [Test]
        public void AssignObjectReference_IsIdempotent()
        {
            ReferenceHolder holder = ScriptableObject.CreateInstance<ReferenceHolder>();
            GameObject target = new("Target");
            try
            {
                Assert.That(
                    SetupSerializedUtility.AssignObjectReference(holder, "reference", target),
                    Is.True);
                Assert.That(
                    SetupSerializedUtility.AssignObjectReference(holder, "reference", target),
                    Is.False);
                Assert.That(
                    SetupSerializedUtility.HasObjectReference(holder, "reference", target),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void GetOrAddComponent_ReturnsSameComponentOnSecondCall()
        {
            GameObject owner = new("Owner");
            try
            {
                Transform first = SetupSerializedUtility.GetOrAddComponent<Transform>(owner);
                Transform second = SetupSerializedUtility.GetOrAddComponent<Transform>(owner);
                Assert.That(second == first, Is.True);
                Assert.That(owner.GetComponents<Transform>().Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
