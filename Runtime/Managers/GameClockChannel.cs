using System;
using UnityEngine;
namespace QuietStatic.Toolkit.Timekeeping
{
    /// <summary>Cross-scene facade. The manager owns all session state; this asset stores only a live service binding.</summary>
    [CreateAssetMenu(menuName = "Quiet Static/Channels/Game Clock")]
    public sealed class GameClockChannel : ScriptableObject
    {
        [NonSerialized] private GameClockManager owner;
        /// <summary>Whether a loaded clock is available.</summary>
        public bool IsAvailable => owner != null;
        /// <summary>Current time of day, or zero when no receiver is loaded.</summary>
        public double MinuteOfDay => owner ? owner.MinuteOfDay : 0;
        /// <summary>Current 12-hour text, or an unavailable placeholder.</summary>
        public string DisplayTime => owner ? owner.DisplayTime : "--:--";
        internal void Bind(GameClockManager clock) => owner = clock;
        internal void Unbind(GameClockManager clock) { if (owner == clock) owner = null; }
        /// <summary>Sets the time of day in minutes (0 is midnight).</summary>
        public void SetMinuteOfDay(float minutes) { if (owner) owner.SetTime(Mathf.FloorToInt(Mathf.Repeat(minutes, 1440)) / 60, Mathf.FloorToInt(Mathf.Repeat(minutes, 1440)) % 60); }
        /// <summary>Advances or rewinds game minutes.</summary>
        public void AdvanceMinutes(float minutes) { if (owner) owner.AdvanceMinutes(minutes); }
        /// <summary>Pauses this clock.</summary>
        public void PauseClock() { if (owner) owner.PauseClock(); }
        /// <summary>Resumes this clock.</summary>
        public void ResumeClock() { if (owner) owner.ResumeClock(); }
    }
}
