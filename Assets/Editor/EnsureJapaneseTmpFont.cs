using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class EnsureJapaneseTmpFont
{
    private const string FontPath = "Assets/Fonts/YuGothM.ttc";
    private const string TmpFontPath = "Assets/Fonts/YuGothM Dynamic SDF.asset";
    private const string WindowsFontPath = "C:/Windows/Fonts/YuGothM.ttc";
    private const string CommonCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" +
        "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん" +
        "ぁぃぅぇぉゃゅょっアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "成功失敗惜右左中真杖次球体避飛魔法絨毯空練習開始本番押止再開入力円判定無傷" +
        "エンタースペースキーゲームチュートリアルステッキ！!？?、。ー・：:";

    static EnsureJapaneseTmpFont()
    {
        EditorApplication.delayCall += Apply;
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) =>
        {
            EditorApplication.delayCall += Apply;
        };
    }

    private static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || (scene.name != "DemoSkeletonScene" && scene.name != "SampleScene"))
        {
            return;
        }

        var fontAsset = EnsureFontAsset();
        if (fontAsset == null)
        {
            Debug.LogWarning("Japanese TMP font asset could not be created.");
            return;
        }

        var changed = false;
        var sceneCharacters = CommonCharacters;
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sceneCharacters += text.text;
            if (text.font == fontAsset)
            {
                continue;
            }

            Undo.RecordObject(text, "Assign Japanese TMP Font");
            text.font = fontAsset;
            EditorUtility.SetDirty(text);
            changed = true;
        }

        if (!string.IsNullOrEmpty(sceneCharacters))
        {
            fontAsset.TryAddCharacters(sceneCharacters, out var missingCharacters);
            if (!string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogWarning("Some Japanese TMP characters could not be added: " + missingCharacters);
            }

            EditorUtility.SetDirty(fontAsset);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static TMP_FontAsset EnsureFontAsset()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (fontAsset != null && IsUsableFontAsset(fontAsset))
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        string originalMeta = null;
        string fontAssetMetaPath = TmpFontPath + ".meta";
        if (File.Exists(fontAssetMetaPath))
        {
            originalMeta = File.ReadAllText(fontAssetMetaPath);
        }

        if (fontAsset != null)
        {
            AssetDatabase.DeleteAsset(TmpFontPath);
            AssetDatabase.Refresh();
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            if (!CopyWindowsFontIntoProject())
            {
                return null;
            }

            AssetDatabase.ImportAsset(FontPath);
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                return null;
            }
        }

        fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);
        fontAsset.name = "YuGothM Dynamic SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(fontAsset, TmpFontPath);

        if (fontAsset.material != null)
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, TmpFontPath);
        }

        foreach (var atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, TmpFontPath);
            }
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(originalMeta))
        {
            File.WriteAllText(fontAssetMetaPath, originalMeta);
            AssetDatabase.Refresh();
            fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        }

        return fontAsset;
    }

    private static bool IsUsableFontAsset(TMP_FontAsset fontAsset)
    {
        return fontAsset != null
            && fontAsset.material != null
            && fontAsset.atlasTextures != null
            && fontAsset.atlasTextures.Length > 0;
    }

    private static bool CopyWindowsFontIntoProject()
    {
        if (!File.Exists(WindowsFontPath))
        {
            return false;
        }

        Directory.CreateDirectory("Assets/Fonts");
        File.Copy(WindowsFontPath, FontPath, true);
        return true;
    }
}
