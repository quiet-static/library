using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Visible geometry used to place a rigid group without relying on its pivot.</summary>
    internal sealed class ClutterPlacementGeometry
    {
        private const int FootprintDivisions = 3;
        private readonly List<Vector3> points = new();
        private readonly List<Vector3> footprint = new();
        private readonly List<Bounds> parts = new();

        private Bounds bounds;

        internal bool HasGeometry => points.Count != 0;
        internal IReadOnlyList<Bounds> Parts => parts;
        internal Vector3 Anchor { get; private set; }
        internal float Height => bounds.size.y;

        internal ClutterPlacementGeometry(GameObject target, Vector3 worldScale)
        {
            Transform root = target.transform;
            Matrix4x4 rootSpace = Matrix4x4.Scale(worldScale) * root.worldToLocalMatrix;
            var cachedVertices = new Dictionary<Mesh, Vector3[]>();

            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;

                if (!cachedVertices.TryGetValue(mesh, out Vector3[] vertices))
                {
                    using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
                    vertices = ReadVertices(data[0]);
                    cachedVertices.Add(mesh, vertices);
                }

                Matrix4x4 transform = rootSpace * filter.transform.localToWorldMatrix;
                Bounds part = default;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 point = transform.MultiplyPoint3x4(vertices[index]);
                    if (points.Count == 0) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    if (index == 0) part = new Bounds(point, Vector3.zero);
                    else part.Encapsulate(point);
                    points.Add(point);
                }

                if (vertices.Length != 0) parts.Add(part);
            }

            if (!HasGeometry) return;

            Transform marker = root.Find("Placement Anchor");
            Anchor = marker != null
                ? rootSpace.MultiplyPoint3x4(marker.position)
                : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            BuildFootprint();
        }

        private void BuildFootprint()
        {
            int cellCount = FootprintDivisions * FootprintDivisions;
            var cells = new Vector3[cellCount];
            var occupied = new bool[cellCount];
            float tolerance = Mathf.Clamp(bounds.size.y * 0.1f, 0.005f, 0.05f);
            foreach (Vector3 point in points)
            {
                if (point.y > bounds.min.y + tolerance) continue;
                int x = GetCell(point.x, bounds.min.x, bounds.max.x);
                int z = GetCell(point.z, bounds.min.z, bounds.max.z);
                int cell = z * FootprintDivisions + x;
                if (occupied[cell] && point.y >= cells[cell].y) continue;
                cells[cell] = point;
                occupied[cell] = true;
            }

            for (int cell = 0; cell < cellCount; cell++)
            {
                if (occupied[cell]) footprint.Add(cells[cell]);
            }
        }

        private static int GetCell(float value, float min, float max) =>
            Mathf.Min(FootprintDivisions - 1, (int)(Mathf.InverseLerp(min, max, value) * FootprintDivisions));

        internal bool TryPlace(
            ClutterPlacementSurface surface, ClutterPlacementSurface.Hit hit, Quaternion heading,
            bool alignToSurface, float offset, out Pose pose, out string warning)
        {
            pose = default;
            warning = string.Empty;
            Vector3 up = alignToSurface ? hit.Normal : Vector3.up;
            float alignment = Vector3.Dot(hit.Normal, up);
            if (alignment < 0.1f)
            {
                warning = "Choose a more upward-facing surface or enable Align to Surface.";
                return false;
            }

            Quaternion rotation = Level(heading, up);
            Vector3 relativeNormal = Quaternion.Inverse(rotation) * hit.Normal;
            float minimum = float.PositiveInfinity;
            Vector3 contact = default;
            foreach (Vector3 point in points)
            {
                float distance = Vector3.Dot(relativeNormal, point - Anchor);
                if (distance >= minimum) continue;
                minimum = distance;
                contact = point;
            }

            Vector3 pivot = hit.Point - rotation * Anchor + up * (offset - minimum / alignment);
            float probeHeight = Mathf.Max(0.25f, Height + Mathf.Abs(offset));
            int supported = 0;
            float lowest = float.PositiveInfinity;
            float highest = float.NegativeInfinity;
            foreach (Vector3 point in footprint) MeasureSupport(point);
            bool extraContact = !footprint.Contains(contact);
            if (extraContact) MeasureSupport(contact);

            if (supported < footprint.Count + (extraContact ? 1 : 0))
            {
                warning = "Some parts of the group extend beyond the surface.";
                if (supported > 0) pivot += up * Mathf.Max(0f, highest);
            }
            else if (supported > 0)
            {
                pivot += up * highest;
                if (highest - lowest > 0.025f) warning = "The group has uneven support on this surface.";
            }

            pose = new Pose(pivot, rotation);
            return true;

            void MeasureSupport(Vector3 point)
            {
                Vector3 support = pivot + rotation * point;
                var probe = new Ray(support + up * probeHeight, -up);
                if (!surface.Raycast(probe, 2f * probeHeight, out var below, hit.Collider)) return;

                float shift = Vector3.Dot(below.Point - support, up) + offset;
                lowest = Mathf.Min(lowest, shift);
                highest = Mathf.Max(highest, shift);
                supported++;
            }
        }

        private static Quaternion Level(Quaternion heading, Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(heading * Vector3.forward, up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.Cross(Vector3.ProjectOnPlane(heading * Vector3.right, up), up);
            return Quaternion.LookRotation(forward, up);
        }

        internal static Vector3[] ReadVertices(Mesh.MeshData data)
        {
            if (data.vertexCount == 0 || !data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position))
                return Array.Empty<Vector3>();

            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices);
            return vertices.ToArray();
        }
    }

    /// <summary>Surfaces used by the placement preview and the support probes.</summary>
    internal sealed class ClutterPlacementSurface
    {
        internal readonly struct Hit
        {
            internal readonly Vector3 Point;
            internal readonly Vector3 Normal;
            internal readonly Collider Collider;

            internal Hit(Vector3 point, Vector3 normal, Collider collider = null)
            {
                Point = point;
                Normal = normal;
                Collider = collider;
            }
        }

        private sealed class SurfaceMesh
        {
            internal readonly Transform Transform;
            internal readonly Bounds Bounds;
            internal readonly Vector3[] Vertices;
            internal readonly List<int> Triangles = new();

            internal SurfaceMesh(MeshFilter filter)
            {
                Mesh mesh = filter.sharedMesh;
                Transform = filter.transform;
                Bounds = mesh.bounds;
                using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
                Mesh.MeshData data = snapshot[0];
                Vertices = ClutterPlacementGeometry.ReadVertices(data);
                if (Vertices.Length == 0) return;

                for (int index = 0; index < data.subMeshCount; index++)
                {
                    var submesh = data.GetSubMesh(index);
                    if (submesh.topology != MeshTopology.Triangles) continue;
                    using var indices = new NativeArray<int>(submesh.indexCount, Allocator.Temp);
                    data.GetIndices(indices, index);
                    foreach (int vertex in indices) Triangles.Add(vertex);
                }
            }

            internal bool Raycast(Ray ray, ref float nearest, out Hit hit)
            {
                hit = default;
                if (Transform == null || !Transform.gameObject.activeInHierarchy) return false;

                Matrix4x4 inverse = Transform.worldToLocalMatrix;
                Vector3 origin = inverse.MultiplyPoint3x4(ray.origin);
                Vector3 direction = inverse.MultiplyVector(ray.direction);
                if (!Bounds.IntersectRay(new Ray(origin, direction))) return false;

                float hitDistance = nearest;
                Vector3 normal = Vector3.zero;
                for (int index = 0; index + 2 < Triangles.Count; index += 3)
                {
                    Vector3 a = Vertices[Triangles[index]];
                    Vector3 edgeA = Vertices[Triangles[index + 1]] - a;
                    Vector3 edgeB = Vertices[Triangles[index + 2]] - a;
                    if (Intersect(origin, direction, a, edgeA, edgeB, out float distance) && distance < hitDistance)
                    {
                        hitDistance = distance;
                        normal = Vector3.Cross(edgeA, edgeB);
                    }
                }

                normal = inverse.transpose.MultiplyVector(normal);
                float normalLengthSquared = normal.sqrMagnitude;
                if (!(normalLengthSquared > 0f) || float.IsInfinity(normalLengthSquared)) return false;

                // Imported mesh triangles can produce very short normals after inverse scaling.
                normal /= Mathf.Sqrt(normalLengthSquared);
                if (Vector3.Dot(ray.direction, normal) > 0f) normal = -normal;
                nearest = hitDistance;
                hit = new Hit(ray.GetPoint(nearest), normal);
                return true;
            }

            private static bool Intersect(
                Vector3 origin, Vector3 direction, Vector3 a, Vector3 edgeA, Vector3 edgeB, out float distance)
            {
                distance = 0f;
                Vector3 cross = Vector3.Cross(direction, edgeB);
                float determinant = Vector3.Dot(edgeA, cross);
                float determinantTolerance = 0.000001f * Mathf.Sqrt(edgeA.sqrMagnitude * cross.sqrMagnitude);
                if (Mathf.Abs(determinant) <= determinantTolerance) return false;

                float inverse = 1f / determinant;
                Vector3 toOrigin = origin - a;
                float u = Vector3.Dot(toOrigin, cross) * inverse;
                if (u < -0.00001f || u > 1.00001f) return false;

                Vector3 otherCross = Vector3.Cross(toOrigin, edgeA);
                float v = Vector3.Dot(direction, otherCross) * inverse;
                if (v < -0.00001f || u + v > 1.00001f) return false;

                distance = Vector3.Dot(edgeB, otherCross) * inverse;
                return distance >= 0f;
            }
        }

        private readonly Scene scene;
        private readonly Transform[] excluded;
        private readonly List<SurfaceMesh> meshes;
        private RaycastHit[] raycastHits = new RaycastHit[32];

        internal bool IsValid => meshes == null || meshes.Count != 0;

        internal ClutterPlacementSurface(Scene scene, GameObject explicitSurface, Transform[] excluded)
        {
            this.scene = scene;
            this.excluded = excluded;
            if (explicitSurface == null) return;

            meshes = new List<SurfaceMesh>();
            if (explicitSurface.scene != scene) return;
            foreach (MeshFilter filter in explicitSurface.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || IsExcluded(filter.transform) ||
                    !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;
                var mesh = new SurfaceMesh(filter);
                if (mesh.Triangles.Count != 0) meshes.Add(mesh);
            }
        }

        internal bool Raycast(Ray ray, float distance, out Hit hit, Collider support = null)
        {
            hit = default;
            bool found = false;
            if (meshes != null)
            {
                foreach (SurfaceMesh mesh in meshes)
                {
                    if (!mesh.Raycast(ray, ref distance, out Hit candidate)) continue;
                    hit = candidate;
                    found = true;
                }

                return found;
            }

            if (support != null)
            {
                if (!support.Raycast(ray, out RaycastHit below, distance)) return false;
                hit = new Hit(below.point, below.normal, support);
                return true;
            }

            PhysicsScene physics = scene.GetPhysicsScene();
            if (!physics.IsValid()) return false;

            int count = CastScene(physics, ray, distance);
            while (count == raycastHits.Length && raycastHits.Length < 1024)
            {
                Array.Resize(ref raycastHits, raycastHits.Length * 2);
                count = CastScene(physics, ray, distance);
            }

            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = raycastHits[index];
                if (candidate.collider == null || candidate.collider.gameObject.scene != scene ||
                    IsExcluded(candidate.collider.transform) || candidate.distance >= distance ||
                    SceneVisibilityManager.instance.IsHidden(candidate.collider.gameObject)) continue;
                hit = new Hit(candidate.point, candidate.normal, candidate.collider);
                distance = candidate.distance;
                found = true;
            }

            return found;
        }

        private int CastScene(PhysicsScene physics, Ray ray, float distance) =>
            physics.Raycast(ray.origin, ray.direction, raycastHits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

        private bool IsExcluded(Transform item)
        {
            foreach (Transform root in excluded)
            {
                if (root != null && (item == root || item.IsChildOf(root))) return true;
            }

            return false;
        }
    }
}
