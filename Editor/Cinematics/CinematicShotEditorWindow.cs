using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Creates, edits, and previews shared cinematic shots in loaded scenes.</summary>
    public sealed class CinematicShotEditorWindow : EditorWindow
    {
        [SerializeField] private CinematicCutsceneCameraDirector director;
        [SerializeField] private int selectedShot;
        [SerializeField] private string newShotName = "New Shot";
        private CinematicCutsceneCameraDirector[] directors = new CinematicCutsceneCameraDirector[0];
        private Vector2 scroll;

        /// <summary>Opens the shot authoring tool from the Content Workspace.</summary>
        public static void Open() => GetWindow<CinematicShotEditorWindow>("Camera Shots");

        /// <summary>Opens the tool with a specific scene camera director selected.</summary>
        public static void Open(CinematicCutsceneCameraDirector target)
        {
            var window = GetWindow<CinematicShotEditorWindow>("Camera Shots");
            window.director = target;
            window.selectedShot = 0;
            window.Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(440f, 540f);
            Refresh();
            EditorApplication.hierarchyChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
        }

        private void Refresh()
        {
            directors = Object.FindObjectsByType<CinematicCutsceneCameraDirector>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(candidate => !EditorUtility.IsPersistent(candidate) && candidate.gameObject.scene.IsValid())
                .OrderBy(candidate => candidate.gameObject.scene.name).ThenBy(candidate => candidate.name).ToArray();
            if (director == null)
            {
                director = Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponentInParent<CinematicCutsceneCameraDirector>() : null;
                if (director == null) director = directors.FirstOrDefault();
            }
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Shared Camera Shots", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
                using (new EditorGUI.DisabledScope(director == null || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Save Scene", EditorStyles.toolbarButton)) EditorSceneManager.SaveScene(director.gameObject.scene);
            }
            if (directors.Length == 0)
            {
                EditorGUILayout.HelpBox("Open a scene with a Cinematic Cutscene Camera Director to create and edit its shots.", MessageType.Info);
                return;
            }
            int current = System.Array.IndexOf(directors, director);
            int next = EditorGUILayout.Popup("Cinematic Camera", Mathf.Max(0, current),
                directors.Select(d => d.gameObject.scene.name + " / " + d.name).ToArray());
            if (next != current) { director = directors[next]; selectedShot = 0; }
            if (director == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Leave Play Mode to edit saved shots. You can still preview existing shots.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Create a Shot", EditorStyles.boldLabel);
                newShotName = EditorGUILayout.TextField("Name", newShotName);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(SceneCamera == null))
                        if (GUILayout.Button("New from Scene View")) CreateFrom(SceneCamera);
                    using (new EditorGUI.DisabledScope(director.CutsceneCamera == null || director.CutsceneCamera.orthographic))
                        if (GUILayout.Button("New from Camera")) CreateFrom(director.CutsceneCamera);
                }
                if (SceneCamera == null)
                    EditorGUILayout.HelpBox("Open a perspective Scene View to capture its framing.", MessageType.Info);
            }
            if (director.ShotCount > 0)
            {
                EditorGUILayout.Space();
                selectedShot = Mathf.Clamp(selectedShot, 0, director.ShotCount - 1);
                selectedShot = EditorGUILayout.Popup("Edit Shot", selectedShot,
                    Enumerable.Range(0, director.ShotCount).Select(i => $"{i + 1}. {director.GetShotDisplayName(i)}").ToArray());
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawShot();
                using (new EditorGUI.DisabledScope(!director.IsShotUsable(selectedShot) || director.CutsceneCamera == null))
                    if (GUILayout.Button("Preview on Cinematic Camera"))
                        CinematicShotPreviewUtility.MoveCameraToShot(director, selectedShot);
                if (!director.IsShotUsable(selectedShot))
                    EditorGUILayout.HelpBox("Assign a marker or focus target, or capture a camera view, to make this shot usable.", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        private static Camera SceneCamera => SceneView.lastActiveSceneView != null &&
            !SceneView.lastActiveSceneView.orthographic ? SceneView.lastActiveSceneView.camera : null;

        private void CreateFrom(Camera source)
        {
            selectedShot = CinematicShotAuthoring.Create(director, newShotName,
                source.transform.position, source.transform.rotation, source.fieldOfView);
            GUIUtility.ExitGUI();
        }

        private void DrawShot()
        {
            var serialized = new SerializedObject(director);
            serialized.Update();
            var shot = serialized.FindProperty("shots").GetArrayElementAtIndex(selectedShot);
            EditorGUILayout.PropertyField(shot.FindPropertyRelative("shotName"), new GUIContent("Name"));
            string id = shot.FindPropertyRelative("shotId").stringValue;
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField(new GUIContent("Shot ID", "Stable reference used by cutscenes; renaming the shot does not change this ID."), id);
            if (string.IsNullOrWhiteSpace(id) || Enumerable.Range(0, director.ShotCount).Count(i => director.GetExplicitShotId(i) == id.Trim()) > 1)
                EditorGUILayout.HelpBox("This shot has a missing or duplicate ID. Duplicate it here to create a shot with a new unique ID.", MessageType.Warning);
            EditorGUILayout.PropertyField(shot.FindPropertyRelative("cameraPositionMarker"), new GUIContent("Position Marker"));
            EditorGUILayout.PropertyField(shot.FindPropertyRelative("focusTarget"), new GUIContent("Look At"));
            var marker = shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue as Transform;
            bool hasFocus = shot.FindPropertyRelative("focusTarget").objectReferenceValue != null;
            if (hasFocus) EditorGUILayout.PropertyField(shot.FindPropertyRelative("lookAtOffset"), new GUIContent("Look At Offset"));
            if (marker == null) EditorGUILayout.PropertyField(shot.FindPropertyRelative("targetRelativeOffset"), new GUIContent("Position Offset"));
            EditorGUILayout.PropertyField(shot.FindPropertyRelative("changeFieldOfView"), new GUIContent("Override Lens"));
            if (shot.FindPropertyRelative("changeFieldOfView").boolValue)
                EditorGUILayout.PropertyField(shot.FindPropertyRelative("fieldOfView"), new GUIContent("Field of View"));
            if (serialized.ApplyModifiedProperties())
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(director);
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }
            if (marker != null && !EditorUtility.IsPersistent(marker))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Marker Pose (World)", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 position = EditorGUILayout.Vector3Field("Position", marker.position);
                Vector3 rotation;
                using (new EditorGUI.DisabledScope(hasFocus)) rotation = EditorGUILayout.Vector3Field("Rotation", marker.eulerAngles);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(marker, "Edit Shot Marker");
                    marker.SetPositionAndRotation(position, Quaternion.Euler(rotation));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                    EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
                    SceneView.RepaintAll();
                }
                if (hasFocus) EditorGUILayout.HelpBox("Look At controls the camera's direction. Move the marker to adjust its angle on the subject.", MessageType.None);
                if (GUILayout.Button("Select Marker in Scene")) { Selection.activeTransform = marker; EditorGUIUtility.PingObject(marker); }
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Capture replaces this shot's position and lens and clears Look At to match the captured view. Its Shot ID stays the same.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(SceneCamera == null))
                    if (GUILayout.Button("Capture Scene View")) CaptureFrom(SceneCamera);
                using (new EditorGUI.DisabledScope(director.CutsceneCamera == null || director.CutsceneCamera.orthographic))
                    if (GUILayout.Button("Capture Camera")) CaptureFrom(director.CutsceneCamera);
                if (GUILayout.Button("Duplicate"))
                {
                    selectedShot = CinematicShotAuthoring.Duplicate(director, selectedShot);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void CaptureFrom(Camera source)
        {
            CinematicShotAuthoring.Capture(director, selectedShot,
                source.transform.position, source.transform.rotation, source.fieldOfView);
            GUIUtility.ExitGUI();
        }
    }
}
