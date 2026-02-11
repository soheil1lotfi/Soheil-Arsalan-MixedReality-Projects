using UnityEngine;

public class LightEctsIntensity : MonoBehaviour
{
    public PlayerController player;
    public Light targetLight;
    public float baseIntensity = 0.1f;
    public float perEctsIntensity = 0.1f;

    private void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }
    }

    private void Update()
    {
        if (player == null || targetLight == null)
        {
            return;
        }

        targetLight.intensity = baseIntensity + (player.GetEcts() * perEctsIntensity);
    }
}
