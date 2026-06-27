using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Wrapper for communicating with the Interaction UI Manager to send and recieve messages
    /// </summary>
    public class InteractionHandler : MonoBehaviour
    {
        /// <summary>
        /// Writes interaction result text to the UI
        /// </summary>
        /// <param name="text">Interaction result text</param>
        public void WriteToInteractionHUD(string text)
        {
            InteractionUIManager.Instance.ShowMessage(text);
        }
    }
}
