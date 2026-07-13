using UnityEngine;

public class MediaPipePoseToShoulderInput : MonoBehaviour
{
    public ShoulderInput shoulderInput;

    private const int LeftShoulderIndex = 11;
    private const int RightShoulderIndex = 12;

    public void OnPoseLandmarksDetected(Vector2[] landmarks)
    {
        if (shoulderInput == null)
        {
            return;
        }

        if (landmarks == null || landmarks.Length <= RightShoulderIndex)
        {
            shoulderInput.LostBody();
            return;
        }

        Vector2 leftShoulder = landmarks[LeftShoulderIndex];
        Vector2 rightShoulder = landmarks[RightShoulderIndex];

        shoulderInput.SetShoulders(leftShoulder, rightShoulder);
    }

    public void OnPoseLost()
    {
        if (shoulderInput != null)
        {
            shoulderInput.LostBody();
        }
    }
}