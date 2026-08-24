using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using QuietStatic.Toolkit.Core;
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

        private static readonly Regex WindowsAbsoluteFileDependency = new(
            "\\\"file:[A-Za-z]:[/\\\\]",
            RegexOptions.CultureInvariant);

        /// <summary>Scans architecture rules that apply to currently loaded scenes.</summary>
        public static IReadOnlyList<ValidationIssue> ScanOpenScenes(
            IEnumerable<Component> components)
        {
            var issues = new List<ValidationIssue>();
            ValidateRequiredCommandChannels(components, issues);
            ValidateCrossSceneManagerReferences(components, issues);
            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>Scans project files and build configuration without modifying them.</summary>
        public static IReadOnlyList<ValidationIssue> ScanProjectConfiguration()
        {
            var issues = new List<ValidationIssue>();
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
            var callers = new Dictionary<CrossSceneCommandChannel, List<Component>>();
            var receivers = new Dictionary<CrossSceneCommandChannel, List<Component>>();

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
                        Dictionary<CrossSceneCommandChannel, List<Component>> index =
                            attribute.IsReceiver ? receivers : callers;
                        if (!index.TryGetValue(channel, out List<Component> owners))
                        {
                            owners = new List<Component>();
                            index.Add(channel, owners);
                        }

                        owners.Add(component);
                        continue;
                    }

                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"{component.GetType().Name}.{field.Name} requires a command channel.",
                        component,
                        GetContextPath(component),
                        MissingCommandChannelCode));
                }
            }

            foreach (KeyValuePair<CrossSceneCommandChannel, List<Component>> pair
                         in callers.OrderBy(
                             item => AssetDatabase.GetAssetPath(item.Key),
                             StringComparer.Ordinal))
            {
                receivers.TryGetValue(pair.Key, out List<Component> channelReceivers);
                int receiverCount = channelReceivers?.Count ?? 0;
                if (receiverCount == 0)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"{pair.Key.name} is used by scene content but has no persistent receiver.",
                        pair.Key,
                        AssetDatabase.GetAssetPath(pair.Key),
                        MissingChannelReceiverCode));
                }
                else if (receiverCount > 1)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Command Channels",
                        $"{pair.Key.name} has {receiverCount} persistent receivers; exactly one is required.",
                        pair.Key,
                        AssetDatabase.GetAssetPath(pair.Key),
                        DuplicateChannelReceiverCode));
                }
            }
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
    }
}
