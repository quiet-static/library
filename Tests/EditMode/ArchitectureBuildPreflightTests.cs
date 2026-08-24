using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ArchitectureBuildPreflightTests
    {
        [Test]
        public void DevelopmentScene_RemainsWarningForDevelopmentBuild()
        {
            var issue = new ValidationIssue(
                ValidationSeverity.Warning,
                "Build Settings",
                "Development scene enabled.",
                code: ArchitectureValidation.DevelopmentSceneCode);

            var result = ArchitectureBuildPreflight.EvaluateForBuild(
                new[] { issue },
                development: true);

            Assert.That(result[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
        }

        [Test]
        public void DevelopmentScene_BecomesErrorForReleaseBuild()
        {
            var issue = new ValidationIssue(
                ValidationSeverity.Warning,
                "Build Settings",
                "Development scene enabled.",
                code: ArchitectureValidation.DevelopmentSceneCode);

            var result = ArchitectureBuildPreflight.EvaluateForBuild(
                new[] { issue },
                development: false);

            Assert.That(result[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(result[0].Code, Is.EqualTo(ArchitectureValidation.DevelopmentSceneCode));
        }
    }
}
