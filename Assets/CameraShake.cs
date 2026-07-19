using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.15f;

    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    void Awake()
    {
        originalPosition = transform.localPosition;
    }

    public void Shake()
    {
        Shake(1f);
    }

    public void Shake(float intensity)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeRoutine(Mathf.Clamp01(intensity)));
    }

    public void ShakeTutorial(float intensity, float durationSeconds)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeRoutine(
            Mathf.Clamp01(intensity),
            Mathf.Max(0.01f, durationSeconds)));
    }

    IEnumerator ShakeRoutine(float intensity)
    {
        yield return ShakeRoutine(intensity, -1f);
    }

    IEnumerator ShakeRoutine(float intensity, float durationOverride)
    {
        float elapsed = 0f;

        float duration = durationOverride > 0f ? durationOverride : shakeDuration * intensity;
        float magnitude = shakeMagnitude * intensity;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalPosition + new Vector3(x, y, 0f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        shakeCoroutine = null;
    }

    public void ResetShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        transform.localPosition = originalPosition;
    }
}
