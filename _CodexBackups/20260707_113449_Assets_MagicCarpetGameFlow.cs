using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class MagicCarpetGameFlow : MonoBehaviour
{
    public enum DevelopmentStartMode
    {
        Tutorial,
        MainReady,
        MainImmediately
    }

    private static MagicCarpetGameFlow instance;
    private const string RuntimeLaneLineName = "Runtime Transparent Lane Line";

    [Header("Tutorial Zone")]
    public float tutorialStartZ = -250f;
    public float mainStartZ = 0f;

    [Header("Development")]
    public DevelopmentStartMode developmentStartMode = DevelopmentStartMode.Tutorial;

    [Header("Main Game Start")]
    public KeyCode startKey = KeyCode.Return;

    [Header("Tutorial Prompts")]
    public string titleObjectName = "Title";
    public string practiceObjectName = "Practice";
    public string rightObjectName = "Right";
    public string leftObjectName = "Left";
    public string middleObjectName = "Middle";
    public string stekkiObjectName = "stekki";
    public string honbanObjectName = "honban";
    public string successObjectName = "Success";
    public string failureObjectName = "Failure";
    public GameObject titleObject;
    public GameObject practiceObject;
    public GameObject rightObject;
    public GameObject leftObject;
    public GameObject middleObject;
    public GameObject stekkiObject;
    public GameObject honbanObject;
    public GameObject successObject;
    public GameObject failureObject;
    public string successText = "せいこう！";
    public string failureText = "おしい！";
    public float resultDisplaySeconds = 1f;
    public int circleChallengeMaxOtherResults = 3;
    [Tooltip("〇判定で画面が止まってから、Pythonの結果を受け付け始めるまでの秒数")]
    public float circleChallengeResultDelaySeconds = 1f;
    public int circleChallengeFirstTryScore = 100;
    public int circleChallengeSecondTryScore = 80;
    public int circleChallengeThirdTryScore = 60;

    [Header("Main Game Objects")]
    public string[] showAtMainStartObjectNames = new[]
    {
        "castle",
        "castle (1)",
        "castle (2)",
        "castle (3)",
        "castle (4)",
        "castle (5)",
        "Castle 0",
        "Castle 1",
        "Castle 2",
        "Castle 3",
        "Castle 4",
        "Castle 5",
        "plane",
        "plane (1)",
        "plane (2)",
        "plane (3)",
        "plane (4)",
        "plane (5)",
        "Plane 0",
        "Plane 1",
        "Plane 2",
        "Plane 3",
        "Plane 4",
        "Plane 5"
    };
    public GameObject[] showAtMainStartObjects;
    public string[] showAtMainStartNameKeywords = new[]
    {
        "castle",
        "plane"
    };

    private Transform player;
    private bool waitingForMainStart;
    private bool mainGameStarted;
    private bool waitingForTutorialPause;
    private float tutorialPauseEndsAt;
    private bool showingTutorialResult;
    private float tutorialResultEndsAt;
    private float tutorialResultLockEndsAt;
    private float laneTransparencyRefreshUntil;
    private bool waitingForCircleChallenge;
    private int circleChallengeOtherResults;
    private float circleChallengeAcceptResultsAt;
    private int pendingCircleChallengeShieldScore;
    private bool waitingAtTitleScreen;
    private Material laneTransparentMaterial;

    public static bool IsMainGameStarted => instance == null || instance.mainGameStarted;
    public static bool HasReachedMainStartLine => instance != null && (instance.waitingForMainStart || instance.mainGameStarted);
    public static bool IsCarpetMovementAllowed => instance == null || (!instance.waitingForMainStart && !instance.waitingForTutorialPause && !instance.waitingAtTitleScreen);
    public static bool IsCircleChallengeActive => instance != null && instance.waitingForCircleChallenge;
    public static bool IsCircleChallengeAcceptingResults => instance != null
        && instance.waitingForCircleChallenge
        && Time.unscaledTime >= instance.circleChallengeAcceptResultsAt;
    public static bool IsTutorialResultLocked => instance != null && instance.IsResultLocked();

    public static void PauseTutorialForSeconds(float seconds)
    {
        PauseTutorialForSeconds(seconds, null);
    }

    public static void PauseTutorialForSeconds(float seconds, string promptObjectName)
    {
        if (instance == null || instance.mainGameStarted || instance.waitingForMainStart || seconds <= 0f)
        {
            return;
        }

        instance.StartTutorialPause(seconds, promptObjectName);
    }

    public static void PauseCircleChallenge(string promptObjectName = null)
    {
        if (instance == null || instance.waitingForMainStart)
        {
            return;
        }

        instance.StartCircleChallengePause(promptObjectName);
    }

    public static void ReportTutorialSuccess()
    {
        if (instance == null || instance.mainGameStarted || instance.waitingForMainStart || instance.IsResultLocked())
        {
            return;
        }

        instance.ShowTutorialResult(instance.successObject, false);
    }

    public static void ReportTutorialFailure()
    {
        if (instance == null || instance.mainGameStarted || instance.waitingForMainStart || instance.IsResultLocked())
        {
            return;
        }

        instance.ShowTutorialResult(instance.failureObject, true);
    }

    public static void ReportCircleChallengeResult(bool success)
    {
        if (instance == null || !instance.waitingForCircleChallenge || !IsCircleChallengeAcceptingResults || instance.IsResultLocked())
        {
            return;
        }

        if (success)
        {
            instance.pendingCircleChallengeShieldScore = instance.GetCircleChallengeScoreForAttempt(instance.circleChallengeOtherResults + 1);
            instance.waitingForCircleChallenge = false;
            instance.waitingForTutorialPause = false;
            instance.circleChallengeOtherResults = 0;
            instance.circleChallengeAcceptResultsAt = 0f;
            Time.timeScale = 1f;
            instance.ShowTutorialResult(instance.successObject, true);
            return;
        }

        instance.circleChallengeOtherResults++;
        if (instance.circleChallengeOtherResults < Mathf.Max(1, instance.circleChallengeMaxOtherResults))
        {
            instance.ShowOnly(instance.stekkiObject);
            return;
        }

        instance.waitingForCircleChallenge = false;
        instance.waitingForTutorialPause = false;
        instance.circleChallengeOtherResults = 0;
        instance.circleChallengeAcceptResultsAt = 0f;
        instance.pendingCircleChallengeShieldScore = 0;
        instance.ShowOnly(null);
        Time.timeScale = 1f;
    }

    public static int ConsumeCircleChallengeShieldScore(int defaultScore)
    {
        if (instance == null || instance.pendingCircleChallengeShieldScore <= 0)
        {
            return defaultScore;
        }

        var score = instance.pendingCircleChallengeShieldScore;
        instance.pendingCircleChallengeShieldScore = 0;
        return score;
    }

    public static void ResetToTitleScreenForNextRun()
    {
        if (instance != null)
        {
            instance.ResetToTitleScreenInternal();
        }
    }

    public static void StartNextRunFromTitle()
    {
        if (instance != null)
        {
            instance.StartNextRunFromTitleInternal();
        }
    }

    private void Awake()
    {
        instance = this;
        player = FindFirstObjectByType<CarpetMove>()?.transform;
        if (player != null)
        {
            var position = player.position;
            position.z = tutorialStartZ;
            player.position = position;
        }

        mainGameStarted = false;
        waitingForMainStart = false;
        waitingForTutorialPause = false;
        showingTutorialResult = false;
        tutorialResultLockEndsAt = 0f;
        waitingForCircleChallenge = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        waitingAtTitleScreen = false;
        ResolveUiObjects();
        ResolveMainStartObjects();
        EnsureUiObjects();
        ApplyLaneTransparency();
        laneTransparencyRefreshUntil = Time.unscaledTime + 5f;
        SetMainStartObjectsVisible(false);
        ShowOnly(practiceObject);
        Time.timeScale = 1f;
        ApplyDevelopmentStartMode();
    }

    private void Update()
    {
        if (Time.unscaledTime < laneTransparencyRefreshUntil || Time.frameCount % 60 == 0)
        {
            ApplyLaneTransparency();
        }

        if (waitingForTutorialPause)
        {
            if (waitingForCircleChallenge)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SkipCircleChallenge();
                }

                return;
            }

            if (Time.unscaledTime >= tutorialPauseEndsAt)
            {
                waitingForTutorialPause = false;
                waitingForCircleChallenge = false;
                circleChallengeOtherResults = 0;
                circleChallengeAcceptResultsAt = 0f;
                pendingCircleChallengeShieldScore = 0;
                ShowOnly(null);
                Time.timeScale = 1f;
            }

            return;
        }

        if (showingTutorialResult && Time.unscaledTime >= tutorialResultEndsAt)
        {
            showingTutorialResult = false;
            ShowOnly(null);
            Time.timeScale = 1f;
        }

        if (mainGameStarted)
        {
            return;
        }

        if (!waitingForMainStart)
        {
            if (player != null && player.position.z >= mainStartZ)
            {
                StopAtMainStartLine();
            }

            return;
        }

        if (Input.GetKeyDown(startKey) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            StartMainGame();
        }
    }

    private void StopAtMainStartLine()
    {
        waitingForTutorialPause = false;
        waitingForCircleChallenge = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        waitingForMainStart = true;
        ResolveMainStartObjects();
        SetMainStartObjectsVisible(true);
        ShowOnly(honbanObject);
        Time.timeScale = 0f;

        if (player != null)
        {
            var position = player.position;
            position.z = mainStartZ;
            player.position = position;
        }

        Debug.Log("Tutorial finished. Press Enter to start the main game.");
    }

    public void StartTutorial()
    {
        ShowOnly(practiceObject);
        Time.timeScale = 1f;

        Debug.Log("Tutorial started.");
    }

    private void ApplyDevelopmentStartMode()
    {
        if (developmentStartMode == DevelopmentStartMode.Tutorial)
        {
            return;
        }

        if (player != null)
        {
            var position = player.position;
            position.z = mainStartZ;
            player.position = position;
        }

        if (developmentStartMode == DevelopmentStartMode.MainReady)
        {
            StopAtMainStartLine();
            Debug.Log("Development start mode: main ready.");
            return;
        }

        if (developmentStartMode == DevelopmentStartMode.MainImmediately)
        {
            waitingForTutorialPause = false;
            waitingForCircleChallenge = false;
            circleChallengeOtherResults = 0;
            circleChallengeAcceptResultsAt = 0f;
            pendingCircleChallengeShieldScore = 0;
            waitingForMainStart = false;
            mainGameStarted = true;
            ResolveMainStartObjects();
            SetMainStartObjectsVisible(true);
            ShowOnly(null);
            Time.timeScale = 1f;
            Debug.Log("Development start mode: main immediately.");
        }
    }

    private void StartTutorialPause(float seconds, string promptObjectName)
    {
        waitingForTutorialPause = true;
        showingTutorialResult = false;
        waitingForCircleChallenge = promptObjectName != null && promptObjectName.Equals(stekkiObjectName, System.StringComparison.OrdinalIgnoreCase);
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = waitingForCircleChallenge
            ? Time.unscaledTime + Mathf.Max(0f, circleChallengeResultDelaySeconds)
            : 0f;
        pendingCircleChallengeShieldScore = 0;
        tutorialPauseEndsAt = Time.unscaledTime + seconds;
        ShowOnly(GetPromptObject(promptObjectName));
        Time.timeScale = 0f;

        Debug.Log($"Tutorial paused for {seconds:0.0} seconds.");
    }

    private void StartCircleChallengePause(string promptObjectName)
    {
        waitingForTutorialPause = true;
        showingTutorialResult = false;
        waitingForCircleChallenge = true;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = Time.unscaledTime + Mathf.Max(0f, circleChallengeResultDelaySeconds);
        pendingCircleChallengeShieldScore = 0;
        tutorialPauseEndsAt = Time.unscaledTime;
        ShowOnly(GetPromptObject(string.IsNullOrWhiteSpace(promptObjectName) ? stekkiObjectName : promptObjectName));
        Time.timeScale = 0f;

        Debug.Log("Circle challenge paused the game.");
    }

    private void SkipCircleChallenge()
    {
        waitingForCircleChallenge = false;
        waitingForTutorialPause = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Circle challenge skipped by Enter.");
    }

    private void ShowTutorialResult(GameObject resultObject)
    {
        ShowTutorialResult(resultObject, false);
    }

    private void ShowTutorialResult(GameObject resultObject, bool allowDuringPause)
    {
        if (resultObject == null || (!allowDuringPause && waitingForTutorialPause))
        {
            return;
        }

        waitingForTutorialPause = false;
        showingTutorialResult = true;
        tutorialResultEndsAt = Time.unscaledTime + resultDisplaySeconds;
        tutorialResultLockEndsAt = tutorialResultEndsAt + 0.25f;
        ShowOnly(resultObject);
        Time.timeScale = 0f;
    }

    private bool IsResultLocked()
    {
        return showingTutorialResult || Time.unscaledTime < tutorialResultLockEndsAt;
    }

    private int GetCircleChallengeScoreForAttempt(int attempt)
    {
        if (attempt <= 1)
        {
            return circleChallengeFirstTryScore;
        }

        if (attempt == 2)
        {
            return circleChallengeSecondTryScore;
        }

        return circleChallengeThirdTryScore;
    }

    private void StartMainGame()
    {
        mainGameStarted = true;
        waitingForMainStart = false;
        waitingAtTitleScreen = false;
        ResolveMainStartObjects();
        SetMainStartObjectsVisible(true);
        ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Main game started.");
    }

    private void ResetToTitleScreenInternal()
    {
        mainGameStarted = false;
        waitingForMainStart = false;
        waitingForTutorialPause = false;
        waitingForCircleChallenge = false;
        showingTutorialResult = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        tutorialResultLockEndsAt = 0f;
        waitingAtTitleScreen = true;

        if (player != null)
        {
            var position = player.position;
            position.x = 0f;
            position.z = tutorialStartZ;
            player.position = position;
            player.rotation = Quaternion.identity;
        }

        ResetSpawners();
        ClearActiveBullets();
        SetMainStartObjectsVisible(false);
        ShowOnly(titleObject);
        Time.timeScale = 0f;

        Debug.Log("Returned to title screen. Press Enter again to start.");
    }

    private void StartNextRunFromTitleInternal()
    {
        if (!waitingAtTitleScreen)
        {
            return;
        }

        mainGameStarted = false;
        waitingForMainStart = false;
        waitingForTutorialPause = false;
        waitingForCircleChallenge = false;
        showingTutorialResult = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        tutorialResultLockEndsAt = 0f;
        waitingAtTitleScreen = false;

        ResetSpawners();
        ShowOnly(practiceObject);
        Time.timeScale = 1f;

        Debug.Log("Next run started.");
    }

    private void ResetSpawners()
    {
        foreach (var spawner in FindObjectsByType<BulletSpawner>(FindObjectsSortMode.None))
        {
            spawner.ResetSchedules();
        }
    }

    private void ClearActiveBullets()
    {
        foreach (var sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsLoadedSceneObject(sceneObject))
            {
                continue;
            }

            if (sceneObject.GetComponent<BallRuntimeController>() != null || sceneObject.CompareTag("Bullet"))
            {
                Destroy(sceneObject);
            }
        }
    }

    private void ResolveUiObjects()
    {
        titleObject = FindSceneObject(titleObjectName) ?? titleObject;
        practiceObject = FindSceneObject(practiceObjectName) ?? practiceObject;
        rightObject = FindSceneObject(rightObjectName) ?? rightObject;
        leftObject = FindSceneObject(leftObjectName) ?? leftObject;
        middleObject = FindSceneObject(middleObjectName) ?? middleObject;
        stekkiObject = FindSceneObject(stekkiObjectName) ?? stekkiObject;
        honbanObject = FindSceneObject(honbanObjectName) ?? honbanObject;
        successObject = FindSceneObject(successObjectName) ?? successObject;
        failureObject = FindSceneObject(failureObjectName) ?? failureObject;
    }

    private void ResolveMainStartObjects()
    {
        if (showAtMainStartObjectNames == null || showAtMainStartObjectNames.Length == 0)
        {
            showAtMainStartObjectNames = GetDefaultMainStartObjectNames();
        }
        else
        {
            showAtMainStartObjectNames = MergeMainStartObjectNames(showAtMainStartObjectNames);
        }

        if (showAtMainStartNameKeywords == null || showAtMainStartNameKeywords.Length == 0)
        {
            showAtMainStartNameKeywords = GetDefaultMainStartNameKeywords();
        }
        else
        {
            showAtMainStartNameKeywords = MergeMainStartKeywords(showAtMainStartNameKeywords);
        }

        if (showAtMainStartObjectNames == null || showAtMainStartObjectNames.Length == 0)
        {
            return;
        }

        var foundObjects = new System.Collections.Generic.List<GameObject>();
        foreach (var objectName in showAtMainStartObjectNames)
        {
            var foundObject = FindSceneObject(objectName);
            AddMainStartObject(foundObjects, foundObject);
        }

        if (showAtMainStartNameKeywords != null)
        {
            foreach (var sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsLoadedSceneObject(sceneObject))
                {
                    continue;
                }

                if (IsMainStartObjectByKeyword(sceneObject.name))
                {
                    AddMainStartObject(foundObjects, sceneObject);
                }
            }
        }

        showAtMainStartObjects = foundObjects.ToArray();
    }

    private bool IsMainStartObjectByKeyword(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || showAtMainStartNameKeywords == null)
        {
            return false;
        }

        foreach (var keyword in showAtMainStartNameKeywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && objectName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLoadedSceneObject(GameObject target)
    {
        return target != null && target.scene.IsValid() && target.scene.isLoaded;
    }

    private void AddMainStartObject(System.Collections.Generic.List<GameObject> foundObjects, GameObject target)
    {
        if (target == null || foundObjects.Contains(target))
        {
            return;
        }

        foreach (var existing in foundObjects)
        {
            if (existing != null && target.transform.IsChildOf(existing.transform))
            {
                return;
            }
        }

        for (var i = foundObjects.Count - 1; i >= 0; i--)
        {
            var existing = foundObjects[i];
            if (existing != null && existing.transform.IsChildOf(target.transform))
            {
                foundObjects.RemoveAt(i);
            }
        }

        foundObjects.Add(target);
    }

    private string[] GetDefaultMainStartObjectNames()
    {
        return new[]
        {
            "castle",
            "castle (1)",
            "castle (2)",
            "castle (3)",
            "castle (4)",
            "castle (5)",
            "Castle",
            "Castle (1)",
            "Castle (2)",
            "Castle (3)",
            "Castle (4)",
            "Castle (5)",
            "Castle 0",
            "Castle 1",
            "Castle 2",
            "Castle 3",
            "Castle 4",
            "Castle 5",
            "castle 0",
            "castle 1",
            "castle 2",
            "castle 3",
            "castle 4",
            "castle 5",
            "Castle0",
            "Castle1",
            "Castle2",
            "Castle3",
            "Castle4",
            "Castle5",
            "castle0",
            "castle1",
            "castle2",
            "castle3",
            "castle4",
            "castle5",
            "Castle_0",
            "Castle_1",
            "Castle_2",
            "Castle_3",
            "Castle_4",
            "Castle_5",
            "castle_0",
            "castle_1",
            "castle_2",
            "castle_3",
            "castle_4",
            "castle_5",
            "plane",
            "plane (1)",
            "plane (2)",
            "plane (3)",
            "plane (4)",
            "plane (5)",
            "Plane",
            "Plane (1)",
            "Plane (2)",
            "Plane (3)",
            "Plane (4)",
            "Plane (5)",
            "Plane 0",
            "Plane 1",
            "Plane 2",
            "Plane 3",
            "Plane 4",
            "Plane 5",
            "plane 0",
            "plane 1",
            "plane 2",
            "plane 3",
            "plane 4",
            "plane 5",
            "Plane0",
            "Plane1",
            "Plane2",
            "Plane3",
            "Plane4",
            "Plane5",
            "plane0",
            "plane1",
            "plane2",
            "plane3",
            "plane4",
            "plane5",
            "Plane_0",
            "Plane_1",
            "Plane_2",
            "Plane_3",
            "Plane_4",
            "Plane_5",
            "plane_0",
            "plane_1",
            "plane_2",
            "plane_3",
            "plane_4",
            "plane_5"
        };
    }

    private string[] GetDefaultMainStartNameKeywords()
    {
        return new[]
        {
            "castle",
            "plane",
            "medieval"
        };
    }

    private string[] MergeMainStartKeywords(string[] currentKeywords)
    {
        var merged = new System.Collections.Generic.List<string>();
        if (currentKeywords != null)
        {
            foreach (var keyword in currentKeywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && !ContainsKeyword(merged, keyword))
                {
                    merged.Add(keyword);
                }
            }
        }

        foreach (var keyword in GetDefaultMainStartNameKeywords())
        {
            if (!ContainsKeyword(merged, keyword))
            {
                merged.Add(keyword);
            }
        }

        return merged.ToArray();
    }

    private string[] MergeMainStartObjectNames(string[] currentObjectNames)
    {
        var merged = new System.Collections.Generic.List<string>();
        if (currentObjectNames != null)
        {
            foreach (var objectName in currentObjectNames)
            {
                if (!string.IsNullOrWhiteSpace(objectName) && !ContainsKeyword(merged, objectName))
                {
                    merged.Add(objectName);
                }
            }
        }

        foreach (var objectName in GetDefaultMainStartObjectNames())
        {
            if (!ContainsKeyword(merged, objectName))
            {
                merged.Add(objectName);
            }
        }

        return merged.ToArray();
    }

    private bool ContainsKeyword(System.Collections.Generic.List<string> keywords, string keyword)
    {
        foreach (var existing in keywords)
        {
            if (existing.Equals(keyword, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetMainStartObjectsVisible(bool visible)
    {
        ResolveMainStartObjects();

        if (showAtMainStartObjects == null)
        {
            return;
        }

        var changedCount = 0;
        foreach (var target in showAtMainStartObjects)
        {
            if (target != null)
            {
                SetSceneObjectVisible(target, visible);
                changedCount++;
            }
        }

        Debug.Log($"Main start background objects visible={visible}: {changedCount}");
    }

    private void SetSceneObjectVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        if (visible)
        {
            var parent = target.transform.parent;
            while (parent != null)
            {
                parent.gameObject.SetActive(true);
                parent = parent.parent;
            }
        }

        target.SetActive(visible);

        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void EnsureUiObjects()
    {
        titleObject ??= CreateTextObject(titleObjectName, "Title");
        practiceObject ??= CreateTextObject(practiceObjectName, "Practice");
        rightObject ??= CreateTextObject(rightObjectName, "Right");
        leftObject ??= CreateTextObject(leftObjectName, "Left");
        middleObject ??= CreateTextObject(middleObjectName, "Middle");
        stekkiObject ??= CreateTextObject(stekkiObjectName, "stekki");
        honbanObject ??= CreateTextObject(honbanObjectName, "honban");
        successObject ??= CreateTextObject(successObjectName, "Success");
        failureObject ??= CreateTextObject(failureObjectName, "Failure");

        SetPromptText(successObject, successText);
        SetPromptText(failureObject, failureText);

        ApplyPromptTextStyle(titleObject);
        ApplyPromptTextStyle(practiceObject);
        ApplyPromptTextStyle(rightObject);
        ApplyPromptTextStyle(leftObject);
        ApplyPromptTextStyle(middleObject);
        ApplyPromptTextStyle(stekkiObject);
        ApplyPromptTextStyle(honbanObject);
        ApplyPromptTextStyle(successObject);
        ApplyPromptTextStyle(failureObject);
    }

    private void SetPromptText(GameObject target, string text)
    {
        var label = target != null ? target.GetComponent<TMP_Text>() : null;
        if (label != null && !string.IsNullOrWhiteSpace(text))
        {
            label.text = text;
        }
    }

    private Transform FindChildByName(Transform root, string objectName)
    {
        if (root.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        foreach (Transform child in root)
        {
            var found = FindChildByName(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var found = FindChildByName(root.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private GameObject GetPromptObject(string promptObjectName)
    {
        if (string.IsNullOrWhiteSpace(promptObjectName))
        {
            return null;
        }

        if (promptObjectName.Equals(rightObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return rightObject;
        }

        if (promptObjectName.Equals(leftObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return leftObject;
        }

        if (promptObjectName.Equals(middleObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return middleObject;
        }

        if (promptObjectName.Equals(stekkiObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return stekkiObject;
        }

        if (promptObjectName.Equals(honbanObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return honbanObject;
        }

        if (promptObjectName.Equals(practiceObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return practiceObject;
        }

        if (promptObjectName.Equals(titleObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return titleObject;
        }

        if (promptObjectName.Equals(successObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return successObject;
        }

        if (promptObjectName.Equals(failureObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return failureObject;
        }

        return FindSceneObject(promptObjectName);
    }

    private void ShowOnly(GameObject visibleObject)
    {
        SetVisible(titleObject, visibleObject == titleObject);
        SetVisible(practiceObject, visibleObject == practiceObject);
        SetVisible(rightObject, visibleObject == rightObject);
        SetVisible(leftObject, visibleObject == leftObject);
        SetVisible(middleObject, visibleObject == middleObject);
        SetVisible(stekkiObject, visibleObject == stekkiObject);
        SetVisible(honbanObject, visibleObject == honbanObject);
        SetVisible(successObject, visibleObject == successObject);
        SetVisible(failureObject, visibleObject == failureObject);
    }

    private void SetVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            if (visible)
            {
                EnsureParentsVisible(target);
                ApplyPromptTextStyle(target);
            }

            target.SetActive(visible);
        }
    }

    private void EnsureParentsVisible(GameObject target)
    {
        var parent = target.transform.parent;
        while (parent != null)
        {
            parent.gameObject.SetActive(true);
            parent = parent.parent;
        }
    }

    private void ApplyLaneTransparency()
    {
        SetAllLaneCylindersTransparency(0.08f);
        SetSoftLaneGuidesTransparency(0.1f);
    }

    private void SetAllLaneCylindersTransparency(float alpha)
    {
        foreach (var sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsLoadedSceneObject(sceneObject))
            {
                continue;
            }

            if (sceneObject.name.StartsWith("Cylinder", System.StringComparison.OrdinalIgnoreCase))
            {
                SetLaneObjectTransparency(sceneObject, alpha);
            }
        }
    }

    private void SetSoftLaneGuidesTransparency(float alpha)
    {
        foreach (var sceneObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsLoadedSceneObject(sceneObject))
            {
                continue;
            }

            if (sceneObject.name.StartsWith("Soft Lane Guide", System.StringComparison.OrdinalIgnoreCase))
            {
                foreach (var renderer in sceneObject.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.enabled = true;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sharedMaterial = GetLaneTransparentMaterial(alpha);
                }
            }
        }
    }

    private void SetLaneObjectTransparency(string objectName, float alpha)
    {
        var target = FindSceneObject(objectName);
        if (target == null)
        {
            return;
        }

        SetLaneObjectTransparency(target, alpha);
    }

    private void SetLaneObjectTransparency(GameObject target, float alpha)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is LineRenderer || renderer.gameObject.name == RuntimeLaneLineName)
            {
                continue;
            }

            renderer.enabled = false;
        }

        CreateOrUpdateLaneLine(target, alpha);
    }

    private void CreateOrUpdateLaneLine(GameObject laneObject, float alpha)
    {
        var lineTransform = laneObject.transform.Find(RuntimeLaneLineName);
        var lineObject = lineTransform != null ? lineTransform.gameObject : new GameObject(RuntimeLaneLineName);
        lineObject.transform.SetParent(laneObject.transform, false);

        var line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = lineObject.AddComponent<LineRenderer>();
        }

        var lineAlpha = Mathf.Clamp01(Mathf.Max(alpha, 0.18f));
        var color = new Color(1f, 0.05f, 0.05f, lineAlpha);
        var width = Mathf.Clamp(laneObject.transform.lossyScale.x * 0.35f, 0.06f, 0.25f);

        line.enabled = true;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, laneObject.transform.TransformPoint(Vector3.down));
        line.SetPosition(1, laneObject.transform.TransformPoint(Vector3.up));
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = GetLaneTransparentMaterial(lineAlpha);
        line.startColor = color;
        line.endColor = color;
    }

    private Material GetLaneTransparentMaterial(float alpha)
    {
        if (laneTransparentMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            laneTransparentMaterial = new Material(shader)
            {
                name = "Runtime Lane Transparent Material"
            };
        }

        ApplyTransparentMaterial(laneTransparentMaterial, alpha);
        return laneTransparentMaterial;
    }

    private void ApplyTransparentMaterial(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        var color = new Color(1f, 0.05f, 0.05f, 1f);
        color.a = Mathf.Clamp01(alpha);

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
            material.SetFloat("_Blend", 0f);
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

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_AlphaToMask"))
        {
            material.SetFloat("_AlphaToMask", 0f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void ApplyPromptTextStyle(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        var rect = target.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1200f, 260f);
            rect.localScale = Vector3.one;
        }

        var label = target.GetComponent<TMP_Text>();
        if (label != null)
        {
            var promptFont = FindPromptFont();
            if (promptFont != null)
            {
                label.font = promptFont;
            }

            label.fontSize = 36f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
        }
    }

    private TMP_FontAsset FindPromptFont()
    {
        return GetFont(titleObject)
            ?? GetFont(practiceObject)
            ?? GetFont(rightObject)
            ?? GetFont(leftObject)
            ?? GetFont(middleObject)
            ?? GetFont(stekkiObject)
            ?? GetFont(honbanObject)
            ?? GetFont(failureObject)
            ?? GetFont(successObject);
    }

    private TMP_FontAsset GetFont(GameObject target)
    {
        var label = target != null ? target.GetComponent<TMP_Text>() : null;
        if (label == null || label.font == null)
        {
            return null;
        }

        return label.font.name.Contains("YuGoth") ? label.font : null;
    }

    private GameObject CreateTextObject(string objectName, string text)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObject = new GameObject("Tutorial Text Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1100f, 240f);

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 36f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
        label.raycastTarget = false;

        textObject.SetActive(false);
        return textObject;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
