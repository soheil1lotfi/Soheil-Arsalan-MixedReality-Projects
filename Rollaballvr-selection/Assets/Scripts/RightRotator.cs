using UnityEngine;

public class VRSmoothTurn : MonoBehaviour
{
    public float turnSpeed = 60f;

    void Update()
    {
        var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 joystick);

        if (Mathf.Abs(joystick.x) > 0.2f)
        {
            transform.RotateAround(Camera.main.transform.position, Vector3.up, joystick.x * turnSpeed * Time.deltaTime);
        }
    }
}