/*
 * DialogueTree.cs
 * 
 * ScriptableObject asset for storing linear or branching dialogue data.
 * 
 * This asset contains only data. It does not run dialogue, show UI, read input,
 * change scenes, or directly communicate with managers.
 */

using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// ScriptableObject asset containing a reusable branching dialogue tree.
    /// </summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Dialogue/Dialogue Tree")]
    public class DialogueTree : ScriptableObject
    {
        /// <summary>
        /// Represents one selectable response from a dialogue node.
        /// </summary>
        [Serializable]
        public class Choice
        {
            [Header("Choice Text")]
            [Tooltip("Text shown on the choice button.")]
            public string text;

            [Header("Flow")]
            [Tooltip("Index of the node to visit after selecting this choice. Use -1 to end dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set when this choice is selected.")]
            public string[] flagsToSet;
        }

        /// <summary>
        /// Represents one dialogue node in the tree.
        /// </summary>
        [Serializable]
        public class Node
        {
            [Header("Dialogue Text")]
            [Tooltip("Name of the speaker, narrator, object, or source.")]
            public string speaker;

            [Tooltip("Dialogue line displayed for this node.")]
            [TextArea(2, 6)]
            public string line;

            [Header("Choices")]
            [Tooltip("Optional response choices. Leave empty for normal linear dialogue.")]
            public Choice[] choices;

            [Header("Flow")]
            [Tooltip("Index of the next node for linear dialogue. Use -1 to end dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set when this node is entered.")]
            public string[] flagsToSetOnEnter;

            /// <summary>
            /// Gets whether this node has at least one response choice.
            /// </summary>
            public bool HasChoices => choices != null && choices.Length > 0;

            /// <summary>
            /// Gets the display text for every response choice.
            /// </summary>
            /// <returns>Array of choice display strings.</returns>
            public string[] GetChoiceTexts()
            {
                if (!HasChoices)
                {
                    return Array.Empty<string>();
                }

                string[] choiceTexts = new string[choices.Length];

                for (int i = 0; i < choices.Length; i++)
                {
                    choiceTexts[i] = choices[i]?.text ?? string.Empty;
                }

                return choiceTexts;
            }
        }

        [Header("Nodes")]
        [Tooltip("All dialogue nodes in this tree. Node indexes are based on this array order.")]
        [SerializeField] private Node[] nodes;

        [Tooltip("Index of the first node played when this tree starts.")]
        [SerializeField] private int startNodeIndex;

        /// <summary>
        /// Gets all dialogue nodes in this tree.
        /// </summary>
        public Node[] Nodes => nodes;

        /// <summary>
        /// Gets the first node index for this tree.
        /// </summary>
        public int StartNodeIndex => startNodeIndex;

        /// <summary>
        /// Attempts to retrieve a node by index.
        /// </summary>
        /// <param name="index">Node index to retrieve.</param>
        /// <param name="node">Returned node when found; otherwise, null.</param>
        /// <returns>True if the node exists; otherwise, false.</returns>
        public bool TryGetNode(int index, out Node node)
        {
            if (nodes == null || index < 0 || index >= nodes.Length)
            {
                node = null;
                return false;
            }

            node = nodes[index];
            return node != null;
        }
    }
}
