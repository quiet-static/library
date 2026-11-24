using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Groups props in the scene and saves the group as a reusable prefab.</summary>
    internal static class ClutterPrefabAuthoring
    {
        private const string AssembleUndo = "Assemble Clutter";
        private const string AddUndo = "Add Clutter Props";
        private const float PropSpacing = 0.15f;

        internal static GameObject CreateAssembly(IEnumerable<GameObject> sources, string name)
        {
            GameObject[] props = GetSourceRoots(sources);
            if (props.Length == 0) throw new InvalidOperationException("Add at least one prop to assemble.");

            GameObject[] sceneProps = props.Where(prop => !EditorUtility.IsPersistent(prop)).ToArray();
            Scene scene = sceneProps.Length > 0 ? sceneProps[0].scene : SceneManager.GetActiveScene();
            ValidateSources(props, scene);

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(AssembleUndo);
            try
            {
                var group = new GameObject(string.IsNullOrWhiteSpace(name) ? "Clutter Group" : name.Trim());
                if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
                group.transform.position = FindAssemblyOrigin(sceneProps);
                Undo.RegisterCreatedObjectUndo(group, AssembleUndo);
                Populate(group, props, AssembleUndo);
                Undo.CollapseUndoOperations(undo);
                return group;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        internal static void AddProps(GameObject assembly, IEnumerable<GameObject> sources)
        {
            ValidateAssembly(assembly);
            GameObject[] available = sources?.Where(source => source != null && source != assembly &&
                !source.transform.IsChildOf(assembly.transform)).ToArray();
            GameObject[] props = GetSourceRoots(available);
            if (props.Any(prop => assembly.transform.IsChildOf(prop.transform)))
                throw new InvalidOperationException("The assembly cannot contain one of its own parents.");
            ValidateSources(props, assembly.scene);
            if (props.Length == 0) return;
            Matrix4x4 worldToAssembly = assembly.transform.worldToLocalMatrix;
            if (props.Any(prop => !EditorUtility.IsPersistent(prop) &&
                HasShearedBasis(worldToAssembly * prop.transform.localToWorldMatrix)))
                throw new InvalidOperationException("The assembly's scale would distort the selected hierarchy props. Use a uniformly scaled assembly.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(AddUndo);
            try
            {
                Populate(assembly, props, AddUndo);
                Undo.CollapseUndoOperations(undo);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        internal static GameObject SavePrefab(GameObject assembly, string assetPath)
        {
            ValidateAssembly(assembly);
            assetPath = assetPath?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(assetPath), ".prefab", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Split('/').Any(segment => segment is "" or "." or "..") ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/')))
                throw new ArgumentException("Choose a prefab path in an existing folder under Assets.");
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || File.Exists(assetPath) || Directory.Exists(assetPath))
                throw new InvalidOperationException($"A file or folder already exists at {assetPath}. Choose a new name.");
            if (assembly.transform.childCount == 0)
                throw new InvalidOperationException("Add props to the assembly before saving it.");

            ValidatePrefabReferences(assembly);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(assembly, assetPath, out bool saved);
            if (!saved || prefab == null) throw new InvalidOperationException($"Unity could not save {assetPath}.");
            return prefab;
        }

        private static GameObject[] GetSourceRoots(IEnumerable<GameObject> sources)
        {
            GameObject[] distinct = sources?.Where(source => source != null).Distinct().ToArray() ?? Array.Empty<GameObject>();
            var selected = new HashSet<Transform>(distinct.Select(source => source.transform));
            return distinct.Where(source => !HasSelectedParent(source.transform, selected)).ToArray();
        }

        private static bool HasSelectedParent(Transform transform, HashSet<Transform> selected)
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                if (selected.Contains(parent)) return true;
            return false;
        }

        private static void ValidateAssembly(GameObject assembly)
        {
            if (assembly == null || EditorUtility.IsPersistent(assembly) || PrefabUtility.IsPartOfPrefabInstance(assembly))
                throw new InvalidOperationException("Choose an editable scene group for the assembly.");
            ValidateScene(assembly.scene);
        }

        private static void ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
                PrefabStageUtility.GetCurrentPrefabStage() != null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Assemble and save clutter in a loaded scene in Edit Mode. Exit Prefab Mode first.");
        }

        private static void ValidateSources(GameObject[] props, Scene scene)
        {
            ValidateScene(scene);
            foreach (GameObject prop in props)
            {
                if (EditorUtility.IsPersistent(prop))
                {
                    string path = AssetDatabase.GetAssetPath(prop);
                    if (PrefabUtility.GetPrefabAssetType(prop) == PrefabAssetType.NotAPrefab ||
                        AssetDatabase.LoadMainAssetAtPath(path) != prop)
                        throw new InvalidOperationException($"Choose the root model or prefab asset for {prop.name}.");
                    continue;
                }

                if (prop.scene != scene)
                    throw new InvalidOperationException("Select hierarchy props from the same scene as the assembly.");
                if (PrefabUtility.IsPartOfPrefabInstance(prop) && PrefabUtility.GetOutermostPrefabInstanceRoot(prop) != prop)
                    throw new InvalidOperationException($"Select the prefab instance root containing {prop.name} to keep its prefab connection.");
                if (prop.transform is RectTransform || HasShearedBasis(prop.transform.localToWorldMatrix))
                    throw new InvalidOperationException($"{prop.name} has a transform that cannot be regrouped while keeping its world shape.");
            }
        }

        private static bool HasShearedBasis(Matrix4x4 matrix)
        {
            Vector3 x = ((Vector3)matrix.GetColumn(0)).normalized;
            Vector3 y = ((Vector3)matrix.GetColumn(1)).normalized;
            Vector3 z = ((Vector3)matrix.GetColumn(2)).normalized;
            const float tolerance = 0.001f;
            return Mathf.Abs(Vector3.Dot(x, y)) > tolerance || Mathf.Abs(Vector3.Dot(x, z)) > tolerance ||
                Mathf.Abs(Vector3.Dot(y, z)) > tolerance;
        }

        private static Vector3 FindAssemblyOrigin(GameObject[] sceneProps)
        {
            if (sceneProps.Length == 0)
                return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

            Bounds bounds = VisualBounds(sceneProps[0], Matrix4x4.identity);
            foreach (GameObject prop in sceneProps.Skip(1)) bounds.Encapsulate(VisualBounds(prop, Matrix4x4.identity));
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static void Populate(GameObject assembly, GameObject[] props, string label)
        {
            Transform parent = assembly.transform;
            foreach (GameObject prop in props.Where(prop => !EditorUtility.IsPersistent(prop)))
                Undo.SetTransformParent(prop.transform, parent, label);

            float cursor = parent.childCount > 0 ? VisualBounds(assembly, parent.worldToLocalMatrix).max.x + PropSpacing : 0f;
            foreach (GameObject asset in props.Where(EditorUtility.IsPersistent))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, assembly.scene);
                if (instance == null) throw new InvalidOperationException($"Unity could not instantiate {asset.name}.");
                Undo.RegisterCreatedObjectUndo(instance, label);
                Transform placed = instance.transform;
                Undo.SetTransformParent(placed, parent, label);
                Undo.RecordObject(placed, label);
                placed.SetLocalPositionAndRotation(Vector3.zero, asset.transform.localRotation);
                placed.localScale = asset.transform.localScale;

                Bounds bounds = VisualBounds(instance, parent.worldToLocalMatrix);
                placed.localPosition = new Vector3(cursor - bounds.min.x, -bounds.min.y, -bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placed);
                cursor += bounds.size.x + PropSpacing;
            }
        }

        private static Bounds VisualBounds(GameObject root, Matrix4x4 worldToSpace)
        {
            Bounds result = default;
            bool foundPoint = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                Bounds local = renderer.localBounds;
                Matrix4x4 toSpace = worldToSpace * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toSpace.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z));
                    if (foundPoint) result.Encapsulate(point);
                    else result = new Bounds(point, Vector3.zero);
                    foundPoint = true;
                }
            }
            return foundPoint ? result : new Bounds(worldToSpace.MultiplyPoint3x4(root.transform.position), Vector3.zero);
        }

        private static void ValidatePrefabReferences(GameObject assembly)
        {
            foreach (Component component in assembly.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    throw new InvalidOperationException("Remove missing scripts from the assembly before saving a prefab.");

                using var data = new SerializedObject(component);
                using SerializedProperty property = data.GetIterator();
                var visited = new HashSet<long>();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = property.propertyType != SerializedPropertyType.ManagedReference ||
                        visited.Add(property.managedReferenceId);
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (component == assembly.transform && property.propertyPath == "m_Father") continue;
                    GameObject reference = property.objectReferenceValue switch
                    {
                        GameObject referencedObject => referencedObject,
                        Component referencedComponent => referencedComponent.gameObject,
                        _ => null
                    };
                    if (reference != null && reference.scene.IsValid() &&
                        reference != assembly && !reference.transform.IsChildOf(assembly.transform))
                        throw new InvalidOperationException($"{component.name} references scene object {reference.name} outside the assembly. Include it or clear the reference before saving.");
                }
            }
        }
    }
}
