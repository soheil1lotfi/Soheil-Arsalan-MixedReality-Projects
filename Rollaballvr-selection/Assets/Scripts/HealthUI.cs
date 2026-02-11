using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    public RawImage[] hearts;

    public void SetHealth(int health)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].enabled = i < health;
        }
    }
}
