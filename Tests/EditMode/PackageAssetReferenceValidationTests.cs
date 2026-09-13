using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Tests.EditMode
{
    public sealed class PackageAssetReferenceValidationTests
    {
        private const string PackageName = "com.quietstatic.core";

        [Test]
        public void ValidateYamlText_RejectsUnresolvedConsumerAndUndeclaredReferences()
        {
            const string yaml = @"%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: Test Object
--- !u!114 &2
MonoBehaviour:
  m_GameObject: {fileID: 1}
  m_EditorClassIdentifier: Test.Assembly::Example.ReferenceOwner
  missingClip: {fileID: 8300000, guid: 11111111111111111111111111111111, type: 3}
  projectAsset: {fileID: 11400000, guid: 22222222222222222222222222222222, type: 2}
  transitiveAsset: {fileID: 11400000, guid: 33333333333333333333333333333333, type: 2}";
            var paths = new Dictionary<string, string>
            {
                ["22222222222222222222222222222222"] = "Assets/Consumer/Project.asset",
                ["33333333333333333333333333333333"] = "Packages/com.example.transitive/Config.asset",
            };

            IReadOnlyList<ValidationIssue> issues =
                PackageAssetReferenceValidation.ValidateYamlText(
                    yaml,
                    "Packages/com.quietstatic.core/Runtime/Test.asset",
                    guid => paths.TryGetValue(guid, out string path) ? path : string.Empty,
                    PackageName,
                    new[] { "com.unity.ugui" });

            Assert.That(
                issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[]
                {
                    PackageAssetReferenceValidation.UnresolvedReferenceCode,
                    PackageAssetReferenceValidation.ConsumerReferenceCode,
                    PackageAssetReferenceValidation.UndeclaredPackageReferenceCode,
                }));
            Assert.That(issues.All(issue => issue.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(issues.All(issue => issue.AssetPath.EndsWith("Runtime/Test.asset")), Is.True);
            Assert.That(issues.Select(issue => issue.Message),
                Has.Some.Contains("Test Object/ReferenceOwner.missingClip"));
            Assert.That(issues.Select(issue => issue.Message),
                Has.Some.Contains("11111111111111111111111111111111"));
            Assert.That(issues.Select(issue => issue.Message),
                Has.Some.Contains("consumer-owned project asset"));
            Assert.That(issues.Select(issue => issue.Message),
                Has.Some.Contains("undeclared package asset"));
        }

        [Test]
        public void ValidateYamlText_AllowsOwnPackageDependenciesAndUnityResources()
        {
            const string yaml = @"%YAML 1.1
--- !u!114 &2
MonoBehaviour:
  ownScript: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}
  inputScript: {fileID: 11500000, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}
  builtinExtra: {fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}
  builtinEditor: {fileID: 1001, guid: 0000000000000000e000000000000000, type: 0}
  nullReference: {fileID: 0, guid: 00000000000000000000000000000000, type: 0}
  liberationSans: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee, type: 2}";

            IReadOnlyList<ValidationIssue> issues =
                PackageAssetReferenceValidation.ValidateYamlText(
                    yaml,
                    "Packages/com.quietstatic.core/Runtime/Test.asset",
                    guid => guid[0] == 'a'
                        ? "Packages/com.quietstatic.core/Runtime/Test.cs"
                        : "Packages/com.unity.inputsystem/InputSystem/InputAction.cs",
                    PackageName,
                    new[] { "com.unity.inputsystem", "com.unity.ugui" });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void ValidateYamlText_GroupsRepeatedGuidAndListsPrefabOverrideFields()
        {
            const string yaml = @"%YAML 1.1
--- !u!1001 &10
PrefabInstance:
  m_Modification:
    m_Modifications:
    - target: {fileID: 20, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}
      propertyPath: inputActions
      objectReference: {fileID: 1, guid: cccccccccccccccccccccccccccccccc, type: 3}
    - target: {fileID: 30, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}
      propertyPath: m_Actions
      objectReference: {fileID: 1, guid: cccccccccccccccccccccccccccccccc, type: 3}";

            IReadOnlyList<ValidationIssue> issues =
                PackageAssetReferenceValidation.ValidateYamlText(
                    yaml,
                    "Packages/com.quietstatic.core/Runtime/Test.prefab",
                    guid => guid[0] == 'a'
                        ? "Packages/com.quietstatic.core/Runtime/Base.prefab"
                        : "Assets/Input.inputactions",
                    PackageName,
                    Array.Empty<string>(),
                    (guid, fileId) => fileId == 20
                        ? "Player/GameInputManager"
                        : "Player/PlayerInput");

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Code,
                Is.EqualTo(PackageAssetReferenceValidation.ConsumerReferenceCode));
            Assert.That(issues[0].Message, Does.Contain("Player/GameInputManager.inputActions"));
            Assert.That(issues[0].Message, Does.Contain("Player/PlayerInput.m_Actions"));
            Assert.That(issues[0].Message, Does.Contain("cccccccccccccccccccccccccccccccc"));
        }

        [Test]
        public void ValidateYamlText_RejectsSerializationItCannotInspect()
        {
            IReadOnlyList<ValidationIssue> issues =
                PackageAssetReferenceValidation.ValidateYamlText(
                    "binary serialized content",
                    "Packages/com.quietstatic.core/Runtime/Opaque.asset",
                    _ => string.Empty,
                    PackageName,
                    Array.Empty<string>());

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Code,
                Is.EqualTo(PackageAssetReferenceValidation.UnverifiableSerializationCode));
            Assert.That(issues[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(issues[0].AssetPath, Does.EndWith("Runtime/Opaque.asset"));
            Assert.That(issues[0].Message, Does.Contain("cannot be verified"));
        }

        [Test]
        public void ScanPhysicalPackageAssets_ScansUnityScenes()
        {
            string root = CreateTemporaryPackageRoot();
            try
            {
                const string consumerGuid = "22222222222222222222222222222222";
                string scenePath = Path.Combine(root, "Samples~", "ConsumerScene.unity");
                WriteText(
                    scenePath,
                    $@"%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: Scene Owner
--- !u!114 &2
MonoBehaviour:
  m_GameObject: {{fileID: 1}}
  m_EditorClassIdentifier: Test.Assembly::Example.SceneReferenceOwner
  projectAsset: {{fileID: 11400000, guid: {consumerGuid}, type: 2}}");

                IReadOnlyList<ValidationIssue> issues =
                    PackageAssetReferenceValidation.ScanPhysicalPackageAssets(
                        root,
                        PackageName,
                        Array.Empty<string>(),
                        guid => guid == consumerGuid
                            ? "Assets/Consumer/Project.asset"
                            : string.Empty);

                Assert.That(issues.Count, Is.EqualTo(1));
                Assert.That(issues[0].Code,
                    Is.EqualTo(PackageAssetReferenceValidation.ConsumerReferenceCode));
                Assert.That(issues[0].AssetPath,
                    Does.EndWith("Samples~/ConsumerScene.unity"));
                Assert.That(issues[0].Message,
                    Does.Contain("Scene Owner/SceneReferenceOwner.projectAsset"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ScanPhysicalPackageAssets_UsesHiddenMetadataAndFailsClosedWhenMissing()
        {
            string root = CreateTemporaryPackageRoot();
            try
            {
                const string hiddenGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                string hiddenAssetPath = Path.Combine(
                    root,
                    "Samples~",
                    "Definitions",
                    "HiddenDefinition.asset");
                WriteText(
                    hiddenAssetPath,
                    @"%YAML 1.1
--- !u!114 &11400000
MonoBehaviour:
  m_Name: Hidden Definition");
                WriteText(
                    hiddenAssetPath + ".meta",
                    $@"fileFormatVersion: 2
guid: {hiddenGuid}");
                WriteText(
                    Path.Combine(root, "Samples~", "HiddenScene.unity"),
                    $@"%YAML 1.1
--- !u!114 &2
MonoBehaviour:
  hiddenDefinition: {{fileID: 11400000, guid: {hiddenGuid}, type: 2}}");

                IReadOnlyList<ValidationIssue> resolved =
                    PackageAssetReferenceValidation.ScanPhysicalPackageAssets(
                        root,
                        PackageName,
                        Array.Empty<string>(),
                        _ => string.Empty);

                Assert.That(resolved, Is.Empty);

                File.Delete(hiddenAssetPath);
                IReadOnlyList<ValidationIssue> orphanedMetadata =
                    PackageAssetReferenceValidation.ScanPhysicalPackageAssets(
                        root,
                        PackageName,
                        Array.Empty<string>(),
                        _ => string.Empty);

                Assert.That(orphanedMetadata.Count, Is.EqualTo(1));
                Assert.That(orphanedMetadata[0].Code,
                    Is.EqualTo(PackageAssetReferenceValidation.UnresolvedReferenceCode));
                Assert.That(orphanedMetadata[0].Message, Does.Contain(hiddenGuid));

                WriteText(
                    hiddenAssetPath,
                    @"%YAML 1.1
--- !u!114 &11400000
MonoBehaviour:
  m_Name: Hidden Definition");
                File.Delete(hiddenAssetPath + ".meta");
                IReadOnlyList<ValidationIssue> missingMetadata =
                    PackageAssetReferenceValidation.ScanPhysicalPackageAssets(
                        root,
                        PackageName,
                        Array.Empty<string>(),
                        _ => string.Empty);

                Assert.That(missingMetadata.Count, Is.EqualTo(1));
                Assert.That(missingMetadata[0].Code,
                    Is.EqualTo(PackageAssetReferenceValidation.UnresolvedReferenceCode));
                Assert.That(missingMetadata[0].AssetPath,
                    Does.EndWith("Samples~/HiddenScene.unity"));
                Assert.That(missingMetadata[0].Message, Does.Contain(hiddenGuid));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CurrentPackage_HasNoExternalReferenceGroups()
        {
            IReadOnlyList<ValidationIssue> issues =
                PackageAssetReferenceValidation.ScanPackageAssets();

            Assert.That(issues, Is.Empty,
                string.Join("\n", issues.Select(issue =>
                    $"{issue.Code}: {issue.AssetPath}: {issue.Message}")));
        }

        private static string CreateTemporaryPackageRoot()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "QuietStaticPackageReferenceTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, content);
        }
    }
}
