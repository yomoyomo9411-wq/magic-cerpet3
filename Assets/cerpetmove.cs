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
    public float autoReturnToTitleSeconds = 15f;
    public float gameOverReturnToTitleSeconds = 5f;

    private float goalEnteredAt;
    private bool endedByGameOver;
    public AudioSource tutorialAmbientSource;

    public ShoulderInput shoulderInput;
    public float shoulderPower = 10f;
    public ShieldController shieldController;
    public GameObject tenkokuDynamicSky;

    [Header("Hit Sound")]
    public AudioSource seSource;
    public AudioClip hitSound1;
    public AudioClip hitSound2;

    [Header("Life Settings")]
    public float maxLife = 10f;
    [Range(0.1f, 3f)]
    public float defaultHitDamage = 1f;
    [Range(0f, 1f)]
    public float minimumHitFeedbackIntensity = 0.55f;
    private float currentLife;
    public TMP_Text lifeText;
    public TMP_Text heartsText;
    [Header("Goal Remaining Life UI")]
    public TMP_Text goalHeartsText;
    public TMP_Text goalLifeText;

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
        currentLife = Mathf.Max(0.1f, maxLife);


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

        if (goalHeartsText != null)
        {
            goalHeartsText.text = "";
        }

        if (goalLifeText != null)
        {
            goalLifeText.text = "";
        }

        if (lifeText != null)
        {
            ApplyLifeTextStyle();
            lifeText.gameObject.SetActive(false);
        }

        if (heartsText != null)
        {
            ApplyHeartsTextStyle();
            heartsText.gameObject.SetActive(false);
        }

        if (goalLifeText != null)
        {
            ApplyGoalLifeTextStyle();
        }

        if (goalHeartsText != null)
        {
            ApplyGoalHeartsTextStyle();
        }

        UpdateLifeText();

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
        damage = Mathf.Clamp(Mathf.Round(damage * 10f) / 10f, 0.1f, 3f);
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
            PlayTutorialDamageFeedback();
            return;
        }

        float feedbackIntensity =
    GetHitFeedbackIntensity(damage);

        // ゲームを停止する前に演出を開始
        PlayDamageFeedback(feedbackIntensity);

        ApplyDamage(damage);
        DestroyBulletObject(bulletRoot);
    }

    public void HandleBulletPassed(GameObject bulletObject)
    {
    }

    public void DestroyBulletImmediately(GameObject bulletObject)
    {
        var bulletRoot = GetBulletRoot(bulletObject);

        if (bulletRoot == null)
        {
            return;
        }

        var runtime = bulletRoot.GetComponent<BallRuntimeController>();

        if (runtime != null)
        {
            if (!runtime.ClaimHit())
            {
                return;
            }

            runtime.StopAfterHit();
            runtime.DisableAllColliders();
        }


        Destroy(bulletRoot);
    }

