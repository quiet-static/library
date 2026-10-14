using System.Collections;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace QuietStatic.Tests.PlayMode
{
    public sealed class DialogueUIControlStateTests
    {
        private GameObject host;
        private GameObject eventHost;
        private DialogueUIManager manager;
        private Button first;
        private Button second;
        private EventSystem events;

        [SetUp]
        public void SetUp()
        {
            eventHost = new GameObject("Dialogue test events", typeof(EventSystem));
            events = eventHost.GetComponent<EventSystem>();
            host = new GameObject("Dialogue test UI");
            host.SetActive(false);
            manager = host.AddComponent<DialogueUIManager>();
            first = CreateButton("First");
            second = CreateButton("Second");
            Set("controlCursor", false);
            Set("choiceButtons", new[] { first, second });
            Set("continueButton", first);
            host.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(host);
            Object.Destroy(eventHost);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewNode_ReleasesSelectedAndPressedChoice_AndHoverStillWorks()
        {
            manager.ShowChoices("Speaker", "First node", new[] { "One", "Two" });
            var pointer = new PointerEventData(events);
            second.OnPointerEnter(pointer);
            second.OnPointerDown(pointer);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(second.gameObject));

            manager.OnChoiceSelected += _ =>
                manager.ShowChoices("Speaker", "Next node", new[] { "Three", "Four" });
            // Exercise synchronous node replacement while the reused control is pressed.
            second.onClick.Invoke();
            Assert.That(events.currentSelectedGameObject, Is.Null);
            second.OnPointerExit(pointer);
            Assert.That(State(second), Is.EqualTo("Normal"));
            first.OnPointerEnter(pointer);
            Assert.That(State(first), Is.EqualTo("Highlighted"));
            first.OnPointerExit(pointer);
            second.OnPointerEnter(pointer);
            Assert.That(State(second), Is.EqualTo("Highlighted"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContinueAndHide_ClearOwnedSelection_WithoutClearingOtherUI()
        {
            manager.ShowLine("Speaker", "First line");
            events.SetSelectedGameObject(first.gameObject);
            manager.OnAdvanceRequested += () => manager.ShowLine("Speaker", "Next line");
            first.OnSubmit(new BaseEventData(events));
            yield return null;
            Assert.That(events.currentSelectedGameObject, Is.Null);
            Assert.That(State(first), Is.EqualTo("Normal"));

            events.SetSelectedGameObject(first.gameObject);
            manager.HideDialogueUI();
            Assert.That(events.currentSelectedGameObject, Is.Null);
            Button other = CreateButton("Other UI");
            events.SetSelectedGameObject(other.gameObject);
            manager.ShowChoices("Speaker", "Question", new[] { "Answer" });
            yield return null;
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(other.gameObject));
        }

        private Button CreateButton(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(host.transform, false);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        private void Set(string name, object value) =>
            typeof(DialogueUIManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, value);

        private static string State(Button button) =>
            typeof(Selectable).GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(button).ToString();
    }
}
