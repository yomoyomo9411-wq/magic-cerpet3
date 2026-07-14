using UnityEngine;
using System.Collections;

public class MagicCarpetGameFlow : MonoBehaviour
{
    public enum DevelopmentStartMode
    {
        Tutorial,
        MainReady,
        MainImmediately
    }

    private static MagicCarpetGameFlow instance;
    [Header("Tutorial Zone")]
    public float tutorialStartZ = -250f;
    public float mainStartZ = 0f;

    [Header("Audio")]
    public AudioSource bgmSource;
    public float bgmFadeOutSeconds = 4f;

    [Header("Development")]
    public DevelopmentStartMode developmentStartMode = DevelopmentStartMode.Tutorial;

    [Header("Main Game Start")]
    public KeyCode startKey = KeyCode.Return;

    public float autoMainStartSeconds = 10f;
    private float autoMainStartAt;

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
    public float resultDisplaySeconds = 1f;
    public int circleChallengeMaxOtherResults = 3;
    public float circleChallengeAttemptSeconds = 5f;
    [Tooltip("〇判定で画面が止まってから、Pythonの結果を受け付け始めるまでの秒数")]
    public float circleChallengeResultDelaySeconds = 1f;
    public int circleChallengeFirstTryScore = 100;
    public int circleChallengeSecondTryScore = 80;
    public int circleChallengeThirdTryScore = 60;

    private readonly System.Collections.Generic.List<GameObject> mainStartObjects
    = new System.Collections.Generic.List<GameObject>();

    private bool mainStartObjectsResolved;

