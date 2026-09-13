using NUnit.Framework;
using QuietStatic.Toolkit.Audio;
using QuietStatic.Toolkit.Characters.Player;
using QuietStatic.Toolkit.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace QuietStatic.Tests.EditMode
{
    public class PackagePrefabNeutralityTests
    {
        private const string FirstPersonPlayerPath =
            "Packages/com.quietstatic.core/Runtime/Characters/Prefabs/FirstPersonPlayer.prefab";
        private const string HudPath =
            "Packages/com.quietstatic.core/Runtime/UI/Prefabs/hud.prefab";
        private const string CrosshairPath =
            "Packages/com.quietstatic.core/Runtime/UI/Prefabs/crosshair_white.png";

        [Test]
        public void FirstPersonPlayer_HasNoConsumerOwnedInputOrFootstepConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonPlayerPath);
            Assert.That(prefab, Is.Not.Null, $"Could not load {FirstPersonPlayerPath}.");

            PlayerInputReader inputReader = prefab.GetComponentInChildren<PlayerInputReader>(true);
            PlayerInput playerInput = prefab.GetComponentInChildren<PlayerInput>(true);
            PlayerFootsteps footsteps = prefab.GetComponentInChildren<PlayerFootsteps>(true);
            CharacterMotor motor = prefab.GetComponentInChildren<CharacterMotor>(true);
            MovementStateController movementState =
                prefab.GetComponentInChildren<MovementStateController>(true);

            Assert.That(inputReader, Is.Not.Null);
            Assert.That(playerInput, Is.Not.Null);
            Assert.That(footsteps, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(movementState, Is.Not.Null);

            var inputReaderSerialized = new SerializedObject(inputReader);
            SerializedProperty inputActions = inputReaderSerialized.FindProperty("inputActions");
            Assert.That(inputActions.objectReferenceValue, Is.Null);
            Assert.That(inputActions.prefabOverride, Is.False,
                "The neutral package prefab should inherit PlayerInputReader's empty input asset.");

            var playerInputSerialized = new SerializedObject(playerInput);
            SerializedProperty actions = playerInputSerialized.FindProperty("m_Actions");
            SerializedProperty defaultActionMap = playerInputSerialized.FindProperty("m_DefaultActionMap");
            SerializedProperty actionEvents = playerInputSerialized.FindProperty("m_ActionEvents");

            Assert.That(actions.objectReferenceValue, Is.Null);
            Assert.That(defaultActionMap.stringValue, Is.Empty);
            Assert.That(actionEvents.arraySize, Is.Zero);
            Assert.That(actions.prefabOverride, Is.False,
                "The neutral package prefab should inherit PlayerInput's empty input asset.");
            Assert.That(defaultActionMap.prefabOverride, Is.False);
            Assert.That(actionEvents.prefabOverride, Is.False);

            var footstepsSerialized = new SerializedObject(footsteps);
            Assert.That(footstepsSerialized.FindProperty("footstepClips").arraySize, Is.Zero);

            var motorSerialized = new SerializedObject(motor);
            Assert.That(
                motorSerialized.FindProperty("movementStateController").objectReferenceValue,
                Is.SameAs(movementState),
                "Neutralizing project-owned input must preserve package-owned component wiring.");
        }

        [Test]
        public void Hud_UsesItsPackageOwnedCrosshairSprite()
        {
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(CrosshairPath);

            Assert.That(hud, Is.Not.Null, $"Could not load {HudPath}.");
            Assert.That(expected, Is.Not.Null, $"Could not load {CrosshairPath}.");

            Image[] images = hud.GetComponentsInChildren<Image>(true);
            Image crosshair = null;
            foreach (Image image in images)
            {
                if (image.name == "img_crosshair_white")
                {
                    crosshair = image;
                    break;
                }
            }

            Assert.That(crosshair, Is.Not.Null, "HUD crosshair Image is missing.");
            Assert.That(crosshair.sprite, Is.SameAs(expected));
        }
    }
}
