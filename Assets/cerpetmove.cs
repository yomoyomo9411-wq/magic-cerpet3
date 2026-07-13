using System.Collections;
using UnityEngine;
using TMPro;

public class CarpetMove : MonoBehaviour
{
    public float forwardSpeed = 30f;
    public float sideSpeed = 5f;

    public float tiltAngle = 20f;
    public float tiltSmooth = 5f;

    public float minX = -8f;
    public float maxX = 8f;
    public float autoReturnToTitleSeconds = 30f;
    public AudioSource tutorialAmbientSource;
    private float goalEnteredAt;

    public float scoreCooldownSeconds = 1f;
    private float nextScoreAllowedTime;

    public ShoulderInput shoulderInput;
    public float shoulderPower = 10f;
    public ShieldController shieldController;
    public GameObject tenkokuDynamicSky;

    [Header("Hit Sound")]
    public AudioSource seSource;
    public AudioClip hitSound1;
    public AudioClip hitSound2;

    [Header("Score Settings")]
    public int dodgeScore = 100;
    public int hitEdgeScore = 99;
    [Range(0f, 1f)]
    public float minimumHitFeedbackIntensity = 0.08f;
    private int currentScore;

    public TMP_Text hpText;
    private bool hpTextVisible;
    public GameObject goalText;
    public DamageFlash damageFlash;

    public GameObject gameOverText;

    public GameObject damageObject;
    public float damageObjectActiveTime = 0.5f;

    public GameObject cameraObject;
    public CameraShake cameraShakeScript;

[Tooltip("当たった後、モンスターの見た目を残す秒数")]
public float hitObjectRemainSeconds = 2.0f;

    public bool isGameOver = false;
    private bool waitingForGoalRestart;
    private bool hitScoreAddedThisFrame;
    private bool returnedToTitleAfterGoal;
    private float lastDodgeScoreZ = -999999f;
    public float sameDodgeGroupZDistance = 5f;

    void Start()
    {
        Time.timeScale = 1f;


        if (gameOverText != null)
        {
            gameOverText.SetActive(false);
        }

        if (damageObject != null)
        {
            damageObject.SetActive(false);
        }

        if (goalText != null)
        {
            goalText.SetActive(false);
        }

        if (hpText != null)
        {
            hpText.text = "Score : " + currentScore;
            hpText.gameObject.SetActive(false);
            hpTextVisible = false;
        }

        if (cameraShakeScript == null && cameraObject != null)
        {
            cameraShakeScript = cameraObject.GetComponent<CameraShake>();
        }

        if (shieldController == null)
        {
            shieldController = GetComponent<ShieldController>();
        }
    }

    void Update()
    {
        hitScoreAddedThisFrame = false;
        if (waitingForGoalRestart)
        {
            HandleGoalRestartInput();
            return;
        }

        UpdateHpTextVisibility();

        if (!MagicCarpetGameFlow.IsCarpetMovementAllowed)
        {
            return;
        }

        if (isGameOver) return;

        transform.Translate(
            Vector3.forward * forwardSpeed * Time.deltaTime,
            Space.World
        );

        float keyboardInput = Input.GetAxis("Horizontal");
        float cameraInput = 0f;

        if (shoulderInput != null && shoulderInput.IsDetected)
        {
            cameraInput = shoulderInput.InputValue * shoulderPower;
        }

        float moveAmount = keyboardInput * sideSpeed + cameraInput;

        moveAmount = Mathf.Clamp(moveAmount, -10f, 10f);

        Vector3 pos = transform.position;
        pos += Vector3.right * moveAmount * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);

        transform.position = pos;

        float targetZAngle = -moveAmount * tiltAngle / 20f;

        Quaternion targetRotation =
            Quaternion.Euler(0f, 0f, targetZAngle);

