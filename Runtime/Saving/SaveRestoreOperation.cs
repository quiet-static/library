using QuietStatic.Toolkit.Dialogue;
using UnityEngine;

namespace QuietStatic.Toolkit.Saving
{
    /// <summary>Hosts one restore and its rollback independently of the requesting manager's lifetime.</summary>
    internal sealed class SaveRestoreOperation : MonoBehaviour
    {
        private static SaveRestoreOperation active;
        private SaveManager owner;

        internal static bool IsInProgress => active != null;
        internal bool CancellationRequested { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => active = null;

        internal static SaveRestoreOperation Begin(SaveManager owner)
        {
            var host = new GameObject("Checkpoint Restoration");
            DontDestroyOnLoad(host);
            active = host.AddComponent<SaveRestoreOperation>();
            active.owner = owner;
            DialogueEventPlayer.RestoreSuppressesTriggerStarts = true;
            return active;
        }

        internal static void Cancel(SaveManager owner)
        {
            if (active != null && ReferenceEquals(active.owner, owner))
                active.CancellationRequested = true;
        }

        internal void Finish()
        {
            if (active == this)
            {
                active = null;
                DialogueEventPlayer.RestoreSuppressesTriggerStarts = false;
            }
            Destroy(gameObject);
        }
    }
}