public void SetHpVisible(bool visible)
    {
        if (lifeText != null)
        {
            lifeText.gameObject.SetActive(visible);
        }

        if (heartsText != null)
        {
            heartsText.gameObject.SetActive(visible);
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
            runtime.DisableAllColliders();
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
            cameraShakeScript.ShakeTutorial(0.8f, 0.6f);
        }
    }

    private void PlayTutorialDamageFeedback()
    {
        if (damageFlash != null)
        {
            damageFlash.FlashTutorial(1f, 1f);
        }

        if (damageObject != null)
        {
            StartCoroutine(ShowDamageObject());
        }

        if (cameraShakeScript != null)
        {
            cameraShakeScript.ShakeTutorial(0.8f, 0.6f);
        }
    }

    private void ApplyLifeTextStyle()
    {
        if (lifeText == null)
        {
            return;
        }

        Material material = new Material(lifeText.fontSharedMaterial);

        // 文字本体の基準色は白
        material.SetColor(
            ShaderUtilities.ID_FaceColor,
            Color.white);

        // 縁は白
        material.SetColor(
            ShaderUtilities.ID_OutlineColor,
            Color.white);

        material.SetFloat(
            ShaderUtilities.ID_OutlineWidth,
            0.2f);

        lifeText.fontMaterial = material;

        // ライフ文字を緑にする
        lifeText.color = new Color32(0, 102, 0, 255);

        lifeText.UpdateMeshPadding();
    }

    private void ApplyGoalLifeTextStyle()
    {
        if (goalLifeText == null)
        {
            return;
        }

        Material material = new Material(goalLifeText.fontSharedMaterial);

        material.SetColor(
            ShaderUtilities.ID_FaceColor,
            Color.white
        );

        material.SetColor(
            ShaderUtilities.ID_OutlineColor,
            Color.white
        );

        material.SetFloat(
            ShaderUtilities.ID_OutlineWidth,
            0.2f
        );

        goalLifeText.fontMaterial = material;

        // 元のLifeTextと同じ緑
        goalLifeText.color = new Color32(0, 102, 0, 255);

        goalLifeText.UpdateMeshPadding();
    }

    public void ResetForNextRun()
    {
        waitingForGoalRestart = false;
        returnedToTitleAfterGoal = false;
        endedByGameOver = false;

        if (cameraShakeScript != null)
        {
            cameraShakeScript.ResetShake();
        }

        isGameOver = false;
        currentLife = Mathf.Max(0.1f, maxLife);

        if (tenkokuDynamicSky != null)
        {
            tenkokuDynamicSky.SetActive(true);
        }

        if (goalText != null)
        {
            goalText.SetActive(false);
        }

        if (gameOverText != null)
        {
            gameOverText.SetActive(false);
        }

        if (goalHeartsText != null)
        {
            goalHeartsText.gameObject.SetActive(false);
        }

        if (goalLifeText != null)
        {
            goalLifeText.gameObject.SetActive(false);
        }

        UpdateLifeText();
        SetHpVisible(false);
    }

    private void ApplyGoalHeartsTextStyle()
    {
        if (goalHeartsText == null)
        {
            return;
        }

        Material material = new Material(goalHeartsText.fontSharedMaterial);

        material.SetColor(
            ShaderUtilities.ID_FaceColor,
            Color.white
        );

        material.SetColor(
            ShaderUtilities.ID_OutlineColor,
            Color.white
        );

        material.SetFloat(
            ShaderUtilities.ID_OutlineWidth,
            0.2f
        );

        goalHeartsText.fontMaterial = material;

        // 元のHeartsTextと同じ赤
        goalHeartsText.color = Color.red;

        goalHeartsText.enableWordWrapping = false;
        goalHeartsText.overflowMode = TextOverflowModes.Overflow;

        goalHeartsText.UpdateMeshPadding();
    }

    private void ApplyHeartsTextStyle()
    {
        if (heartsText == null)
        {
            return;
        }

        Material material = new Material(heartsText.fontSharedMaterial);

        // ハート本体の基準色は白
        material.SetColor(
            ShaderUtilities.ID_FaceColor,
            Color.white);

        // 縁は白
        material.SetColor(
            ShaderUtilities.ID_OutlineColor,
            Color.white);

        material.SetFloat(
            ShaderUtilities.ID_OutlineWidth,
            0.2f);

        heartsText.fontMaterial = material;

        // ハートを赤にする
        heartsText.color = Color.red;

        heartsText.enableWordWrapping = false;
        heartsText.overflowMode = TextOverflowModes.Overflow;

        heartsText.UpdateMeshPadding();
    }

    void UpdateLifeText()
    {
        if (lifeText != null)
        {
            lifeText.text =
                "ライフ：" +
                FormatLifeValue(currentLife) +
                " / " +
                FormatLifeValue(maxLife);
        }

        if (heartsText != null)
        {
            heartsText.text = BuildLifeHearts();
        }
    }

    void UpdateGoalLifeText()
    {
        if (goalHeartsText != null)
        {
            goalHeartsText.gameObject.SetActive(true);
            goalHeartsText.text = BuildGoalHearts();
            goalHeartsText.transform.SetAsLastSibling();
        }

        if (goalLifeText != null)
        {
            goalLifeText.gameObject.SetActive(true);
            goalLifeText.text =
                FormatLifeValue(currentLife) +
                " / " +
                FormatLifeValue(maxLife);

            goalLifeText.transform.SetAsLastSibling();
        }
    }

    string BuildGoalHearts()
    {
        int totalHearts = Mathf.Max(1, Mathf.RoundToInt(maxLife));

        int fullHearts = Mathf.Clamp(
            Mathf.FloorToInt(currentLife),
            0,
            totalHearts
        );

        float fractionalLife =
            currentLife - Mathf.Floor(currentLife);

        bool hasPartialHeart =
            fractionalLife > Mathf.Epsilon &&
            fullHearts < totalHearts;

        bool partialHeartOpaque =
            fractionalLife >= 0.5f;

        var builder = new StringBuilder();

        for (int i = 0; i < fullHearts; i++)
        {
            builder.Append("<alpha=#FF>♥ ");
        }

        if (hasPartialHeart)
        {
            builder.Append(
                partialHeartOpaque
                    ? "<alpha=#FF>♥ "
                    : "<alpha=#80>♥ "
            );
        }

        builder.Append("<alpha=#FF>");

        return builder.ToString();
    }

    string FormatLifeValue(float value)
    {
        return Mathf.Approximately(value % 1f, 0f) ? Mathf.RoundToInt(value).ToString() : value.ToString("0.0");
    }

    string BuildLifeHearts()
    {
        int totalHearts = Mathf.Max(1, Mathf.RoundToInt(maxLife));
        float fractionalLife = currentLife - Mathf.Floor(currentLife);
        int fullHearts = Mathf.Clamp(
            Mathf.FloorToInt(currentLife),
            0,
            totalHearts);

        bool hasHalfHeart =
            fractionalLife > Mathf.Epsilon &&
            fullHearts < totalHearts;

        bool partialHeartOpaque = fractionalLife >= 0.5f;

        var builder = new StringBuilder(totalHearts * 15);

        for (int i = 0; i < totalHearts; i++)
        {
            if (i < fullHearts)
            {
                builder.Append("<alpha=#FF>♥ ");
            }
            else if (i == fullHearts && hasHalfHeart)
            {
                builder.Append(partialHeartOpaque
                    ? "<alpha=#FF>♥ "
                    : "<alpha=#80>♥ <alpha=#FF>");
            }
            else
            {
                builder.Append("<alpha=#00>♥ <alpha=#FF>");
            }
        }

        return builder.ToString();
    }

    float GetHitFeedbackIntensity(float damage)
    {
        float quantizedDamage = Mathf.Clamp(Mathf.Round(damage * 10f) / 10f, 0.1f, 3f);
        float damageRatio = Mathf.InverseLerp(0.1f, 3f, quantizedDamage);
        return Mathf.Lerp(minimumHitFeedbackIntensity, 1f, damageRatio);
    }

    IEnumerator ShowDamageObject()
    {
        damageObject.SetActive(true);

        yield return new WaitForSecondsRealtime(
            damageObjectActiveTime
        );

        damageObject.SetActive(false);
    }

    void EnterGoalState()
    {
        if (waitingForGoalRestart)
        {
            return;
        }

        endedByGameOver = false;

        if (tenkokuDynamicSky != null)
        {
            tenkokuDynamicSky.SetActive(false);
        }

        // 左上の通常ライフ表示を消す
        SetHpVisible(false);

        if (goalText != null)
        {
            goalText.SetActive(true);
        }

        // ゴール画面用のライフ内容を更新
        UpdateGoalLifeText();

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
        float returnDelay = endedByGameOver
    ? gameOverReturnToTitleSeconds
    : autoReturnToTitleSeconds;

        if (!returnedToTitleAfterGoal &&
            Time.unscaledTime >= goalEnteredAt + returnDelay)
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
        currentLife = Mathf.Max(0.1f, maxLife);

        if (cameraShakeScript != null)
        {
            cameraShakeScript.ResetShake();
        }

        if (lifeText != null || heartsText != null)
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
        if (isGameOver)
        {
            return;
        }
        endedByGameOver = true;
        isGameOver = true;

        if (gameOverText != null)
        {
            gameOverText.SetActive(true);
        }

        if (tutorialAmbientSource != null)
        {
            tutorialAmbientSource.Stop();
        }

        waitingForGoalRestart = true;
        returnedToTitleAfterGoal = false;
        goalEnteredAt = Time.unscaledTime;

        MagicCarpetGameFlow.FadeOutBgm();

        Time.timeScale = 0f;

        Debug.Log("GAME OVER");
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
