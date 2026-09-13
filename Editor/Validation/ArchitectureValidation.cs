using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>
    /// Architecture-specific, read-only validation rules shared by editor UI, tests,
    /// build preflight, and batch execution.
    /// </summary>
    public static class ArchitectureValidation
    {
        public const string MissingCommandChannelCode = "QS1001";
        public const string AbsoluteLocalPackageCode = "QS1002";
        public const string DevelopmentSceneCode = "QS1003";
        public const string MissingChannelReceiverCode = "QS1004";
        public const string DuplicateChannelReceiverCode = "QS1005";
        public const string CrossSceneManagerReferenceCode = "QS1006";
        public const string InvalidCommandChannelAttributeCode = "QS1007";
        public const string InvalidReadableConfigurationCode = "QS1012";
        public const string MissingReadableReceiverCode = "QS1013";
        public const string DuplicateReadableReceiverCode = "QS1014";
        public const string FadeChannelFallbackCode = "QS1015";
        public const string InvalidFadeConfigurationCode = "QS1016";
        public const string DuplicateFadeReceiverCode = "QS1017";

        private static readonly Regex WindowsAbsoluteFileDependency = new(
            "\\\"file:[A-Za-z]:[/\\\\]",
            RegexOptions.CultureInvariant);

        /// <summary>Scans architecture rules that apply to currently loaded scenes.</summary>
        public static IReadOnlyList<ValidationIssue> ScanOpenScenes(
            IEnumerable<Component> components)
        {
            var issues = new List<ValidationIssue>();
            ValidateRequiredCommandChannels(components, issues);
            ValidateReadableConfiguration(components, issues);
            ValidateFadeConfiguration(components, issues);
            ValidateCrossSceneManagerReferences(components, issues);
            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>Scans project files and build configuration without modifying them.</summary>
        public static IReadOnlyList<ValidationIssue> ScanProjectConfiguration()
        {
            var issues = new List<ValidationIssue>();
            issues.AddRange(PackageAssetReferenceValidation.ScanPackageAssets());
            foreach (string packageFile in new[] { "manifest.json", "packages-lock.json" })
            {
                string packagePath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, $"../Packages/{packageFile}"));
                if (File.Exists(packagePath))
                {
                    ValidatePackageManifestText(
                        File.ReadAllText(packagePath),
                        $"Packages/{packageFile}",
                        issues);
                }
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes
                         .Where(scene => scene.enabled))
            {
                string path = AssetDatabase.GUIDToAssetPath(scene.guid);
                if (IsDevelopmentOnlyScene(path))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Build Settings",
                        $"Development-only scene is enabled for builds: {path}",
                        null,
                        path,
                        DevelopmentSceneCode));
                }
            }

            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>Validates local package paths in a manifest supplied by tests or tools.</summary>
        public static IReadOnlyList<ValidationIssue> ValidatePackageManifestText(
            string manifest,
            string assetPath = "Packages/manifest.json")
        {
            var issues = new List<ValidationIssue>();
            ValidatePackageManifestText(manifest, assetPath, issues);
            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>Returns the process exit code for a set of issues.</summary>
        public static int GetExitCode(IEnumerable<ValidationIssue> issues)
        {
            return issues != null && issues.Any(
                issue => issue != null &&
                         issue.Severity == ValidationSeverity.Error)
                ? 1
                : 0;
        }

        private static void ValidateRequiredCommandChannels(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            var callers =
                new Dictionary<CrossSceneCommandChannel, List<CommandChannelEndpoint>>();
            var receivers =
                new Dictionary<CrossSceneCommandChannel, List<CommandChannelEndpoint>>();

            foreach (Component component in components ?? Array.Empty<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                foreach (FieldInfo field in GetInstanceFields(component.GetType()))
                {
                    RequiredCommandChannelAttribute attribute =
                        field.GetCustomAttribute<RequiredCommandChannelAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    if (!typeof(CrossSceneCommandChannel).IsAssignableFrom(field.FieldType))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            "Command Channels",
                            $"{component.GetType().Name}.{field.Name} uses " +
                            $"[{nameof(RequiredCommandChannelAttribute)}] but " +
                            $"{field.FieldType.Name} is not a command-channel type.",
                            component,
                            GetContextPath(component),
                            InvalidCommandChannelAttributeCode));
                        continue;
                    }

                    CrossSceneCommandChannel channel =
                        field.GetValue(component) as CrossSceneCommandChannel;
                    if (channel != null)
                    {
                        if (attribute.IsReceiver &&
                            component is Behaviour receiverBehaviour &&
                            !receiverBehaviour.isActiveAndEnabled)
                        {
                            // A disabled or inactive receiver is serialized but cannot
                            // satisfy the runtime subscription represented by HasReceivers.
                            continue;
                        }

                        Dictionary<CrossSceneCommandChannel, List<CommandChannelEndpoint>> index =
                            attribute.IsReceiver ? receivers : callers;
                        if (!index.TryGetValue(
                                channel,
                                out List<CommandChannelEndpoint> owners))
                        {
                            owners = new List<CommandChannelEndpoint>();
                            index.Add(channel, owners);
                        }

                        owners.Add(new CommandChannelEndpoint(component, field));
                        continue;
                    }

                    string role = attribute.IsReceiver ? "receiver" : "sender";
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"{component.GetType().Name}.{field.Name} is a required {role} " +
                        "but has no command-channel asset. Assign the same asset used by " +
                        $"its {(attribute.IsReceiver ? "senders" : "persistent receiver")}.",
                        component,
                        GetContextPath(component),
                        MissingCommandChannelCode));
                }
            }

            foreach (KeyValuePair<CrossSceneCommandChannel, List<CommandChannelEndpoint>> pair
                         in callers.OrderBy(
                             item => AssetDatabase.GetAssetPath(item.Key),
                             StringComparer.Ordinal))
            {
                receivers.TryGetValue(
                    pair.Key,
                    out List<CommandChannelEndpoint> channelReceivers);
                int receiverCount = channelReceivers?.Count ?? 0;
                if (receiverCount == 0)
                {
                    CommandChannelEndpoint caller = pair.Value[0];
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"{caller.Component.GetType().Name}.{caller.Field.Name} sends " +
                        $"through '{pair.Key.name}', but no active persistent receiver " +
                        "is assigned to the same asset. Add or enable exactly one " +
                        "receiver marked RequiredCommandChannel(isReceiver: true).",
                        pair.Key,
                        AssetDatabase.GetAssetPath(pair.Key),
                        MissingChannelReceiverCode));
                }
                else if (receiverCount > 1)
                {
                    string receiverNames = string.Join(
                        ", ",
                        channelReceivers.Select(endpoint =>
                            $"{endpoint.Component.GetType().Name}.{endpoint.Field.Name}"));
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"'{pair.Key.name}' has {receiverCount} active persistent receivers " +
                        $"({receiverNames}); exactly one receiver may use this asset.",
                        pair.Key,
                        AssetDatabase.GetAssetPath(pair.Key),
                        DuplicateChannelReceiverCode));
                }
            }
        }

        private static void ValidateReadableConfiguration(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            Component[] values = (components ?? Array.Empty<Component>())
                .Where(component => component != null)
                .ToArray();
            ReadableOverlayHandler[] overlays = values
                .OfType<ReadableOverlayHandler>()
                .ToArray();
            var activeSubscribersByChannel =
                new Dictionary<InteractionUIChannel, List<ReadableOverlayHandler>>();
            var usableByChannel =
                new Dictionary<InteractionUIChannel, List<ReadableOverlayHandler>>();

            foreach (ReadableOverlayHandler overlay in overlays)
            {
                InteractionUIChannel channel =
                    GetObjectReference<InteractionUIChannel>(overlay, "channel");
                CanvasGroup canvasGroup =
                    GetObjectReference<CanvasGroup>(overlay, "canvasGroup");
                if (canvasGroup == null)
                {
                    canvasGroup = overlay.GetComponent<CanvasGroup>();
                }
                UnityEngine.Object bodyText =
                    GetObjectReference<UnityEngine.Object>(overlay, "bodyText");
                bool hasUsableCanvasGroup =
                    canvasGroup != null && canvasGroup.isActiveAndEnabled;
                bool hasUsableBodyText =
                    bodyText != null &&
                    (bodyText is not Behaviour bodyTextBehaviour ||
                     bodyTextBehaviour.isActiveAndEnabled);

                if (channel == null)
                {
                    AddReadableConfigurationIssue(
                        overlay,
                        "ReadableOverlayHandler.channel has no Interaction UI Channel.",
                        issues);
                }

                if (canvasGroup == null)
                {
                    AddReadableConfigurationIssue(
                        overlay,
                        "ReadableOverlayHandler needs an assigned CanvasGroup or one on the same GameObject.",
                        issues);
                }
                else if (!canvasGroup.isActiveAndEnabled)
                {
                    AddReadableConfigurationIssue(
                        overlay,
                        "ReadableOverlayHandler uses an inactive CanvasGroup, so readable content cannot be shown.",
                        issues);
                }

                if (bodyText == null)
                {
                    AddReadableConfigurationIssue(
                        overlay,
                        "ReadableOverlayHandler.bodyText is required to render readable content.",
                        issues);
                }
                else if (!hasUsableBodyText)
                {
                    AddReadableConfigurationIssue(
                        overlay,
                        "ReadableOverlayHandler.bodyText is inactive and cannot render readable content.",
                        issues);
                }

                if (!overlay.isActiveAndEnabled || channel == null)
                {
                    continue;
                }

                AddReadableReceiver(activeSubscribersByChannel, channel, overlay);
                if (!hasUsableCanvasGroup || !hasUsableBodyText)
                {
                    continue;
                }

                AddReadableReceiver(usableByChannel, channel, overlay);
            }

            foreach (KeyValuePair<InteractionUIChannel, List<ReadableOverlayHandler>> pair
                         in activeSubscribersByChannel.Where(item => item.Value.Count > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Readable UI",
                    $"'{pair.Key.name}' has {pair.Value.Count} active " +
                    $"{nameof(ReadableOverlayHandler)} subscribers. Keep exactly one " +
                    "persistent readable overlay for this channel.",
                    pair.Key,
                    AssetDatabase.GetAssetPath(pair.Key),
                    DuplicateReadableReceiverCode));
            }

            foreach (ReadableInteractionTrigger trigger in values
                         .OfType<ReadableInteractionTrigger>())
            {
                InteractionUIChannel channel =
                    GetObjectReference<InteractionUIChannel>(trigger, "channel");
                ReadableContentDefinition content =
                    GetObjectReference<ReadableContentDefinition>(trigger, "content");

                if (channel == null)
                {
                    AddReadableConfigurationIssue(
                        trigger,
                        "ReadableInteractionTrigger.channel is required to dispatch readable content.",
                        issues);
                }

                if (content == null)
                {
                    AddReadableConfigurationIssue(
                        trigger,
                        "ReadableInteractionTrigger.content is required.",
                        issues);
                }

                if (channel == null)
                {
                    continue;
                }

                usableByChannel.TryGetValue(
                    channel,
                    out List<ReadableOverlayHandler> receivers);
                if ((receivers?.Count ?? 0) == 0)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Readable UI",
                        $"{nameof(ReadableInteractionTrigger)} on '{trigger.name}' uses " +
                        $"'{channel.name}', but no active, configured " +
                        $"{nameof(ReadableOverlayHandler)} uses the same asset. " +
                        $"{nameof(InteractionUIChannelListener)} handles prompts, messages, " +
                        "and progress only; it is not a readable receiver.",
                        trigger,
                        GetContextPath(trigger),
                        MissingReadableReceiverCode));
                }
            }
        }

        private static void AddReadableReceiver(
            IDictionary<InteractionUIChannel, List<ReadableOverlayHandler>> receiversByChannel,
            InteractionUIChannel channel,
            ReadableOverlayHandler overlay)
        {
            if (!receiversByChannel.TryGetValue(
                    channel,
                    out List<ReadableOverlayHandler> receivers))
            {
                receivers = new List<ReadableOverlayHandler>();
                receiversByChannel.Add(channel, receivers);
            }

            receivers.Add(overlay);
        }

        private static void AddReadableConfigurationIssue(
            Component context,
            string message,
            ICollection<ValidationIssue> issues)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Readable UI",
                message,
                context,
                GetContextPath(context),
                InvalidReadableConfigurationCode));
        }

        private static void ValidateFadeConfiguration(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            Component[] values = (components ?? Array.Empty<Component>())
                .Where(component => component != null)
                .ToArray();
            ScreenFader[] faders = values.OfType<ScreenFader>().ToArray();
            ScreenFadeChannelHandler[] handlers = values
                .OfType<ScreenFadeChannelHandler>()
                .ToArray();
            var activeSubscribers =
                new Dictionary<ScreenFadeChannel, List<ScreenFadeChannelHandler>>();
            var usableHandlers =
                new Dictionary<ScreenFadeChannel, List<ScreenFadeChannelHandler>>();

            foreach (ScreenFadeChannelHandler handler in handlers)
            {
                ScreenFadeChannel channel =
                    GetObjectReference<ScreenFadeChannel>(handler, "channel");
                ScreenFader fader =
                    GetObjectReference<ScreenFader>(handler, "screenFader");
                if (fader == null)
                {
                    fader = handler.GetComponent<ScreenFader>();
                }
                CanvasGroup faderCanvasGroup = GetFaderCanvasGroup(fader);

                if (handler.isActiveAndEnabled && channel != null)
                {
                    AddFadeHandler(activeSubscribers, channel, handler);
                }

                if (channel == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Transition Fade",
                        $"{nameof(ScreenFadeChannelHandler)} on '{handler.name}' has no " +
                        "channel. Assign the Screen Fade Channel used by its " +
                        $"{nameof(SceneFlowManager)}.",
                        handler,
                        GetContextPath(handler),
                        InvalidFadeConfigurationCode));
                }

                if (fader == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Transition Fade",
                        $"{nameof(ScreenFadeChannelHandler)} on '{handler.name}' has no " +
                        $"{nameof(ScreenFader)} to execute requests.",
                        handler,
                        GetContextPath(handler),
                        InvalidFadeConfigurationCode));
                }
                else if (faderCanvasGroup == null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Transition Fade",
                        $"{nameof(ScreenFadeChannelHandler)} on '{handler.name}' uses a " +
                        $"{nameof(ScreenFader)} with no CanvasGroup. Assign one on the fader " +
                        "or add one to the fader GameObject.",
                        handler,
                        GetContextPath(handler),
                        InvalidFadeConfigurationCode));
                }
                else if (!faderCanvasGroup.isActiveAndEnabled)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Transition Fade",
                        $"{nameof(ScreenFadeChannelHandler)} on '{handler.name}' uses an " +
                        "inactive CanvasGroup, so fade requests cannot produce a visible overlay.",
                        handler,
                        GetContextPath(handler),
                        InvalidFadeConfigurationCode));
                }

                if (!handler.isActiveAndEnabled ||
                    channel == null ||
                    fader == null ||
                    faderCanvasGroup == null ||
                    !faderCanvasGroup.isActiveAndEnabled)
                {
                    continue;
                }

                AddFadeHandler(usableHandlers, channel, handler);
            }

            foreach (KeyValuePair<ScreenFadeChannel, List<ScreenFadeChannelHandler>> pair
                         in activeSubscribers.Where(item => item.Value.Count > 1))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Transition Fade",
                    $"'{pair.Key.name}' has {pair.Value.Count} active " +
                    $"{nameof(ScreenFadeChannelHandler)} receivers. Keep exactly one.",
                    pair.Key,
                    AssetDatabase.GetAssetPath(pair.Key),
                    DuplicateFadeReceiverCode));
            }

            foreach (SceneFlowManager manager in values
                         .OfType<SceneFlowManager>()
                         .Where(value => value.isActiveAndEnabled))
            {
                SerializedObject serializedManager = new(manager);
                SerializedProperty fadeEnabled =
                    serializedManager.FindProperty("fadeDuringTransitions");
                if (fadeEnabled == null || !fadeEnabled.boolValue)
                {
                    continue;
                }

                ScreenFadeChannel channel =
                    serializedManager.FindProperty("screenFadeChannel")
                        ?.objectReferenceValue as ScreenFadeChannel;
                ScreenFader assignedFader =
                    serializedManager.FindProperty("screenFader")
                        ?.objectReferenceValue as ScreenFader;

                if (channel != null)
                {
                    activeSubscribers.TryGetValue(
                        channel,
                        out List<ScreenFadeChannelHandler> subscribedHandlers);
                    int subscriberCount = subscribedHandlers?.Count ?? 0;
                    if (subscriberCount > 0)
                    {
                        usableHandlers.TryGetValue(
                            channel,
                            out List<ScreenFadeChannelHandler> workingHandlers);
                        if ((workingHandlers?.Count ?? 0) == 0)
                        {
                            AddMissingFadePathIssue(
                                manager,
                                $"{nameof(SceneFlowManager)} requests '{channel.name}', and " +
                                "an active handler is subscribed, but none has an active " +
                                "CanvasGroup. Runtime selects the channel before the direct " +
                                "fallback; repair or disable the broken handler.",
                                issues);
                        }

                        continue;
                    }
                }

                ScreenFader[] discoverableFaders = assignedFader == null
                    ? faders.Where(IsRuntimeDiscoverableFader).ToArray()
                    : Array.Empty<ScreenFader>();
                bool directFallbackAmbiguous =
                    assignedFader == null && discoverableFaders.Length > 1;
                if (directFallbackAmbiguous)
                {
                    AddMissingFadePathIssue(
                        manager,
                        $"{nameof(SceneFlowManager)} can discover {discoverableFaders.Length} " +
                        "active ScreenFaders, so the direct fallback is ambiguous. Assign the " +
                        "intended ScreenFader explicitly.",
                        issues);
                    continue;
                }

                bool hasDirectFallback = assignedFader != null
                    ? IsAssignedFaderUsable(assignedFader)
                    : discoverableFaders.Length == 1 &&
                      IsAssignedFaderUsable(discoverableFaders[0]);

                if (channel == null)
                {
                    if (!hasDirectFallback)
                    {
                        AddMissingFadePathIssue(
                            manager,
                            "Transition fades are enabled, but no active direct ScreenFader " +
                            "is assigned or discoverable. Assign a direct fader, configure a " +
                            "channel and handler, or disable transition fades.",
                            issues);
                    }

                    continue;
                }

                if (hasDirectFallback)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        "Transition Fade",
                        $"{nameof(SceneFlowManager)} requests '{channel.name}', but no active " +
                        $"{nameof(ScreenFadeChannelHandler)} uses the same asset. Runtime will " +
                        "use the supported direct-fader fallback. Assign the channel to one " +
                        "handler to restore the primary boundary, or clear the channel to use " +
                        "the fallback intentionally.",
                        manager,
                        GetContextPath(manager),
                        FadeChannelFallbackCode));
                }
                else
                {
                    AddMissingFadePathIssue(
                        manager,
                        $"{nameof(SceneFlowManager)} requests '{channel.name}', but no active " +
                        $"{nameof(ScreenFadeChannelHandler)} uses the same asset and no direct " +
                        "ScreenFader fallback is available.",
                        issues);
                }
            }
        }

        private static void AddFadeHandler(
            IDictionary<ScreenFadeChannel, List<ScreenFadeChannelHandler>> handlersByChannel,
            ScreenFadeChannel channel,
            ScreenFadeChannelHandler handler)
        {
            if (!handlersByChannel.TryGetValue(
                    channel,
                    out List<ScreenFadeChannelHandler> handlers))
            {
                handlers = new List<ScreenFadeChannelHandler>();
                handlersByChannel.Add(channel, handlers);
            }

            handlers.Add(handler);
        }

        private static bool IsAssignedFaderUsable(ScreenFader fader)
        {
            CanvasGroup canvasGroup = GetFaderCanvasGroup(fader);
            return fader != null &&
                   canvasGroup != null &&
                   canvasGroup.isActiveAndEnabled;
        }

        private static bool IsRuntimeDiscoverableFader(ScreenFader fader) =>
            fader != null &&
            fader.gameObject.activeInHierarchy;

        private static CanvasGroup GetFaderCanvasGroup(ScreenFader fader)
        {
            if (fader == null)
            {
                return null;
            }

            CanvasGroup assigned =
                GetObjectReference<CanvasGroup>(fader, "canvasGroup");
            return assigned != null
                ? assigned
                : fader.GetComponent<CanvasGroup>();
        }

        private static void AddMissingFadePathIssue(
            SceneFlowManager manager,
            string message,
            ICollection<ValidationIssue> issues)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "Transition Fade",
                message,
                manager,
                GetContextPath(manager),
                InvalidFadeConfigurationCode));
        }

        private static T GetObjectReference<T>(
            Component component,
            string propertyName)
            where T : UnityEngine.Object
        {
            return component == null
                ? null
                : new SerializedObject(component)
                    .FindProperty(propertyName)
                    ?.objectReferenceValue as T;
        }

        private static void ValidatePackageManifestText(
            string manifest,
            string assetPath,
            ICollection<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(manifest))
            {
                return;
            }

            if (WindowsAbsoluteFileDependency.IsMatch(manifest) ||
                manifest.Contains("\"file:/", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Packages",
                    "Package manifest contains a machine-specific absolute file dependency. Use a repository-relative file dependency.",
                    null,
                    assetPath,
                    AbsoluteLocalPackageCode));
            }
        }

        private static void ValidateCrossSceneManagerReferences(
            IEnumerable<Component> components,
            ICollection<ValidationIssue> issues)
        {
            foreach (Component source in components ?? Array.Empty<Component>())
            {
                if (source == null || !source.gameObject.scene.IsValid())
                {
                    continue;
                }

                SerializedObject serializedSource = new(source);
                SerializedProperty property = serializedSource.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.propertyPath == "m_Script" ||
                        property.objectReferenceValue is not Component target ||
                        !target.gameObject.scene.IsValid() ||
                        target.gameObject.scene == source.gameObject.scene ||
                        !IsToolkitManager(target.GetType()))
                    {
                        continue;
                    }

                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Scene Boundaries",
                        $"{source.GetType().Name}.{property.propertyPath} directly references " +
                        $"{target.GetType().Name} in {target.gameObject.scene.name}. Use a command channel or local event boundary.",
                        source,
                        GetContextPath(source),
                        CrossSceneManagerReferenceCode));
                }
            }
        }

        private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
        {
            for (Type current = type;
                 current != null && current != typeof(MonoBehaviour);
                 current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    yield return field;
                }
            }
        }

        private static bool IsToolkitManager(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(ToolkitSingleton<>))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetContextPath(Component component)
        {
            if (component != null && component.gameObject.scene.IsValid())
            {
                return component.gameObject.scene.path ?? string.Empty;
            }

            return AssetDatabase.GetAssetPath(component);
        }

        private static bool IsDevelopmentOnlyScene(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/debug/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("/Debug.unity", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/DevelopmentPlayMode.unity", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class CommandChannelEndpoint
        {
            public CommandChannelEndpoint(Component component, FieldInfo field)
            {
                Component = component;
                Field = field;
            }

            public Component Component { get; }
            public FieldInfo Field { get; }
        }
    }
}
