using UnityEngine;
using System.Collections;
using TMPro;

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
    public string rule1ObjectName = "rule1";
    public string rule2ObjectName = "rule2";
    public string moveObjectName = "move";
    public string move2ObjectName = "move2";
    public string circleTestObjectName = "circletest";
    public string rightObjectName = "Right";
    public string leftObjectName = "Left";
    public string middleObjectName = "Middle";
    public string stekkiObjectName = "stekki";
    public string honbanObjectName = "honban";
    public string successObjectName = "Success";
    public string failureObjectName = "Failure";
    public GameObject titleObject;
    public GameObject practiceObject;
    [Header("Practice Text Style")]
    public Color32 practiceTextColor = new Color32(255, 230, 0, 255);
    public Color32 practiceOutlineColor = new Color32(0, 0, 0, 255);

    [Range(0f, 1f)]
    public float practiceOutlineWidth = 0.2f;
    public GameObject rule1Object;
    public GameObject rule2Object;
    public GameObject moveObject;
    public GameObject move2Object;
    public GameObject rightObject;
    public GameObject leftObject;
    public GameObject middleObject;
    public GameObject stekkiObject;
    public GameObject honbanObject;
    public GameObject circleTestObject;
    public GameObject successObject;
    public GameObject failureObject;
    public float resultDisplaySeconds = 0.8f;
    public float practiceDisplaySeconds = 2f;
    public float ruleMoveDisplaySeconds = 5f;
    public float rule2DisplaySeconds = 5f;
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
    private float activeCircleChallengeAttemptSeconds;
    private bool circleChallengeDetectionStarted;
    private int pendingCircleChallengeShieldScore;
    private bool waitingAtTitleScreen;
    private bool tutorialFailureBlocksSuccess;
    private Coroutine tutorialIntroCoroutine;
    private bool move2PromptShown;
    private bool tutorialSecondBallPassed;

    public static bool IsMainGameStarted => instance == null || instance.mainGameStarted;
    public static bool HasReachedMainStartLine => instance != null && (instance.waitingForMainStart || instance.mainGameStarted);
    public static bool IsCarpetMovementAllowed => instance == null || (!instance.waitingForMainStart && !instance.waitingForTutorialPause && !instance.waitingAtTitleScreen);
    public static bool IsCircleChallengeActive => instance != null && instance.waitingForCircleChallenge;
    public static bool IsCircleChallengeAcceptingResults => instance != null
        && instance.waitingForCircleChallenge
        && Time.unscaledTime >= instance.circleChallengeAcceptResultsAt;
    public static bool IsTutorialResultLocked => instance != null && instance.IsResultLocked();
    private Coroutine circleFailureCoroutine;

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

    public static void ReportCircleChallengeComplete()
    {
        if (instance == null || !instance.waitingForCircleChallenge)
        {
            return;
        }

        instance.waitingForCircleChallenge = false;
        instance.waitingForTutorialPause = false;
        instance.circleChallengeDetectionStarted = false;
        instance.circleChallengeOtherResults = 0;
        instance.circleChallengeAcceptResultsAt = 0f;
        instance.pendingCircleChallengeShieldScore = 0;

        if (instance.circleFailureCoroutine != null)
        {
            instance.StopCoroutine(instance.circleFailureCoroutine);
            instance.circleFailureCoroutine = null;
        }

        instance.ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Circle challenge completed after 3 failed attempts.");
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

        // Practiceは本番開始前まで残す
        instance.SetVisible(instance.rule1Object, false);
    }

    public static void HideMove2Prompt()
    {
        if (instance == null || instance.mainGameStarted)
        {
            return;
        }

        instance.tutorialSecondBallPassed = true;
        if (instance.move2PromptShown)
        {
            instance.SetVisible(instance.move2Object, false);
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

            instance.ShowTutorialResultWithoutPause(instance.successObject);
            return;
        }

        instance.circleChallengeOtherResults++;

        bool isLastAttempt =
            instance.circleChallengeOtherResults >=
            Mathf.Max(1, instance.circleChallengeMaxOtherResults);

        if (instance.circleFailureCoroutine != null)
        {
            instance.StopCoroutine(instance.circleFailureCoroutine);
        }

        instance.circleFailureCoroutine =
            instance.StartCoroutine(
                instance.ShowCircleFailureRoutine(isLastAttempt)
            );
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
        circleChallengeDetectionStarted = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;
        tutorialFailureBlocksSuccess = false;
        waitingAtTitleScreen = false;
        move2PromptShown = false;
        tutorialSecondBallPassed = false;
        ValidateUiReferences();
        ApplyPracticeTextStyle();

        SetMainStartObjectsVisible(false);
        ShowOnly(practiceObject);
        Time.timeScale = 1f;
        BeginTutorialIntro();
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

                if (!circleChallengeDetectionStarted
    && Time.unscaledTime >= circleChallengeAcceptResultsAt)
                {
                    circleChallengeDetectionStarted = true;

                    // rule2とmove2を消し、軌跡検知中の文字を表示
                    ShowOnly(circleTestObject);

                    MagicCarpetPoseController.StartCircleDetection();
                }

                return;
            }

            if (Time.unscaledTime >= tutorialPauseEndsAt)
            {
                waitingForTutorialPause = false;
                waitingForCircleChallenge = false;
                circleChallengeDetectionStarted = false;
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

            // 2個目の球をまだ通過していなければmove2を再表示
            if (!mainGameStarted
                && move2PromptShown
                && !tutorialSecondBallPassed)
            {
                ShowOnly(move2Object);
            }
            else
            {
                ShowOnly(null);
            }

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
        BeginTutorialIntro();

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
        circleChallengeDetectionStarted = !waitingForCircleChallenge;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = waitingForCircleChallenge
            ? Time.unscaledTime + Mathf.Max(0f, circleChallengeResultDelaySeconds)
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
        bool isTutorialChallenge = !mainGameStarted && !waitingForMainStart;

        waitingForTutorialPause = true;
        showingTutorialResult = false;
        waitingForCircleChallenge = true;

        // チュートリアルでは、5秒後に軌跡検知を開始する
        circleChallengeDetectionStarted = !isTutorialChallenge;
        circleChallengeOtherResults = 0;

        circleChallengeAcceptResultsAt = Time.unscaledTime + Mathf.Max(
            0f,
            isTutorialChallenge
                ? rule2DisplaySeconds
                : circleChallengeResultDelaySeconds);

        pendingCircleChallengeShieldScore = 0;

        activeCircleChallengeAttemptSeconds = attemptSeconds > 0f
            ? attemptSeconds
            : circleChallengeAttemptSeconds;

        tutorialFailureBlocksSuccess = false;
        tutorialPauseEndsAt = Time.unscaledTime;

        if (isTutorialChallenge)
        {
            // よけられない球が来た直後の5秒間
            ShowOnly(rule2Object, stekkiObject);
        }
        else
        {
            // 本番はすぐに軌跡検知へ入る
            ShowOnly(circleTestObject);
        }

        Time.timeScale = 0f;

        if (!isTutorialChallenge)
        {
            MagicCarpetPoseController.StartCircleDetection();
        }

        Debug.Log("Circle challenge paused the game.");
    }

    private void SkipCircleChallenge()
    {
        waitingForCircleChallenge = false;
        waitingForTutorialPause = false;
        circleChallengeDetectionStarted = false;
        circleChallengeOtherResults = 0;
        circleChallengeAcceptResultsAt = 0f;
        pendingCircleChallengeShieldScore = 0;

        // Failed表示の途中なら止める
        if (circleFailureCoroutine != null)
        {
            StopCoroutine(circleFailureCoroutine);
            circleFailureCoroutine = null;
        }

        // 遅れて届いたPython結果を破棄
        MagicCarpetPoseController.CancelCircleDetection();

        // 軌跡検知関連の表示を確実に消す
        SetVisible(circleTestObject, false);
        SetVisible(failureObject, false);
        SetVisible(successObject, false);
        SetVisible(rule2Object, false);
        SetVisible(stekkiObject, false);

        ShowOnly(null);
        Time.timeScale = 1f;

        Debug.Log("Circle challenge skipped by Enter.");
    }

    private void FinishCircleChallengeWithoutSuccess()
    {
        waitingForCircleChallenge = false;
        waitingForTutorialPause = false;
        circleChallengeDetectionStarted = false;
        circleChallengeOtherResults = 0;
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

        if (!mainGameStarted
            && move2PromptShown
            && !tutorialSecondBallPassed)
        {
            ShowOnly(resultObject, move2Object);
        }
        else
        {
            ShowOnly(resultObject);
        }

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

        if (!mainGameStarted
            && move2PromptShown
            && !tutorialSecondBallPassed)
        {
            ShowOnly(resultObject, move2Object);
        }
        else
        {
            ShowOnly(resultObject);
        }

        Time.timeScale = 1f;
    }

    private IEnumerator ShowCircleFailureRoutine(bool finishAfterDisplay)
    {
        ShowOnly(failureObject);

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, resultDisplaySeconds)
        );

        circleFailureCoroutine = null;

        if (finishAfterDisplay)
        {
            waitingForCircleChallenge = false;
            waitingForTutorialPause = false;
            circleChallengeDetectionStarted = false;
            circleChallengeOtherResults = 0;
            circleChallengeAcceptResultsAt = 0f;
            pendingCircleChallengeShieldScore = 0;

            ShowOnly(null);
            Time.timeScale = 1f;
            yield break;
        }

        if (waitingForCircleChallenge)
        {
            ShowOnly(circleTestObject);
        }
        else
        {
            ShowOnly(null);
        }
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
        move2PromptShown = false;
        tutorialSecondBallPassed = false;

        ResetSpawners();
        ShowOnly(practiceObject);
        Time.timeScale = 1f;
        BeginTutorialIntro();

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
        if (rule1Object == null) Debug.LogError("Rule1 Object is not assigned.", this);
        if (rule2Object == null) Debug.LogError("Rule2 Object is not assigned.", this);
        if (moveObject == null) Debug.LogError("Move Object is not assigned.", this);
        if (move2Object == null) Debug.LogError("Move2 Object is not assigned.", this);
        if (rightObject == null) Debug.LogError("Right Object is not assigned.", this);
        if (leftObject == null) Debug.LogError("Left Object is not assigned.", this);
        if (middleObject == null) Debug.LogError("Middle Object is not assigned.", this);
        if (stekkiObject == null) Debug.LogError("Stekki Object is not assigned.", this);
        if (honbanObject == null) Debug.LogError("Honban Object is not assigned.", this);
        if (successObject == null) Debug.LogError("Success Object is not assigned.", this);
        if (failureObject == null) Debug.LogError("Failure Object is not assigned.", this);
        if (circleTestObject == null)
            Debug.LogError("Circle Test Object is not assigned.", this);
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

        if (promptObjectName.Equals(rule1ObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return rule1Object;
        }

        if (promptObjectName.Equals(rule2ObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return rule2Object;
        }

        if (promptObjectName.Equals(moveObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return moveObject;
        }

        if (promptObjectName.Equals(move2ObjectName, System.StringComparison.OrdinalIgnoreCase))
        {
            return move2Object;
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
        SetVisible(
    practiceObject,
    visibleObject == practiceObject || ShouldKeepPracticeVisible());
        SetVisible(rule1Object, visibleObject == rule1Object);
        SetVisible(rule2Object, visibleObject == rule2Object);
        SetVisible(moveObject, visibleObject == moveObject);
        SetVisible(move2Object, visibleObject == move2Object);
        SetVisible(circleTestObject, visibleObject == circleTestObject);
        SetVisible(rightObject, visibleObject == rightObject);
        SetVisible(leftObject, visibleObject == leftObject);
        SetVisible(middleObject, visibleObject == middleObject);
        SetVisible(stekkiObject, visibleObject == stekkiObject);
        SetVisible(honbanObject, visibleObject == honbanObject);
        SetVisible(successObject, visibleObject == successObject);
        SetVisible(failureObject, visibleObject == failureObject);
    }

    private void ShowOnly(GameObject firstVisibleObject, GameObject secondVisibleObject)
    {
        SetVisible(titleObject, firstVisibleObject == titleObject || secondVisibleObject == titleObject);
        SetVisible(
    practiceObject,
    firstVisibleObject == practiceObject
    || secondVisibleObject == practiceObject
    || ShouldKeepPracticeVisible());
        SetVisible(rule1Object, firstVisibleObject == rule1Object || secondVisibleObject == rule1Object);
        SetVisible(rule2Object, firstVisibleObject == rule2Object || secondVisibleObject == rule2Object);
        SetVisible(moveObject, firstVisibleObject == moveObject || secondVisibleObject == moveObject);
        SetVisible(move2Object, firstVisibleObject == move2Object || secondVisibleObject == move2Object);
        SetVisible(circleTestObject, firstVisibleObject == circleTestObject || secondVisibleObject == circleTestObject);
        SetVisible(rightObject, firstVisibleObject == rightObject || secondVisibleObject == rightObject);
        SetVisible(leftObject, firstVisibleObject == leftObject || secondVisibleObject == leftObject);
        SetVisible(middleObject, firstVisibleObject == middleObject || secondVisibleObject == middleObject);
        SetVisible(stekkiObject, firstVisibleObject == stekkiObject || secondVisibleObject == stekkiObject);
        SetVisible(honbanObject, firstVisibleObject == honbanObject || secondVisibleObject == honbanObject);
        SetVisible(successObject, firstVisibleObject == successObject || secondVisibleObject == successObject);
        SetVisible(failureObject, firstVisibleObject == failureObject || secondVisibleObject == failureObject);
    }

    private void SetVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private bool ShouldKeepPracticeVisible()
    {
        return !mainGameStarted
            && !waitingForMainStart
            && !waitingAtTitleScreen;
    }

    private void ApplyPracticeTextStyle()
    {
        if (practiceObject == null)
        {
            return;
        }

        TMP_Text[] practiceTexts =
            practiceObject.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text practiceText in practiceTexts)
        {
            Material practiceMaterial =
                new Material(practiceText.fontSharedMaterial);

            practiceText.fontMaterial = practiceMaterial;

            Color32 yellow = new Color32(255, 230, 0, 255);
            Color32 black = new Color32(0, 0, 0, 255);

            practiceText.color = yellow;

            practiceMaterial.SetColor(
                ShaderUtilities.ID_FaceColor,
                yellow);

            practiceMaterial.SetColor(
                ShaderUtilities.ID_OutlineColor,
                black);

            practiceMaterial.SetFloat(
                ShaderUtilities.ID_OutlineWidth,
                0.25f);

            practiceText.UpdateMeshPadding();
            practiceText.SetAllDirty();
        }
    }

    private void BeginTutorialIntro()
    {
        if (tutorialIntroCoroutine != null)
        {
            StopCoroutine(tutorialIntroCoroutine);
        }

        tutorialIntroCoroutine = StartCoroutine(ShowRule1AfterPractice());
    }

    private IEnumerator ShowRule1AfterPractice()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, practiceDisplaySeconds));

        if (!mainGameStarted && !waitingForMainStart && !waitingForTutorialPause)
        {
            ShowOnly(rule1Object, moveObject);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, ruleMoveDisplaySeconds));

            if (!mainGameStarted && !waitingForMainStart && !waitingForTutorialPause)
            {
                move2PromptShown = true;
                if (!tutorialSecondBallPassed)
                {
                    ShowOnly(move2Object);
                }
            }
        }

        tutorialIntroCoroutine = null;
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

