using UnityEngine;

public class CarpetLeanController : MonoBehaviour
{
    [Header("Shoulder Tracking")]
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform rightShoulder;
    [SerializeField] private float neutralDeadZoneDegrees = 4f;
    [SerializeField] private float maxLeanDegrees = 22f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float maxX = 4f;
    [SerializeField] private float smoothing = 8f;

    [Header("Debug")]
    [SerializeField] private bool keyboardFallback = true;
    [SerializeField] private float keyboardLean = 18f;

    private float smoothedInput;

    private void Update()
    {
        float leanInput = GetLeanInput();
        smoothedInput = Mathf.Lerp(smoothedInput, leanInput, Time.deltaTime * smoothing);

        Vector3 position = transform.position;
        position.x += smoothedInput * moveSpeed * Time.deltaTime;
        position.x = Mathf.Clamp(position.x, -maxX, maxX);
        transform.position = position;
    }

    private float GetLeanInput()
    {
        float leanDegrees = 0f;

        if (leftShoulder != null && rightShoulder != null)
        {
            Vector3 shoulderLine = rightShoulder.position - leftShoulder.position;
            leanDegrees = Mathf.Atan2(shoulderLine.y, shoulderLine.x) * Mathf.Rad2Deg;
        }
        else if (keyboardFallback)
        {
            leanDegrees = Input.GetAxisRaw("Horizontal") * keyboardLean;
        }

        if (Mathf.Abs(leanDegrees) < neutralDeadZoneDegrees)
        {
            return 0f;
        }

        return Mathf.Clamp(leanDegrees / maxLeanDegrees, -1f, 1f);
    }
}
