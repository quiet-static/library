using UnityEngine;

namespace QuietStatic.Characters
{
    /// <summary>
    /// Preserves player prefabs authored against the toolkit's original character
    /// namespace and script GUID.
    /// </summary>
    /// <remarks>
    /// New code should use
    /// <see cref="Toolkit.Characters.Player.CharacterMotor"/> directly.
    /// </remarks>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : Toolkit.Characters.Player.CharacterMotor
    {
    }
}
