using System;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Compatibility component for scenes created with the former Cinematics credits scroller.
    /// New scenes should use <see cref="UI.CreditsScroller"/> directly.
    /// </summary>
    [Obsolete("Use QuietStatic.Toolkit.UI.CreditsScroller. Existing scene components remain supported.")]
    public class CreditsScroller : UI.CreditsScroller
    {
    }
}
