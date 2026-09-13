using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.DebugTools;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.Interactions;
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
        private readonly List<Object> createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            createdObjects.Clear();
            host = Track(new GameObject("Architecture Validation Fixture"));
            channel = Track(ScriptableObject.CreateInstance<SceneFlowRequestChannel>());
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
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
            Assert.That(issues[0].Message,
                Does.Contain(nameof(RequiredChannelValidationFixture)));
            Assert.That(issues[0].Message.ToLowerInvariant(),
                Does.Contain("active").And.Contain("same asset"));
        }

        [Test]
        public void DisabledCommandReceiver_DoesNotSatisfyActiveSender()
        {
            RequiredChannelValidationFixture caller =
                host.AddComponent<RequiredChannelValidationFixture>();
            ChannelReceiverValidationFixture receiver =
                host.AddComponent<ChannelReceiverValidationFixture>();
            caller.Assign(channel);
            receiver.Assign(channel);
            receiver.enabled = false;

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { caller, receiver });

            Assert.That(
                issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.MissingChannelReceiverCode));
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
                    Is.EquivalentTo(new[]
                    {
                        ToolkitValidation.MissingSaveParticipantIdCode,
                        ToolkitValidation.DuplicateSaveParticipantIdCode,
                    }));
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

        [TestCase(typeof(SaveManager), "requestChannel", true)]
        [TestCase(typeof(SceneFlowManager), "requestChannel", true)]
        [TestCase(typeof(SceneTransitionTrigger), "requestChannel", false)]
        [TestCase(typeof(SceneTransitionHandler), "requestChannel", false)]
        public void RequiredChannelTooltips_DoNotDescribeCanonicalChannelsAsOptional(
            System.Type componentType,
            string fieldName,
            bool isReceiver)
        {
            FieldInfo field = componentType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            RequiredCommandChannelAttribute required =
                field.GetCustomAttribute<RequiredCommandChannelAttribute>();
            TooltipAttribute tooltip = field.GetCustomAttribute<TooltipAttribute>();
            Assert.That(required, Is.Not.Null);
            Assert.That(required.IsReceiver, Is.EqualTo(isReceiver));
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.tooltip.ToLowerInvariant(),
                Does.Contain("required")
                    .And.Not.Contain("optional")
                    .And.Not.Contain("recommended"));
        }

        [Test]
        public void ReadableTrigger_OrdinaryListenerIsNotAReadableCapableReceiver()
        {
            InteractionUIChannel uiChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger trigger =
                host.AddComponent<ReadableInteractionTrigger>();
            InteractionUIChannelListener listener =
                host.AddComponent<InteractionUIChannelListener>();
            SetField(trigger, "channel", uiChannel);
            SetField(trigger, "content", content);
            SetField(listener, "channel", uiChannel);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, listener });

            ValidationIssue issue = issues.Single(
                item => item.Code == ArchitectureValidation.MissingReadableReceiverCode);
            Assert.That(issue.Message,
                Does.Contain(nameof(ReadableOverlayHandler))
                    .And.Contain(nameof(InteractionUIChannelListener)));
        }

        [Test]
        public void ReadableTrigger_RequiresUsableOverlayOnTheSameChannel()
        {
            InteractionUIChannel triggerChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            InteractionUIChannel otherChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger trigger =
                host.AddComponent<ReadableInteractionTrigger>();
            SetField(trigger, "channel", triggerChannel);
            SetField(trigger, "content", content);
            ReadableOverlayHandler overlay = CreateReadableOverlay(otherChannel);

            IReadOnlyList<ValidationIssue> wrongChannelIssues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, overlay });
            Assert.That(wrongChannelIssues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.MissingReadableReceiverCode));

            SetField(overlay, "channel", triggerChannel);
            IReadOnlyList<ValidationIssue> matchingChannelIssues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, overlay });
            Assert.That(matchingChannelIssues.Select(issue => issue.Code),
                Does.Not.Contain(ArchitectureValidation.InvalidReadableConfigurationCode)
                    .And.Not.Contain(ArchitectureValidation.MissingReadableReceiverCode)
                    .And.Not.Contain(ArchitectureValidation.DuplicateReadableReceiverCode));
        }

        [Test]
        public void ReadableValidation_RejectsInactiveCanvasGroupAsAUsableReceiver()
        {
            InteractionUIChannel uiChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger trigger =
                host.AddComponent<ReadableInteractionTrigger>();
            SetField(trigger, "channel", uiChannel);
            SetField(trigger, "content", content);
            ReadableOverlayHandler overlay = CreateReadableOverlay(uiChannel);

            GameObject inactiveVisuals = new("Inactive Readable Visuals");
            inactiveVisuals.transform.SetParent(host.transform);
            CanvasGroup inactiveCanvas = inactiveVisuals.AddComponent<CanvasGroup>();
            inactiveVisuals.SetActive(false);
            SetField(overlay, "canvasGroup", inactiveCanvas);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, overlay });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidReadableConfigurationCode)
                    .And.Contain(ArchitectureValidation.MissingReadableReceiverCode));
        }

        [Test]
        public void ReadableValidation_RejectsInactiveBodyTextAsAUsableReceiver()
        {
            InteractionUIChannel uiChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger trigger =
                host.AddComponent<ReadableInteractionTrigger>();
            SetField(trigger, "channel", uiChannel);
            SetField(trigger, "content", content);
            ReadableOverlayHandler overlay = CreateReadableOverlay(uiChannel);

            Component inactiveBody = CreateText("Inactive Readable Body");
            inactiveBody.gameObject.SetActive(false);
            SetField(overlay, "bodyText", inactiveBody);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, overlay });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidReadableConfigurationCode)
                    .And.Contain(ArchitectureValidation.MissingReadableReceiverCode));
        }

        [Test]
        public void ReadableValidation_ReportsMissingFieldsAndDuplicateOverlays()
        {
            InteractionUIChannel uiChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger trigger =
                host.AddComponent<ReadableInteractionTrigger>();
            SetField(trigger, "channel", uiChannel);
            SetField(trigger, "content", content);
            ReadableOverlayHandler first = CreateReadableOverlay(uiChannel);
            ReadableOverlayHandler second = CreateReadableOverlay(uiChannel);
            SetField(first, "bodyText", null);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { trigger, first, second });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidReadableConfigurationCode));
            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.DuplicateReadableReceiverCode),
                "Every active subscriber counts toward duplicate runtime delivery, even if its visuals are incomplete.");

            SetField(first, "bodyText", CreateText("Restored Body"));
            issues = ArchitectureValidation.ScanOpenScenes(
                new Component[] { trigger, first, second });
            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.DuplicateReadableReceiverCode));
        }

        [Test]
        public void ReadableValidation_ReportsDuplicateSubscribersOncePerChannel()
        {
            InteractionUIChannel uiChannel =
                Track(ScriptableObject.CreateInstance<InteractionUIChannel>());
            ReadableContentDefinition content =
                Track(ScriptableObject.CreateInstance<ReadableContentDefinition>());
            ReadableInteractionTrigger firstTrigger =
                host.AddComponent<ReadableInteractionTrigger>();
            GameObject secondTriggerObject = new("Second Readable Trigger");
            secondTriggerObject.transform.SetParent(host.transform);
            ReadableInteractionTrigger secondTrigger =
                secondTriggerObject.AddComponent<ReadableInteractionTrigger>();
            SetField(firstTrigger, "channel", uiChannel);
            SetField(firstTrigger, "content", content);
            SetField(secondTrigger, "channel", uiChannel);
            SetField(secondTrigger, "content", content);
            ReadableOverlayHandler first = CreateReadableOverlay(uiChannel);
            ReadableOverlayHandler second = CreateReadableOverlay(uiChannel);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[]
                    {
                        firstTrigger,
                        secondTrigger,
                        first,
                        second,
                    });

            Assert.That(
                issues.Count(issue =>
                    issue.Code == ArchitectureValidation.DuplicateReadableReceiverCode),
                Is.EqualTo(1));
        }

        [Test]
        public void FadeValidation_AcceptsDirectFaderFallbackWithoutAChannel()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            ScreenFader fader = host.AddComponent<ScreenFader>();
            host.AddComponent<CanvasGroup>();
            SetField(manager, "screenFader", fader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, fader });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Not.Contain(ArchitectureValidation.FadeChannelFallbackCode)
                    .And.Not.Contain(ArchitectureValidation.InvalidFadeConfigurationCode));
        }

        [Test]
        public void FadeValidation_ExplainsConfiguredChannelFallbackWithoutCallingItRequired()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            ScreenFader fader = host.AddComponent<ScreenFader>();
            host.AddComponent<CanvasGroup>();
            ScreenFadeChannel fadeChannel =
                Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            SetField(manager, "screenFader", fader);
            SetField(manager, "screenFadeChannel", fadeChannel);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, fader });

            ValidationIssue issue = issues.Single(
                item => item.Code == ArchitectureValidation.FadeChannelFallbackCode);
            Assert.That(issue.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(issue.Message.ToLowerInvariant(),
                Does.Contain("fallback").And.Not.Contain("required"));
        }

        [Test]
        public void FadeValidation_RejectsHandlerWithoutItsChannel()
        {
            ScreenFader fader = host.AddComponent<ScreenFader>();
            host.AddComponent<CanvasGroup>();
            ScreenFadeChannelHandler handler =
                host.AddComponent<ScreenFadeChannelHandler>();
            SetField(handler, "screenFader", fader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { fader, handler });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidFadeConfigurationCode));
        }

        [Test]
        public void FadeValidation_RejectsDirectFaderWithoutCanvasGroup()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            ScreenFader fader = host.AddComponent<ScreenFader>();
            SetField(manager, "screenFader", fader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, fader });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidFadeConfigurationCode));
        }

        [Test]
        public void FadeValidation_RejectsHandlerWhoseFaderHasNoCanvasGroup()
        {
            ScreenFadeChannel fadeChannel =
                Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            ScreenFader fader = host.AddComponent<ScreenFader>();
            ScreenFadeChannelHandler handler =
                host.AddComponent<ScreenFadeChannelHandler>();
            SetField(handler, "channel", fadeChannel);
            SetField(handler, "screenFader", fader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { fader, handler });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidFadeConfigurationCode));
        }

        [Test]
        public void FadeValidation_BrokenSubscriberShadowsDirectFallback()
        {
            ScreenFadeChannel fadeChannel =
                Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            CanvasGroup directCanvas = host.AddComponent<CanvasGroup>();
            ScreenFader directFader = host.AddComponent<ScreenFader>();
            SetField(directFader, "canvasGroup", directCanvas);
            SetField(manager, "screenFadeChannel", fadeChannel);
            SetField(manager, "screenFader", directFader);

            GameObject brokenRoot = new("Broken Fade Subscriber");
            brokenRoot.transform.SetParent(host.transform);
            ScreenFader brokenFader = brokenRoot.AddComponent<ScreenFader>();
            ScreenFadeChannelHandler brokenHandler =
                brokenRoot.AddComponent<ScreenFadeChannelHandler>();
            SetField(brokenHandler, "channel", fadeChannel);
            SetField(brokenHandler, "screenFader", brokenFader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[]
                    {
                        manager,
                        directFader,
                        brokenFader,
                        brokenHandler,
                    });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Not.Contain(ArchitectureValidation.FadeChannelFallbackCode));
            Assert.That(
                issues.Any(issue =>
                    issue.Context == manager &&
                    issue.Code == ArchitectureValidation.InvalidFadeConfigurationCode &&
                    issue.Message.Contains("selects the channel")),
                Is.True);
        }

        [Test]
        public void FadeValidation_CountsBrokenAndUsableSubscribersAsDuplicates()
        {
            ScreenFadeChannel fadeChannel =
                Track(ScriptableObject.CreateInstance<ScreenFadeChannel>());
            CanvasGroup validCanvas = host.AddComponent<CanvasGroup>();
            ScreenFader validFader = host.AddComponent<ScreenFader>();
            ScreenFadeChannelHandler validHandler =
                host.AddComponent<ScreenFadeChannelHandler>();
            SetField(validFader, "canvasGroup", validCanvas);
            SetField(validHandler, "channel", fadeChannel);
            SetField(validHandler, "screenFader", validFader);

            GameObject brokenRoot = new("Duplicate Broken Fade Subscriber");
            brokenRoot.transform.SetParent(host.transform);
            ScreenFader brokenFader = brokenRoot.AddComponent<ScreenFader>();
            ScreenFadeChannelHandler brokenHandler =
                brokenRoot.AddComponent<ScreenFadeChannelHandler>();
            SetField(brokenHandler, "channel", fadeChannel);
            SetField(brokenHandler, "screenFader", brokenFader);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[]
                    {
                        validFader,
                        validHandler,
                        brokenFader,
                        brokenHandler,
                    });

            Assert.That(
                issues.Count(issue =>
                    issue.Code == ArchitectureValidation.DuplicateFadeReceiverCode),
                Is.EqualTo(1));
        }

        [Test]
        public void FadeValidation_RejectsInactiveCanvasGroup()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            GameObject faderRoot = new("Inactive Direct Fader");
            faderRoot.transform.SetParent(host.transform);
            CanvasGroup canvasGroup = faderRoot.AddComponent<CanvasGroup>();
            ScreenFader fader = faderRoot.AddComponent<ScreenFader>();
            SetField(fader, "canvasGroup", canvasGroup);
            SetField(manager, "screenFader", fader);
            faderRoot.SetActive(false);

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, fader });

            Assert.That(issues.Select(issue => issue.Code),
                Does.Contain(ArchitectureValidation.InvalidFadeConfigurationCode));
        }

        [Test]
        public void FadeValidation_RejectsAmbiguousFaderDiscovery()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            ScreenFader first = CreateConfiguredFader("First Discoverable Fader");
            ScreenFader second = CreateConfiguredFader("Second Discoverable Fader");

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, first, second });

            Assert.That(
                issues.Any(issue =>
                    issue.Code == ArchitectureValidation.InvalidFadeConfigurationCode &&
                    issue.Message.Contains("ambiguous")),
                Is.True);
        }

        [Test]
        public void FadeValidation_RejectsAmbiguousDiscoveryWithValidAndBrokenFaders()
        {
            SceneFlowManager manager = host.AddComponent<SceneFlowManager>();
            ScreenFader valid = CreateConfiguredFader("Valid Discoverable Fader");
            GameObject brokenObject = new("Broken Discoverable Fader");
            brokenObject.transform.SetParent(host.transform);
            ScreenFader broken = brokenObject.AddComponent<ScreenFader>();

            IReadOnlyList<ValidationIssue> issues =
                ArchitectureValidation.ScanOpenScenes(
                    new Component[] { manager, valid, broken });

            Assert.That(
                issues.Any(issue =>
                    issue.Code == ArchitectureValidation.InvalidFadeConfigurationCode &&
                    issue.Message.Contains("ambiguous")),
                Is.True);
        }

        [Test]
        public void ValidationCodeConstants_AreGloballyUnique()
        {
            var assignments = typeof(ArchitectureValidation).Assembly
                .GetTypes()
                .SelectMany(type => type.GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly)
                    .Where(field =>
                        field.IsLiteral &&
                        !field.IsInitOnly &&
                        field.FieldType == typeof(string) &&
                        field.Name.EndsWith("Code", System.StringComparison.Ordinal))
                    .Select(field => new
                    {
                        Code = (string)field.GetRawConstantValue(),
                        Owner = $"{type.Name}.{field.Name}",
                    }))
                .ToArray();
            string[] duplicates = assignments
                .GroupBy(assignment => assignment.Code)
                .Where(group => group.Count() > 1)
                .Select(group =>
                    $"{group.Key}: {string.Join(", ", group.Select(value => value.Owner))}")
                .ToArray();

            Assert.That(
                duplicates,
                Is.Empty,
                $"Validation codes must be globally unique: {string.Join("; ", duplicates)}");
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

        private ReadableOverlayHandler CreateReadableOverlay(
            InteractionUIChannel uiChannel)
        {
            GameObject overlayObject = new GameObject(
                "Readable Overlay",
                typeof(RectTransform),
                typeof(CanvasGroup));
            overlayObject.transform.SetParent(host.transform);
            ReadableOverlayHandler overlay =
                overlayObject.AddComponent<ReadableOverlayHandler>();
            SetField(overlay, "channel", uiChannel);
            SetField(overlay, "canvasGroup", overlayObject.GetComponent<CanvasGroup>());
            SetField(overlay, "bodyText", CreateText("Readable Body", overlayObject.transform));
            return overlay;
        }

        private ScreenFader CreateConfiguredFader(string name)
        {
            GameObject faderObject = new(name);
            faderObject.transform.SetParent(host.transform);
            CanvasGroup canvasGroup = faderObject.AddComponent<CanvasGroup>();
            ScreenFader fader = faderObject.AddComponent<ScreenFader>();
            SetField(fader, "canvasGroup", canvasGroup);
            return fader;
        }

        private Component CreateText(string name, Transform parent = null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent ?? host.transform);
            System.Type textType = System.Type.GetType(
                "TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(textType, Is.Not.Null);
            return textObject.AddComponent(textType);
        }

        private T Track<T>(T value) where T : Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{name}'.");
            field.SetValue(target, value);
        }
    }
}
