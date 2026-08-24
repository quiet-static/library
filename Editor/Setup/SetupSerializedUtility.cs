using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Setup
{
    /// <summary>Shared deterministic primitives for project-owned setup recipes.</summary>
    public static class SetupSerializedUtility
    {
        /// <summary>Assigns one serialized object reference and reports whether it changed.</summary>
        public static bool AssignObjectReference(
            Object target,
            string propertyName,
            Object expected)
        {
            if (target == null)
            {
                throw new MissingReferenceException("A serialized assignment target is required.");
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null ||
                property.propertyType != SerializedPropertyType.ObjectReference)
            {
                throw new MissingReferenceException(
                    $"{target.GetType().Name}.{propertyName} is not a serialized object reference.");
            }

            if (property.objectReferenceValue == expected)
            {
                return false;
            }

            property.objectReferenceValue = expected;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            return true;
        }

        /// <summary>Returns whether a serialized object reference already matches.</summary>
        public static bool HasObjectReference(Object target, string propertyName, Object expected)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                throw new MissingReferenceException(
                    $"{target.GetType().Name}.{propertyName} is not a serialized object reference.");
            }
            return property.objectReferenceValue == expected;
        }

        /// <summary>Enumerates components owned by one scene, including inactive objects.</summary>
        public static IEnumerable<T> FindComponents<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    yield return component;
                }
            }
        }

        /// <summary>Finds zero or one component and throws when scene ownership is ambiguous.</summary>
        public static T FindSingleComponent<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (T component in FindComponents<T>(scene))
            {
                if (found != null)
                {
                    throw new MissingReferenceException(
                        $"{scene.path} requires exactly one {typeof(T).Name}.");
                }
                found = component;
            }
            return found;
        }

        /// <summary>Gets an existing component or adds exactly one to its owner.</summary>
        public static T GetOrAddComponent<T>(GameObject owner) where T : Component =>
            owner.GetComponent<T>() ?? owner.AddComponent<T>();
    }
}
