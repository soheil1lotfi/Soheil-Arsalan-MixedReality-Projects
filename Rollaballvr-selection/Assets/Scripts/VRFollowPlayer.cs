using UnityEngine;

public class VRFollowPlayer : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        if (player != null)
        {
            transform.position = player.position;
        }
    }
}