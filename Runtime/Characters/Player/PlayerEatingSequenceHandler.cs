using System;

namespace QuietStatic.Toolkit.Characters.Player
{
    /// <summary>Compatibility adapter for scenes using the former eating-specific handler.</summary>
    [Obsolete("Use PlayerActivityHandler.")]
    public sealed class PlayerEatingSequenceHandler : PlayerActivityHandler { }
}
