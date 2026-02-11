using UnityEngine;

public class CollectibleTrigger : MonoBehaviour
{
    [Header("The enemy to spawn for this specific item")]
    public GameObject linkedEnemy; 

    private void Awake()
    {
        if (linkedEnemy != null)
        {
            linkedEnemy.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.AddEcts(5);
            }

            ActivateEnemy();
        }
    }

    void ActivateEnemy()
    {
        if (linkedEnemy != null)
        {
            if (linkedEnemy.transform.IsChildOf(transform))
            {
                linkedEnemy.transform.SetParent(null, true);
            }

            linkedEnemy.SetActive(true);
        }

        Destroy(gameObject);
    }
}
