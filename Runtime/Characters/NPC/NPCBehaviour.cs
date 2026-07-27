using UnityEngine;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Base class for attachable NPC behaviours coordinated by an NPCController.</summary>
    public abstract class NPCBehaviour : MonoBehaviour
    {
        [Tooltip("Whether this behavior begins active when its GameObject starts.")]
        [SerializeField] private bool activeOnStart = true;

        protected NPCController Controller { get; private set; }
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
