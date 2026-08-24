using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Marks a tab for automatic registration in the Content Workspace.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ContentWorkspaceTabAttribute : Attribute { }

    /// <summary>Editor-only adapter implemented by one tab in the unified content workspace.</summary>
    public interface IContentWorkspaceTab
    {
        /// <summary>Gets the stable tab identifier used for persisted selection.</summary>
        string Id { get; }

        /// <summary>Gets the user-facing tab label.</summary>
        string DisplayName { get; }

        /// <summary>Gets the deterministic display order.</summary>
        int Order { get; }

        /// <summary>Creates this tab's retained-mode UI root.</summary>
        VisualElement CreateContent();

        /// <summary>Updates the shared workspace search filter.</summary>
        void SetSearch(string search);

        /// <summary>Refreshes assets, selection, references, and validation state.</summary>
        void Refresh();

        /// <summary>Notifies the adapter that its tab became visible.</summary>
        void OnSelected();

        /// <summary>Notifies the adapter that its tab is no longer visible.</summary>
        void OnDeselected();
    }

    /// <summary>Discovers workspace adapters from editor assemblies in stable display order.</summary>
    public static class ContentWorkspaceTabDiscovery
    {
        public static IReadOnlyList<IContentWorkspaceTab> Discover() =>
            Create(TypeCache.GetTypesDerivedFrom<IContentWorkspaceTab>()
                .Where(type => type.GetCustomAttributes(typeof(ContentWorkspaceTabAttribute), false).Length > 0));

        /// <summary>Creates adapters from an explicit type set for deterministic unit testing.</summary>
        public static IReadOnlyList<IContentWorkspaceTab> Create(IEnumerable<Type> types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));

            var tabs = new List<IContentWorkspaceTab>();
            foreach (Type type in types.Where(type =>
                         type != null && !type.IsAbstract && !type.IsInterface &&
                         typeof(IContentWorkspaceTab).IsAssignableFrom(type)))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"Workspace tab '{type.FullName}' requires a public parameterless constructor.");
                }
                tabs.Add((IContentWorkspaceTab)Activator.CreateInstance(type));
            }

            string duplicate = tabs.GroupBy(tab => Normalize(tab.Id), StringComparer.Ordinal)
                .FirstOrDefault(group => group.Key.Length == 0 || group.Count() > 1)?.Key;
            if (duplicate != null)
            {
                throw new InvalidOperationException(duplicate.Length == 0
                    ? "Workspace tab IDs must be non-empty."
                    : $"Workspace tab ID '{duplicate}' is duplicated.");
            }

            return tabs.OrderBy(tab => tab.Order)
                .ThenBy(tab => tab.DisplayName, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