        transform.rotation =
            Quaternion.Lerp(
                transform.rotation,
                targetRotation,
                tiltSmooth * Time.deltaTime
            );
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
        {
            HandleBulletHit(collision.gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
        {
            HandleBulletHit(other.gameObject);
        }
        else if (other.CompareTag("GOAL"))
        {
            EnterGoalState();
        }
    }

    void AddScore(int scoreToAdd)
    {
        if (scoreToAdd <= 0)
        {
            return;
        }

        if (Time.unscaledTime < nextScoreAllowedTime)
        {
            return;
        }

        nextScoreAllowedTime = Time.unscaledTime + scoreCooldownSeconds;

        currentScore += scoreToAdd;
        UpdateScoreText();

        Debug.Log("Score : " + currentScore);
    }

    public void HandleBulletHit(GameObject bulletObject)
    {
        HandleBulletHitInternal(bulletObject, false);
    }

    public void HandleRuntimeBulletHit(GameObject bulletObject)
    {
        HandleBulletHitInternal(bulletObject, true);
    }

    private void HandleBulletHitInternal(GameObject bulletObject, bool hitAlreadyClaimed)
    {
        if (bulletObject == null || !bulletObject.CompareTag("Bullet"))
        {
            return;
        }

        var bulletRoot = GetBulletRoot(bulletObject);
        var runtime = bulletRoot != null ? bulletRoot.GetComponent<BallRuntimeController>() : null;
        if (!hitAlreadyClaimed && runtime != null && !runtime.ClaimHit())
        {
            return;
        }

        PlayHitSound(bulletRoot);

        if (shieldController != null && shieldController.IsShieldActive)
        {
            if (MagicCarpetGameFlow.IsMainGameStarted)
            {
                var shieldScore = IsCircleChallengeObstacle(bulletRoot)
                    ? MagicCarpetGameFlow.ConsumeCircleChallengeShieldScore(dodgeScore)
                    : dodgeScore;
                AddScore(shieldScore);
            }

            DestroyBulletObject(bulletRoot);
            return;
        }

        if (!MagicCarpetGameFlow.IsMainGameStarted)
        {
            ReportTutorialBulletFailure(bulletRoot);
            PlayDamageFeedback(1f);
            return;
        }

        int hitScore = IsCircleChallengeObstacle(bulletRoot) ? 0 : CalculateHitScore(bulletRoot);

        if (!hitScoreAddedThisFrame)
        {
            AddScore(hitScore);
            hitScoreAddedThisFrame = true;
        }

        PlayDamageFeedback(GetHitFeedbackIntensity(hitScore));
        DestroyBulletObject(bulletRoot);
    }

    public void HandleBulletPassed(GameObject bulletObject)
    {
        if (bulletObject == null || !MagicCarpetGameFlow.IsMainGameStarted)
        {
            return;
        }

        if (IsCircleChallengeObstacle(bulletObject))
        {
            return;
        }

        float bulletZ = bulletObject.transform.position.z;

        if (Mathf.Abs(bulletZ - lastDodgeScoreZ) <= sameDodgeGroupZDistance)
        {
            return;
        }

        lastDodgeScoreZ = bulletZ;
        AddScore(dodgeScore);
    }

    void ReportTutorialBulletFailure(GameObject hitObject)
    {
        var reporter = hitObject.GetComponentInParent<TutorialBallResultReporter>();
        if (reporter == null)
        {
            reporter = hitObject.GetComponentInChildren<TutorialBallResultReporter>();
        }

        if (reporter != null)
        {
            reporter.ReportFailure();
            DestroyBulletObject(hitObject);
            return;
        }

        MagicCarpetGameFlow.ReportTutorialFailure();
        DestroyBulletObject(hitObject);
    }

    void DestroyBulletObject(GameObject hitObject)
    {
        var bulletRoot = GetBulletRoot(hitObject);
        if (bulletRoot == null)
        {
            return;
        }

        var reporter = hitObject.GetComponentInParent<TutorialBallResultReporter>();
        if (reporter == null)
        {
            reporter = hitObject.GetComponentInChildren<TutorialBallResultReporter>();
        }

        if (reporter != null && MagicCarpetGameFlow.IsMainGameStarted)
        {
            reporter.MarkHandled();
        }

        var runtime = bulletRoot.GetComponent<BallRuntimeController>();
        if (runtime != null)
        {
            runtime.StopAfterHit();
        }

        foreach (var collider in bulletRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        StartCoroutine(DestroyBulletAfterDelay(bulletRoot));
    }

    GameObject GetBulletRoot(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return null;
        }

        var runtime = hitObject.GetComponentInParent<BallRuntimeController>();
        if (runtime != null)
        {
            return runtime.gameObject;
        }

        var reporter = hitObject.GetComponentInParent<TutorialBallResultReporter>();
        if (reporter != null)
        {
            return reporter.gameObject;
        }

        return hitObject;
    }

    IEnumerator DestroyBulletAfterDelay(GameObject bulletObject)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, hitObjectRemainSeconds));

        if (bulletObject != null)
        {
            Destroy(bulletObject);
        }
    }

    public void PlayDamageFeedback()
    {
        PlayDamageFeedback(1f);
    }

    public void PlayDamageFeedback(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        if (damageFlash != null)
        {
            damageFlash.Flash(intensity);
        }

        if (damageObject != null)
        {
            StartCoroutine(ShowDamageObject());
        }

        if (cameraShakeScript != null)
        {
            cameraShakeScript.Shake(intensity);
        }
    }

    void UpdateHpTextVisibility()
    {
        if (hpText == null)
        {
            return;
        }

        var shouldShow = MagicCarpetGameFlow.HasReachedMainStartLine;
        if (hpTextVisible == shouldShow)
        {
            return;
        }

        if (shouldShow)
        {
            EnsureParentsVisible(hpText.gameObject);
        }

        hpText.gameObject.SetActive(shouldShow);
        hpTextVisible = shouldShow;
        UpdateScoreText();
    }

    void EnsureParentsVisible(GameObject target)
    {
        var parent = target.transform.parent;
        while (parent != null)
        {
            parent.gameObject.SetActive(true);
            parent = parent.parent;
        }
    }

    void UpdateScoreText()
    {
        if (hpText != null)
        {
            hpText.text = "Score : " + currentScore;
        }
    }

    int CalculateHitScore(GameObject hitObject)
    {
        var bounds = GetObjectBounds(hitObject);
        if (!bounds.HasValue)
        {
            return 0;
        }

        var halfWidth = Mathf.Max(0.01f, bounds.Value.extents.x);
        var distanceFromCenterX = Mathf.Abs(transform.position.x - bounds.Value.center.x);
        var ratioToEdge = Mathf.Clamp01(distanceFromCenterX / halfWidth);
        return Mathf.Clamp(Mathf.RoundToInt(ratioToEdge * Mathf.Max(0, hitEdgeScore)), 0, 99);
    }

    bool IsCircleChallengeObstacle(GameObject hitObject)
    {
        return hitObject != null && hitObject.GetComponentInParent<CircleChallengeObstacle>() != null;
    }

    float GetHitFeedbackIntensity(int hitScore)
    {
        float maxHitScore = Mathf.Max(1f, Mathf.Min(99f, hitEdgeScore));
        float scoreRatio = Mathf.Clamp01(hitScore / maxHitScore);
        return Mathf.Lerp(1f, minimumHitFeedbackIntensity, scoreRatio);
    }

    Bounds? GetObjectBounds(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        Bounds? bounds = null;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!bounds.HasValue)
            {
                bounds = renderer.bounds;
            }
            else
            {
                var current = bounds.Value;
                current.Encapsulate(renderer.bounds);
                bounds = current;
            }
        }

        if (bounds.HasValue)
        {
            return bounds;
        }

        foreach (var collider in target.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!bounds.HasValue)
            {
                bounds = collider.bounds;
            }
            else
            {
                var current = bounds.Value;
                current.Encapsulate(collider.bounds);
                bounds = current;
            }
        }

        return bounds;
    }

    IEnumerator ShowDamageObject()
    {
        damageObject.SetActive(true);

        yield return new WaitForSeconds(damageObjectActiveTime);

        damageObject.SetActive(false);
    }

    void EnterGoalState()
    {
        if (waitingForGoalRestart)
        {
            return;
        }

        if (tenkokuDynamicSky != null)
        {
            tenkokuDynamicSky.SetActive(false);
        }

        if (goalText != null)
        {
            goalText.SetActive(true);
        }

        if (tutorialAmbientSource != null)
        {
            tutorialAmbientSource.Stop();
        }

        waitingForGoalRestart = true;
        returnedToTitleAfterGoal = false;
        isGameOver = true;

        MagicCarpetGameFlow.FadeOutBgm();

        Time.timeScale = 0f;
        goalEnteredAt = Time.unscaledTime;

        Debug.Log("GOAL!");
    }

    void HandleGoalRestartInput()
    {
        if (!returnedToTitleAfterGoal && Time.unscaledTime >= goalEnteredAt + autoReturnToTitleSeconds)
        {
            ReturnToTitleAfterGoal();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            return;
        }

        if (!returnedToTitleAfterGoal)
        {
            ReturnToTitleAfterGoal();
            return;
        }

        waitingForGoalRestart = false;
        returnedToTitleAfterGoal = false;
        MagicCarpetGameFlow.StartNextRunFromTitle();
    }

    void ReturnToTitleAfterGoal()
    {
        returnedToTitleAfterGoal = true;
        isGameOver = false;
        currentScore = 0;

        if (hpText != null)
        {
            hpText.text = "Score : " + currentScore;
            hpText.gameObject.SetActive(false);
            hpTextVisible = false;
        }

        if (goalText != null)
        {
            goalText.SetActive(false);
        }

        if (gameOverText != null)
        {
            gameOverText.SetActive(false);
        }

        if (damageObject != null)
        {
            damageObject.SetActive(false);
        }

        MagicCarpetGameFlow.ResetToTitleScreenForNextRun();
    }

    void GameOver()
    {
        isGameOver = true;
        if (gameOverText != null)
        {
            gameOverText.SetActive(true);
        }

        Debug.Log("GAME OVER");

        Time.timeScale = 0f;
    }

    void PlayHitSound(GameObject bulletRoot)
    {
        if (seSource == null || hitSound1 == null)
        {
            return;
        }

        var marker = bulletRoot != null ? bulletRoot.GetComponent<ObstacleHitSoundMarker>() : null;
        var clip = marker != null && marker.useHitSound2 && hitSound2 != null
            ? hitSound2
            : hitSound1;

        seSource.PlayOneShot(clip);
    }
}
