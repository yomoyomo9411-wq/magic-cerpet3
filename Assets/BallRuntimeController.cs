using UnityEngine;

public class BallRuntimeController : MonoBehaviour
{
    public Transform player;
    public Vector3 velocity;
    public float destroyBehindDistance = 20f;
    public float touchCheckDistance = 10f;
    public float damage = 1f;

    private CarpetMove carpet;
    private Collider[] ballColliders;
    private Collider[] playerColliders;

    private bool hitReported;
    private bool passReported;

    private void Awake()
    {
        ballColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        if (player != null)
        {
            carpet = player.GetComponent<CarpetMove>();
            playerColliders = player.GetComponentsInChildren<Collider>(true);
        }
    }

    public bool ClaimHit()
    {
        if (hitReported)
        {
            return false;
        }

        hitReported = true;
        passReported = true;
        return true;
    }

    public void StopAfterHit()
    {
        velocity = Vector3.zero;
        hitReported = true;
        passReported = true;
        enabled = false;
    }

    public void DisableAllColliders()
    {
        if (ballColliders == null)
        {
            return;
        }

        foreach (var collider in ballColliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    private void Update()
    {
        transform.position += velocity * Time.deltaTime;

        if (player == null)
        {
            return;
        }

        // プレイヤーとの接触判定
        if (!hitReported &&
            IsNearPlayerForTouchCheck() &&
            IsTouchingPlayer())
        {
            hitReported = true;
            passReported = true;

            if (carpet != null)
            {
                carpet.HandleRuntimeBulletHit(gameObject);
            }

            return;
        }

        // 球がプレイヤーを完全に通過したか判定
        if (GetFrontZ() < player.position.z)
        {
            if (!hitReported && !passReported)
            {
                passReported = true;

                var reporter =
                    GetComponentInParent<TutorialBallResultReporter>();

                if (reporter == null)
                {
                    reporter =
                        GetComponentInChildren<TutorialBallResultReporter>();
                }

                if (reporter != null &&
                    !MagicCarpetGameFlow.IsMainGameStarted)
                {
                    reporter.ReportPassSuccess();
                }
                else if (carpet != null)
                {
                    carpet.HandleBulletPassed(gameObject);
                }
            }

            velocity = Vector3.zero;
            enabled = false;
            Destroy(gameObject, 5f);
        }
    }

    private float GetFrontZ()
    {
        float frontZ = transform.position.z;

        if (ballColliders == null)
        {
            return frontZ;
        }

        foreach (var col in ballColliders)
        {
            if (col == null || !col.enabled)
            {
                continue;
            }

            frontZ = Mathf.Max(frontZ, col.bounds.max.z);
        }

        return frontZ;
    }

    private bool IsTouchingPlayer()
    {
        Physics.SyncTransforms();

        if (playerColliders == null || ballColliders == null)
        {
            return false;
        }

        foreach (var ballCollider in ballColliders)
        {
            if (ballCollider == null || !ballCollider.enabled)
            {
                continue;
            }

            foreach (var playerCollider in playerColliders)
            {
                if (playerCollider == null || !playerCollider.enabled)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        ballCollider,
                        ballCollider.transform.position,
                        ballCollider.transform.rotation,
                        playerCollider,
                        playerCollider.transform.position,
                        playerCollider.transform.rotation,
                        out _,
                        out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsNearPlayerForTouchCheck()
    {
        if (player == null)
        {
            return false;
        }

        return Mathf.Abs(
            transform.position.z - player.position.z
        ) <= touchCheckDistance;
    }
}