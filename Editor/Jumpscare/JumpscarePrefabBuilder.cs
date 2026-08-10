using System.IO;
using QuietStatic.Toolkit.Jumpscare;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Editor.Jumpscare
{
    /// <summary>Builds the package's neutral customizable jumpscare prefab.</summary>
    public static class JumpscarePrefabBuilder
    {
        private const string Output = "Packages/com.quietstatic.core/Runtime/Jumpscare/Prefabs";
        private const string PrefabPath = Output + "/CustomJumpscare.prefab";

        [InitializeOnLoadMethod]
        private static void BuildWhenMissing()
        {
            if (!File.Exists(PrefabPath)) EditorApplication.delayCall += Build;
        }

        [MenuItem("Tools/Quiet Static/Build Custom Jumpscare Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            GameObject root = new("CustomJumpscare");
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            JumpscareEvent sequence = root.AddComponent<JumpscareEvent>();

            GameObject visual = new("ScareVisual");
            visual.transform.SetParent(root.transform, false);
            visual.SetActive(false);

            GameObject particlesObject = new("RevealParticles");
            particlesObject.transform.SetParent(visual.transform, false);
            ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            GameObject lightObject = new("RevealLight");
            lightObject.transform.SetParent(root.transform, false);
            Light revealLight = lightObject.AddComponent<Light>();
            revealLight.type = LightType.Point;
            revealLight.range = 8f;
            revealLight.intensity = 3f;
            revealLight.color = new Color(1f, 0.08f, 0.04f);
            revealLight.enabled = false;

            GameObject canvasObject = new("FlashOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            GameObject imageObject = new("Flash", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            GameObject triggerObject = new("TriggerVolume");
            triggerObject.transform.SetParent(root.transform, false);
            BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2f, 2f, 2f);
            JumpscareTrigger trigger = triggerObject.AddComponent<JumpscareTrigger>();

            SerializedObject sequenceData = new(sequence);
            Set(sequenceData, "scareObject", visual);
            Set(sequenceData, "audioSource", audio);
            Set(sequenceData, "revealParticles", new Object[] { particles });
            Set(sequenceData, "revealLights", new Object[] { revealLight });
            Set(sequenceData, "flashCanvasGroup", canvasGroup);
            Set(sequenceData, "flashImage", image);
            sequenceData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject triggerData = new(trigger);
            Set(triggerData, "jumpscare", sequence);
            triggerData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameLogger.Log(nameof(JumpscarePrefabBuilder), null, $"Created {PrefabPath}");
        }

        private static void Set(SerializedObject owner, string property, Object value) =>
            owner.FindProperty(property).objectReferenceValue = value;

        private static void Set(SerializedObject owner, string property, Object[] values)
        {
            SerializedProperty array = owner.FindProperty(property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
