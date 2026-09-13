using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace QuietStatic.Toolkit.Editor.Samples
{
    /// <summary>
    /// Imports and deterministically configures the package's executable sample.
    /// </summary>
    public static class ExecutableSampleSetup
    {
        internal const string SampleDisplayName =
            "Executable Toolkit Example";

        internal static readonly string[] RequiredSceneNames =
        {
            "QuietStaticSampleBootstrap",
            "QuietStaticSampleSystems",
            "QuietStaticSampleContent"
        };

        [MenuItem(
            QuietStaticMenuPaths.Root +
            "Samples/Import and Open Executable Example",
            false,
            20)]
        public static void ImportAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            PackageManagerPackageInfo package =
                PackageManagerPackageInfo.FindForAssembly(
                typeof(SceneBootstrapProfile).Assembly);

            if (package == null)
            {
                ReportFailure("Could not resolve the Quiet Static package.");
                return;
            }

            Sample[] samples = Sample
                .FindByPackage(package.name, package.version)
                .Where(sample => string.Equals(
                    sample.displayName,
                    SampleDisplayName,
                    StringComparison.Ordinal))
                .ToArray();

            if (samples.Length != 1)
            {
                ReportFailure(
                    $"Expected exactly one '{SampleDisplayName}' sample, " +
                    $"but found {samples.Length}.");
                return;
            }

            Sample sample = samples[0];

            if (!sample.isImported &&
                !sample.Import(Sample.ImportOptions.HideImportWindow))
            {
                ReportFailure("Unity could not import the executable sample.");
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string sampleRoot = FileUtil.GetProjectRelativePath(
                sample.importPath.Replace('\\', '/'));

            if (string.IsNullOrWhiteSpace(sampleRoot))
            {
                sampleRoot = sample.importPath.Replace('\\', '/');
            }

            if (!TryConfigureImportedSample(sampleRoot, out string error))
            {
                ReportFailure(error);
                return;
            }

            GameLogger.Log(
                nameof(ExecutableSampleSetup),
                null,
                "Imported the executable sample, configured its three scenes first " +
                "in Build Settings, and opened the bootstrap scene.");
        }

        internal static bool TryConfigureImportedSample(
            string sampleRoot,
            out string error)
        {
            string[] candidateScenePaths = AssetDatabase
                .FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();

            if (!TryResolveRequiredScenePaths(
                    sampleRoot,
                    candidateScenePaths,
                    out string[] requiredPaths,
                    out error))
            {
                return false;
            }

            string profilePath =
                $"{NormalizeAssetPath(sampleRoot).TrimEnd('/')}/Data/" +
                "QuietStaticSampleBootstrapProfile.asset";
            SceneBootstrapProfile profile =
                AssetDatabase.LoadAssetAtPath<SceneBootstrapProfile>(profilePath);
            if (profile == null)
            {
                error = $"The executable sample profile is missing at '{profilePath}'.";
                return false;
            }

            string[] expectedProfileScenes = RequiredSceneNames.Skip(1).ToArray();
            if (!profile.ReferencedSceneNames.SequenceEqual(expectedProfileScenes) ||
                !profile.PersistentSceneNames.SequenceEqual(expectedProfileScenes.Take(1)) ||
                !string.Equals(
                    profile.InitialSceneName,
                    expectedProfileScenes[1],
                    StringComparison.Ordinal))
            {
                error =
                    "The executable sample profile no longer describes the required " +
                    "Systems then Content bootstrap flow.";
                return false;
            }

            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                Scene bootstrapScene = EditorSceneManager.OpenScene(
                    requiredPaths[0],
                    OpenSceneMode.Single);
                SceneBootstrapper[] bootstrappers = bootstrapScene
                    .GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<SceneBootstrapper>(true))
                    .ToArray();
                if (bootstrappers.Length != 1)
                {
                    error =
                        "The executable Bootstrap scene must contain exactly one " +
                        $"SceneBootstrapper; found {bootstrappers.Length}.";
                    return false;
                }

                var serializedBootstrapper = new SerializedObject(bootstrappers[0]);
                SerializedProperty profileProperty =
                    serializedBootstrapper.FindProperty("profile");
                if (profileProperty == null ||
                    profileProperty.objectReferenceValue != profile)
                {
                    error =
                        "The executable Bootstrap scene is not bound to its sample " +
                        "bootstrap profile.";
                    return false;
                }

                EditorBuildSettings.scenes = OrderBuildSettings(
                    previousScenes,
                    requiredPaths);
                return true;
            }
            catch (Exception exception)
            {
                EditorBuildSettings.scenes = previousScenes;
                error =
                    "The sample scenes resolved, but Unity could not open the " +
                    $"bootstrap scene. Build Settings were restored. {exception.Message}";
                return false;
            }
        }

        internal static bool TryResolveRequiredScenePaths(
            string sampleRoot,
            IEnumerable<string> candidateScenePaths,
            out string[] requiredPaths,
            out string error)
        {
            string normalizedRoot = NormalizeAssetPath(sampleRoot)
                .TrimEnd('/');
            string[] candidates = (candidateScenePaths ??
                    Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizeAssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var resolved = new List<string>(RequiredSceneNames.Length);

            foreach (string sceneName in RequiredSceneNames)
            {
                string[] exactMatches = candidates
                    .Where(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        sceneName,
                        StringComparison.Ordinal))
                    .ToArray();

                if (exactMatches.Length != 1)
                {
                    requiredPaths = Array.Empty<string>();
                    error =
                        $"Scene '{sceneName}' must resolve exactly once; " +
                        $"found {exactMatches.Length} exact matches.";
                    return false;
                }

                string expectedPath =
                    $"{normalizedRoot}/Scenes/{sceneName}.unity";

                if (!string.Equals(
                        exactMatches[0],
                        expectedPath,
                        StringComparison.Ordinal))
                {
                    requiredPaths = Array.Empty<string>();
                    error =
                        $"Scene '{sceneName}' resolved outside the imported sample: " +
                        exactMatches[0];
                    return false;
                }

                resolved.Add(exactMatches[0]);
            }

            requiredPaths = resolved.ToArray();
            error = string.Empty;
            return true;
        }

        internal static EditorBuildSettingsScene[] OrderBuildSettings(
            IEnumerable<EditorBuildSettingsScene> currentScenes,
            IEnumerable<string> requiredScenePaths)
        {
            string[] required = (requiredScenePaths ??
                    Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizeAssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var ordered = new List<EditorBuildSettingsScene>();
            var requiredSet = new HashSet<string>(
                required,
                StringComparer.Ordinal);

            foreach (string path in required)
            {
                ordered.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene scene in
                     currentScenes ?? Array.Empty<EditorBuildSettingsScene>())
            {
                string path = NormalizeAssetPath(scene.path);

                if (requiredSet.Contains(path))
                {
                    continue;
                }

                // Preserve unrelated entries verbatim, including disabled, duplicate,
                // and currently unresolved entries.
                ordered.Add(scene);
            }

            return ordered.ToArray();
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Trim().Replace('\\', '/') ?? string.Empty;
        }

        private static void ReportFailure(string message)
        {
            GameLogger.Error(nameof(ExecutableSampleSetup), null, message);
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Quiet Static Executable Sample",
                    message,
                    "OK");
            }
        }
    }
}
