using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using QuietStatic.Toolkit.Spawning;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class SpawnManagerTests
    {
        private GameObject managerObject;
        private SpawnManager manager;
        private List<GameObject> createdSpawnPoints;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Spawn Manager");
            manager = managerObject.AddComponent<SpawnManager>();
            createdSpawnPoints = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawnPoint in createdSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    Object.DestroyImmediate(spawnPoint);
                }
            }

            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void MoveToSpawn_AppliesPositionAndRotation()
        {
            SpawnPoint spawnPoint = CreateSpawnPoint(
                "Entrance",
                new Vector3(4f, 2f, -3f),
                Quaternion.Euler(0f, 90f, 0f));
            var target = new GameObject("Target");

            bool moved = manager.MoveToSpawn(
                target.transform,
                " Entrance ");

            Assert.That(moved, Is.True);
            Assert.That(
                target.transform.position,
                Is.EqualTo(spawnPoint.transform.position));
            Assert.That(
                target.transform.rotation,
                Is.EqualTo(spawnPoint.transform.rotation));

            Object.DestroyImmediate(target);
        }

        [Test]
        public void MoveToSpawn_UsesConfiguredFallback()
        {
            SpawnPoint fallback = CreateSpawnPoint(
                "Default",
                new Vector3(1f, 0f, 7f),
                Quaternion.identity);
            var target = new GameObject("Target");

            bool moved = manager.MoveToSpawn(
                target.transform,
                "Missing");

            Assert.That(moved, Is.True);
            Assert.That(
                target.transform.position,
                Is.EqualTo(fallback.transform.position));

            Object.DestroyImmediate(target);
        }

        [Test]
        public void Spawn_InstantiatesPrefabAtResolvedPoint()
        {
            SpawnPoint spawnPoint = CreateSpawnPoint(
                "Kitchen",
                new Vector3(8f, 1f, 2f),
                Quaternion.Euler(0f, 180f, 0f));
            var prefab = new GameObject("Prefab");

            GameObject instance = manager.Spawn(prefab, "Kitchen");

            Assert.That(instance, Is.Not.Null);
            Assert.That(
                instance.transform.position,
                Is.EqualTo(spawnPoint.transform.position));
            Assert.That(
                instance.transform.rotation,
                Is.EqualTo(spawnPoint.transform.rotation));

            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void RegisteredMove_DiscoversSpawnTargetWhenRegistryIsEmpty()
        {
            SpawnPoint spawnPoint = CreateSpawnPoint(
                "Checkpoint",
                new Vector3(-2f, 3f, 5f),
                Quaternion.identity);
            var targetObject = new GameObject("Player");
            SpawnTarget spawnTarget =
                targetObject.AddComponent<SpawnTarget>();
            SetPrivateField(spawnTarget, "targetId", "Player");

            bool moved = manager.MoveRegisteredTargetToSpawn(
                "Player",
                "Checkpoint");

            Assert.That(moved, Is.True);
            Assert.That(
                targetObject.transform.position,
                Is.EqualTo(spawnPoint.transform.position));
            Assert.That(manager.RegisteredTargetCount, Is.EqualTo(1));

            Object.DestroyImmediate(targetObject);
        }

        private SpawnPoint CreateSpawnPoint(
            string id,
            Vector3 position,
            Quaternion rotation)
        {
            var spawnObject = new GameObject($"Spawn {id}");
            createdSpawnPoints.Add(spawnObject);
            spawnObject.transform.SetPositionAndRotation(position, rotation);
            SpawnPoint spawnPoint =
                spawnObject.AddComponent<SpawnPoint>();
            SetPrivateField(spawnPoint, "id", id);
            return spawnPoint;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
