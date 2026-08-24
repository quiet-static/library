using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Deterministic searchable index of project assets of one Unity type.</summary>
    public sealed class AssetPickerModel<T> where T : UnityEngine.Object
    {
        private readonly IReadOnlyList<T> assets;

        public AssetPickerModel(IEnumerable<T> assets)
        {
            this.assets = (assets ?? throw new ArgumentNullException(nameof(assets)))
                .Where(asset => asset != null)
                .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<T> Search(string query)
        {
            string value = query?.Trim() ?? string.Empty;
            if (value.Length == 0) return assets;
            return assets.Where(asset => Contains(asset.name, value) ||
                                         Contains(AssetDatabase.GetAssetPath(asset), value))
                .ToArray();
        }

        public static AssetPickerModel<T> BuildProjectIndex()
        {
            T[] found = AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
            return new AssetPickerModel<T>(found);
        }

        private static bool Contains(string text, string query) =>
            (text ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Reusable UI Toolkit asset picker with one shared search and selection contract.</summary>
    public sealed class SearchableAssetPicker<T> : VisualElement where T : UnityEngine.Object
    {
        private readonly TextField searchField;
        private readonly ListView list;
        private AssetPickerModel<T> model;

        public SearchableAssetPicker(string searchLabel = "Search")
        {
            style.flexGrow = 1f;
            searchField = new TextField(searchLabel);
            searchField.RegisterValueChangedCallback(change => Refresh(change.newValue));
            Add(searchField);

            list = new ListView { selectionType = SelectionType.Single, style = { flexGrow = 1f } };
            list.makeItem = () => new Label();
            list.bindItem = (element, index) =>
            {
                T asset = (T)list.itemsSource[index];
                ((Label)element).text = $"{asset.name}  —  {AssetDatabase.GetAssetPath(asset)}";
            };
            list.selectionChanged += values => SelectionChanged?.Invoke(values.Cast<T>().FirstOrDefault());
            Add(list);
        }

        public event Action<T> SelectionChanged;

        public void SetModel(AssetPickerModel<T> value)
        {
            model = value;
            Refresh(searchField.value);
        }

        public void SetSearch(string value) => searchField.SetValueWithoutNotify(value ?? string.Empty);

        public void Refresh(string query = null)
        {
            list.itemsSource = model?.Search(query ?? searchField.value).ToList() ?? new List<T>();
            list.Rebuild();
        }
    }
}
