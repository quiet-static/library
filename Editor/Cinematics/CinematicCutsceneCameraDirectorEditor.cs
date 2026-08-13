using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Adds validation and undoable edit-mode shot placement to the camera director.</summary>
    [CustomEditor(typeof(CinematicCutsceneCameraDirector))]
    [CanEditMultipleObjects]
    public sealed class CinematicCutsceneCameraDirectorEditor : UnityEditor.Editor
    {
        private int selectedShotIndex;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox(
                    "Select one camera director to preview its shots.",
                    MessageType.Info);
                return;
            }

            var director = (CinematicCutsceneCameraDirector)target;
            DrawIdentityWarnings(director);
            DrawPreviewControls(director);
        }

        private void DrawPreviewControls(
            CinematicCutsceneCameraDirector director)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Shot Preview", EditorStyles.boldLabel);

            var labels = new List<string>();
            for (int index = 0; index < director.ShotCount; index++)
            {
                string name = director.GetShotDisplayName(index);
                string id = director.GetExplicitShotId(index);
                labels.Add(string.IsNullOrEmpty(id)
                    ? $"{name} — <Shot ID required>"
                    : string.Equals(name, id, StringComparison.Ordinal)
                        ? name
                        : $"{name} — {id}");
            }

            if (labels.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add at least one shot before previewing.",
                    MessageType.Info);
                return;
            }

            selectedShotIndex = Mathf.Clamp(
                selectedShotIndex,
                0,
                labels.Count - 1);
            selectedShotIndex = EditorGUILayout.Popup(
                "Camera Shot",
                selectedShotIndex,
                labels.ToArray());

            string buttonLabel = EditorApplication.isPlaying
                ? "Cut to Selected Shot"
                : "Move Camera to Selected Shot";
            using (new EditorGUI.DisabledScope(
                       !director.IsShotUsable(selectedShotIndex)))
            {
                if (GUILayout.Button(buttonLabel))
                {
                    CinematicShotPreviewUtility.MoveCameraToShot(
                        director,
                        selectedShotIndex);
                }
            }
        }

        private static void DrawIdentityWarnings(
            CinematicCutsceneCameraDirector director)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var problems = new List<string>();
            for (int index = 0; index < director.ShotCount; index++)
            {
                string id = director.GetExplicitShotId(index);
                if (string.IsNullOrEmpty(id))
                {
                    problems.Add(
                        $"Shot {index + 1} needs an explicit Shot ID before dropdowns can reference it.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    problems.Add($"Shot ID '{id}' is duplicated.");
                }
            }

            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    string.Join("\n", problems),
                    MessageType.Warning);
            }
        }
    }

    /// <summary>Shared undoable edit-mode operation used by cinematic shot inspectors.</summary>
    public static class CinematicShotPreviewUtility
    {
        /// <summary>Moves a director's cutscene camera to a configured shot.</summary>
        /// <param name="director">Director that owns the requested shot.</param>
        /// <param name="shotId">Stable ID of the requested shot.</param>
        /// <returns>True when the shot was found and applied.</returns>
        public static bool MoveCameraToShot(
            CinematicCutsceneCameraDirector director,
            string shotId)
        {
            if (director == null ||
                !director.TryGetShotIndex(shotId, out int shotIndex))
            {
                return false;
            }

            return MoveCameraToShot(director, shotIndex);
        }

        /// <summary>Moves a director's cutscene camera to a configured shot.</summary>
        /// <param name="director">Director that owns the requested shot.</param>
        /// <param name="shotIndex">Zero-based index of the requested shot.</param>
        /// <returns>True when the shot was usable and applied.</returns>
        public static bool MoveCameraToShot(
            CinematicCutsceneCameraDirector director,
            int shotIndex)
        {
            if (director == null || !director.IsShotUsable(shotIndex))
            {
                return false;
            }

            if (EditorApplication.isPlaying)
            {
                director.CutToShot(shotIndex);
                return true;
            }

            Camera camera = director.CutsceneCamera;
            var undoTargets = new List<UnityEngine.Object>
            {
                director.transform,
            };
            if (camera != null)
            {
                undoTargets.Add(camera);
            }

            Undo.RecordObjects(
                undoTargets.ToArray(),
                "Move Cinematic Camera to Shot");
            if (!director.PreviewShot(shotIndex))
            {
                return false;
            }

            EditorUtility.SetDirty(director.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                director.transform);
            if (camera != null)
            {
                EditorUtility.SetDirty(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }

            if (director.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }

            SceneView.RepaintAll();
            return true;
        }
    }
}
