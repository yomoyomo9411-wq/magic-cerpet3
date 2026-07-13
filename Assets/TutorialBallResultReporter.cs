using UnityEngine;

public class TutorialBallResultReporter : MonoBehaviour
{
    public Transform player;
    public float passedDistance = 2f;
    public bool ignorePassSuccess;

    private bool reported;

    public void ReportFailureAndDestroy()
    {
        ReportFailure();
    }

    public void ReportFailure()
    {
        if (reported)
        {
            return;
        }

        reported = true;
        enabled = false;
        MagicCarpetGameFlow.ReportTutorialFailure();
    }

    public void ReportPassSuccess()
    {
        if (reported)
        {
            return;
        }

        reported = true;
        enabled = false;

        if (ignorePassSuccess)
        {
            return;
        }

        MagicCarpetGameFlow.ReportTutorialSuccess();
    }

    public void MarkHandled()
    {
        reported = true;
        enabled = false;
    }

    private void Update()
    {
        if (reported || player == null || MagicCarpetGameFlow.IsMainGameStarted || MagicCarpetGameFlow.IsTutorialResultLocked)
        {
            return;
        }

        if (transform.position.z < player.position.z - passedDistance)
        {
            reported = true;
            enabled = false;
            if (ignorePassSuccess)
            {
                return;
            }

            MagicCarpetGameFlow.ReportTutorialSuccess();
        }
    }
}
