using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Shrinks a mess visual with a hold meter, leaving its interaction target stable until completion.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(HoldInteractable))]
    public sealed class CleanableMess : MonoBehaviour
    {
        [SerializeField, Tooltip("Child containing the debris. Keep the interaction collider on the unscaled root.")]
        private Transform visual;
        [SerializeField, Tooltip("Target collider disabled once the mess has been cleaned.")]
        private Collider targetCollider;
        [SerializeField, Tooltip("Place the mess just above the solid floor on scene start.")]
        private bool snapToFloor = true;
        private Vector3 originalScale;
        private bool initialized;

        /// <summary>The reusable hold interaction that owns this mess's progress.</summary>
        public HoldInteractable Interaction => GetComponent<HoldInteractable>();

        private void Awake() => Initialize();
        private void Start()
        {
            if (!snapToFloor) return;
            Physics.SyncTransforms();
            Vector3 probe = transform.position + Vector3.up * 2f;
            if (NavMesh.SamplePosition(transform.position, out var walkable, 1f, NavMesh.AllAreas))
            {
                // Probe just above the walkable surface rather than landing on a nearby shelf.
                probe = walkable.position + Vector3.up * .5f;
                transform.position = walkable.position;
            }
            RaycastHit? floor = null;
            foreach (var hit in Physics.RaycastAll(probe,
                         Vector3.down, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.normal.y < .7f) continue;
                if (!floor.HasValue || hit.distance < floor.Value.distance) floor = hit;
            }
            if (floor.HasValue)
            {
                var position = transform.position;
                position.y = floor.Value.point.y + .015f;
                transform.position = position;
            }
        }
        private void Initialize()
        {
            if (initialized) return;
            if (targetCollider == null) targetCollider = GetComponent<Collider>();
            if (visual != null) originalScale = visual.localScale;
            initialized = true;
        }

        private void OnEnable()
        {
            Initialize();
            Interaction.ProgressChanged += Refresh;
            Refresh(Interaction.Progress);
        }

        private void OnDisable() => Interaction.ProgressChanged -= Refresh;

        private void Refresh(float progress)
        {
            bool complete = progress >= 1f;
            if (visual != null)
            {
                visual.localScale = originalScale * (1f - Mathf.Clamp01(progress));
                visual.gameObject.SetActive(!complete);
            }
            if (targetCollider != null) targetCollider.enabled = !complete;
        }
    }
}
