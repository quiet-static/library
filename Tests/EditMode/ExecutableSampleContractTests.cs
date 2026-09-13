using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor.PackageManager;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class ExecutableSampleContractTests
    {
        private const string ExecutableSamplePath =
            "Samples~/ExecutableToolkitExample";

        [Test]
        public void PackageDeclaresExecutableAndCompositionSamples()
        {
            string packageJson = File.ReadAllText(
                Path.Combine(GetPackageRoot(), "package.json"));
            PackageManifest manifest = JsonUtility.FromJson<PackageManifest>(packageJson);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.samples, Is.Not.Null);
            Assert.That(
                manifest.samples.Count(sample =>
                    sample.displayName == "Executable Toolkit Example" &&
                    sample.path == "Samples~/ExecutableToolkitExample"),
                Is.EqualTo(1));
            Assert.That(
                manifest.samples.Count(sample =>
                    sample.displayName == "Composition References (Non-Runnable)" &&
                    sample.path == "Samples~/CompositionReferences"),
                Is.EqualTo(1));
        }

        [Test]
        public void ExecutableSampleShipsCompleteThreeSceneContract()
        {
            string root = GetExecutableSampleRoot();
            string[] expectedFiles =
            {
                "README.md",
                "Scenes/QuietStaticSampleBootstrap.unity",
                "Scenes/QuietStaticSampleSystems.unity",
                "Scenes/QuietStaticSampleContent.unity",
                "Data/QuietStaticSampleBootstrapProfile.asset",
                "Data/QuietStaticSampleFlags.asset",
                "Data/QuietStaticSampleObjective.asset",
                "Data/QuietStaticSampleObjectives.asset",
                "Data/QuietStaticSampleSceneFlowChannel.asset"
            };

            string[] missing = expectedFiles
                .Where(relativePath =>
                    !File.Exists(Path.Combine(
                        root,
                        relativePath.Replace('/', Path.DirectorySeparatorChar))))
                .ToArray();

            Assert.That(
                missing,
                Is.Empty,
                "The executable sample is incomplete: " +
                string.Join(", ", missing));
        }

        [Test]
        public void RenamedSamplesDoNotReuseFormerToolkitExampleGuids()
        {
            string[] retiredGuids =
            {
                "13af40e4da043c74e88edfb857168d37",
                "03be2ad4acc938d4faaa691b3bdb4a9c",
                "d4f2c6f001984c54a12f9a8be04d3c71",
                "fe10c8606590191458deaca8baa3ac0f",
                "1531b22c54300e84ab8f71794c365dce",
                "7f4b4992b79085d48a00052c671367ee",
                "3befa8f66b058ce4f907178daf7836ac",
                "fc740da18f192e743ac56d30b098722d",
                "094ac3002ff55ef4ea79fa010f6dadd8",
                "0d4a106282a85a2409b8b80ee879a9d7",
                "8256b2d98b4ed7c469fc5f0a286170c0"
            };
            string samplesRoot = Path.Combine(GetPackageRoot(), "Samples~");
            string[] reused = Directory
                .EnumerateFiles(samplesRoot, "*.meta", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Select(meta => Regex.Match(
                    meta,
                    @"(?m)^guid:\s*([0-9a-f]{32})\s*$",
                    RegexOptions.CultureInvariant))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value)
                .Intersect(retiredGuids, StringComparer.OrdinalIgnoreCase)
                .OrderBy(guid => guid, StringComparer.Ordinal)
                .ToArray();

            Assert.That(
                reused,
                Is.Empty,
                "Renamed samples must not collide with an older imported " +
                "Toolkit Examples sample.");
        }

        [Test]
        public void SerializedSampleWiresBootstrapInteractionObjectiveAndUiFlow()
        {
            string root = GetExecutableSampleRoot();
            string data = Path.Combine(root, "Data");
            string scenes = Path.Combine(root, "Scenes");

            string profilePath = Path.Combine(
                data,
                "QuietStaticSampleBootstrapProfile.asset");
            string flagsPath = Path.Combine(
                data,
                "QuietStaticSampleFlags.asset");
            string objectivePath = Path.Combine(
                data,
                "QuietStaticSampleObjective.asset");
            string objectivesPath = Path.Combine(
                data,
                "QuietStaticSampleObjectives.asset");
            string channelPath = Path.Combine(
                data,
                "QuietStaticSampleSceneFlowChannel.asset");

            string profile = Read(profilePath);
            string flags = Read(flagsPath);
            string objective = Read(objectivePath);
            string objectives = Read(objectivesPath);
            string bootstrap = Read(Path.Combine(
                scenes,
                "QuietStaticSampleBootstrap.unity"));
            string systems = Read(Path.Combine(
                scenes,
                "QuietStaticSampleSystems.unity"));
            string content = Read(Path.Combine(
                scenes,
                "QuietStaticSampleContent.unity"));

            StringAssert.Contains(
                "sceneName: QuietStaticSampleSystems",
                profile);
            StringAssert.Contains(
                "sceneName: QuietStaticSampleContent",
                profile);
            StringAssert.DoesNotContain("sceneName: System", profile);
            StringAssert.DoesNotContain("sceneName: House", profile);

            StringAssert.Contains(ReadGuid(profilePath), bootstrap);
            StringAssert.Contains(
                "QuietStatic.Toolkit.SceneFlow.SceneBootstrapper",
                bootstrap);

            StringAssert.Contains(ReadGuid(flagsPath), systems);
            StringAssert.Contains(ReadGuid(objectivesPath), systems);
            StringAssert.Contains(ReadGuid(channelPath), systems);
            StringAssert.Contains("QuietStatic.SceneFlowManager", systems);
            StringAssert.Contains(
                "QuietStatic.Toolkit.Flags.FlagManager",
                systems);
            StringAssert.Contains(
                "QuietStatic.Toolkit.Objectives.ObjectiveManager",
                systems);
            StringAssert.Contains(
                "QuietStatic.Toolkit.Objectives.ObjectivePresenter",
                systems);
            StringAssert.Contains("InputSystemUIInputModule", systems);
            StringAssert.Contains("Sample complete", systems);
            StringAssert.DoesNotContain(
                "m_LocalScale: {x: 0, y: 0, z: 0}",
                systems);

            StringAssert.Contains("quietstatic.sample.ready", flags);
            StringAssert.Contains("quietstatic.sample.inspected", flags);
            StringAssert.Contains("quietstatic.sample.inspect", objective);
            StringAssert.Contains("quietstatic.sample.ready", objective);
            StringAssert.Contains("quietstatic.sample.inspected", objective);
            StringAssert.Contains(ReadGuid(objectivePath), objectives);

            StringAssert.Contains(
                "QuietStatic.Toolkit.Interactions.Interactable",
                content);
            StringAssert.Contains("quietstatic.sample.inspected", content);
            StringAssert.Contains("m_MethodName: Interact", content);
            StringAssert.Contains("UnityEngine.UI.Button", content);
            StringAssert.Contains("--- !u!20 &", content);
            StringAssert.Contains("--- !u!81 &", content);
            StringAssert.DoesNotContain(
                "m_LocalScale: {x: 0, y: 0, z: 0}",
                content);
        }

        private static string GetPackageRoot()
        {
            PackageInfo package = PackageInfo.FindForAssembly(
                typeof(SceneBootstrapProfile).Assembly);
            Assert.That(package, Is.Not.Null);
            return package.resolvedPath;
        }

        private static string GetExecutableSampleRoot()
        {
            return Path.Combine(
                GetPackageRoot(),
                ExecutableSamplePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string Read(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"Missing sample file: {path}");
            return File.ReadAllText(path);
        }

        private static string ReadGuid(string assetPath)
        {
            string meta = Read(assetPath + ".meta");
            Match match = Regex.Match(
                meta,
                @"(?m)^guid:\s*([0-9a-f]{32})\s*$",
                RegexOptions.CultureInvariant);
            Assert.That(match.Success, Is.True, $"Missing GUID in {assetPath}.meta");
            return match.Groups[1].Value;
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public PackageSample[] samples;
        }

        [Serializable]
        private sealed class PackageSample
        {
            public string displayName;
            public string path;
        }
    }
}
