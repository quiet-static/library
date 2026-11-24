using System;
using UnityEngine;

namespace QuietStatic.Toolkit.State
{
    /// <summary>Keeps game time frozen until all active owners release their handles.</summary>
    public sealed class TimeScaleFreeze : IDisposable
    {
        private static int activeHandles;
        private static float resumeScale;
        private static object playSession;
        private readonly object session;
        private bool released;

        private TimeScaleFreeze()
        {
            session = playSession;
            if (activeHandles++ == 0)
                resumeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <summary>Freezes game time. Dispose the returned handle to release the freeze.</summary>
        public static TimeScaleFreeze Acquire() => new();

        /// <summary>Sets the game time scale to use once the active freezes end.</summary>
        public static void SetResumeScale(float scale)
        {
            if (activeHandles == 0)
                Time.timeScale = scale;
            else
                resumeScale = scale;
        }

        /// <summary>Releases this owner's freeze.</summary>
        public void Dispose()
        {
            if (released || !ReferenceEquals(session, playSession)) return;
            released = true;
            if (--activeHandles == 0)
                Time.timeScale = resumeScale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            activeHandles = 0;
            playSession = new object();
        }
    }
}
