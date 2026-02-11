using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float rotationSpeed = 100f;

    void Update()
    {
        transform.Rotate(new Vector3(-10, rotationSpeed, 0) * Time.deltaTime);
    }
}
