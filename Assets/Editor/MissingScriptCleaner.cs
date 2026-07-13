using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptCleaner
{
    private const string SessionKey = "MissingScriptCleaner.CleanedPurchasedAssets";

    [InitializeOnLoadMethod]
    private static void CleanOnceAfterReload()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += CleanPurchasedAssetsAndOpenScene;
    }

    [MenuItem("Tools/Clean Missing Scripts")]
    public static void CleanPurchasedAssetsAndOpenScene()
    {
        var removedCount = 0;

        removedCount += CleanPrefabAssets("Assets/PurchasedAssets");
        removedCount += CleanOpenScene();

        if (removedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"Removed {removedCount} missing script reference(s).");
        }
    }

    private static int CleanPrefabAssets(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return 0;
        }

        var removedCount = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            var removed = CleanMissingScriptsInChildren(root);

            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                removedCount += removed;
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        return removedCount;
    }

    private static int CleanOpenScene()
    {
        var removedCount = 0;
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return 0;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            removedCount += CleanMissingScriptsInChildren(root);
        }

        if (removedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return removedCount;
    }

    private static int CleanMissingScriptsInChildren(GameObject root)
    {
        var removedCount = 0;
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
        }

        return removedCount;
    }
}
