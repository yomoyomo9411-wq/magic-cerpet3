using UnityEngine;

public class ShieldAuraPreviewAnimator : MonoBehaviour
{
    public float rotationSpeed = 18f;
    public float pulseSpeed = 1.4f;
    public float pulseAmount = 0.08f;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);

        var pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
