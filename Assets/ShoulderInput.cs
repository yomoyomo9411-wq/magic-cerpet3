using UnityEngine;

public class ShoulderInput : MonoBehaviour
{
    public bool IsDetected { get; private set; }
    public float InputValue { get; private set; }

    [Header("Œ¨‚ÌŒX‚«’²®")]
    public float deadZone = 0.03f;
    public float sensitivity = 4f;
    public float smooth = 8f;

    [Header("¶‰E”½“]")]
    public bool invert = false;

    private float currentInput = 0f;

    public void SetShoulders(Vector2 leftShoulder, Vector2 rightShoulder)
    {
        IsDetected = true;

        float diffY = leftShoulder.y - rightShoulder.y;

        float targetInput = 0f;

        if (Mathf.Abs(diffY) > deadZone)
        {
            targetInput = diffY * sensitivity;
        }

        targetInput = Mathf.Clamp(targetInput, -1f, 1f);

        if (invert)
        {
            targetInput *= -1f;
        }

        currentInput = Mathf.Lerp(
            currentInput,
            targetInput,
            smooth * Time.deltaTime
        );

        InputValue = currentInput;
    }

    public void LostBody()
    {
        IsDetected = false;

        currentInput = Mathf.Lerp(
            currentInput,
            0f,
            smooth * Time.deltaTime
        );

        InputValue = currentInput;
    }
}