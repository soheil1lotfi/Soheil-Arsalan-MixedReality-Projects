using UnityEngine;

public class VRPlayerMovement : MonoBehaviour
{
    public float moveSpeed = 10f;
    public Transform cameraTransform;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 joystickInput);

        if (joystickInput.sqrMagnitude > 0.01f)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * joystickInput.y + right * joystickInput.x);
            rb.AddForce(moveDirection * moveSpeed, ForceMode.Force);
        }
    }
}