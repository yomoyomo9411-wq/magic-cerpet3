using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class DaVinciAssetFixer
{
    private const string LaneGuideMaterialPath = "Assets/Resources/GeneratedShieldAura/Generated Soft Lane Guide.mat";
    private const string DragonMaterialFolder = "Assets/AoNecoDo/231227_01_Dragon/Materials";

    static DaVinciAssetFixer()
    {
        EditorApplication.delayCall += ApplyFixes;
    }

    [MenuItem("Tools/DaVinci/Fix Materials")]
    public static void ApplyFixes()
    {
        FixLaneGuideMaterial();
        FixDragonMaterials();
        AssetDatabase.SaveAssets();
    }

    private static void FixLaneGuideMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(LaneGuideMaterialPath);
        if (material == null)
        {
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            material.shader = shader;
        }

        var color = new Color(1f, 0.05f, 0.05f, 0.1f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        SetTransparent(material, alphaBlend: true);
        EditorUtility.SetDirty(material);
    }

    private static void FixDragonMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            return;
        }

        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { DragonMaterialFolder });
        foreach (var guid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }

            var baseTexture = GetTexture(material, "_BaseMap") ?? GetTexture(material, "_BaseColorMap") ?? GetTexture(material, "_MainTex");
            var normalTexture = GetTexture(material, "_BumpMap");
            var color = GetColor(material, "_BaseColor", Color.white);
            if (color == Color.white)
            {
                color = GetColor(material, "_Color", Color.white);
            }

            material.shader = shader;

            if (baseTexture != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseTexture);
            }

            if (normalTexture != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normalTexture);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            SetOpaque(material);
            EditorUtility.SetDirty(material);
        }
    }

    private static Texture GetTexture(Material material, string propertyName)
    {
        return material.HasProperty(propertyName) ? material.GetTexture(propertyName) : null;
    }

    private static Color GetColor(Material material, string propertyName, Color fallback)
    {
        return material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
    }

    private static void SetOpaque(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }

        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.renderQueue = -1;
    }

    private static void SetTransparent(Material material, bool alphaBlend)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", alphaBlend ? 0f : 2f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
