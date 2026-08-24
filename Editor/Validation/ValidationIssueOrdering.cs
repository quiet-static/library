using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Provides one deterministic order for validation UI, tests, and batch output.</summary>
    public static class ValidationIssueOrdering
    {
        /// <summary>Returns a stable, materialized ordering without modifying the input.</summary>
        public static IReadOnlyList<ValidationIssue> Sort(
            IEnumerable<ValidationIssue> issues)
        {
            return (issues ?? Array.Empty<ValidationIssue>())
                .Where(issue => issue != null)
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Category, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
