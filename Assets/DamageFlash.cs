using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageFlash : MonoBehaviour
{
    private Image flashImage;

    public float flashAlpha = 0.5f;
    public float fadeSpeed = 2f;

    private Coroutine flashCoroutine;

    void Awake()
    {
        flashImage = GetComponent<Image>();
    }

    public void Flash()
    {
        Flash(1f);
    }

    public void Flash(float intensity)
    {
        if (flashImage == null) return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRoutine(Mathf.Clamp01(intensity)));
    }

    IEnumerator FlashRoutine(float intensity)
    {
        Color c = flashImage.color;
        c.a = flashAlpha * intensity;
        flashImage.color = c;

        while (flashImage.color.a > 0)
        {
            c = flashImage.color;
            c.a -= fadeSpeed * Time.deltaTime;
            flashImage.color = c;
            yield return null;
        }

        c.a = 0;
        flashImage.color = c;
    }
}
