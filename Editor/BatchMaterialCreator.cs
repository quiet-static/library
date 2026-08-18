using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates one material beside each selected texture and assigns that texture to a shader property.
/// </summary>
/// <remarks>
/// Open from <c>Tools &gt; Quiet Static &gt; Asset Utilities &gt; Materials &gt; Bulk Material Creator</c>.
/// Asset creation is not automatically reversible, so verify the shader, property name, and
/// selection before confirming.
/// </remarks>
public class BulkMaterialCreator : ScriptableWizard
{
    [Tooltip("Shader texture property that receives each selected texture, such as _BaseMap.")]
    public string PropertyName = "_MainTex";

    [Tooltip("Text appended to each generated material asset name.")]
    public string Suffix;

    [Tooltip("Shader assigned to every generated material.")]
    public Shader Shader;

    /// <summary>Opens the material creation wizard.</summary>
    [MenuItem(itemName: QuietStaticMenuPaths.MaterialUtilities + "Bulk Material Creator")]
    public static void CreateWizard() => DisplayWizard(title: "Bulk Material Creator", klass: typeof(BulkMaterialCreator));

    public void OnWizardUpdate() { }

    /// <summary>Creates material assets for all selected Texture2D assets.</summary>
    public void OnWizardCreate()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj.GetType() == typeof(Texture2D))
            {
                Texture2D texture = (Texture2D)obj;
                Material material = GenerateMaterial(texture);
                string path = GetDirectory(obj) + "/" + material.name + ".mat";
                AssetDatabase.CreateAsset(asset: material, path);
            }
        }
    }

    private Material GenerateMaterial(Texture2D texture)
    {
        Material material = new Material(shader: Shader);
        material.name = texture.name + Suffix;
        material.SetTexture(name: PropertyName, value: texture);
        return material;
    }

    private string GetDirectory(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(assetObject: obj);
        if (path.Contains(value: '/'))
        {
            path = path.Substring(startIndex: 0, length: path.LastIndexOf(value: '/'));
        }
        return path;
    }
}
