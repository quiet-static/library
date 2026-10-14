using System;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    [CustomEditor(typeof(CinematicTreePlayer))]
    public sealed class CinematicTreePlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var player = (CinematicTreePlayer)target;
            if (player.Tree != null && GUILayout.Button("Edit Cinematic Tree")) CinematicTreeWorkspaceWindow.Open(player.Tree);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play")) player.Play();
                if (GUILayout.Button("Advance")) player.Advance();
                if (GUILayout.Button("Stop")) player.Stop();
            }
            if (player.IsRunning) EditorGUILayout.LabelField("Current Beat", player.CurrentBeat?.id ?? "");
        }
    }

    [CustomPropertyDrawer(typeof(CinematicTreeShotCues.Cue))]
    public sealed class CinematicTreeCueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * 4 + 12 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("onBeatEntered"));
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(row, label);
            row.y += row.height + 3;
            var player = property.serializedObject.FindProperty("player")?.objectReferenceValue as CinematicTreePlayer;
            var beatId = property.FindPropertyRelative("beatId");
            string[] ids = new[] { "" }.Concat(player?.Tree?.Beats.Select(beat => beat?.id ?? "") ?? Array.Empty<string>()).Distinct().ToArray();
            if (!ids.Contains(beatId.stringValue)) ids = ids.Concat(new[] { beatId.stringValue }).ToArray();
            string[] labels = ids.Select(id => id.Length == 0 ? "<Select Beat>" : player?.Tree?.Beats.Any(beat => beat?.id == id) == true ? id : "<Missing> " + id).ToArray();
            EditorGUI.BeginChangeCheck();
            int choice = EditorGUI.Popup(row, "Text Beat", Mathf.Max(0, Array.IndexOf(ids, beatId.stringValue)), labels);
            if (EditorGUI.EndChangeCheck()) beatId.stringValue = ids[choice];
            row.y += row.height + 3;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("cameraDirector"));
            row.y += row.height + 3;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("cameraShotId"));
            row.y += row.height + 3;
            row.height = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("onBeatEntered"));
            EditorGUI.PropertyField(row, property.FindPropertyRelative("onBeatEntered"));
            EditorGUI.EndProperty();
        }
    }
}
