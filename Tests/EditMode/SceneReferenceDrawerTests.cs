using NUnit.Framework;
using QuietStatic.Toolkit.Editor.SceneFlow;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SceneReferenceDrawerTests
    {
        private sealed class SceneReferenceHolder : ScriptableObject
        {
            [SerializeField] private SceneReference scene = new();

            public string SceneName => scene.SceneName;
        }

        private sealed class PopupProbe
        {
            private readonly int? selectedIndex;

            public PopupProbe(int? selectedIndex)
            {
                this.selectedIndex = selectedIndex;
            }

            public int CurrentIndex { get; private set; }

            public string[] DisplayedOptions { get; private set; }

            public int DrawPopup(
                Rect position,
                GUIContent label,
                int currentIndex,
                string[] displayedOptions)
            {
                CurrentIndex = currentIndex;
                DisplayedOptions = displayedOptions;
                GUI.changed = selectedIndex.HasValue;
                return selectedIndex ?? currentIndex;
            }
        }

        [Test]
        public void OnGUI_RepaintPreservesMissingSceneName()
        {
            SceneReferenceDrawer drawer = CreateDrawer(new[]
            {
                BuildSettingsScene("EnabledScene", enabled: true),
            }, null, out PopupProbe probe);

            string result = Draw(drawer, "MissingScene");

            Assert.That(result, Is.EqualTo("MissingScene"));
            Assert.That(probe.CurrentIndex, Is.EqualTo(1));
            Assert.That(
                probe.DisplayedOptions,
                Does.Contain("<Missing/disabled: MissingScene>"));
        }

        [Test]
        public void OnGUI_RepaintPreservesDisabledSceneName()
        {
            SceneReferenceDrawer drawer = CreateDrawer(new[]
            {
                BuildSettingsScene("DisabledScene", enabled: false),
                BuildSettingsScene("EnabledScene", enabled: true),
            }, null, out PopupProbe probe);

            string result = Draw(drawer, "DisabledScene");

            Assert.That(result, Is.EqualTo("DisabledScene"));
            Assert.That(probe.CurrentIndex, Is.EqualTo(1));
            Assert.That(
                probe.DisplayedOptions,
                Does.Contain("<Missing/disabled: DisabledScene>"));
        }

        [Test]
        public void OnGUI_DeliberateNoneSelectionClearsSceneName()
        {
            SceneReferenceDrawer drawer = CreateDrawer(
                new[] { BuildSettingsScene("EnabledScene", enabled: true) },
                0,
                out _);

            string result = Draw(drawer, "EnabledScene");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void OnGUI_DeliberateEnabledSceneSelectionChangesSceneName()
        {
            SceneReferenceDrawer drawer = CreateDrawer(
                new[]
                {
                    BuildSettingsScene("FirstScene", enabled: true),
                    BuildSettingsScene("SecondScene", enabled: true),
                },
                2,
                out _);

            string result = Draw(drawer, "FirstScene");

            Assert.That(result, Is.EqualTo("SecondScene"));
        }

        private static SceneReferenceDrawer CreateDrawer(
            EditorBuildSettingsScene[] scenes,
            int? selectedIndex,
            out PopupProbe probe)
        {
            probe = new PopupProbe(selectedIndex);
            return new SceneReferenceDrawer(() => scenes, probe.DrawPopup);
        }

        private static string Draw(
            SceneReferenceDrawer drawer,
            string initialSceneName)
        {
            SceneReferenceHolder holder =
                ScriptableObject.CreateInstance<SceneReferenceHolder>();
            try
            {
                SerializedObject serialized = new(holder);
                SerializedProperty scene = serialized.FindProperty("scene");
                SerializedProperty sceneName =
                    scene.FindPropertyRelative("sceneName");
                sceneName.stringValue = initialSceneName;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                serialized.Update();
                bool previousChanged = GUI.changed;
                try
                {
                    GUI.changed = false;
                    drawer.OnGUI(
                        new Rect(0f, 0f, 300f, EditorGUIUtility.singleLineHeight),
                        scene,
                        new GUIContent("Scene"));
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                finally
                {
                    GUI.changed = previousChanged;
                }

                return holder.SceneName;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static EditorBuildSettingsScene BuildSettingsScene(
            string sceneName,
            bool enabled)
        {
            return new EditorBuildSettingsScene(
                $"Assets/Scenes/{sceneName}.unity",
                enabled);
        }
    }
}
