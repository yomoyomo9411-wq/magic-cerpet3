using System.Collections;
using UnityEngine;

public class ShieldController : MonoBehaviour
{
    [Header("Shield")]
    public bool showVisualEffect = false;
    public bool alwaysVisible = false;
    public float shieldDuration = 1f;
    public string sceneVisualEffectName = "ShieldAuraPreview";
    public string magicCircleResourceName = "MagicCircleShield";
    public bool attachToMainCamera = false;
    public bool forceParticleLocalAlignment = true;
    public Vector3 localPosition = new Vector3(0f, 1.3f, 3.0f);
    public Vector3 localRotation = new Vector3(0f, 0f, 0f);
    public Vector3 effectRotationOffset = new Vector3(0f, 0f, 0f);
    public Vector3 localScale = new Vector3(2.8f, 2.8f, 2.8f);
    public float appearSeconds = 0.16f;
    public float disappearSeconds = 0.12f;

    [Header("Magic Attack")]
    [Tooltip("前方へ飛ばす魔法エフェクト。シーン内の非表示オブジェクトでも可")]
    public GameObject magicProjectileEffect;

    [Tooltip("モンスター命中時に表示する赤いエフェクト")]
    public GameObject monsterHitEffect;

    [Tooltip("魔法エフェクトの大きさ")]
    public Vector3 magicProjectileScale = Vector3.one;

    [Tooltip("魔法エフェクトの向きの追加調整")]
    public Vector3 magicProjectileRotationOffset = Vector3.zero;

    [Tooltip("魔法の発射位置。空ならプレイヤー位置を基準にします")]
    public Transform magicProjectileSpawnPoint;

    [Tooltip("魔法弾が命中後も残る時間")]
    [Min(0f)]
    public float magicProjectileRemainSeconds = 0.5f;

    [Tooltip("発射位置の調整")]
    public Vector3 magicProjectileSpawnOffset =
        new Vector3(0f, 1.3f, 3f);

    [Tooltip("魔法陣を出してから魔法を発射するまでの時間")]
    public float magicAttackDelay = 0.5f;

    [Tooltip("魔法が飛ぶ速さ")]
    public float magicProjectileSpeed = 25f;

    [Tooltip("この距離まで近づいたら命中扱い")]
    public float magicHitDistance = 0.8f;

    [Tooltip("命中後、赤いエフェクトを見せる時間")]
    public float monsterHitEffectSeconds = 0.2f;

    [Tooltip("赤いエフェクトの位置調整")]
    public Vector3 monsterHitEffectOffset = Vector3.zero;

    private Coroutine magicAttackCoroutine;

    [Header("Shield Sound")]
    public AudioSource shieldSeSource;
    public AudioClip shieldActivateSound;

    public bool IsShieldActive => alwaysVisible || Time.unscaledTime < shieldActiveUntil;

    private float shieldActiveUntil;
    private Transform shieldRoot;
    private GameObject shieldEffect;
    private bool usingSceneVisualEffect;
    private bool shieldVisibleTarget;
    private Vector3 baseShieldScale = Vector3.one;
    private Coroutine visibilityCoroutine;

    private void Awake()
    {
        CreateShieldEffect();
        if (shieldEffect != null)
        {
            shieldEffect.SetActive(false);
        }

        if (magicProjectileEffect != null)
        {
            magicProjectileEffect.SetActive(false);
        }

        if (monsterHitEffect != null)
        {
            monsterHitEffect.SetActive(false);
        }

        shieldVisibleTarget = false;
    }

    private void Update()
    {
        var shouldShowEffect = showVisualEffect && IsShieldActive;
        if (shieldEffect != null && shieldVisibleTarget != shouldShowEffect)
        {
            SetShieldVisible(shouldShowEffect);
        }
    }

    public void ActivateShield()
    {
        if (shieldEffect == null)
        {
            CreateShieldEffect();
        }

        shieldActiveUntil = Time.unscaledTime + shieldDuration;
        ApplyTransform();
        SetShieldVisible(showVisualEffect);

        if (shieldSeSource != null && shieldActivateSound != null)
        {
            shieldSeSource.PlayOneShot(shieldActivateSound);
        }

        var target =
    MagicCarpetGameFlow.GetCircleChallengeTarget();

        if (target != null)
        {
            if (magicAttackCoroutine != null)
            {
                StopCoroutine(magicAttackCoroutine);
            }

            magicAttackCoroutine =
                StartCoroutine(MagicAttackRoutine(target));
        }
    }

