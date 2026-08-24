using System;

namespace QuietStatic.Toolkit.Input
{
    /// <summary>Input groups that a temporary owner can suppress.</summary>
    [Flags]
    public enum InputBlockGroups
    {
        None = 0,
        Gameplay = 1 << 0,
        UI = 1 << 1,
        Cutscene = 1 << 2,
        Pause = 1 << 3,
        All = Gameplay | UI | Cutscene | Pause
    }

    /// <summary>
    /// Disposable ownership token returned by InputModeManager.
    /// </summary>
    public sealed class InputBlockHandle : IDisposable
    {
        private InputModeManager manager;
        private readonly int token;

        internal InputBlockHandle(InputModeManager manager, int token)
        {
            this.manager = manager;
            this.token = token;
        }

        /// <summary>Whether this handle still owns an active input block.</summary>
        public bool IsActive => manager != null;

        /// <summary>Releases this claim. Repeated calls are safe.</summary>
        public void Dispose()
        {
            if (manager == null)
            {
                return;
            }

            InputModeManager owner = manager;
            manager = null;
            owner.ReleaseInputBlock(token);
        }
    }
}
