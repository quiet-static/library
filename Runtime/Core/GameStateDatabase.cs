using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Core
{
    /// <summary>
    /// 
    /// </remarks>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/State/Game State Database")]
    public class GameStateDatabase : ScriptableObject
    {
        /// <summary>
        /// 
        /// </summary>
        [Serializable]
        public class StateDefinition
        {
            public string state;

            [TextArea]
            public string description;
        }

        [SerializeField] private StateDefinition[] states;

        public StateDefinition[] States => states;
    }
}
