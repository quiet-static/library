using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Previews and applies interaction presets to explicit scene or prefab targets.</summary>
    internal static class ClutterBatchInteractionAuthoring
    {
        internal enum Preset { Keep, Flavor, Pickup }
        internal enum Status { Ready, Skipped, Applied, Failed, Unchanged }

        internal sealed class Target
        {
            private readonly GameObject source;
            private readonly GlobalObjectId? assetId;
            private readonly string originalPrompt;
            private readonly string originalFlavorText;

            public Target(GameObject source)
            {
                this.source = source;
                if (source != null && EditorUtility.IsPersistent(source))
                    assetId = GlobalObjectId.GetGlobalObjectIdSlow(source);

                originalPrompt = source != null ? source.GetComponent<Interactable>()?.DisplayName ?? string.Empty : string.Empty;
                originalFlavorText = source != null ? source.GetComponent<FlavorTextInteraction>()?.FlavorText ?? string.Empty : string.Empty;
                Prompt = originalPrompt;
                FlavorText = originalFlavorText;
            }

            public GameObject Source => source != null ? source : assetId.HasValue
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(assetId.Value) as GameObject : null;
            public string Prompt { get; set; }
            public string FlavorText { get; set; }

            internal string PromptFor(Interactable current, Preset preset)
            {
                if (current != null && (string.IsNullOrWhiteSpace(Prompt) || Prompt == originalPrompt))
                    return current.DisplayName;
                if (!string.IsNullOrWhiteSpace(Prompt)) return Prompt.Trim();
                return preset == Preset.Flavor ? "Inspect" : "Pick Up";
            }

            internal string FlavorFor(FlavorTextInteraction current) =>
                current != null && FlavorText == originalFlavorText ? current.FlavorText : FlavorText;
        }

        internal sealed class Row
        {
            internal Row(Target target, GameObject owner, string location)
            {
                Target = target;
                Owner = owner;
                Location = location;
            }

            public Target Target { get; }
            public string Location { get; }
            public string Detail { get; private set; }
            public Status Status { get; private set; }
            public bool CanApply => Status == ClutterBatchInteractionAuthoring.Status.Ready;
            internal GameObject Owner { get; }
            internal string PrefabPath { get; set; }
            internal int[] SiblingPath { get; set; }

            internal void SetStatus(Status status, string detail)
            {
                Status = status;
                Detail = detail;
            }
        }

        internal static IReadOnlyList<Row> Preview(IReadOnlyList<Target> targets, Preset preset, InteractionUIChannel channel)
        {
            var rows = new List<Row>(targets.Count);
            var selected = new HashSet<GameObject>(targets.Select(target => target?.Source).Where(source => source != null));
            var seen = new HashSet<GameObject>();
            foreach (Target target in targets)
            {
                GameObject source = target?.Source;
                var row = new Row(target, source, DescribeLocation(source));
                rows.Add(row);
                if (source != null && !seen.Add(source)) row.SetStatus(Status.Skipped, "This prop is already listed in an earlier row.");
                else Inspect(row, selected, preset, channel);
            }
            return rows;
        }

        internal static IReadOnlyList<Row> Apply(IReadOnlyList<Target> targets, Preset preset, InteractionUIChannel channel)
        {
            IReadOnlyList<Row> rows = Preview(targets, preset, channel);
            Row[] sceneRows = rows.Where(row => row.CanApply && row.PrefabPath == null).ToArray();
            if (sceneRows.Length > 0) ApplyToScenes(sceneRows, preset, channel);

            foreach (var group in rows.Where(row => row.CanApply && row.PrefabPath != null).GroupBy(row => row.PrefabPath))
                ApplyToPrefab(group.Key, group.ToArray(), preset, channel);
            return rows;
        }

        private static void Inspect(Row row, HashSet<GameObject> selected, Preset preset, InteractionUIChannel channel)
        {
            row.SetStatus(Status.Skipped, "Target is no longer available.");
            GameObject source = row.Owner;
            if (source == null) return;
            if (preset == Preset.Keep)
            {
                row.SetStatus(Status.Unchanged, "Leave this prop's interactions as authored.");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                row.SetStatus(Status.Skipped, "Exit Play Mode to edit this prop.");
                return;
            }

            if (!CheckSource(row) || !CheckInteraction(row, selected, preset)) return;

            var interaction = source.GetComponent<Interactable>();
            var flavor = source.GetComponent<FlavorTextInteraction>();
            if (preset == Preset.Flavor)
            {
                if (string.IsNullOrWhiteSpace(row.Target.FlavorFor(flavor)))
                {
                    row.SetStatus(Status.Skipped, "Enter flavor text for this prop.");
                    return;
                }
                if ((flavor == null || flavor.Channel == null) && channel == null)
                {
                    row.SetStatus(Status.Skipped, "Assign the batch Interaction UI Channel.");
                    return;
                }
            }
            else if (!CheckPickupPhysics(row)) return;

            bool needsCollider = NeedsCollider(source, preset, interaction == null, out _);
            if (needsCollider && source.GetComponentInChildren<Renderer>(true) == null)
            {
                row.SetStatus(Status.Skipped, "Add a collider or rendered geometry to this prop.");
                return;
            }

            bool needsChanges = interaction == null || needsCollider ||
                source.GetComponentInChildren<InteractionHighlighter>(true) == null ||
                row.Target.PromptFor(interaction, preset) != interaction.DisplayName;
            if (preset == Preset.Flavor)
                needsChanges |= flavor == null || flavor.Channel == null || row.Target.FlavorFor(flavor) != flavor.FlavorText;
            else
                needsChanges |= interaction == null || !interaction.IsGrabbable ||
                    source.GetComponent<Grabbable>() == null || source.GetComponent<Rigidbody>() == null;

            if (needsChanges)
            {
                string action = preset == Preset.Flavor ? "Set up flavor interaction" : "Set up physical pickup";
                row.SetStatus(Status.Ready, row.PrefabPath == null ? action + " on this scene object." : action + " in this prefab asset.");
            }
            else row.SetStatus(Status.Unchanged, "This preset is already configured.");
        }

        private static bool CheckSource(Row row)
        {
            GameObject source = row.Owner;
            if (!EditorUtility.IsPersistent(source))
            {
                if (source.scene.IsValid() && source.scene.isLoaded)
                {
                    PrefabStage stage = PrefabStageUtility.GetPrefabStage(source);
                    if (stage == null || IsProjectPrefab(stage.assetPath) && AssetDatabase.IsOpenForEdit(stage.assetPath)) return true;
                    row.SetStatus(Status.Skipped, "Create a writable project-owned prefab variant before editing this source.");
                    return false;
                }
                row.SetStatus(Status.Skipped, "Open this object's scene or prefab before applying a preset.");
                return false;
            }

            GameObject root = source.transform.root.gameObject;
            string path = AssetDatabase.GetAssetPath(root);
            if (!IsProjectPrefab(path) || PrefabUtility.IsPartOfImmutablePrefab(root))
            {
                row.SetStatus(Status.Skipped, "Create a project-owned prefab variant before editing this source.");
                return false;
            }
            if (!AssetDatabase.IsOpenForEdit(path))
            {
                row.SetStatus(Status.Skipped, "The prefab asset is read-only.");
                return false;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
            {
                row.SetStatus(Status.Skipped, "Select the object in the active Prefab Mode hierarchy to edit it.");
                return false;
            }

            row.PrefabPath = path;
            row.SiblingPath = BuildSiblingPath(source.transform);
            return true;
        }

        private static bool IsProjectPrefab(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        private static bool CheckInteraction(Row row, HashSet<GameObject> selected, Preset preset)
        {
            GameObject source = row.Owner;
            Transform owner = source.transform;
            if (selected.Any(other => other != source && other.transform.IsChildOf(owner)))
            {
                row.SetStatus(Status.Skipped, "A child has its own row. Choose the group root or its children.");
                return false;
            }
            if ((owner.parent != null && (owner.parent.GetComponentInParent<HoldInteractable>(true) != null ||
                                          owner.parent.GetComponentInParent<ActivatedProgressInteractable>(true) != null ||
                                          owner.parent.GetComponentInParent<IInteractionTarget>(true) != null)) ||
                FindOwners(source, true).Any(component => component.gameObject != source))
            {
                row.SetStatus(Status.Skipped, "This prop overlaps an existing interaction in its hierarchy.");
                return false;
            }
            if (source.GetComponentInParent<NPCController>(true) != null || source.GetComponentInChildren<NPCController>(true) != null ||
                source.GetComponent<DialogueEventPlayer>() != null || source.GetComponent<ConditionalInteractionMessage>() != null ||
                source.GetComponent<ReadableInteractionTrigger>() != null)
            {
                row.SetStatus(Status.Skipped, "This object is already used by another gameplay system.");
                return false;
            }

            MonoBehaviour[] existing = FindOwners(source, false).ToArray();
            if (existing.Length == 0) return true;
            bool hasFlavor = source.GetComponent<FlavorTextInteraction>() != null;
            bool hasPickup = source.GetComponent<Grabbable>() != null;
            if (existing.Length == 1 && existing[0] is Interactable interaction && interaction.GetType() == typeof(Interactable) &&
                interaction.DialogueAction == null && (preset == Preset.Flavor ? hasFlavor && !hasPickup && !interaction.IsGrabbable
                    : !hasFlavor && (hasPickup || interaction.IsGrabbable))) return true;

            row.SetStatus(Status.Skipped, "This object already has a different interaction. Its setup is preserved.");
            return false;
        }

        private static IEnumerable<MonoBehaviour> FindOwners(GameObject source, bool includeChildren)
        {
            MonoBehaviour[] behaviours = includeChildren ? source.GetComponentsInChildren<MonoBehaviour>(true) : source.GetComponents<MonoBehaviour>();
            return behaviours.Where(behaviour => behaviour is IInteractionTarget or HoldInteractable or ActivatedProgressInteractable);
        }

        private static bool CheckPickupPhysics(Row row)
        {
            GameObject source = row.Owner;
            Rigidbody body = source.GetComponent<Rigidbody>();
            if (body != null && (body.isKinematic || !body.detectCollisions))
            {
                row.SetStatus(Status.Skipped, "The existing Rigidbody must be dynamic with collisions enabled for pickup.");
                return false;
            }
            if ((source.transform.parent != null && source.transform.parent.GetComponentInParent<Rigidbody>(true) != null) ||
                source.GetComponentsInChildren<Rigidbody>(true).Any(nested => nested != body))
            {
                row.SetStatus(Status.Skipped, "Choose a prop with its own physics hierarchy for pickup.");
                return false;
            }
            if (source.GetComponentsInChildren<Collider>(true).Any(collider => collider is TerrainCollider or CharacterController ||
                    collider is MeshCollider mesh && !mesh.convex))
            {
                row.SetStatus(Status.Skipped, "Pickup requires colliders compatible with a dynamic Rigidbody.");
                return false;
            }
            return true;
        }

        private static bool NeedsCollider(GameObject source, Preset preset, bool newInteraction, out bool isTrigger)
        {
            Collider[] colliders = source.GetComponentsInChildren<Collider>(true);
            if (preset == Preset.Pickup && !colliders.Any(collider =>
                    !collider.isTrigger && IsEnabledInTarget(collider, source.transform)))
            {
                isTrigger = false;
                return true;
            }

            isTrigger = true;
            if (!newInteraction) return preset == Preset.Flavor && colliders.Length == 0;
            int ignoredLayer = LayerMask.NameToLayer("Ignore Raycast");
            return !colliders.Any(collider => collider.gameObject.layer != ignoredLayer && IsEnabledInTarget(collider, source.transform));
        }

        private static bool IsEnabledInTarget(Collider collider, Transform target)
        {
            if (!collider.enabled) return false;
            for (Transform current = collider.transform; current != target; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static void ApplyToScenes(IEnumerable<Row> rows, Preset preset, InteractionUIChannel channel)
        {
            Undo.IncrementCurrentGroup();
            int batchGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set Up Clutter Interactions");
            foreach (Row row in rows)
            {
                Undo.IncrementCurrentGroup();
                int rowGroup = Undo.GetCurrentGroup();
                try
                {
                    Configure(row.Owner, row.Target, preset, channel, true);
                    EditorSceneManager.MarkSceneDirty(row.Owner.scene);
                    row.SetStatus(Status.Applied, "Configured on this scene object.");
                }
                catch (Exception error)
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(rowGroup);
                    row.SetStatus(Status.Failed, error.Message);
                }
            }
            Undo.CollapseUndoOperations(batchGroup);
        }

        private static void ApplyToPrefab(string path, IReadOnlyList<Row> rows, Preset preset, InteractionUIChannel channel)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                foreach (Row row in rows)
                {
                    Transform target = root.transform;
                    foreach (int index in row.SiblingPath) target = target.GetChild(index);
                    Configure(target.gameObject, row.Target, preset, channel, false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Unity did not save this prefab asset.");
                foreach (Row row in rows) row.SetStatus(Status.Applied, "Saved to this prefab asset.");
            }
            catch (Exception error)
            {
                foreach (Row row in rows) row.SetStatus(Status.Failed, error.Message);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Configure(GameObject owner, Target target, Preset preset, InteractionUIChannel channel, bool useUndo)
        {
            var existing = owner.GetComponent<Interactable>();
            string prompt = target.PromptFor(existing, preset);
            var interaction = ClutterInteractableWindow.EnsureComponent<Interactable>(owner, useUndo);
            ClutterInteractableWindow.EnsureHighlighter(owner, useUndo);
            bool newInteraction = existing == null;
            if (NeedsCollider(owner, preset, newInteraction, out bool isTrigger))
            {
                GameObject colliderOwner = newInteraction ? GetColliderOwner(owner, useUndo) : owner;
                ClutterInteractableWindow.AddBoundsCollider(owner, isTrigger, useUndo, colliderOwner);
            }

            using (var data = new SerializedObject(interaction))
            {
                data.FindProperty("displayName").stringValue = prompt;
                if (preset == Preset.Pickup) data.FindProperty("isGrabbable").boolValue = true;
                ApplyProperties(data, useUndo);
            }

            if (preset == Preset.Flavor)
            {
                var flavor = owner.GetComponent<FlavorTextInteraction>();
                string message = target.FlavorFor(flavor);
                flavor = ClutterInteractableWindow.EnsureComponent<FlavorTextInteraction>(owner, useUndo);
                using var data = new SerializedObject(flavor);
                data.FindProperty("flavorText").stringValue = message;
                if (flavor.Channel == null) data.FindProperty("channel").objectReferenceValue = channel;
                ApplyProperties(data, useUndo);
            }
            else
            {
                ClutterInteractableWindow.EnsureComponent<Rigidbody>(owner, useUndo);
                ClutterInteractableWindow.EnsureComponent<Grabbable>(owner, useUndo);
            }
        }

        private static GameObject GetColliderOwner(GameObject owner, bool useUndo)
        {
            int interactionLayer = LayerMask.NameToLayer("Interactables");
            if (interactionLayer < 0) interactionLayer = LayerMask.NameToLayer("Default");
            if (owner.layer == interactionLayer) return owner;

            // A separate target preserves the layers of authored colliders and visual geometry.
            if (owner.layer != 0 || owner.GetComponent<Collider>() != null)
            {
                var child = new GameObject("Interaction Collider") { layer = interactionLayer };
                if (child.scene != owner.scene) SceneManager.MoveGameObjectToScene(child, owner.scene);
                child.transform.SetParent(owner.transform, false);
                if (useUndo) Undo.RegisterCreatedObjectUndo(child, "Create Clutter Interaction Collider");
                return child;
            }

            if (useUndo) Undo.RecordObject(owner, "Set Clutter Interaction Layer");
            owner.layer = interactionLayer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            return owner;
        }

        private static void ApplyProperties(SerializedObject data, bool useUndo)
        {
            if (useUndo) data.ApplyModifiedProperties();
            else if (data.ApplyModifiedPropertiesWithoutUndo())
                PrefabUtility.RecordPrefabInstancePropertyModifications(data.targetObject);
        }

        private static int[] BuildSiblingPath(Transform target)
        {
            var indices = new Stack<int>();
            for (Transform current = target; current.parent != null; current = current.parent)
                indices.Push(current.GetSiblingIndex());
            return indices.ToArray();
        }

        private static string DescribeLocation(GameObject source)
        {
            if (source == null) return "Missing target";
            var names = new Stack<string>();
            for (Transform current = source.transform; current != null; current = current.parent)
                names.Push(current.name);
            string location = EditorUtility.IsPersistent(source) ? AssetDatabase.GetAssetPath(source.transform.root.gameObject) : source.scene.name;
            return $"{location} / {string.Join("/", names)}";
        }
    }
}
