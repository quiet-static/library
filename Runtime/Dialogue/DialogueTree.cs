using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// ScriptableObject asset that stores a reusable branching dialogue tree.
    /// </summary>
    /// <remarks>
    /// A dialogue tree is made up of ordered <see cref="Node"/> entries. Each node can display
    /// a speaker name, one line of dialogue, optional choices, optional flags to set when the
    /// node is entered, and a fallback next-node index for linear dialogue.
    ///
    /// This asset only stores dialogue data. Runtime behavior is handled by
    /// <see cref="DialogueRunner"/>, and UI display is handled separately by a view such as
    /// <see cref="DialogueTMPView"/>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Dialogue/Dialogue Tree")]
    public class DialogueTree : ScriptableObject
    {
        /// <summary>
        /// Represents one selectable response or branch from a dialogue node.
        /// </summary>
        /// <remarks>
        /// Choices are evaluated by <see cref="DialogueRunner"/> when the player selects a
        /// response. A choice can move the dialogue to another node and optionally set one
        /// or more gameplay flags.
        /// </remarks>
        [Serializable]
        public class Choice
        {
            [Header("Choice Text")]
            [Tooltip("Text shown on the choice button or response UI.")]
            public string text;

            [Header("Flow")]
            [Tooltip("Index of the node to visit after this choice is selected. Use -1 to end the dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set when this choice is selected.")]
            public string[] flagsToSet;
        }

        /// <summary>
        /// Represents one dialogue entry in the tree.
        /// </summary>
        /// <remarks>
        /// A node can be used as a simple linear line of dialogue, or as a branching point
        /// with one or more <see cref="Choice"/> entries. If choices are present, the runner
        /// will use the chosen response's next node instead of this node's
        /// <see cref="nextNodeIndex"/>.
        /// </remarks>
        [Serializable]
        public class Node
        {
            [Header("Dialogue Text")]
            [Tooltip("Name of the character, narrator, object, or source speaking this line.")]
            public string speaker;

            [Tooltip("Dialogue line displayed for this node.")]
            [TextArea(2, 6)]
            public string line;

            [Header("Choices")]
            [Tooltip("Optional response choices for this node. Leave empty for normal linear dialogue.")]
            public Choice[] choices;

            [Header("Flow")]
            [Tooltip("Index of the next node for linear dialogue. Use -1 to end the dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set as soon as this node is entered.")]
            public string[] flagsToSetOnEnter;
        }

        [Header("Nodes")]
        [Tooltip("All dialogue nodes in this tree. Node indexes are based on this array order.")]
        [SerializeField] private Node[] nodes;

        [Tooltip("Index of the first node that should play when this dialogue tree starts.")]
        [SerializeField] private int startNodeIndex;

        /// <summary>
        /// Gets all dialogue nodes in this tree.
        /// </summary>
        public Node[] Nodes => nodes;

        /// <summary>
        /// Gets the index of the node where this tree should begin.
        /// </summary>
        public int StartNodeIndex => startNodeIndex;

        /// <summary>
        /// Attempts to retrieve a dialogue node by index.
        /// </summary>
        /// <param name="index">The node index to retrieve from the <see cref="Nodes"/> array.</param>
        /// <param name="node">
        /// When this method returns, contains the node at the requested index if one exists;
        /// otherwise, <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the requested index exists and a node was returned; otherwise,
        /// <c>false</c>.
        /// </returns>
        public bool TryGetNode(int index, out Node node)
        {
            if (nodes == null || index < 0 || index >= nodes.Length)
            {
                node = null;
                return false;
            }

            node = nodes[index];
            return true;
        }
    }
}
