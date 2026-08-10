/*
 * DialogueTree.cs
 * 
 * ScriptableObject asset for storing linear or branching dialogue data.
 * 
 * This asset contains only data. It does not run dialogue, show UI, read input,
 * change scenes, or directly communicate with managers.
 */

using System;
using QuietStatic.Toolkit.Flags;
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
            [TextArea(1, 3)]
            /// <summary>Player-facing choice text.</summary>
            public string text;

            [Header("Flow")]
            [Tooltip("Index of the node to visit after selecting this choice. Use -1 to end dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set when this choice is selected.")]
            [FlagId]
            /// <summary>Flags set after this choice is selected.</summary>
            public string[] flagsToSet;

            [Header("Availability")]
            [Tooltip("Optional flag condition that controls whether this choice is shown and selectable.")]
            public FlagRequirement availabilityRequirement = new();

            /// <summary>Returns whether this choice is available for the current flags.</summary>
            public bool IsAvailable(FlagManager flagManager = null) =>
                availabilityRequirement == null ||
                availabilityRequirement.IsMet(flagManager);
        }

        /// <summary>
        /// Represents one dialogue node in the tree.
        /// </summary>
        [Serializable]
        public class Node
        {
            [Header("Identity")]
            [Tooltip("Stable, human-readable authoring ID. Generated dialogue uses this instead of array indexes for references.")]
            /// <summary>Stable authoring identifier for this node.</summary>
            public string id;

            [Header("Dialogue Text")]
            [Tooltip("Name of the speaker, narrator, object, or source.")]
            /// <summary>Player-facing speaker name.</summary>
            public string speaker;

            [Tooltip("Dialogue line displayed for this node.")]
            [TextArea(2, 6)]
            /// <summary>Player-facing dialogue line.</summary>
            public string line;

            [Header("Choices")]
            [Tooltip("Optional response choices. Leave empty for normal linear dialogue.")]
            /// <summary>Optional responses available from this node.</summary>
            public Choice[] choices;

            [Header("Flow")]
            [Tooltip("Index of the next node for linear dialogue. Use -1 to end dialogue.")]
            public int nextNodeIndex = -1;

            [Header("Flags")]
            [Tooltip("Optional flag IDs to set when this node is entered.")]
            [FlagId]
            /// <summary>Flags set when this node becomes active.</summary>
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

            /// <summary>Gets authored indexes for choices currently available.</summary>
            public int[] GetAvailableChoiceIndexes(FlagManager flagManager = null)
            {
                if (!HasChoices) return Array.Empty<int>();
                var indexes = new System.Collections.Generic.List<int>();
                for (int index = 0; index < choices.Length; index++)
                {
                    if (choices[index] != null && choices[index].IsAvailable(flagManager))
                        indexes.Add(index);
                }
                return indexes.ToArray();
            }
        }

        [Header("Nodes")]
        [Tooltip("All dialogue nodes in this tree. Node indexes are based on this array order.")]
        [SerializeField] private Node[] nodes;

        [Tooltip("Index of the first node played when this tree starts.")]
        [SerializeField] private int startNodeIndex;

        [Header("Generation")]
        [Tooltip("True when this asset is generated from an external dialogue JSON file.")]
        [SerializeField, HideInInspector] private bool generatedFromJson;

        [Tooltip("Project-relative path of the JSON source used to generate this asset.")]
        [SerializeField, HideInInspector] private string sourceJsonPath;

        /// <summary>
        /// Gets all dialogue nodes in this tree.
        /// </summary>
        public Node[] Nodes => nodes;

        /// <summary>
        /// Gets the first node index for this tree.
        /// </summary>
        public int StartNodeIndex => startNodeIndex;

        /// <summary>Gets whether this asset is generated from dialogue JSON.</summary>
        public bool GeneratedFromJson => generatedFromJson;

        /// <summary>Gets the project-relative JSON source path for generated assets.</summary>
        public string SourceJsonPath => sourceJsonPath;

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
