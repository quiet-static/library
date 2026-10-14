using System;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Undoable authoring operations on a director's existing scene-local shot list.</summary>
    public static class CinematicShotAuthoring
    {
        /// <summary>Creates an independent marker and shot using a world-space camera pose.</summary>
        public static int Create(CinematicCutsceneCameraDirector director, string name,
            Vector3 position, Quaternion rotation, float fieldOfView, int copyFrom = -1)
        {
            Validate(director);
            if (copyFrom < -1 || copyFrom >= director.ShotCount)
                throw new ArgumentOutOfRangeException(nameof(copyFrom));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Camera Shot");
            string label = string.IsNullOrWhiteSpace(name) ? "New Shot" : name.Trim();
            // Hierarchy Undo operations can refresh serialized objects. Create the marker
            // before opening the serialized shot list so pending edits cannot be discarded.
            Transform marker = CreateMarker(director, label, position, rotation);
            Undo.RegisterCompleteObjectUndo(director, "Create Camera Shot");
            var serialized = new SerializedObject(director);
            var shots = serialized.FindProperty("shots");
            int index = shots.arraySize;
            shots.InsertArrayElementAtIndex(index);
            var shot = shots.GetArrayElementAtIndex(index);
            shot.FindPropertyRelative("shotId").stringValue = "shot." + Guid.NewGuid().ToString("N");
            shot.FindPropertyRelative("shotName").stringValue = label;
            shot.FindPropertyRelative("focusTarget").objectReferenceValue = null;
            shot.FindPropertyRelative("targetRelativeOffset").vector3Value = Vector3.zero;
            shot.FindPropertyRelative("lookAtOffset").vector3Value = Vector3.zero;
            shot.FindPropertyRelative("changeFieldOfView").boolValue = true;
            shot.FindPropertyRelative("fieldOfView").floatValue = Mathf.Clamp(fieldOfView, 10f, 100f);
            if (copyFrom >= 0)
            {
                var source = shots.GetArrayElementAtIndex(copyFrom);
                shot.FindPropertyRelative("focusTarget").objectReferenceValue = source.FindPropertyRelative("focusTarget").objectReferenceValue;
                shot.FindPropertyRelative("lookAtOffset").vector3Value = source.FindPropertyRelative("lookAtOffset").vector3Value;
                shot.FindPropertyRelative("targetRelativeOffset").vector3Value = source.FindPropertyRelative("targetRelativeOffset").vector3Value;
                shot.FindPropertyRelative("changeFieldOfView").boolValue = source.FindPropertyRelative("changeFieldOfView").boolValue;
                shot.FindPropertyRelative("fieldOfView").floatValue = source.FindPropertyRelative("fieldOfView").floatValue;
            }
            shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue = marker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Finish(director);
            Undo.CollapseUndoOperations(group);
            return index;
        }

        /// <summary>Duplicates a shot without sharing its editable position marker or stable ID.</summary>
        public static int Duplicate(CinematicCutsceneCameraDirector director, int index)
        {
            Validate(director);
            var shot = GetShot(director, index);
            var marker = shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue as Transform;
            var focus = shot.FindPropertyRelative("focusTarget").objectReferenceValue as Transform;
            Vector3 position = marker != null ? marker.position : focus != null
                ? focus.TransformPoint(shot.FindPropertyRelative("targetRelativeOffset").vector3Value)
                : director.transform.position;
            Quaternion rotation = marker != null ? marker.rotation : director.transform.rotation;
            return Create(director, director.GetShotDisplayName(index) + " Copy", position, rotation,
                shot.FindPropertyRelative("fieldOfView").floatValue, index);
        }

        /// <summary>Replaces a shot's framing with a camera pose, retaining its stable ID.</summary>
        public static void Capture(CinematicCutsceneCameraDirector director, int index,
            Vector3 position, Quaternion rotation, float fieldOfView)
        {
            Validate(director);
            var shot = GetShot(director, index);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Capture Camera Shot");
            var marker = shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue as Transform;
            var shots = shot.serializedObject.FindProperty("shots");
            int uses = 0;
            for (int i = 0; i < shots.arraySize; i++)
                if (shots.GetArrayElementAtIndex(i).FindPropertyRelative("cameraPositionMarker").objectReferenceValue == marker) uses++;
            if (marker == null || EditorUtility.IsPersistent(marker) || uses > 1 ||
                marker.gameObject.scene != director.gameObject.scene || marker == director.transform || marker.IsChildOf(director.transform))
                marker = CreateMarker(director, director.GetShotDisplayName(index), position, rotation);
            else
            {
                Undo.RecordObject(marker, "Capture Camera Shot");
                marker.SetPositionAndRotation(position, rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
            }
            Undo.RegisterCompleteObjectUndo(director, "Capture Camera Shot");
            shot = GetShot(director, index);
            shot.FindPropertyRelative("cameraPositionMarker").objectReferenceValue = marker;
            shot.FindPropertyRelative("focusTarget").objectReferenceValue = null;
            shot.FindPropertyRelative("lookAtOffset").vector3Value = Vector3.zero;
            shot.FindPropertyRelative("changeFieldOfView").boolValue = true;
            shot.FindPropertyRelative("fieldOfView").floatValue = Mathf.Clamp(fieldOfView, 10f, 100f);
            shot.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            Finish(director);
            Undo.CollapseUndoOperations(group);
        }

        private static SerializedProperty GetShot(CinematicCutsceneCameraDirector director, int index)
        {
            if (index < 0 || index >= director.ShotCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new SerializedObject(director).FindProperty("shots").GetArrayElementAtIndex(index);
        }

        private static Transform CreateMarker(CinematicCutsceneCameraDirector director, string name,
            Vector3 position, Quaternion rotation)
        {
            Transform parent = director.transform.parent;
            Transform folder = parent != null ? parent.Find("Shots") : director.gameObject.scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Shots")?.transform;
            if (folder == null)
            {
                var created = new GameObject("Shots");
                SceneManager.MoveGameObjectToScene(created, director.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(created, "Create Shots Group");
                if (parent != null) Undo.SetTransformParent(created.transform, parent, "Parent Shots Group");
                created.transform.localPosition = Vector3.zero;
                created.transform.localRotation = Quaternion.identity;
                folder = created.transform;
            }
            var marker = new GameObject(name);
            SceneManager.MoveGameObjectToScene(marker, director.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(marker, "Create Shot Marker");
            Undo.SetTransformParent(marker.transform, folder, "Parent Shot Marker");
            marker.transform.SetPositionAndRotation(position, rotation);
            return marker.transform;
        }

        private static void Validate(CinematicCutsceneCameraDirector director)
        {
            if (director == null || EditorUtility.IsPersistent(director) || !director.gameObject.scene.IsValid()
                || !director.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Select a camera director in a loaded scene outside Play Mode.");
        }

        private static void Finish(CinematicCutsceneCameraDirector director)
        {
            EditorUtility.SetDirty(director);
            PrefabUtility.RecordPrefabInstancePropertyModifications(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            SceneView.RepaintAll();
        }
    }
}