    private IEnumerator FlashMonsterRed(GameObject target, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        Renderer[] renderers =
            target.GetComponentsInChildren<Renderer>(true);

        var materials =
            new System.Collections.Generic.List<Material>();

        var originalColors =
            new System.Collections.Generic.List<Color>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            foreach (Material material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                string colorProperty = null;

                if (material.HasProperty("_BaseColor"))
                {
                    colorProperty = "_BaseColor";
                }
                else if (material.HasProperty("_Color"))
                {
                    colorProperty = "_Color";
                }

                if (colorProperty == null)
                {
                    continue;
                }

                Color originalColor =
                    material.GetColor(colorProperty);

                materials.Add(material);
                originalColors.Add(originalColor);

                Color redAdded = originalColor + new Color(0.8f, 0f, 0f, 0f);
                redAdded.a = originalColor.a;

                material.SetColor(colorProperty, redAdded);
            }
        }

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0.01f, duration)
        );

        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];

            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    originalColors[i]
                );
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor(
                    "_Color",
                    originalColors[i]
                );
            }
        }
    }

    public void ApplyTransform()
    {
        if (shieldRoot == null || shieldEffect == null)
        {
            return;
        }

        var parent = GetShieldParent();

        if (usingSceneVisualEffect)
        {
            CaptureBaseScale();
            ConfigureParticleRenderers();
            return;
        }

        if (shieldRoot.parent != parent)
        {
            shieldRoot.SetParent(parent, false);
        }

        shieldRoot.localPosition = localPosition;
        shieldRoot.localRotation = Quaternion.Euler(localRotation);
        shieldRoot.localScale = localScale;
        CaptureBaseScale();

        ConfigureParticleRenderers();

        if (shieldEffect.transform != shieldRoot)
        {
            shieldEffect.transform.localPosition = Vector3.zero;
            shieldEffect.transform.localRotation = Quaternion.Euler(effectRotationOffset);
            shieldEffect.transform.localScale = Vector3.one;
        }
    }

    private void CreateShieldEffect()
    {
        if (shieldEffect != null)
        {
            return;
        }

        var sceneVisualEffect = FindSceneVisualEffect();
        if (sceneVisualEffect != null)
        {
            shieldEffect = sceneVisualEffect;
            shieldRoot = shieldEffect.transform;
            usingSceneVisualEffect = true;
            CaptureBaseScale();
            ConfigureParticleRenderers();
            return;
        }

        if (!string.IsNullOrWhiteSpace(sceneVisualEffectName))
        {
            Debug.LogWarning($"Shield visual object was not found: {sceneVisualEffectName}");
            return;
        }

        shieldRoot = new GameObject("Magic Circle Shield Root").transform;
        shieldRoot.SetParent(GetShieldParent(), false);
        usingSceneVisualEffect = false;

        var prefab = Resources.Load<GameObject>(magicCircleResourceName);
        if (prefab != null)
        {
            shieldEffect = Instantiate(prefab, shieldRoot);
            shieldEffect.name = "Magic Circle Shield";
            ConfigureParticleRenderers();
        }
        else
        {
            shieldEffect = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shieldEffect.name = "Simple Shield";
            shieldEffect.transform.SetParent(shieldRoot, false);
            Destroy(shieldEffect.GetComponent<Collider>());
        }

        ApplyTransform();
    }

    private GameObject FindSceneVisualEffect()
    {
        if (string.IsNullOrWhiteSpace(sceneVisualEffectName))
        {
            return FindSceneVisualEffectByName("ShieldAuraPreview") ?? FindSceneVisualEffectByName("ShieidAuraPreview");
        }

        return FindSceneVisualEffectByName(sceneVisualEffectName)
            ?? FindSceneVisualEffectByName("ShieldAuraPreview")
            ?? FindSceneVisualEffectByName("ShieidAuraPreview");
    }

    private GameObject FindSceneVisualEffectByName(string visualEffectName)
    {
        if (string.IsNullOrWhiteSpace(visualEffectName))
        {
            return null;
        }

        var children = GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (child.name == visualEffectName)
            {
                return child.gameObject;
            }
        }

        var rootObjects = gameObject.scene.GetRootGameObjects();
        foreach (var rootObject in rootObjects)
        {
            var transforms = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                if (transform.name == visualEffectName)
                {
                    return transform.gameObject;
                }
            }
        }

        return null;
    }

    private void ConfigureParticleRenderers()
    {
        if (!forceParticleLocalAlignment || shieldEffect == null)
        {
            return;
        }

        var renderers = shieldEffect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var renderer in renderers)
        {
            renderer.alignment = ParticleSystemRenderSpace.Local;
        }
    }

    private Transform GetShieldParent()
    {
        if (attachToMainCamera && Camera.main != null)
        {
            return Camera.main.transform;
        }

        return transform;
    }

    private void SetShieldVisible(bool visible)
    {
        if (shieldEffect == null)
        {
            return;
        }

        shieldVisibleTarget = visible;
        if (visibilityCoroutine != null)
        {
            StopCoroutine(visibilityCoroutine);
        }

        visibilityCoroutine = StartCoroutine(AnimateShieldVisibility(visible));
    }

    private IEnumerator AnimateShieldVisibility(bool visible)
    {
        CaptureBaseScale();
        var target = shieldRoot != null ? shieldRoot : shieldEffect.transform;
        var duration = Mathf.Max(0.01f, visible ? appearSeconds : disappearSeconds);
        var startScale = target.localScale;
        var endScale = visible ? baseShieldScale : Vector3.zero;

        if (visible)
        {
            shieldEffect.SetActive(true);
            startScale = Vector3.zero;
            target.localScale = startScale;
        }

        for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            var t = Mathf.Clamp01(elapsed / duration);
            t = visible ? EaseOutBack(t) : EaseIn(t);
            target.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            yield return null;
        }

        target.localScale = endScale;

        if (!visible)
        {
            shieldEffect.SetActive(false);
            target.localScale = baseShieldScale;
        }

        visibilityCoroutine = null;
    }

    private void CaptureBaseScale()
    {
        var target = shieldRoot != null ? shieldRoot : shieldEffect != null ? shieldEffect.transform : null;
        if (target == null)
        {
            return;
        }

        if (target.localScale.sqrMagnitude > 0.0001f)
        {
            baseShieldScale = target.localScale;
        }
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseIn(float t)
    {
        return t * t;
    }

    private IEnumerator MagicAttackRoutine(
    GameObject target)
    {
        // 魔法陣展開後に0.5秒待つ
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, magicAttackDelay)
        );

        if (target == null)
        {
            MagicCarpetGameFlow.CompleteMagicAttack();
            magicAttackCoroutine = null;
            yield break;
        }

        if (magicProjectileEffect == null)
        {
            Debug.LogWarning(
                "Magic Projectile Effect is not assigned.",
                this
            );

            MagicCarpetGameFlow.CompleteMagicAttack();
            magicAttackCoroutine = null;
            yield break;
        }

        Vector3 spawnPosition;

        if (magicProjectileSpawnPoint != null)
        {
            spawnPosition =
                magicProjectileSpawnPoint.position;
        }
        else
        {
            spawnPosition =
                transform.TransformPoint(
                    magicProjectileSpawnOffset
                );
        }

        GameObject projectile =
            Instantiate(
                magicProjectileEffect,
                spawnPosition,
                Quaternion.identity
            );

        projectile.name = "Magic Projectile Runtime";
        projectile.transform.localScale =
    magicProjectileScale;
        projectile.SetActive(true);

        SetParticleSystemsToUnscaled(projectile);

        while (projectile != null &&
               target != null)
        {
            Vector3 targetPosition =
                GetTargetPosition(target);

            projectile.transform.position =
                Vector3.MoveTowards(
                    projectile.transform.position,
                    targetPosition,
                    Mathf.Max(0.01f, magicProjectileSpeed)
                    * Time.unscaledDeltaTime
                );

            Vector3 direction =
                targetPosition -
                projectile.transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                projectile.transform.rotation =
                    Quaternion.LookRotation(direction)
                    *Quaternion.Euler(
                       magicProjectileRotationOffset
                     );
            }

            float distance =
                Vector3.Distance(
                    projectile.transform.position,
                    targetPosition
                );

            if (distance <=
                Mathf.Max(0.01f, magicHitDistance))
            {
                break;
            }

            yield return null;
        }

        if (target == null)
        {
            if (projectile != null)
            {
                Destroy(
                    projectile,
                    Mathf.Max(0f, magicProjectileRemainSeconds)
                );
            }

            // 命中した敵本体を一瞬赤くする
            StartCoroutine(
                FlashMonsterRed(
                    target,
                    monsterHitEffectSeconds
                )
            );

            // 赤い演出を見せる
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0f, monsterHitEffectSeconds)
            );

            MagicCarpetGameFlow.CompleteMagicAttack();
            magicAttackCoroutine = null;
            yield break;
        }

        // 命中したモンスターを停止
        var runtime =
            target.GetComponent<BallRuntimeController>();

        if (runtime != null)
        {
            runtime.StopAfterHit();
            runtime.DisableAllColliders();
        }

        if (projectile != null)
        {
            Destroy(projectile);
        }

        // 赤い演出を見せる
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, monsterHitEffectSeconds)
        );

        if (target != null)
        {
            Destroy(target);
        }

        // 命中した時点でゲーム進行を再開
        MagicCarpetGameFlow.CompleteMagicAttack();

        magicAttackCoroutine = null;
    }

    private Vector3 GetTargetPosition(
        GameObject target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        var renderers =
            target.GetComponentsInChildren<Renderer>(true);

        bool foundRenderer = false;
        Bounds combinedBounds = new Bounds();

        foreach (var targetRenderer in renderers)
        {
            if (targetRenderer == null ||
                !targetRenderer.enabled)
            {
                continue;
            }

            if (!foundRenderer)
            {
                combinedBounds = targetRenderer.bounds;
                foundRenderer = true;
            }
            else
            {
                combinedBounds.Encapsulate(
                    targetRenderer.bounds
                );
            }
        }

        if (foundRenderer)
        {
            return combinedBounds.center;
        }

        return target.transform.position;
    }

    private void SetParticleSystemsToUnscaled(
        GameObject effectObject)
    {
        if (effectObject == null)
        {
            return;
        }

        var particleSystems =
            effectObject.GetComponentsInChildren
                <ParticleSystem>(true);

        foreach (var particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            var main = particleSystem.main;
            main.useUnscaledTime = true;

            particleSystem.Play(true);
        }
    }
}
