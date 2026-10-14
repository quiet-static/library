using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Cross-scene requests for named screen overlays. Assign this asset directly to UnityEvents.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Cinematics/Screen Overlay Channel")]
    public sealed class ScreenOverlayChannel : CrossSceneCommandChannel<ScreenOverlayCommand>
    {
        /// <summary>Fades the named overlay in until Hide is requested.</summary>
        public void Show(string id) => Dispatch(new ScreenOverlayCommand(id, true, false));
        /// <summary>Fades the named overlay out, cancelling any pulse.</summary>
        public void Hide(string id) => Dispatch(new ScreenOverlayCommand(id, false, false));
        /// <summary>Shows the overlay, holds for its configured duration, then fades out.</summary>
        public void Pulse(string id) => Dispatch(new ScreenOverlayCommand(id, true, true));
        /// <summary>Immediately clears all overlays.</summary>
        public void ClearAll() => Dispatch(new ScreenOverlayCommand(null, false, false));
    }

    /// <summary>A named overlay visibility request.</summary>
    public readonly struct ScreenOverlayCommand
    {
        public readonly string Id;
        public readonly bool Visible;
        public readonly bool Timed;
        public ScreenOverlayCommand(string id, bool visible, bool timed)
        { Id = id; Visible = visible; Timed = timed; }
    }
}
