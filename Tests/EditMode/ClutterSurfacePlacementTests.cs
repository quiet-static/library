using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ClutterSurfacePlacementTests
    {
        private Scene testScene;
        private Mesh customSurface;

        [SetUp]
        public void SetUp()
        {
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            ClutterSurfacePlacement.Cancel();
            if (testScene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (customSurface != null) Object.DestroyImmediate(customSurface);
        }

        [Test]
        public void SnapUsesVisibleGeometryPreservingHeadingAndChildLayoutAndSupportsUndo()
        {
            GameObject table = Cube("Table", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
            GameObject trigger = Cube("Overlapping interaction trigger", Vector3.up, new Vector3(10f, 0.1f, 10f));
            trigger.GetComponent<BoxCollider>().isTrigger = true;
            var group = new GameObject("Group");
            Vector3 originalPosition = new(0.2f, 2f, 0.4f);
            Quaternion originalRotation = Quaternion.Euler(23f, 36f, -16f);
            group.transform.SetPositionAndRotation(originalPosition, originalRotation);
            GameObject prop = Cube("Off-center prop", Vector3.zero, new Vector3(0.8f, 0.6f, 0.5f));
            prop.transform.SetParent(group.transform, false);
            prop.transform.localPosition = new Vector3(0.45f, 0.7f, 0.25f);
            Vector3 localPosition = prop.transform.localPosition;
            Vector3 heading = Vector3.ProjectOnPlane(originalRotation * Vector3.forward, Vector3.up).normalized;
            const float offset = 0.03f;
            Selection.activeGameObject = group;

            ClutterSurfacePlacement.SnapSelection(false, offset);

            Assert.That(prop.GetComponent<Renderer>().bounds.min.y,
                Is.EqualTo(table.GetComponent<BoxCollider>().bounds.max.y + offset).Within(0.001f));
            Assert.That(Vector3.Dot(group.transform.up, Vector3.up), Is.GreaterThan(0.9999f));
            Assert.That(Vector3.Dot(group.transform.forward, heading), Is.GreaterThan(0.9999f));
            Assert.That(prop.transform.localPosition, Is.EqualTo(localPosition));

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(group.transform.position, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(group.transform.rotation, originalRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void SnapUsesExplicitRenderOnlySurfaceAndAlignsToItsNormal()
        {
            GameObject surface = Cube("Render-only shelf", Vector3.zero, new Vector3(5f, 0.2f, 5f));
            surface.transform.rotation = Quaternion.Euler(19f, 0f, -13f);
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            Cube("Unrelated closer collider", new Vector3(0f, 0.5f, 0f), new Vector3(5f, 0.1f, 5f));

            GameObject group = Cube("Clutter", Vector3.up * 2f, new Vector3(0.9f, 0.8f, 0.4f));
            Selection.activeGameObject = group;
            const float offset = 0.025f;

            ClutterSurfacePlacement.SnapSelection(true, offset, surface);

            Vector3 up = surface.transform.up;
            Vector3 planePoint = surface.transform.TransformPoint(Vector3.up * 0.5f);
            float lowest = group.GetComponent<MeshFilter>().sharedMesh.vertices
                .Min(vertex => Vector3.Dot(up, group.transform.TransformPoint(vertex) - planePoint));
            Assert.That(Vector3.Dot(group.transform.up, up), Is.GreaterThan(0.9999f));
            Assert.That(lowest, Is.EqualTo(offset).Within(0.001f));
            Assert.That(surface.GetComponents<Collider>(), Is.Empty);
        }

        [Test]
        public void SnapRaisesTheWholeGroupToClearTheHighestSampledSupport()
        {
            var surface = new GameObject("Uneven render-only shelf");
            customSurface = new Mesh { name = "Stepped test support" };
            customSurface.vertices = new[]
            {
                new Vector3(-3f, 0f, -3f), new Vector3(0.2f, 0f, -3f),
                new Vector3(0.2f, 0f, 3f), new Vector3(-3f, 0f, 3f),
                new Vector3(0.2f, 0.16f, -3f), new Vector3(3f, 0.16f, -3f),
                new Vector3(3f, 0.16f, 3f), new Vector3(0.2f, 0.16f, 3f)
            };
            customSurface.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            customSurface.RecalculateBounds();
            surface.AddComponent<MeshFilter>().sharedMesh = customSurface;
            surface.AddComponent<MeshRenderer>();

            GameObject group = Cube("Clutter", Vector3.up * 2, new Vector3(2f, 0.2f, 1.4f));
            Selection.activeGameObject = group;
            LogAssert.Expect(LogType.Warning, new Regex("uneven support", RegexOptions.IgnoreCase));

            ClutterSurfacePlacement.SnapSelection(false, 0f, surface);

            Assert.That(group.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(0.16f).Within(0.001f));
            Assert.That(Vector3.Dot(group.transform.up, Vector3.up), Is.GreaterThan(0.9999f));
        }

        [Test]
        public void SnapUsesTinyImportedTrianglesOnALargeScaleRenderOnlySurface()
        {
            var surface = new GameObject("Scaled render-only shelf");
            surface.transform.localScale = Vector3.one * 100f;
            customSurface = new Mesh { name = "Small local triangles" };
            customSurface.vertices = new[]
            {
                new Vector3(-0.003f, 0f, -0.003f), new Vector3(0.003f, 0f, -0.003f),
                new Vector3(0.003f, 0f, 0.003f), new Vector3(-0.003f, 0f, 0.003f)
            };
            customSurface.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            customSurface.RecalculateBounds();
            surface.AddComponent<MeshFilter>().sharedMesh = customSurface;
            surface.AddComponent<MeshRenderer>();

            GameObject group = Cube("Clutter", Vector3.up, Vector3.one * 0.2f);
            Selection.activeGameObject = group;

            ClutterSurfacePlacement.SnapSelection(false, 0.02f, surface);

            Assert.That(group.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(0.02f).Within(0.001f));
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            return cube;
        }
    }
}
