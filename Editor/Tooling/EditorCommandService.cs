using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Immutable preview required before a mutating editor command can apply.</summary>
    public sealed class EditorCommandPreview
    {
        public EditorCommandPreview(string token, IEnumerable<string> changes)
        {
            Token = string.IsNullOrWhiteSpace(token)
                ? throw new ArgumentException("Preview token must be non-empty.", nameof(token))
                : token.Trim();
            Changes = (changes ?? Array.Empty<string>()).Where(change => !string.IsNullOrWhiteSpace(change))
                .Select(change => change.Trim()).ToArray();
        }

        public string Token { get; }
        public IReadOnlyList<string> Changes { get; }
        public bool HasChanges => Changes.Count > 0;
    }

    /// <summary>Preview/apply contract shared by imports and other mutating editor operations.</summary>
    public interface IPreviewedEditorCommand
    {
        EditorCommandPreview Preview();
        void Apply(EditorCommandPreview preview);
    }

    /// <summary>Read-only export contract shared by workspace and graph toolbar commands.</summary>
    public interface IEditorExportCommand
    {
        void Export();
    }

    /// <summary>Runs editor commands with one enforced preview-before-apply lifecycle.</summary>
    public static class EditorCommandService
    {
        public static EditorCommandPreview Preview(IPreviewedEditorCommand command) =>
            (command ?? throw new ArgumentNullException(nameof(command))).Preview() ??
            throw new InvalidOperationException("Editor command returned no preview.");

        public static bool Apply(IPreviewedEditorCommand command, EditorCommandPreview preview)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (!preview.HasChanges) return false;
            command.Apply(preview);
            return true;
        }

        public static void Export(IEditorExportCommand command) =>
            (command ?? throw new ArgumentNullException(nameof(command))).Export();
    }
}
