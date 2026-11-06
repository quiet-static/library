using System;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic.Toolkit.Timekeeping
{
    /// <summary>Owns continuous game time in a retained systems scene. Respects Time.timeScale.</summary>
    public sealed class GameClockManager : ToolkitSingleton<GameClockManager>
    {
        [SerializeField, Tooltip("Shared query and command channel for this clock.")] private GameClockChannel channel;
        [SerializeField, Min(0.01f), Tooltip("Real seconds per game minute at time scale 1.")] private float secondsPerGameMinute = 2f;
        [SerializeField, Range(0, 23), Tooltip("Initial hour in 24-hour time.")] private int startHour = 7;
        [SerializeField, Range(0, 59), Tooltip("Initial minute.")] private int startMinute;
        [SerializeField, Tooltip("Whether the clock advances automatically.")] private bool running = true;
        private double totalMinutes;
        /// <summary>Elapsed game minutes from day zero, including fractional minutes.</summary>
        public double TotalMinutes => totalMinutes;
        /// <summary>Time of day in the range [0,1440).</summary>
        public double MinuteOfDay => totalMinutes % 1440;
        /// <summary>Zero-based current day.</summary>
        public int Day => (int)(totalMinutes / 1440);
        /// <summary>Current 24-hour hour.</summary>
        public int Hour => (int)MinuteOfDay / 60;
        /// <summary>Current minute within the hour.</summary>
        public int Minute => (int)MinuteOfDay % 60;
        /// <summary>Current time in 12-hour notation.</summary>
        public string DisplayTime => $"{(Hour % 12 == 0 ? 12 : Hour % 12)}:{Minute:00} {(Hour < 12 ? "AM" : "PM")}";
        /// <summary>Real seconds per game minute; can be changed during play.</summary>
        public float SecondsPerGameMinute { get => secondsPerGameMinute; set => secondsPerGameMinute = Mathf.Max(.01f, value); }
        /// <summary>Raised on every time change, including explicit jumps.</summary>
        public event Action Changed;
        protected override void Awake() { base.Awake(); if (Instance == this) totalMinutes = startHour * 60 + startMinute; }
        private void OnEnable() { if (Instance == this && channel) channel.Bind(this); }
        private void OnDisable() { if (channel) channel.Unbind(this); }
        private void Update() { if (running) AdvanceMinutes(Time.deltaTime / Math.Max(.01, secondsPerGameMinute)); }
        /// <summary>Sets total elapsed minutes, clamping before day zero. Rejects nonfinite input.</summary>
        public void SetTotalMinutes(double minutes) { if (double.IsNaN(minutes) || double.IsInfinity(minutes)) return; totalMinutes = Math.Max(0, minutes); Changed?.Invoke(); }
        /// <summary>Advances or rewinds the clock by game minutes.</summary>
        public void AdvanceMinutes(double minutes) => SetTotalMinutes(totalMinutes + minutes);
        /// <summary>Sets an hour and minute on the current day.</summary>
        public void SetTime(int hour, int minute) => SetTotalMinutes(Day * 1440d + Mathf.Clamp(hour, 0, 23) * 60 + Mathf.Clamp(minute, 0, 59));
        /// <summary>Pauses automatic clock advancement.</summary>
        public void PauseClock() => running = false;
        /// <summary>Resumes automatic clock advancement.</summary>
        public void ResumeClock() => running = true;
    }
}
