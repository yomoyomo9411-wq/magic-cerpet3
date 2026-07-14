using System.Collections;
using System.Text;
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


    public ShoulderInput shoulderInput;
    public float shoulderPower = 10f;
    public ShieldController shieldController;
    public GameObject tenkokuDynamicSky;

    [Header("Hit Sound")]
    public AudioSource seSource;
    public AudioClip hitSound1;
    public AudioClip hitSound2;

    [Header("Life Settings")]
    public float maxLife = 30f;
    [Range(0.5f, 3f)]
    public float defaultHitDamage = 1f;
    [Range(0f, 1f)]
    public float minimumHitFeedbackIntensity = 0.55f;
    private float currentLife;
    public TMP_Text hpText;
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
    private bool returnedToTitleAfterGoal;

    void Start()
    {
        Time.timeScale = 1f;
        currentLife = Mathf.Max(0.5f, maxLife);


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
            UpdateLifeText();
            hpText.gameObject.SetActive(false);
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
        if (waitingForGoalRestart)
        {
            HandleGoalRestartInput();
            return;
        }


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

    void ApplyDamage(float damage)
    {
        damage = Mathf.Clamp(damage, 0.5f, 3f);
        currentLife = Mathf.Max(0f, currentLife - damage);
        UpdateLifeText();

        if (currentLife <= 0f)
        {
            GameOver();
        }
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
            DestroyBulletObject(bulletRoot);
            return;
        }

        float damage = runtime != null ? runtime.damage : defaultHitDamage;

        if (!MagicCarpetGameFlow.IsMainGameStarted)
        {
            ReportTutorialBulletFailure(bulletRoot);
            PlayDamageFeedback(GetHitFeedbackIntensity(damage));
            return;
        }

        ApplyDamage(damage);
        PlayDamageFeedback(GetHitFeedbackIntensity(damage));
        DestroyBulletObject(bulletRoot);
    }

    public void HandleBulletPassed(GameObject bulletObject)
    {
    }

    public void SetHpVisible(bool visible)
    {
        if (hpText != null)
        {
            hpText.gameObject.SetActive(visible);
        }
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

    void UpdateLifeText()
    {
        if (hpText != null)
        {
            hpText.text =
                "<color=#66FF66>ライフ：" + FormatLifeValue(currentLife) + "/" + FormatLifeValue(maxLife) + "</color>"
                + "\n"
                + BuildLifeHearts();
        }
    }

    string FormatLifeValue(float value)
    {
        return Mathf.Approximately(value % 1f, 0f) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
    }

    string BuildLifeHearts()
    {
        int totalHearts = Mathf.Max(1, Mathf.RoundToInt(maxLife));
        int fullHearts = Mathf.Clamp(Mathf.FloorToInt(currentLife), 0, totalHearts);
        bool hasHalfHeart = currentLife - fullHearts >= 0.5f && fullHearts < totalHearts;

        var builder = new StringBuilder(totalHearts * 26);
        for (int i = 0; i < totalHearts; i++)
        {
            if (i > 0 && i % 10 == 0)
            {
                builder.Append("\n");
            }

            if (i < fullHearts)
            {
                builder.Append("<color=#FF3030>\u2665</color>");
            }
            else if (i == fullHearts && hasHalfHeart)
            {
                builder.Append("<color=#00000000>\u2665</color>");
            }
            else
            {
                builder.Append("<color=#00000000>\u2665</color>");
            }
        }

        return builder.ToString();
    }

    float GetHitFeedbackIntensity(float damage)
    {
        float damageRatio = Mathf.InverseLerp(0.5f, 3f, Mathf.Clamp(damage, 0.5f, 3f));
        return Mathf.Lerp(minimumHitFeedbackIntensity, 1f, damageRatio);
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
        currentLife = Mathf.Max(0.5f, maxLife);

        if (hpText != null)
        {
            UpdateLifeText();
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
