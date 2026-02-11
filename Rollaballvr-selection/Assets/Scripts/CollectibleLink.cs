using UnityEngine;

public class CollectibleLink : MonoBehaviour
{
    public GameObject linkedEnemy;

    void Start()
    {
        // Ensure the enemy starts hidden
        if (linkedEnemy != null)
        {
            linkedEnemy.SetActive(false);
        }
    }
}
