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

        /// <summary>
        /// Shows a prompt under the HUD crosshair
        /// </summary>
        /// <param name="prompt">What to show under the crosshair</param>
        public void ShowPrompt(string prompt)
        {
            InteractionUIManager.Instance.ShowPrompt(prompt);
        }

        /// <summary>
        /// Hides the text under the HUD crosshair
        /// </summary>
        public void HidePrompt()
        {
            InteractionUIManager.Instance.HidePrompt();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        public void ShowMessage(string text)
        {
            InteractionUIManager.Instance.ShowMessage(text);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="seconds"></param>
        public void ShowMessageForSeconds(string text, float seconds)
        {
            InteractionUIManager.Instance.ShowMessageForSeconds(text, seconds);
        }
    }
}
