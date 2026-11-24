using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>Keeps the cursor available while any cooperating modal view owns it.</summary>
    internal static class ModalCursorPolicy
    {
        private static readonly HashSet<Behaviour> owners = new();
        private static CursorLockMode baselineLock;
        private static bool baselineVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => owners.Clear();

        internal static void Claim(Behaviour owner)
        {
            if (owners.Count == 0)
            {
                baselineLock = Cursor.lockState;
                baselineVisible = Cursor.visible;
            }
            owners.Add(owner);
            Apply();
        }

        internal static void Release(Behaviour owner)
        {
            if (owners.Remove(owner)) Apply();
        }

        internal static void SetBaseline(CursorLockMode lockMode, bool visible)
        {
            baselineLock = lockMode;
            baselineVisible = visible;
            Apply();
        }

        private static void Apply()
        {
            CursorLockMode lockMode = owners.Count > 0 ? CursorLockMode.None : baselineLock;
            bool visible = owners.Count > 0 || baselineVisible;
            if (Cursor.lockState != lockMode) Cursor.lockState = lockMode;
            if (Cursor.visible != visible) Cursor.visible = visible;
        }
    }
}
