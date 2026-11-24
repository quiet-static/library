using System;
using QuietStatic.Toolkit.Cameras;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>Identity and scene-owner notifications for one traversal of a dialogue tree.</summary>
    internal sealed class DialogueSession
    {
        internal DialogueTree Tree { get; }
        internal Transform FocusTarget { get; }
        internal bool HasEnteredNode { get; set; }
        internal event Action<DialogueTree.Node> NodePresented;
        internal event Action<DialogueSession> Completed;

        private CameraFocusController focusController;

        internal DialogueSession(DialogueTree tree, Transform focusTarget = null)
        {
            Tree = tree;
            FocusTarget = focusTarget;
        }

        internal void BeginFocus()
        {
            if (FocusTarget != null && CameraManager.Instance != null)
                focusController = CameraManager.Instance.BeginFocus(FocusTarget, this);
        }

        internal void ReleaseFocus()
        {
            if (focusController != null) focusController.EndFocus(this);
            focusController = null;
        }

        internal void PresentNode(DialogueTree.Node node) => NodePresented?.Invoke(node);

        internal void Finish() => Completed?.Invoke(this);
    }
}
