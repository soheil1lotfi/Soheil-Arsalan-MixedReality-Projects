using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public GameObject player;
    public float distance = 5.0f;
    public float sensitivity = 0.5f;
    public float zoomSpeed = 0.25f;
    public float minDistance = 2.0f;
    public float maxDistance = 10.0f;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (player != null)
        {
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                rotationX += delta.x * sensitivity;
                rotationY -= delta.y * sensitivity;

                rotationY = Mathf.Clamp(rotationY, 0, 85);

                float scroll = Mouse.current.scroll.ReadValue().y;
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0);
            Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + player.transform.position;

            transform.rotation = rotation;
            transform.position = position;
        }
    }
}
