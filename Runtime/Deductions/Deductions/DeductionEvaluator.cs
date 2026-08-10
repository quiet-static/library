using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace QuietStatic.Toolkit.Deductions
{
    /// <summary>Selects the highest-priority deduction result matching current flags.</summary>
    [MovedFrom(true, "QuietStatic.NightwatchTheatre.Deductions", null, "DeductionEvaluator")]
    [AddComponentMenu("Quiet Static Toolkit/Deductions/Evaluator")]
    public sealed class DeductionEvaluator : MonoBehaviour
    {
        /// <summary>UnityEvent carrying the selected deduction result.</summary>
        [Serializable]
        public sealed class ResultUnityEvent : UnityEvent<DeductionResultDefinition> { }

        [Tooltip("Possible results. Array order breaks ties between equal priorities.")]
        [SerializeField] private DeductionResultDefinition[] results;

        [Tooltip("Invoked with the winning result. Wire a presenter or scene-level ending handler here.")]
        [SerializeField] private ResultUnityEvent onResultEvaluated;

        [Tooltip("Invoked when no configured result matches.")]
        [SerializeField] private UnityEvent onNoResult;

        /// <summary>Gets the most recently evaluated result.</summary>
        public DeductionResultDefinition CurrentResult { get; private set; }

        /// <summary>Evaluates current flags, stores the winner, and invokes result events.</summary>
        public void Evaluate()
        {
            CurrentResult = FindResult(results);

            if (CurrentResult != null)
            {
                onResultEvaluated?.Invoke(CurrentResult);
            }
            else
            {
                onNoResult?.Invoke();
            }
        }

        /// <summary>Returns the highest-priority matching definition.</summary>
        public static DeductionResultDefinition FindResult(
            DeductionResultDefinition[] candidates,
            FlagManager flagManager = null)
        {
            DeductionResultDefinition winner = null;

            foreach (DeductionResultDefinition candidate in
                     candidates ?? Array.Empty<DeductionResultDefinition>())
            {
                if (candidate == null || !candidate.Matches(flagManager))
                {
                    continue;
                }

                if (winner == null || candidate.Priority > winner.Priority)
                {
                    winner = candidate;
                }
            }

            return winner;
        }
    }
}
