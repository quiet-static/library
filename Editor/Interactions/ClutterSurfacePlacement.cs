using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Previews and commits rigid clutter group placement in an open scene.</summary>
    internal static class ClutterSurfacePlacement
    {
        private const float MaximumRayDistance = 2000f;
        private const string PlaceUndoName = "Place Clutter Prefab";
        private const string SnapUndoName = "Level and Snap Clutter";
        private static PlacementSession session;

        internal static bool IsPlacing => session != null;

        internal static void Begin(
            GameObject prefab, bool alignToSurface, float offset, Transform parent = null, GameObject meshSurface = null)
        {
            Cancel();
            if (!CanPlace(out string error) || !IsFinite(offset))
            {
                Debug.LogWarning(error ?? "Clutter placement requires a finite surface offset.");
                return;
            }

            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.transform.parent != null)
            {
                Debug.LogWarning("Choose a prefab asset root to place clutter.");
                return;
            }

            Scene scene = parent != null ? parent.gameObject.scene : SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !TryGetParentScale(parent, out float parentScale))
            {
                Debug.LogWarning("Use an open scene and a uniformly scaled, non-mirrored placement parent.");
                return;
            }

            var geometry = new ClutterPlacementGeometry(prefab, prefab.transform.localScale * parentScale);
            if (!geometry.HasGeometry)
            {
                Debug.LogWarning("The prefab needs an enabled mesh renderer for surface placement.", prefab);
                return;
            }

            var surface = new ClutterPlacementSurface(scene, meshSurface, System.Array.Empty<Transform>());
            if (!surface.IsValid)
            {
                Debug.LogWarning("Choose an enabled triangle mesh in the placement scene as the mesh surface.", meshSurface);
                return;
            }

            session = new PlacementSession(prefab, parent, scene, geometry, surface, alignToSurface, offset, parentScale);
            SceneView.duringSceneGui += DrawScenePlacement;
            EditorApplication.playModeStateChanged += HandlePlayModeChange;
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) view = EditorWindow.GetWindow<SceneView>();
            view.Focus();
            SceneView.RepaintAll();
        }

        internal static void Cancel()
        {
            if (session == null) return;

            session = null;
            SceneView.duringSceneGui -= DrawScenePlacement;
            EditorApplication.playModeStateChanged -= HandlePlayModeChange;
            SceneView.RepaintAll();
        }

        internal static void SnapSelection(bool alignToSurface, float offset, GameObject meshSurface = null)
        {
            Cancel();
            if (!CanPlace(out string error) || !IsFinite(offset))
            {
                Debug.LogWarning(error ?? "Clutter placement requires a finite surface offset.");
                return;
            }

            Transform[] selection = Selection.transforms.Where(item => item != null &&
                !EditorUtility.IsPersistent(item) && item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded).ToArray();
            Transform[] roots = selection.Where(item => !selection.Any(other => other != item && item.IsChildOf(other))).ToArray();
            if (roots.Length == 0)
            {
                Debug.LogWarning("Select one or more clutter groups in the scene to level and snap.");
                return;
            }

            Physics.SyncTransforms();
            var surfaces = new Dictionary<Scene, ClutterPlacementSurface>();
            var placements = new List<(Transform Target, Pose Pose)>();
            foreach (Transform root in roots)
            {
                Scene scene = root.gameObject.scene;
                if (!surfaces.TryGetValue(scene, out ClutterPlacementSurface surface))
                {
                    surface = new ClutterPlacementSurface(scene, meshSurface, roots);
                    surfaces.Add(scene, surface);
                }

                if (TrySnap(root, surface, alignToSurface, offset, out Pose pose, out string detail))
                    placements.Add((root, pose));

                if (detail.Length != 0)
                    Debug.LogWarning($"Clutter placement for '{root.name}': {detail}", root);
            }

            if (placements.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(SnapUndoName);
            foreach (var placement in placements)
            {
                Undo.RecordObject(placement.Target, SnapUndoName);
                ApplyPose(placement.Target, placement.Pose);
            }

            Undo.CollapseUndoOperations(undoGroup);
            SceneView.RepaintAll();
        }

        private static bool TrySnap(
            Transform root, ClutterPlacementSurface surface, bool alignToSurface, float offset, out Pose pose, out string detail)
        {
            pose = default;
            if (!surface.IsValid)
            {
                detail = "Choose an enabled triangle mesh in the same scene, outside the selected groups.";
                return false;
            }

            if (!TryGetParentScale(root.parent, out float parentScale))
            {
                detail = "The group needs a uniformly scaled, non-mirrored parent.";
                return false;
            }

            Vector3 worldScale = root.localScale * parentScale;
            var geometry = new ClutterPlacementGeometry(root.gameObject, worldScale);
            if (!geometry.HasGeometry)
            {
                detail = "The group needs an enabled mesh renderer.";
                return false;
            }

            Vector3 anchor = root.position + root.rotation * geometry.Anchor;
            Vector3 start = anchor + Vector3.up * Mathf.Max(0.25f, geometry.Height);
            if (!surface.Raycast(new Ray(start, Vector3.down), MaximumRayDistance, out ClutterPlacementSurface.Hit hit))
            {
                detail = "No placement surface was found below the group.";
                return false;
            }

            return geometry.TryPlace(surface, hit, root.rotation, alignToSurface, offset, out pose, out detail);
        }

        private static void DrawScenePlacement(SceneView view)
        {
            PlacementSession current = session;
            if (current == null) return;
            if (!CanPlace(out _) || current.Prefab == null || !current.Scene.isLoaded)
            {
                Cancel();
                return;
            }

            if (current.HasParent && (current.Parent == null || current.Parent.gameObject.scene != current.Scene ||
                !TryGetParentScale(current.Parent, out float parentScale) || !Mathf.Approximately(parentScale, current.ParentScale)))
            {
                Debug.LogWarning("The placement parent or its scale changed. Start placement again with the updated parent.");
                Cancel();
                return;
            }

            Event input = Event.current;
            if (input.type == EventType.KeyDown)
            {
                if (input.keyCode == KeyCode.Escape)
                {
                    input.Use();
                    Cancel();
                    return;
                }

                if (!input.alt && !input.control && !input.command && (input.keyCode == KeyCode.Q || input.keyCode == KeyCode.E))
                {
                    current.Yaw += input.keyCode == KeyCode.Q ? -15f : 15f;
                    input.Use();
                    view.Repaint();
                    return;
                }
            }

            if (input.type == EventType.Layout)
            {
                if (!input.alt) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (input.type == EventType.MouseMove)
            {
                view.Repaint();
                return;
            }

            bool commit = input.type == EventType.MouseDown && input.button == 0 && !input.alt && !input.control && !input.command;
            if (input.type != EventType.Repaint && !commit) return;

            Physics.SyncTransforms();
            Ray ray = HandleUtility.GUIPointToWorldRay(input.mousePosition);
            string message = "Point at a collider or the assigned mesh surface.";
            if (current.Surface.Raycast(ray, MaximumRayDistance, out ClutterPlacementSurface.Hit hit))
            {
                Quaternion heading = Quaternion.AngleAxis(current.Yaw, current.Align ? hit.Normal : Vector3.up) * current.Prefab.transform.rotation;
                if (current.Geometry.TryPlace(current.Surface, hit, heading, current.Align, current.Offset, out Pose pose, out message))
                {
                    if (commit)
                    {
                        input.Use();
                        Commit(current, pose);
                        Cancel();
                        return;
                    }

                    DrawPreview(current.Geometry, pose, hit.Point, message.Length != 0);
                }
            }

            if (commit) input.Use();
            else DrawControls(message);
        }

        private static void DrawPreview(ClutterPlacementGeometry geometry, Pose pose, Vector3 point, bool warning)
        {
            Color color = warning ? new Color(1f, 0.67f, 0.22f) : new Color(0.2f, 0.85f, 0.65f);
            var matrix = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
            using (new Handles.DrawingScope(color, matrix))
            {
                foreach (Bounds part in geometry.Parts) Handles.DrawWireCube(part.center, part.size);
            }

            using (new Handles.DrawingScope(color))
            {
                Handles.DrawWireDisc(point, pose.rotation * Vector3.up, HandleUtility.GetHandleSize(point) * 0.06f);
            }
        }

        private static void DrawControls(string message)
        {
            Handles.BeginGUI();
            var region = new Rect(12f, 46f, 420f, message.Length != 0 ? 45f : 25f);
            string text = "Click to place    Q / E to rotate    Esc to cancel";
            if (message.Length != 0) text += "\n" + message;
            GUI.Box(region, text, EditorStyles.helpBox);
            Handles.EndGUI();
        }

        private static void Commit(PlacementSession request, Pose pose)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(PlaceUndoName);
            var instance = PrefabUtility.InstantiatePrefab(request.Prefab, request.Scene) as GameObject;
            if (instance == null) return;

            Undo.RegisterCreatedObjectUndo(instance, PlaceUndoName);
            Transform target = instance.transform;
            if (request.Parent != null) Undo.SetTransformParent(target, request.Parent, PlaceUndoName);
            Undo.RecordObject(target, PlaceUndoName);
            target.localScale = request.Prefab.transform.localScale;
            ApplyPose(target, pose);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instance;
        }

        private static void ApplyPose(Transform target, Pose pose)
        {
            target.SetPositionAndRotation(pose.position, pose.rotation);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        }

        private static bool CanPlace(out string error)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Leave Play Mode to place clutter.";
                return false;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                error = "Leave Prefab Mode to place clutter in a scene.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryGetParentScale(Transform parent, out float scale)
        {
            scale = 1f;
            if (parent == null) return true;

            Matrix4x4 matrix = parent.localToWorldMatrix;
            Quaternion unrotate = Quaternion.Inverse(parent.rotation);
            Vector3 right = unrotate * matrix.MultiplyVector(Vector3.right);
            Vector3 up = unrotate * matrix.MultiplyVector(Vector3.up);
            Vector3 forward = unrotate * matrix.MultiplyVector(Vector3.forward);
            scale = right.x;
            float tolerance = Mathf.Max(0.00001f, scale * 0.0001f);
            return scale > 0.00001f &&
                (right - Vector3.right * scale).sqrMagnitude < tolerance * tolerance &&
                (up - Vector3.up * scale).sqrMagnitude < tolerance * tolerance &&
                (forward - Vector3.forward * scale).sqrMagnitude < tolerance * tolerance;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void HandlePlayModeChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Cancel();
        }

        private sealed class PlacementSession
        {
            internal readonly GameObject Prefab;
            internal readonly Transform Parent;
            internal readonly Scene Scene;
            internal readonly ClutterPlacementGeometry Geometry;
            internal readonly ClutterPlacementSurface Surface;
            internal readonly bool Align;
            internal readonly float Offset;
            internal readonly bool HasParent;
            internal readonly float ParentScale;
            internal float Yaw;

            internal PlacementSession(GameObject prefab, Transform parent, Scene scene, ClutterPlacementGeometry geometry,
                ClutterPlacementSurface surface, bool align, float offset, float parentScale)
            {
                Prefab = prefab;
                Parent = parent;
                Scene = scene;
                Geometry = geometry;
                Surface = surface;
                Align = align;
                Offset = offset;
                HasParent = parent != null;
                ParentScale = parentScale;
            }
        }
    }
}
