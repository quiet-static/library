using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Applies shared architecture validation before Unity begins a player build.</summary>
    public sealed class ArchitectureBuildPreflight : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool development = (report.summary.options & UnityEditor.BuildOptions.Development) != 0;
            IReadOnlyList<ValidationIssue> issues = EvaluateForBuild(
                ArchitectureValidationBatch.ScanAllBuildInputs(),
                development);
            ValidationIssue firstError = issues.FirstOrDefault(
                issue => issue.Severity == ValidationSeverity.Error);
            if (firstError != null)
            {
                throw new BuildFailedException(
                    $"Architecture preflight failed [{firstError.Code}]: {firstError.Message}");
            }
        }

        /// <summary>Applies development/release severity policy to deterministic issues.</summary>
        public static IReadOnlyList<ValidationIssue> EvaluateForBuild(
            IEnumerable<ValidationIssue> issues,
            bool development)
        {
            return ValidationIssueOrdering.Sort((issues ?? Enumerable.Empty<ValidationIssue>())
                .Select(issue =>
                    !development &&
                    issue.Severity == ValidationSeverity.Warning &&
                    issue.Code == ArchitectureValidation.DevelopmentSceneCode
                        ? new ValidationIssue(
                            ValidationSeverity.Error,
                            issue.Category,
                            issue.Message,
                            issue.Context,
                            issue.AssetPath,
                            issue.Code)
                        : issue));
        }
    }
}
