using UnityEngine;

public class ShieldEnemyDestroyer : MonoBehaviour
{
    [Tooltip("ÉvÉåÉCÉÑÅ[ÇÃCarpetMove")]
    public CarpetMove carpetMove;

    private void Awake()
    {
        if (carpetMove == null)
        {
            carpetMove = GetComponentInParent<CarpetMove>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        DestroyEnemy(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        DestroyEnemy(collision.gameObject);
    }

    private void DestroyEnemy(GameObject enemyObject)
    {
        if (enemyObject == null || !enemyObject.CompareTag("Bullet"))
        {
            return;
        }

        if (carpetMove == null ||
            carpetMove.shieldController == null ||
            !carpetMove.shieldController.IsShieldActive)
        {
            return;
        }

        carpetMove.DestroyBulletImmediately(enemyObject);
    }
}