using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietStatic.Tests.EditMode
{
    public class ObjectStateHandlerTests
    {
        private GameObject root;
        private GameObject emptyDishVisual;
        private GameObject dinnerVisual;
        private ObjectStateDefinition emptyHand;
        private ObjectStateDefinition emptyDish;
        private ObjectStateDefinition dinner;
        private ObjectStateChannel channel;
        private ObjectStateHandler handler;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("State Handler");
            emptyDishVisual = new GameObject("Empty Dish");
            dinnerVisual = new GameObject("Dinner");
            emptyDishVisual.transform.SetParent(root.transform);
            dinnerVisual.transform.SetParent(root.transform);

            emptyHand = CreateState("Empty Hand");
            emptyDish = CreateState("Empty Dish");
            dinner = CreateState("Dinner");
            channel = ScriptableObject.CreateInstance<ObjectStateChannel>();

            handler = root.AddComponent<ObjectStateHandler>();
            SetPrivateField(
                "states",
                new[]
                {
                    new ObjectStateHandler.StateBinding(emptyHand),
                    new ObjectStateHandler.StateBinding(emptyDish, emptyDishVisual),
                    new ObjectStateHandler.StateBinding(dinner, dinnerVisual)
                }
            );
            handler.ClearState();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(emptyHand);
            Object.DestroyImmediate(emptyDish);
            Object.DestroyImmediate(dinner);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void ActivateState_OnlyActivatesObjectsForSelectedState()
        {
            handler.ActivateState(emptyDish);

            Assert.That(handler.CurrentState, Is.SameAs(emptyDish));
            Assert.That(emptyDishVisual.activeSelf, Is.True);
            Assert.That(dinnerVisual.activeSelf, Is.False);

            handler.ActivateState(dinner);

            Assert.That(emptyDishVisual.activeSelf, Is.False);
            Assert.That(dinnerVisual.activeSelf, Is.True);
        }

        [Test]
        public void StateWithNoObjects_RepresentsAnEmptyVisualState()
        {
            handler.ActivateState(dinner);
            handler.ActivateState(emptyHand);

            Assert.That(handler.IsStateActive(emptyHand), Is.True);
            Assert.That(emptyDishVisual.activeSelf, Is.False);
            Assert.That(dinnerVisual.activeSelf, Is.False);
        }

        [Test]
        public void ClearState_DisablesAllStateObjects()
        {
            handler.ActivateState(emptyDish);
            handler.ClearState();

            Assert.That(handler.CurrentState, Is.Null);
            Assert.That(emptyDishVisual.activeSelf, Is.False);
            Assert.That(dinnerVisual.activeSelf, Is.False);
        }

        [Test]
        public void StateChangedUnityEvent_IsOnlyRaisedForStateTransitions()
        {
            int notificationCount = 0;
            var stateChanged =
                new ObjectStateHandler.StateChangedUnityEvent();
            stateChanged.AddListener(_ => notificationCount++);
            SetPrivateField("onStateChanged", stateChanged);

            handler.ActivateState(emptyDish);
            handler.ActivateState(emptyDish);
            handler.ActivateState(dinner);
            handler.ClearState();

            Assert.That(notificationCount, Is.EqualTo(3));
        }

        [Test]
        public void UnknownState_DoesNotChangeTheActiveState()
        {
            ObjectStateDefinition unknown = CreateState("Unknown");
            handler.ActivateState(emptyDish);
            LogAssert.Expect(
                LogType.Warning,
                "[Warning] ActivateState: " +
                "ObjectStateHandler has no binding for state 'Unknown'."
            );

            handler.ActivateState(unknown);

            Assert.That(handler.CurrentState, Is.SameAs(emptyDish));
            Assert.That(emptyDishVisual.activeSelf, Is.True);
            Object.DestroyImmediate(unknown);
        }

        [Test]
        public void ChannelRequest_ActivatesStateOnSubscribedHandler()
        {
            ConfigureChannel(channel);

            channel.ActivateState(dinner);

            Assert.That(handler.CurrentState, Is.SameAs(dinner));
            Assert.That(emptyDishVisual.activeSelf, Is.False);
            Assert.That(dinnerVisual.activeSelf, Is.True);
        }

        [Test]
        public void ChannelClearRequest_ClearsSubscribedHandler()
        {
            ConfigureChannel(channel);
            channel.ActivateState(emptyDish);

            channel.ClearState();

            Assert.That(handler.CurrentState, Is.Null);
            Assert.That(emptyDishVisual.activeSelf, Is.False);
        }

        [Test]
        public void DisabledHandler_DoesNotReceiveChannelRequests()
        {
            ConfigureChannel(channel);
            handler.enabled = false;

            channel.ActivateState(dinner);

            Assert.That(handler.CurrentState, Is.Null);
            Assert.That(dinnerVisual.activeSelf, Is.False);
        }

        private static ObjectStateDefinition CreateState(string name)
        {
            ObjectStateDefinition state = ScriptableObject.CreateInstance<ObjectStateDefinition>();
            state.name = name;
            return state;
        }

        private void SetPrivateField(string fieldName, object value)
        {
            typeof(ObjectStateHandler)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(handler, value);
        }

        private void ConfigureChannel(ObjectStateChannel value)
        {
            handler.SetChannel(value);
        }
    }
}
