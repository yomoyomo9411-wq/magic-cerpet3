using System.Collections;
using UnityEngine;

public class ShieldController : MonoBehaviour
{
    [Header("Shield")]
    public bool showVisualEffect = false;
    public bool alwaysVisible = false;
    public float shieldDuration = 2f;
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
}
