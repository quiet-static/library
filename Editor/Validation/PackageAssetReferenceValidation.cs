using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>
    /// Validates serialized package assets without loading them, so missing GUID references
    /// remain observable even when Unity cannot construct an object reference for them.
    /// </summary>
    public static class PackageAssetReferenceValidation
    {
        public const string UnresolvedReferenceCode = "QS1008";
        public const string ConsumerReferenceCode = "QS1009";
        public const string UndeclaredPackageReferenceCode = "QS1010";
        public const string UnverifiableSerializationCode = "QS1011";

        private const string PackageName = "com.quietstatic.core";
        private const string PackageAssetRoot = "Packages/" + PackageName;

        private static readonly Regex DocumentHeaderPattern = new(
            @"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)",
            RegexOptions.CultureInvariant);

        private static readonly Regex ObjectReferencePattern = new(
            @"\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-fA-F]{32})[^}\r\n]*\}",
            RegexOptions.CultureInvariant);

        private static readonly Regex FileIdPattern = new(
            @"\bfileID:\s*(?<fileId>-?\d+)",
            RegexOptions.CultureInvariant);

        private static readonly Regex InlineGuidPattern = new(
            @"\bguid:\s*(?<guid>[0-9a-fA-F]{32})",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> BuiltInGuids = new(
            new[]
            {
                "0000000000000000e000000000000000",
                "0000000000000000f000000000000000",
            },
            StringComparer.OrdinalIgnoreCase);

        // Unity's uGUI package migration table maps its bundled LiberationSans SDF resource
        // to this virtual GUID. It is valid only while uGUI remains a direct dependency.
        private static readonly IReadOnlyDictionary<string, string> DependencyVirtualGuids =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["8f586378b4e144a9851e7b34d9b748ee"] = "com.unity.ugui",
            };

        /// <summary>Scans all text-serialized prefabs, assets, and scenes owned by this package.</summary>
        public static IReadOnlyList<ValidationIssue> ScanPackageAssets()
        {
            PackageInfo package = PackageInfo.FindForAssetPath(
                PackageAssetRoot + "/package.json");
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath) ||
                !Directory.Exists(package.resolvedPath))
            {
                return new[]
                {
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        "Package References",
                        $"Could not locate the resolved {PackageName} package root.",
                        null,
                        PackageAssetRoot,
                        UnresolvedReferenceCode),
                };
            }

            var declaredDependencies = new HashSet<string>(
                package.dependencies?.Select(dependency => dependency.name) ??
                Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            return ScanPhysicalPackageAssets(
                package.resolvedPath,
                PackageName,
                declaredDependencies,
                AssetDatabase.GUIDToAssetPath);
        }

        /// <summary>
        /// Scans a resolved package directory. The physical metadata index includes folders
        /// hidden from Unity, such as Samples~, while the fallback resolves dependencies and
        /// consumer assets known to the current project.
        /// </summary>
        internal static IReadOnlyList<ValidationIssue> ScanPhysicalPackageAssets(
            string physicalRoot,
            string ownPackageName,
            IEnumerable<string> declaredDependencies,
            Func<string, string> fallbackResolveGuid = null)
        {
            if (string.IsNullOrWhiteSpace(physicalRoot) ||
                !Directory.Exists(physicalRoot))
            {
                string packageRoot = "Packages/" + ownPackageName;
                return new[]
                {
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        "Package References",
                        $"Could not locate the resolved {ownPackageName} package root.",
                        null,
                        packageRoot,
                        UnresolvedReferenceCode),
                };
            }

            string normalizedPhysicalRoot = Path.GetFullPath(physicalRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string packageAssetRoot = "Packages/" + ownPackageName;
            var guidResolver = new PhysicalPackageGuidResolver(
                normalizedPhysicalRoot,
                packageAssetRoot,
                fallbackResolveGuid);
            var ownerResolver = new AssetOwnerResolver(
                guidResolver.ResolveAssetPath,
                guidResolver.ResolvePhysicalPath);
            var dependencies = new HashSet<string>(
                declaredDependencies ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            var issues = new List<ValidationIssue>();

            foreach (string physicalPath in Directory
                         .EnumerateFiles(normalizedPhysicalRoot, "*", SearchOption.AllDirectories)
                         .Where(IsSerializedAssetPath)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                string assetPath = ToPackageAssetPath(
                    normalizedPhysicalRoot,
                    packageAssetRoot,
                    physicalPath);
                string yaml = File.ReadAllText(physicalPath);
                issues.AddRange(ValidateYamlText(
                    yaml,
                    assetPath,
                    guidResolver.ResolveAssetPath,
                    ownPackageName,
                    dependencies,
                    ownerResolver.Resolve));
            }

            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>
        /// Validates one Unity YAML document. Kept internal so focused tests can exercise
        /// classification and context reporting without a consumer project checkout.
        /// </summary>
        internal static IReadOnlyList<ValidationIssue> ValidateYamlText(
            string yaml,
            string assetPath,
            Func<string, string> resolveGuid,
            string ownPackageName,
            IEnumerable<string> declaredDependencies,
            Func<string, long, string> resolveOwner = null)
        {
            if (string.IsNullOrEmpty(yaml) ||
                yaml.IndexOf("--- !u!", StringComparison.Ordinal) < 0)
            {
                return new[]
                {
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        "Package References",
                        "Serialized references cannot be verified because the asset is not Unity text YAML. " +
                        "Use Force Text serialization or explicitly exclude the asset with a documented policy.",
                        null,
                        assetPath,
                        UnverifiableSerializationCode),
                };
            }

            string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
            IReadOnlyList<YamlDocument> documents = ParseDocuments(lines);
            var dependencies = new HashSet<string>(
                declaredDependencies ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            string ownRoot = "Packages/" + ownPackageName;
            var occurrences = new List<ReferenceOccurrence>();

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                MatchCollection matches = ObjectReferencePattern.Matches(lines[lineIndex]);
                foreach (Match match in matches)
                {
                    string guid = match.Groups["guid"].Value.ToLowerInvariant();
                    ReferenceClassification classification = Classify(
                        guid,
                        resolveGuid,
                        ownRoot,
                        dependencies);
                    if (classification == null)
                    {
                        continue;
                    }

                    YamlDocument document = FindDocument(documents, lineIndex);
                    string field = DescribeField(lines, document?.StartLine ?? 0, lineIndex);
                    string owner = DescribeOwner(
                        lines,
                        document,
                        lineIndex,
                        resolveGuid,
                        resolveOwner);
                    occurrences.Add(new ReferenceOccurrence(
                        guid,
                        classification,
                        owner,
                        field,
                        lineIndex + 1));
                }
            }

            return occurrences
                .GroupBy(
                    occurrence => new
                    {
                        occurrence.Guid,
                        occurrence.Classification.Code,
                        occurrence.Classification.Reason,
                    })
                .Select(group =>
                {
                    string contexts = string.Join(
                        "; ",
                        group.Select(occurrence =>
                                $"{occurrence.Owner}.{occurrence.Field} (line {occurrence.Line})")
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(value => value, StringComparer.Ordinal));
                    ReferenceOccurrence first = group.First();
                    return new ValidationIssue(
                        ValidationSeverity.Error,
                        "Package References",
                        $"{contexts}: GUID {first.Guid} {first.Classification.Reason}.",
                        null,
                        assetPath,
                        first.Classification.Code);
                })
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray();
        }

        private static ReferenceClassification Classify(
            string guid,
            Func<string, string> resolveGuid,
            string ownPackageRoot,
            ISet<string> declaredDependencies)
        {
            if (guid.All(character => character == '0') || BuiltInGuids.Contains(guid))
            {
                return null;
            }

            if (DependencyVirtualGuids.TryGetValue(guid, out string requiredDependency) &&
                declaredDependencies.Contains(requiredDependency))
            {
                return null;
            }

            string resolvedPath = NormalizeAssetPath(resolveGuid?.Invoke(guid));
            if (string.IsNullOrEmpty(resolvedPath))
            {
                return new ReferenceClassification(
                    UnresolvedReferenceCode,
                    "does not resolve to this package, a declared dependency, or an approved Unity built-in resource");
            }

            if (IsPathWithin(resolvedPath, ownPackageRoot))
            {
                return null;
            }

            if (resolvedPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                int packageEnd = resolvedPath.IndexOf('/', "Packages/".Length);
                string packageName = packageEnd < 0
                    ? resolvedPath.Substring("Packages/".Length)
                    : resolvedPath.Substring("Packages/".Length, packageEnd - "Packages/".Length);
                if (declaredDependencies.Contains(packageName))
                {
                    return null;
                }

                return new ReferenceClassification(
                    UndeclaredPackageReferenceCode,
                    $"resolves to undeclared package asset '{resolvedPath}'");
            }

            if (resolvedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return new ReferenceClassification(
                    ConsumerReferenceCode,
                    $"resolves to consumer-owned project asset '{resolvedPath}'");
            }

            return new ReferenceClassification(
                UnresolvedReferenceCode,
                $"resolves outside this package and its declared dependencies at '{resolvedPath}'");
        }

        private static IReadOnlyList<YamlDocument> ParseDocuments(string[] lines)
        {
            var documents = new List<YamlDocument>();
            YamlDocument current = null;
            for (int index = 0; index < lines.Length; index++)
            {
                Match header = DocumentHeaderPattern.Match(lines[index]);
                if (header.Success)
                {
                    if (current != null)
                    {
                        current.EndLine = index - 1;
                    }

                    current = new YamlDocument(
                        index,
                        int.Parse(header.Groups["classId"].Value),
                        long.Parse(header.Groups["fileId"].Value));
                    documents.Add(current);
                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                string line = lines[index];
                string trimmed = line.Trim();
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && trimmed.EndsWith(":"))
                {
                    current.TypeName = trimmed.TrimEnd(':');
                }
                else if (trimmed.StartsWith("m_GameObject:", StringComparison.Ordinal))
                {
                    current.GameObjectFileId = ExtractFileId(trimmed);
                }
                else if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                {
                    current.Name = Unquote(ValueAfterColon(trimmed));
                }
                else if (trimmed.StartsWith("m_EditorClassIdentifier:", StringComparison.Ordinal))
                {
                    current.EditorClassIdentifier = ValueAfterColon(trimmed);
                }
                else if (trimmed.StartsWith("m_Script:", StringComparison.Ordinal))
                {
                    Match guid = InlineGuidPattern.Match(trimmed);
                    if (guid.Success)
                    {
                        current.ScriptGuid = guid.Groups["guid"].Value;
                    }
                }
            }

            if (current != null)
            {
                current.EndLine = lines.Length - 1;
            }

            return documents;
        }

        private static YamlDocument FindDocument(
            IReadOnlyList<YamlDocument> documents,
            int lineIndex)
        {
            for (int index = documents.Count - 1; index >= 0; index--)
            {
                YamlDocument document = documents[index];
                if (lineIndex >= document.StartLine && lineIndex <= document.EndLine)
                {
                    return document;
                }
            }

            return null;
        }

        private static string DescribeOwner(
            string[] lines,
            YamlDocument document,
            int lineIndex,
            Func<string, string> resolveGuid,
            Func<string, long, string> resolveOwner)
        {
            if (lines[lineIndex].TrimStart()
                .StartsWith("objectReference:", StringComparison.Ordinal))
            {
                for (int index = lineIndex - 1;
                     index >= (document?.StartLine ?? 0);
                     index--)
                {
                    string trimmed = lines[index].TrimStart();
                    if (!trimmed.StartsWith("- target:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Match guid = InlineGuidPattern.Match(trimmed);
                    long fileId = ExtractFileId(trimmed);
                    if (guid.Success)
                    {
                        string resolved = resolveOwner?.Invoke(guid.Groups["guid"].Value, fileId);
                        return string.IsNullOrWhiteSpace(resolved)
                            ? $"Prefab target {fileId}"
                            : resolved;
                    }

                    break;
                }
            }

            return DescribeDocument(document, lines, resolveGuid);
        }

        private static string DescribeDocument(
            YamlDocument document,
            string[] lines,
            Func<string, string> resolveGuid)
        {
            if (document == null)
            {
                return "Serialized object";
            }

            if (string.Equals(document.TypeName, "GameObject", StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(document.Name)
                    ? $"GameObject {document.FileId}"
                    : document.Name;
            }

            string gameObjectName = null;
            if (document.GameObjectFileId != 0)
            {
                YamlDocument gameObject = ParseDocuments(lines)
                    .FirstOrDefault(candidate =>
                        candidate.FileId == document.GameObjectFileId &&
                        string.Equals(candidate.TypeName, "GameObject", StringComparison.Ordinal));
                gameObjectName = gameObject?.Name;
            }

            string componentName = ShortTypeName(document.EditorClassIdentifier);
            if (string.IsNullOrWhiteSpace(componentName) &&
                !string.IsNullOrWhiteSpace(document.ScriptGuid))
            {
                string scriptPath = resolveGuid?.Invoke(document.ScriptGuid);
                componentName = string.IsNullOrWhiteSpace(scriptPath)
                    ? "MonoBehaviour"
                    : Path.GetFileNameWithoutExtension(scriptPath);
            }

            if (string.IsNullOrWhiteSpace(componentName))
            {
                componentName = string.IsNullOrWhiteSpace(document.TypeName)
                    ? $"Object {document.FileId}"
                    : document.TypeName;
            }

            return string.IsNullOrWhiteSpace(gameObjectName)
                ? componentName
                : gameObjectName + "/" + componentName;
        }

        private static string DescribeField(string[] lines, int documentStart, int lineIndex)
        {
            string trimmed = lines[lineIndex].Trim();
            if (trimmed.StartsWith("objectReference:", StringComparison.Ordinal))
            {
                for (int index = lineIndex - 1; index >= documentStart; index--)
                {
                    string candidate = lines[index].Trim();
                    if (candidate.StartsWith("propertyPath:", StringComparison.Ordinal))
                    {
                        return ValueAfterColon(candidate);
                    }

                    if (candidate.StartsWith("- target:", StringComparison.Ordinal))
                    {
                        break;
                    }
                }
            }

            Match key = Regex.Match(trimmed, @"^(?:-\s+)?(?<key>[^:{]+):");
            if (key.Success && !trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                return key.Groups["key"].Value.Trim();
            }

            if (trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                int currentIndent = LeadingWhitespace(lines[lineIndex]);
                for (int index = lineIndex - 1; index >= documentStart; index--)
                {
                    string candidate = lines[index].Trim();
                    if (!candidate.EndsWith(":", StringComparison.Ordinal) ||
                        candidate.StartsWith("-", StringComparison.Ordinal) ||
                        LeadingWhitespace(lines[index]) > currentIndent)
                    {
                        continue;
                    }

                    string parent = candidate.TrimEnd(':').Trim();
                    int itemIndex = 0;
                    for (int item = index + 1; item < lineIndex; item++)
                    {
                        if (LeadingWhitespace(lines[item]) == currentIndent &&
                            lines[item].TrimStart().StartsWith("- ", StringComparison.Ordinal))
                        {
                            itemIndex++;
                        }
                    }

                    return $"{parent}[{itemIndex}]";
                }
            }

            return "serialized reference";
        }

        private static string ShortTypeName(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return string.Empty;
            }

            string value = identifier.Trim();
            int assemblySeparator = value.LastIndexOf("::", StringComparison.Ordinal);
            if (assemblySeparator >= 0)
            {
                value = value.Substring(assemblySeparator + 2);
            }

            int namespaceSeparator = value.LastIndexOf('.');
            return namespaceSeparator >= 0
                ? value.Substring(namespaceSeparator + 1)
                : value;
        }

        private static long ExtractFileId(string value)
        {
            Match match = FileIdPattern.Match(value ?? string.Empty);
            return match.Success && long.TryParse(match.Groups["fileId"].Value, out long fileId)
                ? fileId
                : 0;
        }

        private static int LeadingWhitespace(string value)
        {
            int count = 0;
            while (count < value.Length && char.IsWhiteSpace(value[count]))
            {
                count++;
            }

            return count;
        }

        private static string ValueAfterColon(string value)
        {
            int separator = value.IndexOf(':');
            return separator < 0 ? string.Empty : value.Substring(separator + 1).Trim();
        }

        private static string Unquote(string value) =>
            (value ?? string.Empty).Trim().Trim('\'', '"');

        private static bool IsSerializedAssetPath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToPackageAssetPath(
            string physicalRoot,
            string packageAssetRoot,
            string physicalPath)
        {
            string relativePath = Path.GetFullPath(physicalPath)
                .Substring(physicalRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            return packageAssetRoot.TrimEnd('/') + "/" + relativePath;
        }

        private static bool IsPathWithin(string path, string root) =>
            string.Equals(path, root, StringComparison.Ordinal) ||
            path.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal);

        private static string NormalizeAssetPath(string path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim();

        private sealed class ReferenceClassification
        {
            public ReferenceClassification(string code, string reason)
            {
                Code = code;
                Reason = reason;
            }

            public string Code { get; }
            public string Reason { get; }
        }

        private sealed class ReferenceOccurrence
        {
            public ReferenceOccurrence(
                string guid,
                ReferenceClassification classification,
                string owner,
                string field,
                int line)
            {
                Guid = guid;
                Classification = classification;
                Owner = owner;
                Field = field;
                Line = line;
            }

            public string Guid { get; }
            public ReferenceClassification Classification { get; }
            public string Owner { get; }
            public string Field { get; }
            public int Line { get; }
        }

        private sealed class YamlDocument
        {
            public YamlDocument(int startLine, int classId, long fileId)
            {
                StartLine = startLine;
                ClassId = classId;
                FileId = fileId;
            }

            public int StartLine { get; }
            public int EndLine { get; set; }
            public int ClassId { get; }
            public long FileId { get; }
            public string TypeName { get; set; }
            public long GameObjectFileId { get; set; }
            public string Name { get; set; }
            public string EditorClassIdentifier { get; set; }
            public string ScriptGuid { get; set; }
        }

        private sealed class PhysicalPackageGuidResolver
        {
            private static readonly Regex MetaGuidPattern = new(
                @"^guid:\s*(?<guid>[0-9a-fA-F]{32})\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline);

            private readonly string physicalRoot;
            private readonly string packageAssetRoot;
            private readonly Func<string, string> fallbackResolveGuid;
            private readonly Dictionary<string, PhysicalAssetRecord> assetsByGuid =
                new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> ambiguousGuids =
                new(StringComparer.OrdinalIgnoreCase);

            public PhysicalPackageGuidResolver(
                string physicalRoot,
                string packageAssetRoot,
                Func<string, string> fallbackResolveGuid)
            {
                this.physicalRoot = physicalRoot;
                this.packageAssetRoot = packageAssetRoot;
                this.fallbackResolveGuid = fallbackResolveGuid;
                IndexPhysicalMetadata();
            }

            public string ResolveAssetPath(string guid)
            {
                if (string.IsNullOrWhiteSpace(guid) || ambiguousGuids.Contains(guid))
                {
                    return string.Empty;
                }

                if (assetsByGuid.TryGetValue(guid, out PhysicalAssetRecord asset))
                {
                    return asset.AssetPath;
                }

                return NormalizeAssetPath(fallbackResolveGuid?.Invoke(guid));
            }

            public string ResolvePhysicalPath(string guid)
            {
                if (string.IsNullOrWhiteSpace(guid) || ambiguousGuids.Contains(guid))
                {
                    return string.Empty;
                }

                return assetsByGuid.TryGetValue(guid, out PhysicalAssetRecord asset)
                    ? asset.PhysicalPath
                    : string.Empty;
            }

            private void IndexPhysicalMetadata()
            {
                foreach (string metaPath in Directory
                             .EnumerateFiles(physicalRoot, "*.meta", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    Match match = MetaGuidPattern.Match(File.ReadAllText(metaPath));
                    if (!match.Success)
                    {
                        continue;
                    }

                    string guid = match.Groups["guid"].Value.ToLowerInvariant();
                    if (ambiguousGuids.Contains(guid))
                    {
                        continue;
                    }

                    if (assetsByGuid.ContainsKey(guid))
                    {
                        assetsByGuid.Remove(guid);
                        ambiguousGuids.Add(guid);
                        continue;
                    }

                    string physicalPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                    if (!File.Exists(physicalPath) && !Directory.Exists(physicalPath))
                    {
                        // A leftover .meta file does not make its deleted asset a valid
                        // package-owned reference target.
                        continue;
                    }

                    assetsByGuid.Add(
                        guid,
                        new PhysicalAssetRecord(
                            ToPackageAssetPath(
                                physicalRoot,
                                packageAssetRoot,
                                physicalPath),
                            physicalPath));
                }
            }
        }

        private sealed class PhysicalAssetRecord
        {
            public PhysicalAssetRecord(string assetPath, string physicalPath)
            {
                AssetPath = assetPath;
                PhysicalPath = physicalPath;
            }

            public string AssetPath { get; }
            public string PhysicalPath { get; }
        }

        private sealed class AssetOwnerResolver
        {
            private readonly Func<string, string> resolveGuid;
            private readonly Func<string, string> resolvePhysicalPath;
            private readonly Dictionary<string, IReadOnlyDictionary<long, string>> cache =
                new(StringComparer.OrdinalIgnoreCase);

            public AssetOwnerResolver(
                Func<string, string> resolveGuid,
                Func<string, string> resolvePhysicalPath)
            {
                this.resolveGuid = resolveGuid;
                this.resolvePhysicalPath = resolvePhysicalPath;
            }

            public string Resolve(string guid, long fileId)
            {
                if (!cache.TryGetValue(guid, out IReadOnlyDictionary<long, string> owners))
                {
                    owners = LoadOwners(guid);
                    cache.Add(guid, owners);
                }

                return owners.TryGetValue(fileId, out string owner) ? owner : null;
            }

            private IReadOnlyDictionary<long, string> LoadOwners(string guid)
            {
                string assetPath = NormalizeAssetPath(resolveGuid?.Invoke(guid));
                string physicalPath = resolvePhysicalPath?.Invoke(guid);
                if (string.IsNullOrWhiteSpace(physicalPath))
                {
                    physicalPath = ToPhysicalPath(assetPath);
                }

                if (string.IsNullOrWhiteSpace(physicalPath) || !File.Exists(physicalPath))
                {
                    return new Dictionary<long, string>();
                }

                string yaml = File.ReadAllText(physicalPath);
                string[] lines = yaml.Replace("\r\n", "\n").Split('\n');
                IReadOnlyList<YamlDocument> documents = ParseDocuments(lines);
                return documents
                    .GroupBy(document => document.FileId)
                    .ToDictionary(
                        group => group.Key,
                        group => DescribeDocument(group.First(), lines, resolveGuid));
            }

            private static string ToPhysicalPath(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return string.Empty;
                }

                if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    return Path.Combine(
                        Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? string.Empty,
                        assetPath.Replace('/', Path.DirectorySeparatorChar));
                }

                if (!assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                PackageInfo package = PackageInfo.FindForAssetPath(assetPath);
                if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
                {
                    return string.Empty;
                }

                string packageRoot = "Packages/" + package.name;
                if (!IsPathWithin(assetPath, packageRoot))
                {
                    return string.Empty;
                }

                string relative = assetPath.Substring(packageRoot.Length).TrimStart('/');
                return Path.Combine(
                    package.resolvedPath,
                    relative.Replace('/', Path.DirectorySeparatorChar));
            }
        }
    }
}
