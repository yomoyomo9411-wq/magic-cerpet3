using UnityEngine;

public class MonsterIdleMotion : MonoBehaviour
{
    public float bobHeight = 1.3f;
    public float bobSpeed = 2.2f;
    public float yawDegrees = 8f;
    public float rollDegrees = 3f;
    public float scalePulse = 0.15f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private float phase;
    private bool initialized;

    private void Awake()
    {
        phase = Random.Range(0f, 10f);
    }

    private void Start()
    {
        if (!initialized)
        {
            CaptureBaseTransform();
        }
    }

    public void CaptureBaseTransform()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalScale = transform.localScale;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        var wave = Mathf.Sin((Time.time + phase) * bobSpeed);
        var sideWave = Mathf.Sin((Time.time + phase) * bobSpeed * 0.67f);

        transform.localPosition =
            baseLocalPosition + Vector3.up * (wave * bobHeight);

        transform.localRotation =
            baseLocalRotation *
            Quaternion.Euler(
                0f,
                sideWave * yawDegrees,
                wave * rollDegrees
            );

        var pulse = 1f + wave * scalePulse;

        transform.localScale = new Vector3(
            baseLocalScale.x * pulse,
            baseLocalScale.y * (1f - wave * scalePulse * 0.4f),
            baseLocalScale.z * pulse
        );
    }
}