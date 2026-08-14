using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>Shared filesystem and Unity asset-path safeguards for narrative JSON tooling.</summary>
    internal static class NarrativeJsonPathUtility
    {
        public static string ResolveAssetIdentity(UnityEngine.Object asset, string explicitId)
        {
            if (explicitId != null)
                return explicitId;
            if (asset == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(assetPath)
                ? asset.name
                : Path.GetFileNameWithoutExtension(assetPath);
        }

        public static void ValidateIdentity(
            string value,
            string label,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"{label} must be non-empty.");
            else if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                errors.Add($"{label} must not have leading or trailing whitespace.");
        }

        public static string GetUnityAssetPath(UnityEngine.Object asset)
        {
            string path = asset == null ? null : AssetDatabase.GetAssetPath(asset);
            return IsCanonicalAssetPath(path) ? path : null;
        }

        public static void ValidateUnityAssetPath(
            string path,
            Type expectedType,
            string label,
            ICollection<string> errors)
        {
            if (path == null)
                return;
            if (!IsCanonicalAssetPath(path))
            {
                errors.Add($"{label} must be a canonical .asset path below Assets.");
                return;
            }

            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !expectedType.IsInstanceOfType(existing))
            {
                errors.Add(
                    $"{label} points to {existing.GetType().Name}, not {expectedType.Name}: {path}");
            }
        }

        public static bool IsCanonicalAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !string.Equals(path, path.Trim(), StringComparison.Ordinal) ||
                path.IndexOf('\\') >= 0 ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(path), ".asset", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] segments = path.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(segments[index]) ||
                    segments[index] == "." ||
                    segments[index] == "..")
                    return false;
            }

            try
            {
                string assetsFolder = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(path);
                string expectedPrefix = assetsFolder + Path.DirectorySeparatorChar;
                StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return fullPath.StartsWith(expectedPrefix, comparison);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return false;
            }
        }

        public static void EnsureAssetFolderForPath(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || folder == "Assets")
                return;

            string current = "Assets";
            foreach (string segment in folder.Split('/'))
            {
                if (segment == "Assets")
                    continue;
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        public static string WriteJson(string outputPath, string json)
        {
            string fullPath = ValidateJsonOutputPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);

            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, null);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            string assetPath = TryGetProjectRelativePath(fullPath);
            if (assetPath != null &&
                (assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                 assetPath.StartsWith("Packages/", StringComparison.Ordinal)))
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return fullPath;
        }

        public static string GetInitialFolder(UnityEngine.Object asset)
        {
            string assetPath = asset == null ? null : AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrWhiteSpace(assetPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Path.GetFullPath(assetPath));
        }

        public static string TryGetProjectRelativePath(string fullPath)
        {
            string projectFolder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = projectFolder + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison))
                return null;

            return fullPath.Substring(prefix.Length).Replace('\\', '/');
        }

        private static string ValidateJsonOutputPath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path must be non-empty.", nameof(outputPath));

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(outputPath);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                throw new ArgumentException("Output path is invalid.", nameof(outputPath), exception);
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output path must end in .json.", nameof(outputPath));
            if (Directory.Exists(fullPath))
                throw new ArgumentException("Output path points to a directory.", nameof(outputPath));
            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fullPath)))
                throw new ArgumentException("Output path must include a filename.", nameof(outputPath));
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(fullPath)))
                throw new ArgumentException("Output path must include a directory.", nameof(outputPath));
            return fullPath;
        }
    }
}
