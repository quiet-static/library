using System.IO;
using QuietStatic.Toolkit.Settings;
using QuietStatic.Toolkit.UI.Menu;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Editor.Settings
{
    /// <summary>Creates neutral, reusable settings and pause menu prefab starting points.</summary>
    public static class SettingsMenuPrefabBuilder
    {
        private const string Output = "Packages/com.quietstatic.core/Runtime/UI/Prefabs";
        private static readonly DefaultControls.Resources Resources = new();

        [InitializeOnLoadMethod]
        private static void BuildMissingPrefabs()
        {
            GameObject settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Output}/SettingsMenu.prefab");
            bool hasCurrentSettingsLayout = settingsPrefab != null &&
                                            settingsPrefab.transform.Find("Master volume") != null;
            if (hasCurrentSettingsLayout &&
                File.Exists($"{Output}/PauseMenu.prefab") &&
                File.Exists($"{Output}/InputRebindControl.prefab")) return;
            EditorApplication.delayCall += BuildAll;
        }

        [MenuItem("Tools/Quiet Static/Build Settings and Pause Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Output);
            GameObject settings = BuildSettingsMenu();
            PrefabUtility.SaveAsPrefabAsset(settings, $"{Output}/SettingsMenu.prefab");
            Object.DestroyImmediate(settings);

            GameObject rebind = BuildRebindControl();
            PrefabUtility.SaveAsPrefabAsset(rebind, $"{Output}/InputRebindControl.prefab");
            Object.DestroyImmediate(rebind);

            GameObject pause = BuildPauseMenu();
            PrefabUtility.SaveAsPrefabAsset(pause, $"{Output}/PauseMenu.prefab");
            Object.DestroyImmediate(pause);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameLogger.Log(nameof(SettingsMenuPrefabBuilder), null,
                "Created reusable SettingsMenu and PauseMenu prefabs.");
        }

        private static GameObject BuildSettingsMenu()
        {
            GameObject root = Panel("SettingsMenu", new Color(0.035f, 0.04f, 0.05f, 0.98f));
            root.AddComponent<CanvasGroup>();
            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.spacing = 10;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddTitle(root.transform, "SETTINGS");

            SettingsMenuView view = root.AddComponent<SettingsMenuView>();
            SerializedObject serialized = new(view);
            Set(serialized, "masterVolume", AddSlider(root.transform, "Master volume"));
            Set(serialized, "musicVolume", AddSlider(root.transform, "Music volume"));
            Set(serialized, "sfxVolume", AddSlider(root.transform, "Sound effects volume"));
            Set(serialized, "lookSensitivity", AddSlider(root.transform, "Look sensitivity"));
            Set(serialized, "vSync", AddToggle(root.transform, "VSync"));
            Button back = AddButton(root.transform, "Back");
            UnityEventTools.AddPersistentListener(back.onClick, view.RequestBack);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildPauseMenu()
        {
            GameObject root = Panel("PauseMenu", new Color(0f, 0f, 0f, 0.72f));
            root.AddComponent<CanvasGroup>();
            GameObject main = Panel("MainPage", new Color(0.04f, 0.045f, 0.055f, 0.98f));
            main.transform.SetParent(root.transform, false);
            VerticalLayoutGroup layout = main.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 48, 48);
            layout.spacing = 18;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            AddTitle(main.transform, "PAUSED");
            Button resume = AddButton(main.transform, "Resume");
            Button settingsButton = AddButton(main.transform, "Settings");
            Button exit = AddButton(main.transform, "Exit Game");

            GameObject settings = BuildSettingsMenu();
            settings.name = "SettingsPage";
            settings.transform.SetParent(root.transform, false);
            settings.SetActive(false);

            GameQuitter quitter = root.AddComponent<GameQuitter>();
            PauseMenuView view = root.AddComponent<PauseMenuView>();
            SerializedObject serialized = new(view);
            Set(serialized, "mainPage", main);
            Set(serialized, "settingsPage", settings);
            Set(serialized, "gameQuitter", quitter);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(resume.onClick, view.Resume);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, view.ShowSettingsPage);
            UnityEventTools.AddPersistentListener(
                settings.GetComponent<SettingsMenuView>().BackRequested,
                view.ShowMainPage);
            UnityEventTools.AddPersistentListener(exit.onClick, view.ExitGame);
            return root;
        }

        private static GameObject BuildRebindControl()
        {
            GameObject row = Row(null, "Input Action");
            row.name = "InputRebindControl";
            Text action = row.transform.GetChild(0).GetComponent<Text>();
            Button button = AddButton(row.transform, "Rebind");
            button.GetComponent<LayoutElement>().preferredWidth = 220;
            Text binding = button.GetComponentInChildren<Text>();
            InputRebindControl control = row.AddComponent<InputRebindControl>();
            SerializedObject serialized = new(control);
            Set(serialized, "actionLabel", action);
            Set(serialized, "bindingLabel", binding);
            Set(serialized, "rebindButton", button);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        private static GameObject Panel(string name, Color color)
        {
            GameObject panel = DefaultControls.CreatePanel(Resources);
            panel.name = name;
            panel.GetComponent<Image>().color = color;
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(860, 0);
            return panel;
        }

        private static void AddTitle(Transform parent, string value)
        {
            Text text = AddText(parent, value, 30);
            text.alignment = TextAnchor.MiddleCenter;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        }

        private static void AddNotice(Transform parent, string title, string body)
        {
            AddText(parent, title, 18).gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            Text text = AddText(parent, body, 14);
            text.color = new Color(0.75f, 0.78f, 0.82f);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
        }

        private static Toggle AddToggle(Transform parent, string label)
        {
            GameObject item = DefaultControls.CreateToggle(Resources);
            item.name = label;
            item.transform.SetParent(parent, false);
            item.GetComponentInChildren<Text>().text = label;
            item.AddComponent<LayoutElement>().preferredHeight = 34;
            return item.GetComponent<Toggle>();
        }

        private static Dropdown AddDropdown(Transform parent, string label)
        {
            GameObject row = Row(parent, label);
            GameObject item = DefaultControls.CreateDropdown(Resources);
            item.name = "Control";
            item.transform.SetParent(row.transform, false);
            item.AddComponent<LayoutElement>().preferredWidth = 300;
            return item.GetComponent<Dropdown>();
        }

        private static Slider AddSlider(Transform parent, string label)
        {
            GameObject row = Row(parent, label);
            GameObject item = DefaultControls.CreateSlider(Resources);
            item.name = "Control";
            item.transform.SetParent(row.transform, false);
            item.AddComponent<LayoutElement>().preferredWidth = 300;
            Slider slider = item.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static GameObject Row(Transform parent, string label)
        {
            GameObject row = new(label, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            if (parent != null) row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 38;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18;
            layout.childAlignment = TextAnchor.MiddleLeft;
            Text text = AddText(row.transform, label, 16);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return row;
        }

        private static Button AddButton(Transform parent, string label)
        {
            GameObject item = DefaultControls.CreateButton(Resources);
            item.name = label;
            item.transform.SetParent(parent, false);
            item.GetComponentInChildren<Text>().text = label;
            item.AddComponent<LayoutElement>().preferredHeight = 52;
            return item.GetComponent<Button>();
        }

        private static Text AddText(Transform parent, string value, int size)
        {
            GameObject item = DefaultControls.CreateText(Resources);
            item.name = "Label";
            item.transform.SetParent(parent, false);
            Text text = item.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            return text;
        }

        private static void Set(SerializedObject owner, string property, Object value) =>
            owner.FindProperty(property).objectReferenceValue = value;
    }
}
