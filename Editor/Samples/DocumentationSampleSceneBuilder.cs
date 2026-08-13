using System.IO;
using QuietStatic.Toolkit.Horror;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Jumpscare;
using QuietStatic.Toolkit.Narrative;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Editor.Samples
{
    /// <summary>Builds general instructional scenes used by the package documentation.</summary>
    public static class DocumentationSampleSceneBuilder
    {
        private const string SampleRoot = "Packages/com.quietstatic.core/Samples";
        private const string DefinitionRoot = SampleRoot + "/Definitions";

        [InitializeOnLoadMethod]
        private static void BuildWhenMissing()
        {
            if (!File.Exists(SampleRoot + "/SystemsAndMenusExample.unity") ||
                !File.Exists(SampleRoot + "/NarrativeHorrorExample.unity") ||
                !File.Exists(SampleRoot + "/InteractionObjectiveExample.unity"))
                EditorApplication.delayCall += BuildAll;
        }

        [MenuItem("Tools/Quiet Static/Builders/Build Documentation Sample Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(DefinitionRoot);
            HorrorTensionDefinition tension = CreateTensionDefinition();
            StorySequenceDefinition story = CreateStoryDefinition();
            ObjectiveDefinition objective = CreateObjectiveDefinition();
            BuildSystemsAndMenus();
            BuildNarrativeHorror(tension, story);
            BuildInteractionObjective();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameLogger.Log(nameof(DocumentationSampleSceneBuilder), null,
                "Created three Quiet Static documentation sample scenes.");
        }

        private static void BuildSystemsAndMenus()
        {
            Scene scene = NewScene("Systems & Menus Example");
            GameObject systems = new("Persistent Systems - one copy only");
            systems.AddComponent<FlagManager>();
            systems.AddComponent<SettingsManager>();

            GameObject canvasObject = new("Persistent UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            InstantiatePrefab("Runtime/UI/Prefabs/SettingsMenu.prefab", canvasObject.transform, "Settings Menu - assign project styling");
            GameObject pause = InstantiatePrefab("Runtime/UI/Prefabs/PauseMenu.prefab", canvasObject.transform, "Pause Menu - normally loaded additively");
            if (pause != null) pause.SetActive(false);
            Save(scene, "SystemsAndMenusExample");
        }

        private static void BuildNarrativeHorror(HorrorTensionDefinition tension, StorySequenceDefinition story)
        {
            Scene scene = NewScene("Narrative & Horror Example");
            GameObject flags = new("Flags (move to persistent Systems scene in a game)");
            flags.AddComponent<FlagManager>();

            GameObject narrative = new("Story Sequence Runner");
            StorySequenceRunner runner = narrative.AddComponent<StorySequenceRunner>();
            Set(runner, "sequence", story);

            GameObject tensionObject = new("Horror Tension Controller");
            tensionObject.AddComponent<AudioSource>();
            HorrorTensionController controller = tensionObject.AddComponent<HorrorTensionController>();
            Set(controller, "definition", tension);

            InstantiatePrefab("Runtime/Jumpscare/Prefabs/CustomJumpscare.prefab", null, "Custom Jumpscare - replace visual and audio");
            Save(scene, "NarrativeHorrorExample");
        }

        private static void BuildInteractionObjective()
        {
            Scene scene = NewScene("Interaction & Objective Example");
            GameObject flags = new("Flags (persistent in production)");
            flags.AddComponent<FlagManager>();

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Inspectable Object - configure success events";
            Interactable interactable = target.AddComponent<Interactable>();
            Set(interactable, "displayName", "Inspect");

            GameObject objectiveObject = new("Objective Manager - move to Systems scene");
            objectiveObject.AddComponent<ObjectiveManager>();
            new GameObject("Objective asset example - see Definitions folder");
            Save(scene, "InteractionObjectiveExample");
        }

        private static Scene NewScene(string rootName)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(rootName);
            return scene;
        }

        private static void Save(Scene scene, string name) =>
            EditorSceneManager.SaveScene(scene, $"{SampleRoot}/{name}.unity");

        private static GameObject InstantiatePrefab(string packageRelativePath, Transform parent, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.quietstatic.core/" + packageRelativePath);
            if (prefab == null) return null;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            if (parent != null) instance.transform.SetParent(parent, false);
            return instance;
        }

        private static HorrorTensionDefinition CreateTensionDefinition()
        {
            const string path = DefinitionRoot + "/ExampleTension.asset";
            HorrorTensionDefinition asset = AssetDatabase.LoadAssetAtPath<HorrorTensionDefinition>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<HorrorTensionDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            SerializedObject data = new(asset);
            data.FindProperty("defaultStateId").stringValue = "calm";
            SerializedProperty states = data.FindProperty("states");
            states.arraySize = 2;
            SetTensionState(states.GetArrayElementAtIndex(0), "calm", 0);
            SetTensionState(states.GetArrayElementAtIndex(1), "uneasy", 10);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetTensionState(SerializedProperty state, string id, int priority)
        {
            state.FindPropertyRelative("id").stringValue = id;
            state.FindPropertyRelative("priority").intValue = priority;
        }

        private static StorySequenceDefinition CreateStoryDefinition()
        {
            const string path = DefinitionRoot + "/ExampleStorySequence.asset";
            StorySequenceDefinition asset = AssetDatabase.LoadAssetAtPath<StorySequenceDefinition>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<StorySequenceDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            SerializedObject data = new(asset);
            data.FindProperty("id").stringValue = "example.chapter";
            data.FindProperty("startingStageId").stringValue = "arrival";
            SerializedProperty stages = data.FindProperty("stages");
            stages.arraySize = 2;
            SetStoryStage(stages.GetArrayElementAtIndex(0), "arrival", "Arrival", "investigate");
            SetStoryStage(stages.GetArrayElementAtIndex(1), "investigate", "Investigate", string.Empty);
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void SetStoryStage(SerializedProperty stage, string id, string title, string next)
        {
            stage.FindPropertyRelative("id").stringValue = id;
            stage.FindPropertyRelative("title").stringValue = title;
            stage.FindPropertyRelative("nextStageId").stringValue = next;
        }

        private static ObjectiveDefinition CreateObjectiveDefinition()
        {
            const string path = DefinitionRoot + "/ExampleObjective.asset";
            ObjectiveDefinition asset = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            SerializedObject data = new(asset);
            data.FindProperty("id").stringValue = "example.inspect";
            data.FindProperty("title").stringValue = "Inspect the object";
            data.FindProperty("description").stringValue = "Use the interaction to inspect the marked object.";
            data.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void Set(Object target, string property, Object value)
        {
            SerializedObject data = new(target);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, string value)
        {
            SerializedObject data = new(target);
            data.FindProperty(property).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
