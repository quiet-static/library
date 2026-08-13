using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Interactions;
using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Generates working example assets for cinematics and readable interactions.</summary>
    public static class CinematicExamplePrefabBuilder
    {
        private const string Root = "Assets/QuietStatic Examples/Cinematics and Readables";

        [MenuItem("Tools/Quiet Static/Cinematics/Generate Cinematic & Readable Examples")]
        public static void Generate()
        {
            GenerateExamples(true);
        }

        /// <summary>Generates examples without displaying a completion dialog.</summary>
        public static void GenerateSilently()
        {
            GenerateExamples(false);
        }

        private static void GenerateExamples(bool showDialog)
        {
            EnsureFolder("Assets", "QuietStatic Examples");
            EnsureFolder("Assets/QuietStatic Examples", "Cinematics and Readables");
            EnsureFolder(Root, "Channels");
            EnsureFolder(Root, "Content");
            EnsureFolder(Root, "Prefabs");

            ScreenFadeChannel fadeChannel = CreateOrLoad<ScreenFadeChannel>(
                $"{Root}/Channels/ExampleScreenFadeChannel.asset");
            InteractionUIChannel uiChannel = CreateOrLoad<InteractionUIChannel>(
                $"{Root}/Channels/ExampleInteractionUIChannel.asset");
            ReadableContentDefinition letter = CreateOrLoad<ReadableContentDefinition>(
                $"{Root}/Content/ExampleLetter.asset");
            Set(letter, "title", "A Letter Left Behind");
            Set(letter, "body", "Sam,\n\nI left this here in case you came looking for me. The spare key is where we used to hide it.\n\n— A.");
            Set(letter, "closeLabel", "Put Away");

            CreateCutscenePrefab(fadeChannel);
            CreateScreenFaderPrefab(fadeChannel);
            CreateReadableOverlayPrefab(uiChannel);
            CreateReadableItemPrefab(uiChannel, letter);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>($"{Root}/Prefabs/ExampleReadableOverlay.prefab");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Examples Generated",
                    $"Created or refreshed examples under:\n{Root}",
                    "OK");
            }
        }

        private static void CreateCutscenePrefab(ScreenFadeChannel channel)
        {
            GameObject root = new("ExampleCutscene");
            CutsceneSequenceRunner runner = root.AddComponent<CutsceneSequenceRunner>();
            CutsceneCharacterController characters = root.AddComponent<CutsceneCharacterController>();
            DialogueNodeCinematicCue nodeCues = root.AddComponent<DialogueNodeCinematicCue>();
            DialogueRunner dialogue = Child(root.transform, "Dialogue").gameObject.AddComponent<DialogueRunner>();
            Transform cameraRig = Child(root.transform, "Cinematic Camera");
            Camera camera = cameraRig.gameObject.AddComponent<Camera>();
            cameraRig.gameObject.AddComponent<AudioListener>();
            CinematicCutsceneCameraDirector director = cameraRig.gameObject.AddComponent<CinematicCutsceneCameraDirector>();
            Transform markers = Child(root.transform, "Shot Markers");
            Transform pose = Child(markers, "Shot 01 - Wide");
            pose.localPosition = new Vector3(0f, 1.6f, -3f);
            Transform closePose = Child(markers, "Shot 02 - Close");
            closePose.localPosition = new Vector3(0.8f, 1.7f, -1.8f);

            Set(director, "cutsceneCamera", camera);
            SerializedObject serializedDirector = new(director);
            SerializedProperty shots = serializedDirector.FindProperty("shots");
            shots.arraySize = 2;
            ConfigureShot(
                shots.GetArrayElementAtIndex(0),
                "example.wide",
                "Wide",
                pose,
                50f);
            ConfigureShot(
                shots.GetArrayElementAtIndex(1),
                "example.close",
                "Close",
                closePose,
                38f);
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();

            Set(nodeCues, "dialogueRunner", dialogue);
            SerializedObject serialized = new(runner);
            serialized.FindProperty("fadeChannel").objectReferenceValue = channel;
            SerializedProperty steps = serialized.FindProperty("steps");
            steps.arraySize = 1;
            SerializedProperty step = steps.GetArrayElementAtIndex(0);
            step.FindPropertyRelative("name").stringValue = "Opening Shot";
            step.FindPropertyRelative("cameraDirector").objectReferenceValue = director;
            step.FindPropertyRelative("cameraShotId").stringValue = "example.wide";
            step.FindPropertyRelative("dialogueRunner").objectReferenceValue = dialogue;
            step.FindPropertyRelative("waitAfterStep").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Keep the controller in the starter hierarchy so designers can add reusable
            // character entries and per-node CutsceneCharacterStepTrigger children.
            _ = characters;
            SavePrefab(root, "ExampleCutscene.prefab");
        }

        private static void ConfigureShot(
            SerializedProperty shot,
            string id,
            string name,
            Transform marker,
            float fieldOfView)
        {
            shot.FindPropertyRelative("shotId").stringValue = id;
            shot.FindPropertyRelative("shotName").stringValue = name;
            shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue = marker;
            shot.FindPropertyRelative("changeFieldOfView").boolValue = true;
            shot.FindPropertyRelative("fieldOfView").floatValue = fieldOfView;
        }

        private static void CreateScreenFaderPrefab(ScreenFadeChannel channel)
        {
            GameObject root = UiRoot("ExampleScreenFader");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            Image image = root.AddComponent<Image>();
            image.color = Color.black;
            ScreenFader fader = root.AddComponent<ScreenFader>();
            ScreenFadeChannelHandler handler = root.AddComponent<ScreenFadeChannelHandler>();
            Set(fader, "canvasGroup", group);
            Set(fader, "fadeImage", image);
            Set(handler, "channel", channel);
            Set(handler, "screenFader", fader);
            SavePrefab(root, "ExampleScreenFader.prefab");
        }

        private static void CreateReadableOverlayPrefab(InteractionUIChannel channel)
        {
            GameObject root = UiRoot("ExampleReadableOverlay");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.72f);
            ReadableOverlayHandler handler = root.AddComponent<ReadableOverlayHandler>();

            GameObject panel = UiChild(root.transform, "Readable Panel", new Vector2(760f, 650f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.93f, 0.89f, 0.76f, 1f);
            Component title = CreateText(panel.transform, "Title", new Vector2(680f, 70f), 34f);
            Component body = CreateText(panel.transform, "Body", new Vector2(680f, 430f), 25f);
            GameObject buttonObject = UiChild(panel.transform, "Close Button", new Vector2(180f, 58f));
            Button button = buttonObject.AddComponent<Button>();
            Image buttonImage = buttonObject.AddComponent<Image>();
            button.targetGraphic = buttonImage;
            Component closeText = CreateText(buttonObject.transform, "Label", new Vector2(170f, 48f), 23f);
            UnityEventTools.AddPersistentListener(button.onClick, handler.Close);

            Set(handler, "channel", channel);
            Set(handler, "canvasGroup", group);
            Set(handler, "backdrop", backdrop);
            Set(handler, "titleText", title);
            Set(handler, "bodyText", body);
            Set(handler, "closeLabelText", closeText);
            SavePrefab(root, "ExampleReadableOverlay.prefab");
        }

        private static void CreateReadableItemPrefab(
            InteractionUIChannel channel,
            ReadableContentDefinition content)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "ExampleReadableLetter";
            root.transform.localScale = new Vector3(0.22f, 0.015f, 0.3f);
            Interactable interactable = root.AddComponent<Interactable>();
            ReadableInteractionTrigger trigger = root.AddComponent<ReadableInteractionTrigger>();
            Set(interactable, "displayName", "Read Letter");
            Set(trigger, "channel", channel);
            Set(trigger, "content", content);
            SavePrefab(root, "ExampleReadableLetter.prefab");
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Set(Object target, string property, Object value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, string value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SavePrefab(GameObject root, string fileName)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{fileName}");
            Object.DestroyImmediate(root);
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject UiRoot(string name)
        {
            GameObject value = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)value.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return value;
        }

        private static GameObject UiChild(Transform parent, string name, Vector2 size)
        {
            GameObject value = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)value.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return value;
        }

        private static Component CreateText(
            Transform parent,
            string name,
            Vector2 size,
            float fontSize)
        {
            GameObject value = UiChild(parent, name, size);
            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (textType == null)
            {
                throw new InvalidOperationException(
                    "TextMeshProUGUI could not be loaded. Import TextMeshPro before generating examples.");
            }

            Component text = value.AddComponent(textType);
            SerializedObject serialized = new(text);
            SerializedProperty sizeProperty = serialized.FindProperty("m_fontSize");
            if (sizeProperty != null) sizeProperty.floatValue = fontSize;
            SerializedProperty colorProperty = serialized.FindProperty("m_fontColor");
            if (colorProperty != null) colorProperty.colorValue = Color.black;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
