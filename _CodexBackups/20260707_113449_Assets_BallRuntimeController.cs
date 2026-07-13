using UnityEngine;

public class BallRuntimeController : MonoBehaviour
{
    public Transform player;
    public Vector3 velocity;
    public float destroyBehindDistance = 20f;

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
        return true;
    }

    public void StopAfterHit()
    {
        velocity = Vector3.zero;
        enabled = false;
    }

    private void Update()
    {
        transform.position += velocity * Time.deltaTime;

        if (player == null)
        {
            return;
        }

        if (transform.position.z < player.position.z - destroyBehindDistance)
        {
            if (!hitReported && !passReported && carpet != null)
            {
                passReported = true;
                carpet.HandleBulletPassed(gameObject);
            }

            Destroy(gameObject);
            return;
        }

        if (!hitReported && IsTouchingPlayer())
        {
            hitReported = true;
            if (carpet != null)
            {
                carpet.HandleRuntimeBulletHit(gameObject);
            }
        }
    }

    private bool IsTouchingPlayer()
    {
        Physics.SyncTransforms();

        if (ballColliders == null || playerColliders == null)
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
}
