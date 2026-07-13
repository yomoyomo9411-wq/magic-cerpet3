using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnsureMagicCarpetManagers
{
    private const string GameFlowName = "Magic Carpet Game Flow";
    private const string PoseInputName = "MediaPipe Pose Input";
    private const string JapaneseFontPath = "Assets/Fonts/YuGothM Dynamic SDF.asset";
    private const string LegacyJapaneseFontPath = "Assets/Fonts/YuGothM SDF.asset";

    static EnsureMagicCarpetManagers()
    {
        EditorApplication.delayCall += EnsureManagers;
        EditorSceneManager.activeSceneChangedInEditMode += (_, _) =>
        {
            EditorApplication.delayCall += EnsureManagers;
        };
    }

    private static void EnsureManagers()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "DemoSkeletonScene")
        {
            return;
        }

        var carpet = Object.FindFirstObjectByType<CarpetMove>();
        if (carpet == null)
        {
            return;
        }

        var changed = false;

        var gameFlow = FindObjectInScene(GameFlowName);
        if (gameFlow == null)
        {
            gameFlow = new GameObject(GameFlowName);
            Undo.RegisterCreatedObjectUndo(gameFlow, "Create Magic Carpet Game Flow");
            changed = true;
        }

        var flow = gameFlow.GetComponent<MagicCarpetGameFlow>();
        if (flow == null)
        {
            flow = Undo.AddComponent<MagicCarpetGameFlow>(gameFlow);
            changed = true;
        }

        if (Mathf.Approximately(flow.tutorialStartZ, -200f) || Mathf.Approximately(flow.tutorialStartZ, -300f))
        {
            Undo.RecordObject(flow, "Update Tutorial Start Z");
            flow.tutorialStartZ = -250f;
            EditorUtility.SetDirty(flow);
            changed = true;
        }

        changed |= MovePlayerToTutorialStart(carpet.transform, flow);

        var poseInput = FindObjectInScene(PoseInputName);
        if (poseInput == null)
        {
            poseInput = new GameObject(PoseInputName);
            Undo.RegisterCreatedObjectUndo(poseInput, "Create MediaPipe Pose Input");
            changed = true;
        }

        if (poseInput.GetComponent<MagicCarpetPoseController>() == null)
        {
            Undo.AddComponent<MagicCarpetPoseController>(poseInput);
            changed = true;
        }

        if (!Application.isPlaying)
        {
            changed |= SetPromptVisible("Title", true);
            changed |= SetPromptVisible("Practice", false);
            changed |= SetPromptVisible("Right", false);
            changed |= SetPromptVisible("Left", false);
            changed |= SetPromptVisible("Middle", false);
            changed |= SetPromptVisible("stekki", false);
            changed |= SetPromptVisible("honban", false);
            changed |= SetPromptVisible("Success", false);
            changed |= SetPromptVisible("Failure", false);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static bool MovePlayerToTutorialStart(Transform player, MagicCarpetGameFlow flow)
    {
        if (player == null || flow == null)
        {
            return false;
        }

        var position = player.position;
        if (!Mathf.Approximately(position.z, flow.mainStartZ) && !Mathf.Approximately(position.z, -200f) && !Mathf.Approximately(position.z, -300f))
        {
            return false;
        }

        position.z = flow.tutorialStartZ;
        Undo.RecordObject(player, "Move Player To Tutorial Start");
        player.position = position;
        EditorUtility.SetDirty(player);
        return true;
    }

    private static bool SetPromptVisible(string objectName, bool visible)
    {
        var prompt = FindObjectInScene(objectName);
        if (prompt == null)
        {
            return false;
        }

        var changed = false;
        if (prompt.activeSelf != visible)
        {
            Undo.RecordObject(prompt, "Set Tutorial Prompt Visibility");
            prompt.SetActive(visible);
            EditorUtility.SetDirty(prompt);
            changed = true;
        }

        var label = prompt.GetComponent<TMP_Text>();
        var fontAsset = GetJapaneseFontAsset();
        if (label != null && (label.color != Color.black || !Mathf.Approximately(label.fontSize, 36f) || (fontAsset != null && label.font != fontAsset)))
        {
            Undo.RecordObject(label, "Set Tutorial Prompt Style");
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            label.color = Color.black;
            label.fontSize = 36f;
            EditorUtility.SetDirty(label);
            changed = true;
        }

        return changed;
    }

    private static TMP_FontAsset GetJapaneseFontAsset()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontPath);
        if (fontAsset != null)
        {
            return fontAsset;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyJapaneseFontPath);
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
}