    private Transform player;
    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;
    private bool hasInitialPlayerPose;
    private bool waitingForMainStart;
    private bool mainGameStarted;
    private bool waitingForTutorialPause;
    private float tutorialPauseEndsAt;
    private bool showingTutorialResult;
    private float tutorialResultEndsAt;
    private float tutorialResultLockEndsAt;
    private bool waitingForCircleChallenge;
    private int circleChallengeOtherResults;
    private float circleChallengeAcceptResultsAt;
    private float circleChallengeStopAt;
    private float activeCircleChallengeAttemptSeconds;
    private int pendingCircleChallengeShieldScore;
    private bool waitingAtTitleScreen;
    private bool tutorialFailureBlocksSuccess;

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
        PauseCircleChallenge(promptObjectName, 0f);
    }

    public static void PauseCircleChallenge(string promptObjectName, float attemptSeconds)
    {
        if (instance == null || instance.waitingForMainStart)
        {
            return;
        }

        instance.StartCircleChallengePause(promptObjectName, attemptSeconds);
    }

    public static void ReportTutorialSuccess()
    {
        if (instance == null || instance.mainGameStarted || instance.waitingForMainStart || instance.tutorialFailureBlocksSuccess)
        {
            return;
        }

        instance.ShowTutorialResultWithoutPause(instance.successObject);
    }

    public static void HidePracticePrompt()
    {
        if (instance == null || instance.mainGameStarted)
        {
            return;
        }

        if (instance.practiceObject != null)
        {
            instance.practiceObject.SetActive(false);
        }
    }

    public static void ReportTutorialFailure()
    {
        if (instance == null || instance.mainGameStarted || instance.waitingForMainStart || instance.IsResultLocked())
        {
            return;
        }

        instance.tutorialFailureBlocksSuccess = true;
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
            instance.circleChallengeStopAt = 0f;

            instance.ShowTutorialResultWithoutPause(instance.successObject);
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
        instance.circleChallengeStopAt = 0f;
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
            initialPlayerPosition = player.position;
            initialPlayerRotation = player.rotation;
            hasInitialPlayerPose = true;
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
        tutorialFailureBlocksSuccess = false;
        waitingAtTitleScreen = false;
        ValidateUiReferences();
        SetMainStartObjectsVisible(false);
        ShowOnly(practiceObject);
        Time.timeScale = 1f;
        ApplyDevelopmentStartMode();
    }

    private void Update()
    {
        if (waitingForTutorialPause)
        {
            if (waitingForCircleChallenge)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SkipCircleChallenge();
                }

                if (circleChallengeStopAt > 0f && Time.unscaledTime >= circleChallengeStopAt)
                {
                    FinishCircleChallengeWithoutSuccess();
                }

                return;
            }

            if (Time.unscaledTime >= tutorialPauseEndsAt)
            {
                waitingForTutorialPause = false;
                waitingForCircleChallenge = false;
                circleChallengeOtherResults = 0;
                circleChallengeAcceptResultsAt = 0f;
                circleChallengeStopAt = 0f;
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

        if (Input.GetKeyDown(startKey)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space)
            || Time.unscaledTime >= autoMainStartAt)
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
        tutorialFailureBlocksSuccess = false;
        waitingForMainStart = true;
        SetMainStartObjectsVisible(true);
        ShowOnly(honbanObject);
        Time.timeScale = 0f;
        autoMainStartAt = Time.unscaledTime + autoMainStartSeconds;

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
            SetMainStartObjectsVisible(true);
            ShowOnly(null);
            var carpetMove = player != null ? player.GetComponent<CarpetMove>() : null;
            if (carpetMove != null)
            {
                carpetMove.SetHpVisible(true);
            }
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
        circleChallengeStopAt = waitingForCircleChallenge
            ? GetCircleChallengeStopTime()
            : 0f;
        pendingCircleChallengeShieldScore = 0;
        tutorialFailureBlocksSuccess = false;
        tutorialPauseEndsAt = Time.unscaledTime + seconds;
        ShowOnly(GetPromptObject(promptObjectName));
        Time.timeScale = 0f;

        if (waitingForCircleChallenge)
        {
            MagicCarpetPoseController.StartCircleDetection();
        }

        Debug.Log($"Tutorial paused for {seconds:0.0} seconds.");
    }

    private void StartCircleChallengePause(string promptObjectName, float attemptSeconds)
    {
        waitingForTutorialPause = true;
        showingTutorialResult = false;
        waitingForCircleChallenge = true;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = Time.unscaledTime + Mathf.Max(0f, circleChallengeResultDelaySeconds);
        pendingCircleChallengeShieldScore = 0;
        activeCircleChallengeAttemptSeconds = attemptSeconds > 0f
            ? attemptSeconds
            : circleChallengeAttemptSeconds;
        circleChallengeStopAt = GetCircleChallengeStopTime();
        tutorialFailureBlocksSuccess = false;
        tutorialPauseEndsAt = Time.unscaledTime;
        ShowOnly(GetPromptObject(string.IsNullOrWhiteSpace(promptObjectName) ? stekkiObjectName : promptObjectName));
        Time.timeScale = 0f;
        MagicCarpetPoseController.StartCircleDetection();

        Debug.Log("Circle challenge paused the game.");
    }

    private void SkipCircleChallenge()
    {
        waitingForCircleChallenge = false;
        waitingForTutorialPause = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        circleChallengeStopAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Circle challenge skipped by Enter.");
    }

    private float GetCircleChallengeStopTime()
    {
        return Time.unscaledTime
            + Mathf.Max(0f, circleChallengeResultDelaySeconds)
            + Mathf.Max(1f, activeCircleChallengeAttemptSeconds) * Mathf.Max(1, circleChallengeMaxOtherResults)
            + 0.5f;
    }

    private void FinishCircleChallengeWithoutSuccess()
    {
        waitingForCircleChallenge = false;
        waitingForTutorialPause = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        circleChallengeStopAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Circle challenge finished without success.");
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
        Time.timeScale = 1f;
    }

    private void ShowTutorialResultWithoutPause(GameObject resultObject)
    {
        if (resultObject == null)
        {
            return;
        }

        waitingForTutorialPause = false;
        showingTutorialResult = true;
        tutorialResultEndsAt = Time.unscaledTime + resultDisplaySeconds;
        tutorialResultLockEndsAt = tutorialResultEndsAt + 0.25f;
        ShowOnly(resultObject);
        Time.timeScale = 1f;
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
        SetMainStartObjectsVisible(true);
        ShowOnly(null);
        var carpetMove = player != null ? player.GetComponent<CarpetMove>() : null;
        if (carpetMove != null)
        {
            carpetMove.SetHpVisible(true);
        }
        Time.timeScale = 1f;

        if (bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.Play();
        }

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
        tutorialFailureBlocksSuccess = false;
        tutorialResultLockEndsAt = 0f;
        waitingAtTitleScreen = true;

        if (player != null)
        {
            if (hasInitialPlayerPose)
            {
                player.SetPositionAndRotation(initialPlayerPosition, initialPlayerRotation);
            }
            else
            {
                var position = player.position;
                position.x = 0f;
                position.z = tutorialStartZ;
                player.position = position;
                player.rotation = Quaternion.identity;
            }
        }

        ResetSpawners();
        ClearActiveBullets();
        SetMainStartObjectsVisible(false);
        ShowOnly(titleObject);
        Time.timeScale = 0f;

        var carpetMove = player != null ? player.GetComponent<CarpetMove>() : null;
        if (carpetMove != null)
        {
            carpetMove.SetHpVisible(false);
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
        }

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
        tutorialFailureBlocksSuccess = false;
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

    private void ValidateUiReferences()
    {
        if (titleObject == null) Debug.LogError("Title Object is not assigned.", this);
        if (practiceObject == null) Debug.LogError("Practice Object is not assigned.", this);
        if (rightObject == null) Debug.LogError("Right Object is not assigned.", this);
        if (leftObject == null) Debug.LogError("Left Object is not assigned.", this);
        if (middleObject == null) Debug.LogError("Middle Object is not assigned.", this);
        if (stekkiObject == null) Debug.LogError("Stekki Object is not assigned.", this);
        if (honbanObject == null) Debug.LogError("Honban Object is not assigned.", this);
        if (successObject == null) Debug.LogError("Success Object is not assigned.", this);
        if (failureObject == null) Debug.LogError("Failure Object is not assigned.", this);
    }

    private void ResolveMainStartObjects()
    {
        if (mainStartObjectsResolved)
        {
            return;
        }

        mainStartObjects.Clear();

        foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsLoadedSceneObject(obj))
            {
                continue;
            }

            string name = obj.name.ToLower();

            if (name.Contains("castle") || name.Contains("plane"))
            {
                mainStartObjects.Add(obj);
            }
        }

        mainStartObjectsResolved = mainStartObjects.Count > 0;

        Debug.Log($"Main background objects found: {mainStartObjects.Count}");
    }

    private bool IsLoadedSceneObject(GameObject target)
    {
        return target != null && target.scene.IsValid() && target.scene.isLoaded;
    }

    private void SetMainStartObjectsVisible(bool visible)
    {
        ResolveMainStartObjects();

        int changedCount = 0;

        foreach (var target in mainStartObjects)
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

        return null;
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
            target.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    public static void FadeOutBgm()
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.FadeOutBgmRoutine());
        }
    }

    private IEnumerator FadeOutBgmRoutine()
    {
        if (bgmSource == null)
        {
            yield break;
        }

        float startVolume = bgmSource.volume;

        while (bgmSource.volume > 0f)
        {
            bgmSource.volume -= startVolume * Time.unscaledDeltaTime / bgmFadeOutSeconds;
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = startVolume;
    }
}

