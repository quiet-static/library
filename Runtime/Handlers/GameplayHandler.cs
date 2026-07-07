using QuietStatic.Toolkit.State;
using UnityEngine;

namespace QuietStatic
{
    public class GameplayHandler : MonoBehaviour
    {
        public void SetState(string newState)
        {
            GameStateManager.Instance.SetState(newState);
        }
    }
}
