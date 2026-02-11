using UnityEngine;

public class GrabAndThrow : MonoBehaviour
{
    [Header("Settings")]
    public float grabRange = 2f;
    public float holdDistance = 0.5f;
    public float throwForceMultiplier = 3f;
    public Transform cameraTransform;
    public LayerMask throwableLayer;

    private GameObject heldObject;
    private Rigidbody heldRb;
    private bool wasGripping = false;

    private Vector3 previousControllerPos;
    private Vector3 controllerVelocity;

    private LineRenderer laserLine;

    void Start()
    {
        laserLine = gameObject.AddComponent<LineRenderer>();
        laserLine.startWidth = 0.005f;
        laserLine.endWidth = 0.005f;

        laserLine.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        laserLine.material.color = Color.red;
        laserLine.material.EnableKeyword("_EMISSION");
        laserLine.material.SetColor("_EmissionColor", Color.red);

        laserLine.positionCount = 2;
        laserLine.useWorldSpace = true;
    }
    void Update()
    {
        var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerValue);
        bool isGripping = triggerValue > 0.7f;

        Vector3 currentPos = transform.position;
        controllerVelocity = (currentPos - previousControllerPos) / Time.deltaTime;
        previousControllerPos = currentPos;

        if (heldObject == null)
        {
            laserLine.enabled = true;
            laserLine.SetPosition(0, transform.position);
            laserLine.SetPosition(1, transform.position + transform.forward * grabRange);
        }
        else
        {
            laserLine.enabled = false;
        }

        if (isGripping && !wasGripping)
        {
            TryGrab();
        }
        else if (!isGripping && wasGripping && heldObject != null)
        {
            ThrowObject();
        }

        if (isGripping && heldObject != null)
        {
            HoldObject();
        }

        wasGripping = isGripping;
    }

    void TryGrab()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, grabRange, throwableLayer))
        {
            if (hit.collider.CompareTag("Throwable"))
            {
                heldObject = hit.collider.gameObject;
                heldRb = heldObject.GetComponent<Rigidbody>();
                heldRb.isKinematic = true;
                heldRb.useGravity = false;
            }
        }
    }

    void HoldObject()
    {
        Vector3 holdPos = cameraTransform.position + cameraTransform.forward * holdDistance;
        heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, holdPos, Time.deltaTime * 15f);
        heldObject.transform.rotation = cameraTransform.rotation;
    }

    void ThrowObject()
    {
        heldRb.isKinematic = false;
        heldRb.useGravity = true;
        heldRb.linearVelocity = controllerVelocity * throwForceMultiplier;

        heldObject = null;
        heldRb = null;
    }
}