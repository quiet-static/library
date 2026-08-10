using System;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace QuietStatic.Toolkit.Deductions
{
    /// <summary>
    /// Keeps each configured deduction category mutually exclusive while continuing to
    /// use <see cref="FlagManager"/> as the authoritative state store.
    /// </summary>
    [MovedFrom(true, "QuietStatic.NightwatchTheatre.Deductions", null, "DeductionCategoryController")]
    [AddComponentMenu("Quiet Static Toolkit/Deductions/Category Controller")]
    public sealed class DeductionCategoryController : MonoBehaviour
    {
        /// <summary>Defines the answer flags that belong to one deduction question.</summary>
        [Serializable]
        public sealed class Category
        {
            [Tooltip("Designer-facing category name, such as Suspect or Motive.")]
            [SerializeField] private string name;

            [Tooltip("Mutually exclusive answer flags in this category.")]
            [FlagId]
            [SerializeField] private string[] answerFlags;

            /// <summary>Gets the designer-facing category name.</summary>
            public string Name => name ?? string.Empty;

            /// <summary>Gets the answer flags in this category.</summary>
            public string[] AnswerFlags => answerFlags ?? Array.Empty<string>();
        }

        [Tooltip("Deduction categories whose answer flags must remain mutually exclusive.")]
        [SerializeField] private Category[] categories;

        private bool isUpdating;

        private void OnEnable()
        {
            FlagManager.OnFlagSet += HandleFlagSet;
        }

        private void OnDisable()
        {
            FlagManager.OnFlagSet -= HandleFlagSet;
        }

        /// <summary>
        /// Selects an answer through the existing flag manager. This is useful for
        /// buttons or UnityEvents; dialogue choices may continue setting flags directly.
        /// </summary>
        public void SelectAnswer(string answerFlag)
        {
            FlagManager.Instance?.SetFlag(answerFlag);
        }

        private void HandleFlagSet(string selectedFlag)
        {
            FlagManager manager = FlagManager.Instance;

            if (isUpdating || manager == null || string.IsNullOrWhiteSpace(selectedFlag))
            {
                return;
            }

            isUpdating = true;

            try
            {
                foreach (Category category in categories ?? Array.Empty<Category>())
                {
                    if (category == null || !Contains(category.AnswerFlags, selectedFlag))
                    {
                        continue;
                    }

                    foreach (string answerFlag in category.AnswerFlags)
                    {
                        if (!string.Equals(answerFlag, selectedFlag, StringComparison.Ordinal))
                        {
                            manager.ClearFlag(answerFlag);
                        }
                    }
                }
            }
            finally
            {
                isUpdating = false;
            }
        }

        private static bool Contains(string[] values, string target)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.Equals(value, target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
