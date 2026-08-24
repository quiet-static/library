using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.DebugTools;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.Saving;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    internal sealed class RequiredChannelValidationFixture : MonoBehaviour
    {
        [RequiredCommandChannel]
        [SerializeField] private SceneFlowRequestChannel channel;

        public void Assign(SceneFlowRequestChannel value) => channel = value;
    }

    internal sealed class ChannelReceiverValidationFixture : MonoBehaviour
    {
        [RequiredCommandChannel(isReceiver: true)]
        [SerializeField] private SceneFlowRequestChannel channel;

        public void Assign(SceneFlowRequestChannel value) => channel = value;
    }

    internal sealed class InvalidRequiredChannelValidationFixture : MonoBehaviour
    {
        [RequiredCommandChannel]
        [SerializeField] private string channel;
    }

    internal sealed class ValidationManagerFixture :
        ToolkitSingleton<ValidationManagerFixture> { }

    internal sealed class CrossSceneReferenceValidationFixture : MonoBehaviour
    {
        [SerializeField] private ValidationManagerFixture manager;

        public void Assign(ValidationManagerFixture value) => manager = value;
    }

    internal sealed class SaveParticipantValidationFixture : MonoBehaviour, ISaveParticipant
    {
        public string Id { private get; set; }
        public string SaveId => Id;
        public string CaptureSaveState() => string.Empty;
        public void RestoreSaveState(string json) { }
    }

    public sealed class ArchitectureValidationTests
    {
        private GameObject host;
        private SceneFlowRequestChannel channel;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Architecture Validation Fixture");
            channel = ScriptableObject.CreateInstance<SceneFlowRequestChannel>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(channel);
        }

        [Test]
        public void RequiredCommandChannel_ReportsMissingAssignment()
        {
            RequiredChannelValidationFixture fixture =
                host.AddComponent<RequiredChannelValidationFixture>();

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(new Component[] { fixture });

            Assert.AreEqual(1, issues.Count,
                "Expected exactly one missing-channel diagnostic.");
            Assert.That(issues[0].Code,
                Is.EqualTo(ArchitectureValidation.MissingCommandChannelCode));
            Assert.That(issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(issues[0].Context, Is.SameAs(fixture));
        }

        [Test]
        public void RequiredCommandChannel_IsAnInspectorPropertyAttribute()
        {
            Assert.That(
                new RequiredCommandChannelAttribute(),
                Is.AssignableTo<UnityEngine.PropertyAttribute>());
        }

        [Test]
        public void RequiredCommandChannel_ReportsAttributeOnUnsupportedFieldType()
        {
            InvalidRequiredChannelValidationFixture fixture =
                host.AddComponent<InvalidRequiredChannelValidationFixture>();

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(new Component[] { fixture });

            Assert.AreEqual(1, issues.Count,
                "Expected exactly one invalid-attribute diagnostic.");
            Assert.That(issues[0].Code,
                Is.EqualTo(ArchitectureValidation.InvalidCommandChannelAttributeCode));
            Assert.That(issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(issues[0].Context, Is.SameAs(fixture));
        }

        [Test]
        public void RequiredCommandChannel_AcceptsAssignedChannel()
        {
            RequiredChannelValidationFixture fixture =
                host.AddComponent<RequiredChannelValidationFixture>();
            ChannelReceiverValidationFixture receiver =
                host.AddComponent<ChannelReceiverValidationFixture>();
            fixture.Assign(channel);
            receiver.Assign(channel);

            IReadOnlyList<ValidationIssue> issues = ArchitectureValidation.ScanOpenScenes(
                new Component[] { fixture, receiver });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void CommandChannel_ReportsMissingReceiverOnce()
        {
            RequiredChannelValidationFixture first =
                host.AddComponent<RequiredChannelValidationFixture>();
            RequiredChannelValidationFixture second =
                host.AddComponent<RequiredChannelValidationFixture>();
            first.Assign(channel);
            second.Assign(channel);

            IReadOnlyList<ValidationIssue> issues = ArchitectureValidation.ScanOpenScenes(
                new Component[] { first, second });

            Assert.AreEqual(1, issues.Count);
            Assert.That(issues[0].Code,
                Is.EqualTo(ArchitectureValidation.MissingChannelReceiverCode));
        }

        [Test]
        public void CommandChannel_ReportsDuplicateReceiversOnce()
        {
            RequiredChannelValidationFixture caller =
                host.AddComponent<RequiredChannelValidationFixture>();
            ChannelReceiverValidationFixture first =
                host.AddComponent<ChannelReceiverValidationFixture>();
            ChannelReceiverValidationFixture second =
                host.AddComponent<ChannelReceiverValidationFixture>();
            caller.Assign(channel);
            first.Assign(channel);
            second.Assign(channel);

            IReadOnlyList<ValidationIssue> issues = ArchitectureValidation.ScanOpenScenes(
                new Component[] { caller, first, second });

            Assert.AreEqual(1, issues.Count);
            Assert.That(issues[0].Code,
                Is.EqualTo(ArchitectureValidation.DuplicateChannelReceiverCode));
        }

        [Test]
        public void CrossSceneManagerReference_IsRejected()
        {
            CrossSceneReferenceValidationFixture source =
                host.AddComponent<CrossSceneReferenceValidationFixture>();
            Scene managerScene = EditorSceneManager.NewPreviewScene();
            GameObject managerObject = new("Validation Manager");
            SceneManager.MoveGameObjectToScene(managerObject, managerScene);
            ValidationManagerFixture manager =
                managerObject.AddComponent<ValidationManagerFixture>();
            source.Assign(manager);

            try
            {
                IReadOnlyList<ValidationIssue> issues =
                    ArchitectureValidation.ScanOpenScenes(
                        new Component[] { source, manager });

                Assert.AreEqual(1, issues.Count);
                Assert.That(issues[0].Code,
                    Is.EqualTo(ArchitectureValidation.CrossSceneManagerReferenceCode));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                EditorSceneManager.ClosePreviewScene(managerScene);
            }
        }

        [Test]
        public void SameSceneManagerReference_IsAccepted()
        {
            CrossSceneReferenceValidationFixture source =
                host.AddComponent<CrossSceneReferenceValidationFixture>();
            ValidationManagerFixture manager =
                host.AddComponent<ValidationManagerFixture>();
            source.Assign(manager);

            IReadOnlyList<ValidationIssue> issues = ArchitectureValidation.ScanOpenScenes(
                new Component[] { source, manager });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void PackageManifest_RejectsWindowsAbsoluteFileDependency()
        {
            const string manifest =
                "{\"dependencies\":{\"example\":\"file:C:/machine/package\"}}";

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ValidatePackageManifestText(manifest);

            Assert.That(issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[] { ArchitectureValidation.AbsoluteLocalPackageCode }));
        }

        [Test]
        public void SaveParticipants_ReportEmptyAndDuplicateStableIds()
        {
            SaveParticipantValidationFixture empty =
                host.AddComponent<SaveParticipantValidationFixture>();
            var duplicateHost = new GameObject("Duplicate Save Participant");
            try
            {
                SaveParticipantValidationFixture first =
                    duplicateHost.AddComponent<SaveParticipantValidationFixture>();
                SaveParticipantValidationFixture second =
                    duplicateHost.AddComponent<SaveParticipantValidationFixture>();
                first.Id = "shared.id";
                second.Id = "shared.id";

                var issues = new List<ValidationIssue>();
                ToolkitValidation.ValidateSaveParticipants(
                    new Component[] { empty, first, second },
                    issues);

                Assert.That(
                    issues.Select(issue => issue.Code),
                    Is.EquivalentTo(new[] { "QS1300", "QS1301" }));
            }
            finally
            {
                Object.DestroyImmediate(duplicateHost);
            }
        }

        [Test]
        public void DebugDashboard_RequiresChannelAndHasNoSelfPersistenceOption()
        {
            FieldInfo channelField = typeof(DebugDashboard).GetField(
                "sceneFlowRequestChannel",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(channelField, Is.Not.Null);
            Assert.That(
                channelField.GetCustomAttribute<RequiredCommandChannelAttribute>(),
                Is.Not.Null);
            Assert.That(
                typeof(DebugDashboard).GetField(
                    "persistBetweenScenes",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void PackageManifest_AcceptsRepositoryRelativeFileDependency()
        {
            const string manifest =
                "{\"dependencies\":{\"example\":\"file:../../../libraries/library\"}}";

            Assert.That(
                ArchitectureValidation.ValidatePackageManifestText(manifest),
                Is.Empty);
        }

        [Test]
        public void IssueOrdering_IsDeterministicAndPlacesErrorsFirst()
        {
            var issues = new[]
            {
                new ValidationIssue(ValidationSeverity.Warning, "Z", "Second", code: "QS2000"),
                new ValidationIssue(ValidationSeverity.Error, "A", "Third", code: "QS3000"),
                new ValidationIssue(ValidationSeverity.Error, "B", "First", code: "QS1000")
            };

            IReadOnlyList<ValidationIssue> sorted = ValidationIssueOrdering.Sort(issues);

            Assert.That(sorted.Select(issue => issue.Code),
                Is.EqualTo(new[] { "QS1000", "QS3000", "QS2000" }));
        }

        [Test]
        public void ExitCode_FailsOnlyForErrors()
        {
            Assert.That(ArchitectureValidation.GetExitCode(new[]
            {
                new ValidationIssue(ValidationSeverity.Info, "Test", "Info"),
                new ValidationIssue(ValidationSeverity.Warning, "Test", "Warning")
            }), Is.Zero);

            Assert.That(ArchitectureValidation.GetExitCode(new[]
            {
                new ValidationIssue(ValidationSeverity.Error, "Test", "Error")
            }), Is.EqualTo(1));
        }
    }
}
