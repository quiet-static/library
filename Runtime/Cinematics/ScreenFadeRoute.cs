using System.Collections;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Selects a live channel receiver first, then the caller's direct fader.</summary>
    internal readonly struct ScreenFadeRoute
    {
        private readonly ScreenFadeChannel channel;
        internal ScreenFader DirectFader { get; }
        internal bool UsesChannel => channel != null;
        internal bool IsAvailable => UsesChannel || DirectFader != null;

        internal ScreenFadeRoute(ScreenFadeChannel channel, ScreenFader directFader)
        {
            this.channel = HasChannelReceiver(channel) ? channel : null;
            DirectFader = this.channel == null ? directFader : null;
        }

        internal static bool HasChannelReceiver(ScreenFadeChannel channel) =>
            channel != null && channel.HasReceiver;

        internal IEnumerator FadeRoutine(ScreenFadeTarget target, float duration, bool useDirectDefaults = false)
        {
            if (UsesChannel)
                yield return channel.FadeRoutine(target, duration);
            else if (DirectFader != null)
                yield return DirectFader.ReplaceFadeRoutine(target, useDirectDefaults ? null : duration);
        }
    }
}
