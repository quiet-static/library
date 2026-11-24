using System;
using NUnit.Framework;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ClutterPrefabAuthoringTests
    {
        private Scene scene;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", $"__ClutterTest_{Guid.NewGuid():N}"));
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void GroupingScenePropsKeepsWorldLayoutAndPrefabOverridesAndCanBeUndone()
        {
            GameObject source = CreateProp("Book", PrimitiveType.Cube);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            placed.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 30, 0));
            var parent = new GameObject("Current layout");
            placed.transform.SetParent(parent.transform, true);
            BoxCollider collider = placed.GetComponent<BoxCollider>();
            collider.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            var mug = new GameObject("Mug");
            mug.transform.position = new Vector3(3, 3, 4);

            GameObject group = ClutterPrefabAuthoring.CreateAssembly(new[] { placed, mug }, "Bedside Clutter");

            Assert.That(placed.transform.parent, Is.EqualTo(group.transform));
            Assert.That(placed.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(Quaternion.Angle(placed.transform.rotation, Quaternion.Euler(0, 30, 0)), Is.LessThan(0.01f));
            Assert.That(mug.transform.position, Is.EqualTo(new Vector3(3, 3, 4)));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(placed), Is.EqualTo(source));
            Assert.That(collider.enabled, Is.False);

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();

            Assert.That(group == null, Is.True);
            Assert.That(placed.transform.parent, Is.EqualTo(parent.transform));
            Assert.That(mug.transform.parent, Is.Null);
        }

        [Test]
        public void SavedGroupPreservesNestedLinksAndLeavesTheEditableStagingGroup()
        {
            GameObject book = CreateProp("Book", PrimitiveType.Cube);
            GameObject mug = CreateProp("Mug", PrimitiveType.Cylinder);
            GameObject paper = CreateProp("Paper", PrimitiveType.Quad);
            GameObject group = ClutterPrefabAuthoring.CreateAssembly(new[] { book, mug }, "Bedside Clutter");
            ClutterPrefabAuthoring.AddProps(group, new[] { paper });

            string path = $"{folder}/Bedside Clutter.prefab";
            GameObject prefab = ClutterPrefabAuthoring.SavePrefab(group, path);

            Assert.That(prefab.transform.childCount, Is.EqualTo(3));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab.transform.GetChild(0).gameObject),
                Is.EqualTo(AssetDatabase.GetAssetPath(book)));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab.transform.GetChild(2).gameObject),
                Is.EqualTo(AssetDatabase.GetAssetPath(paper)));
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(group), Is.False);
            Assert.That(group.transform.childCount, Is.EqualTo(3));
            Assert.Throws<InvalidOperationException>(() => ClutterPrefabAuthoring.SavePrefab(group, path));
        }

        [Test]
        public void SavingReportsSceneReferencesThatCannotTravelWithThePrefab()
        {
            var prop = new GameObject("Prop");
            var speaker = new GameObject("External speaker");
            prop.AddComponent<DialogueEventPlayer>().SetDefaultSpeaker(speaker.transform);
            GameObject group = ClutterPrefabAuthoring.CreateAssembly(new[] { prop }, "Group");

            var error = Assert.Throws<InvalidOperationException>(() =>
                ClutterPrefabAuthoring.SavePrefab(group, $"{folder}/Group.prefab"));

            Assert.That(error.Message, Does.Contain(speaker.name));
            Assert.That(AssetDatabase.LoadMainAssetAtPath($"{folder}/Group.prefab"), Is.Null);
        }

        private GameObject CreateProp(string name, PrimitiveType primitive)
        {
            GameObject prop = GameObject.CreatePrimitive(primitive);
            prop.name = name;
            try { return PrefabUtility.SaveAsPrefabAsset(prop, $"{folder}/{name}.prefab"); }
            finally { Object.DestroyImmediate(prop); }
        }
    }
}
