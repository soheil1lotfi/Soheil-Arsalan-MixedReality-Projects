// using UnityEngine;

// public class CubeProjectile : MonoBehaviour
// {
//     void OnCollisionEnter(Collision collision)
//     {
//         if (collision.collider.CompareTag("Enemy"))
//         {
//             Destroy(collision.collider.gameObject);
//             Destroy(gameObject);
//         }
//     }
// }
using UnityEngine;

public class CubeProjectile : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Enemy"))
        {
            DeadlineMovement enemy = collision.collider.GetComponentInParent<DeadlineMovement>();
            if (enemy != null)
            {
                enemy.Die();
            }
            Destroy(gameObject);
        }
    }
}