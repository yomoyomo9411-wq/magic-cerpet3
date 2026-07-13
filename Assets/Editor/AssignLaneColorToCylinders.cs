using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AssignLaneColorToCylinders
{
    private const string MaterialName = "lanecolor";
    private const string GuideParentName = "Soft Lane Guides";
    private const string GuideMaterialName = "Generated Soft Lane Guide";
    private static readonly float[] LanePositions = { -8f, -4f, 0f, 4f, 8f };

    static AssignLaneColorToCylinders()
    {
        EditorApplication.delayCall += Apply;
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) => EditorApplication.delayCall += Apply;
        EditorApplication.hierarchyChanged += Apply;
    }

    private static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "DemoSkeletonScene")
        {
            return;
        }

        var material = FindLaneMaterial();
        if (material == null)
        {
            Debug.LogWarning("lanecolor material was not found.");
            return;
        }

        var softMaterial = CreateSoftLaneMaterial(material);
        var changed = HideCylinderRenderers();
        changed |= EnsureSoftLaneGuides(softMaterial);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static bool HideCylinderRenderers()
    {
        var changed = false;
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsTargetLane(renderer.gameObject.name))
            {
                continue;
            }

            if (!renderer.enabled)
            {
                continue;
            }

            Undo.RecordObject(renderer, "Hide Original Lane Cylinder");
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
            changed = true;
        }

        return changed;
    }

    private static bool EnsureSoftLaneGuides(Material material)
    {
        var parent = FindObjectInScene(GuideParentName);
        var changed = false;
        if (parent == null)
        {
            parent = new GameObject(GuideParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Soft Lane Guides");
            changed = true;
        }

        if (!parent.activeSelf)
        {
            return changed;
        }

        for (var i = 0; i < LanePositions.Length; i++)
        {
            var guideName = $"Soft Lane Guide {i + 1}";
            var guide = parent.transform.Find(guideName);
            GameObject guideObject;
            if (guide == null)
            {
                guideObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                guideObject.name = guideName;
                Undo.RegisterCreatedObjectUndo(guideObject, "Create Soft Lane Guide");
                guideObject.transform.SetParent(parent.transform, false);

                var collider = guideObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }

                changed = true;
            }
            else
            {
                guideObject = guide.gameObject;
            }

            var targetPosition = new Vector3(LanePositions[i], -0.05f, 60f);
            var targetScale = new Vector3(0.08f, 0.02f, 760f);

            if (guideObject.transform.position != targetPosition || guideObject.transform.localScale != targetScale)
            {
                Undo.RecordObject(guideObject.transform, "Style Soft Lane Guide");
                guideObject.transform.position = targetPosition;
                guideObject.transform.rotation = Quaternion.identity;
                guideObject.transform.localScale = targetScale;
                EditorUtility.SetDirty(guideObject.transform);
                changed = true;
            }

            var renderer = guideObject.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != material)
            {
                Undo.RecordObject(renderer, "Assign Soft Lane Material");
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
                changed = true;
            }
        }

        return changed;
    }

    private static bool IsTargetLane(string objectName)
    {
        for (var i = 2; i <= 8; i++)
        {
            if (objectName == $"Cylinder ({i})")
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject FindObjectInScene(string objectName)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static Material CreateSoftLaneMaterial(Material source)
    {
        var materialPath = $"Assets/Resources/GeneratedShieldAura/{GuideMaterialName}.mat";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/GeneratedShieldAura"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "GeneratedShieldAura");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            material = new Material(shader) { name = GuideMaterialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        var color = source.color;
        color.a = 0.28f;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 2f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material FindLaneMaterial()
    {
        var guids = AssetDatabase.FindAssets($"{MaterialName} t:Material");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.name == MaterialName)
            {
                return material;
            }
        }

        return null;
    }
}
