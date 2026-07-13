using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlaceShieldAuraPreview
{
    private const string PreviewName = "ShieldAuraPreview";

    static PlaceShieldAuraPreview()
    {
        EditorApplication.delayCall += EnsurePreviewExists;
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) =>
        {
            EditorApplication.delayCall += EnsurePreviewExists;
        };
    }

    private static void EnsurePreviewExists()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "DemoSkeletonScene")
        {
            return;
        }

        var preview = FindPreviewObject();
        if (preview == null)
        {
            var player = GameObject.Find("Player");
            preview = new GameObject(PreviewName);
            Undo.RegisterCreatedObjectUndo(preview, "Create Shield Aura Preview");

            if (player != null)
            {
                preview.transform.SetParent(player.transform, false);
                preview.transform.localPosition = new Vector3(0f, 2f, 5f);
            }
            else
            {
                preview.transform.position = new Vector3(0f, 2f, 5f);
            }

            preview.transform.localRotation = Quaternion.identity;
            preview.transform.localScale = Vector3.one;
            preview.SetActive(false);
        }

        RebuildPreview(preview.transform);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static GameObject FindPreviewObject()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == PreviewName)
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private static void RebuildPreview(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        CreateQuad(
            root,
            "Magic Circle Base",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Circles/Magic_Circle_1.png",
            new Color(0.72f, 0.22f, 1f, 0.52f),
            1.75f,
            0f,
            0f);

        CreateQuad(
            root,
            "Rune Ring",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Runes/Rune_1.png",
            new Color(0.95f, 0.58f, 1f, 0.3f),
            1.52f,
            0.03f,
            12f);

        CreateQuad(
            root,
            "Inner Circle",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Circles/Magic_Circle_2.png",
            new Color(0.88f, 0.32f, 1f, 0.34f),
            1.08f,
            0.06f,
            -18f);

        CreateQuad(
            root,
            "Soft Rays",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/FX/Rays_2.png",
            new Color(0.5f, 0.18f, 1f, 0.12f),
            1.9f,
            -0.04f,
            0f);

        CreateQuad(
            root,
            "Center Glyph",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Glyphs/Glyph_2.png",
            new Color(1f, 0.72f, 1f, 0.48f),
            0.5f,
            0.09f,
            0f);

        CreateReferenceGeometry(root);
        CreateRotatingAccent(root);
        CreateSubtleParticles(root);
    }

    private static void CreateReferenceGeometry(Transform parent)
    {
        var crisp = new Color(0.84f, 0.24f, 1f, 0.45f);
        var soft = new Color(0.82f, 0.32f, 1f, 0.26f);
        var pale = new Color(1f, 0.64f, 1f, 0.36f);

        CreateCircleLine(parent, "Clean Outer Line", 0.9f, 96, crisp, 0.006f, 0.13f);
        CreateCircleLine(parent, "Clean Inner Line", 0.64f, 80, soft, 0.005f, 0.14f);
        CreatePolygonLine(parent, "Upright Triangle", 3, 0.62f, 90f, soft, 0.005f, 0.15f);
        CreatePolygonLine(parent, "Inverted Triangle", 3, 0.62f, -90f, soft, 0.005f, 0.16f);
        CreateSmallNodeCircles(parent, 6, 0.62f, 0.08f, pale, 0.004f, 0.18f);
        CreateCompassStar(parent, crisp, 0.005f, 0.2f);
    }

    private static void CreateRotatingAccent(Transform parent)
    {
        var accent = new GameObject("Slow Rune Accent");
        Undo.RegisterCreatedObjectUndo(accent, "Create Shield Aura Accent");
        accent.transform.SetParent(parent, false);
        accent.transform.localPosition = Vector3.zero;
        accent.transform.localRotation = Quaternion.identity;
        accent.transform.localScale = Vector3.one * 0.82f;

        var animator = accent.AddComponent<ShieldAuraPreviewAnimator>();
        animator.rotationSpeed = -8f;
        animator.pulseSpeed = 1.05f;
        animator.pulseAmount = 0.025f;

        CreateQuad(
            accent.transform,
            "Rotating Thin Rune",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Runes/Rune_7.png",
            new Color(0.92f, 0.42f, 1f, 0.18f),
            1.25f,
            0.12f,
            0f);
    }

    private static void CreateQuad(Transform parent, string name, string texturePath, Color color, float size, float zOffset, float zRotation)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Undo.RegisterCreatedObjectUndo(quad, "Create Shield Aura Quad");
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, 0f, zOffset);
        quad.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        quad.transform.localScale = new Vector3(size, size, 1f);

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        quad.GetComponent<MeshRenderer>().sharedMaterial = CreateAuraMaterial($"Generated {name}", texturePath, color);
    }

    private static void CreateCircleLine(Transform parent, string name, float radius, int segments, Color color, float width, float zOffset)
    {
        var points = new Vector3[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var angle = Mathf.PI * 2f * i / segments;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, zOffset);
        }

        CreateLine(parent, name, points, color, width);
    }

    private static void CreatePolygonLine(Transform parent, string name, int sides, float radius, float rotationDegrees, Color color, float width, float zOffset)
    {
        var points = new Vector3[sides + 1];
        var rotation = rotationDegrees * Mathf.Deg2Rad;
        for (var i = 0; i <= sides; i++)
        {
            var angle = rotation + Mathf.PI * 2f * i / sides;
            points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, zOffset);
        }

        CreateLine(parent, name, points, color, width);
    }

    private static void CreateRadialLines(Transform parent, string name, int count, float innerRadius, float outerRadius, Color color, float width, float zOffset)
    {
        var group = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(group, "Create Shield Aura Guide Lines");
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;

        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.PI * 2f * i / count;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            CreateLine(
                group.transform,
                $"Guide {i + 1:00}",
                new[] { direction * innerRadius + Vector3.forward * zOffset, direction * outerRadius + Vector3.forward * zOffset },
                color,
                width);
        }
    }

    private static void CreateSmallNodeCircles(Transform parent, int count, float radius, float nodeRadius, Color color, float width, float zOffset)
    {
        var group = new GameObject("Six Seal Nodes");
        Undo.RegisterCreatedObjectUndo(group, "Create Shield Aura Nodes");
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;

        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.PI * 2f * i / count + Mathf.PI * 0.5f;
            var node = new GameObject($"Seal Node {i + 1:00}");
            Undo.RegisterCreatedObjectUndo(node, "Create Shield Aura Node");
            node.transform.SetParent(group.transform, false);
            node.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            CreateCircleLine(node.transform, "Node Circle", nodeRadius, 36, color, width, zOffset);
            CreateCircleLine(node.transform, "Node Inner Circle", nodeRadius * 0.62f, 28, new Color(color.r, color.g, color.b, color.a * 0.75f), width * 0.75f, zOffset + 0.01f);
        }
    }

    private static void CreateCompassStar(Transform parent, Color color, float width, float zOffset)
    {
        var group = new GameObject("Central Compass Star");
        Undo.RegisterCreatedObjectUndo(group, "Create Shield Aura Compass");
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;

        CreateCircleLine(group.transform, "Central Ring", 0.22f, 48, new Color(color.r, color.g, color.b, color.a * 0.75f), width, zOffset);
        CreateCircleLine(group.transform, "Central Small Ring", 0.14f, 36, new Color(1f, 0.72f, 1f, 0.32f), width * 0.8f, zOffset + 0.01f);

        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.PI * 2f * i / 8f;
            var longPoint = new Vector3(Mathf.Cos(angle) * 0.26f, Mathf.Sin(angle) * 0.26f, zOffset + 0.02f);
            var shortLeft = new Vector3(Mathf.Cos(angle + 0.22f) * 0.08f, Mathf.Sin(angle + 0.22f) * 0.08f, zOffset + 0.02f);
            var shortRight = new Vector3(Mathf.Cos(angle - 0.22f) * 0.08f, Mathf.Sin(angle - 0.22f) * 0.08f, zOffset + 0.02f);
            CreateLine(group.transform, $"Star Point {i + 1:00}", new[] { shortLeft, longPoint, shortRight }, color, width);
        }
    }

    private static void CreateLine(Transform parent, string name, Vector3[] points, Color color, float width)
    {
        var lineObject = new GameObject(name, typeof(LineRenderer));
        Undo.RegisterCreatedObjectUndo(lineObject, "Create Shield Aura Line");
        lineObject.transform.SetParent(parent, false);
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localRotation = Quaternion.identity;
        lineObject.transform.localScale = Vector3.one;

        var line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.TransformZ;
        line.material = CreateLineMaterial("Generated Reference Shield Line", color);
        line.startColor = color;
        line.endColor = color;
    }

    private static void CreateSubtleParticles(Transform parent)
    {
        var particlesObject = new GameObject("Subtle Purple Sparks", typeof(ParticleSystem));
        Undo.RegisterCreatedObjectUndo(particlesObject, "Create Shield Aura Particles");
        particlesObject.transform.SetParent(parent, false);
        particlesObject.transform.localPosition = Vector3.zero;
        particlesObject.transform.localRotation = Quaternion.identity;

        var particles = particlesObject.GetComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.9f, 0.28f, 1f, 0.75f),
            new Color(0.45f, 0.18f, 1f, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = particles.emission;
        emission.rateOverTime = 7f;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.82f;
        shape.radiusThickness = 0.18f;

        var renderer = particlesObject.GetComponent<ParticleSystemRenderer>();
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.sharedMaterial = CreateAuraMaterial(
            "Generated Subtle Purple Spark",
            "Assets/AS_Magic_Aura_Construction_Kit_FREE/Particles/Particle_1.png",
            new Color(1f, 0.7f, 1f, 1f));
    }

    private static Material CreateAuraMaterial(string materialName, string texturePath, Color color)
    {
        EnsureGeneratedFolder();
        var materialPath = $"Assets/Resources/GeneratedShieldAura/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        ConfigureAuraMaterial(material, texturePath, color);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material CreateLineMaterial(string materialName, Color color)
    {
        EnsureGeneratedFolder();
        var materialPath = $"Assets/Resources/GeneratedShieldAura/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        ConfigureLineMaterial(material, color);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/GeneratedShieldAura"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "GeneratedShieldAura");
        }
    }

    private static void ConfigureLineMaterial(Material material, Color color)
    {
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
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.enableInstancing = true;
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureAuraMaterial(Material material, string texturePath, Color color)
    {
        material.color = color;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

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
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
    }
}
