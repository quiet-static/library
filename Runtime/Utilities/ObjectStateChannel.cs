using UnityEngine;

namespace QuietStatic.Toolkit.Utilities
{
    /// <summary>Operation carried by an <see cref="ObjectStateCommand"/>.</summary>
    public enum ObjectStateCommandType
    {
        Activate,
        Clear
    }

    /// <summary>Typed cross-scene object-state command.</summary>
    public readonly struct ObjectStateCommand
    {
        /// <summary>Creates an object-state command.</summary>
        public ObjectStateCommand(
            ObjectStateCommandType type,
            ObjectStateDefinition state = null)
        {
            Type = type;
            State = state;
        }

        /// <summary>Requested object-state operation.</summary>
        public ObjectStateCommandType Type { get; }

        /// <summary>Definition selected by an activate command.</summary>
        public ObjectStateDefinition State { get; }
    }

    /// <summary>
    /// Relays object-state requests between scenes without holding references to scene objects.
    /// </summary>
    /// <remarks>
    /// Assign this asset to an <see cref="ObjectStateHandler"/>, then invoke
    /// <see cref="ActivateState"/> or <see cref="ClearState"/> from UnityEvents in any scene.
    /// Every enabled handler listening to this channel will receive the request.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ObjectStateChannel",
        menuName = "Quiet Static Toolkit/Utilities/Object State Channel"
    )]
    public sealed class ObjectStateChannel :
        CrossSceneCommandChannel<ObjectStateCommand>
    {
        /// <summary>
        /// Requests that enabled listeners activate the supplied state definition.
        /// </summary>
        /// <param name="state">State selected through code or a UnityEvent asset picker.</param>
        public void ActivateState(ObjectStateDefinition state)
        {
            if (state == null)
            {
                GameLogger.Warning(
                    "ActivateState",
                    this,
                    "ObjectStateChannel cannot activate a null state. Use ClearState instead."
                );
                return;
            }

            Dispatch(new ObjectStateCommand(
                ObjectStateCommandType.Activate,
                state));
        }

        /// <summary>Requests that enabled listeners disable every configured state object.</summary>
        public void ClearState()
        {
            Dispatch(new ObjectStateCommand(ObjectStateCommandType.Clear));
        }
    }
}
