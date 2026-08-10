using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Preserves movement-state components authored against the toolkit's original
    /// namespace and script GUID.
    /// </summary>
    /// <remarks>
    /// New code should use
    /// <see cref="Toolkit.Characters.Player.MovementStateController"/> directly.
    /// </remarks>
    [AddComponentMenu("")]
    public class MovementStateController :
        Toolkit.Characters.Player.MovementStateController
    {
    }
}
