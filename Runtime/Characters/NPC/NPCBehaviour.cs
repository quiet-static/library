using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Base class for attachable NPC behaviours coordinated by an NPCController.</summary>
    public abstract class NPCBehaviour : MonoBehaviour
    {
        [Tooltip("Whether this behavior begins active when its GameObject starts.")]
        [SerializeField] private bool activeOnStart = true;

        /// <summary>Gets the NPC controller that coordinates this behavior.</summary>
        protected NPCController Controller { get; private set; }

        /// <summary>Gets whether this behavior is currently active.</summary>
        public bool IsBehaviourActive { get; private set; }

        protected virtual void Awake()
        {
            Controller = GetComponent<NPCController>();
            if (Controller == null)
            {
                GameLogger.Warning(
                    "Awake",
                    this,
                    $"{GetType().Name} on {name} requires an NPCController."
                );
                enabled = false;
            }
        }

        protected virtual void Start()
        {
            SetBehaviourActive(activeOnStart);
        }

        /// <summary>
        /// Activates or deactivates this behavior and invokes its matching lifecycle hook.
        /// </summary>
        /// <param name="active">Whether the behavior should be active.</param>
        public virtual void SetBehaviourActive(bool active)
        {
            if (IsBehaviourActive == active)
                return;

            IsBehaviourActive = active;
            if (active) OnBehaviourActivated();
            else OnBehaviourDeactivated();
        }

        protected virtual void OnBehaviourActivated() { }
        protected virtual void OnBehaviourDeactivated() { }
    }
}
