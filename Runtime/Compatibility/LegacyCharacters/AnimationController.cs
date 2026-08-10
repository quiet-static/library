using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Preserves animation components authored against the toolkit's original
    /// namespace and script GUID.
    /// </summary>
    /// <remarks>
    /// New code should use
    /// <see cref="Toolkit.Characters.Player.AnimationController"/> directly.
    /// </remarks>
    [AddComponentMenu("")]
    [RequireComponent(typeof(Animator))]
    public class AnimationController : Toolkit.Characters.Player.AnimationController
    {
    }
}
