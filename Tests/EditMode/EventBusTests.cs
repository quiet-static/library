using System.Collections.Generic;
using NUnit.Framework;

namespace QuietStatic.Tests.EditMode
{
    public sealed class EventBusTests
    {
        private readonly List<string> calls = new();

        private readonly struct TestEvent : IEvent
        {
            public TestEvent(int value) => Value = value;

            public int Value { get; }
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<TestEvent>.Unsubscribe(FirstListener);
            EventBus<TestEvent>.Unsubscribe(SecondListener);
            calls.Clear();
        }

        [Test]
        public void Publish_DeliversPayloadInReverseSubscriptionOrder()
        {
            EventBus<TestEvent>.Subscribe(FirstListener);
            EventBus<TestEvent>.Subscribe(SecondListener);

            EventBus<TestEvent>.Publish(new TestEvent(42));

            CollectionAssert.AreEqual(
                new[] { "second:42", "first:42" },
                calls);
        }

        [Test]
        public void Subscribe_DoesNotRegisterTheSameListenerTwice()
        {
            EventBus<TestEvent>.Subscribe(FirstListener);
            EventBus<TestEvent>.Subscribe(FirstListener);

            EventBus<TestEvent>.Publish(new TestEvent(7));

            CollectionAssert.AreEqual(new[] { "first:7" }, calls);
        }

        [Test]
        public void Listener_CanUnsubscribeItselfDuringPublication()
        {
            EventBus<TestEvent>.Subscribe(FirstListener);
            EventBus<TestEvent>.Subscribe(SelfRemovingListener);

            EventBus<TestEvent>.Publish(new TestEvent(1));
            EventBus<TestEvent>.Publish(new TestEvent(2));

            CollectionAssert.AreEqual(
                new[] { "self:1", "first:1", "first:2" },
                calls);
        }

        private void FirstListener(TestEvent payload) => calls.Add($"first:{payload.Value}");

        private void SecondListener(TestEvent payload) => calls.Add($"second:{payload.Value}");

        private void SelfRemovingListener(TestEvent payload)
        {
            calls.Add($"self:{payload.Value}");
            EventBus<TestEvent>.Unsubscribe(SelfRemovingListener);
        }
    }
}
