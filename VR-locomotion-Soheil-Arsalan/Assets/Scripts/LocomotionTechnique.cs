using UnityEngine;
using System;
using System.IO;
using System.Text;

public class LocomotionTechnique : MonoBehaviour
{
    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;
    public GameObject hmd;

    [Header("Speed")]
    public float maxSpeed = 5f;
    public float acceleration = 2f;
    public float deceleration = 3f;
    private float currentSpeed;
    private float currentLeanSpeed;
    private float currentBoostSpeed;

    [Header("Distance -> Speed")]
    public float distanceThreshold = 0.8f;
    public float responsePower = 2f;

    [Header("Boost")]
    public OVRInput.Button boostButton = OVRInput.Button.PrimaryIndexTrigger;
    public float boostMaxSpeed = 5f;
    public float boostAcceleration = 5f;

    [Header("Wand")]
    public Transform wand;

    [Header("Portals")]
    public GameObject portalPrefab;
    public OVRInput.Button placePortalButton = OVRInput.Button.Four;
    public float portalMinDistance = 2f;
    public float portalMaxDistance = 50f;
    public float portalDistanceSpeed = 10f;
    private GameObject[] portals = new GameObject[2];
    private int nextPortalIndex = 0;
    private float currentPortalDistance = 2f;
    private bool isPlacingPortal = false;
    private GameObject portalPreview;

    [Header("Broom")]
    public Transform broom;
    public OVRInput.Button calibrateButton = OVRInput.Button.One;
    public Vector3 broomLocalAxis = Vector3.forward;
    public Vector3 leftControllerLocalAxis = Vector3.forward;
    private bool calibrated = false;
    private Quaternion broomBaseRotation;
    private Vector3 broomAxisDir = Vector3.forward;
    private Quaternion calibrationOffset = Quaternion.identity;
    private Vector3 calibratedNeutralForward = Vector3.forward;
    private float calibratedBroomLength = 0f;

    [Header("Start Gate")]
    public bool waitForStartButton = true;
    private bool locomotionStarted = false;

    [Header("Logging")]
    public float logInterval = 0.1f; // seconds between position samples
    public string participantId = "P00";

    // These are for the game mechanism.
    public ParkourCounter parkourCounter;
    public string stage;
    public SelectionTaskMeasure selectionTaskMeasure;

    private float logTimer;
    private float sessionStartTime;
    private bool loggingActive = false;
    private string logFilePath;
    private StreamWriter logWriter;

    void Start()
    {
        locomotionStarted = !waitForStartButton;
        if (!locomotionStarted) ResetSpeeds();

        InitLog();
    }

    void Update()
    {
        if (!locomotionStarted)
        {
            if (!OVRInput.GetDown(calibrateButton, leftController))
            {
                ResetSpeeds();
                return;
            }
            locomotionStarted = true;
            LogEvent("LOCOMOTION_START");
        }

        if (wand != null)
        {
            wand.position = ControllerWorldPos(leftController);
            wand.rotation = ControllerWorldRot(leftController);
        }
        if (portalPrefab != null) UpdatePortals();

        UpdateSpeed();
        UpdateBroomCalibration();
        Move();
        // HandleRespawn();
        UpdateLog();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            LogEvent("SESSION_PAUSE");
            CloseLog();
        }
    }

    void OnApplicationQuit()
    {
        LogEvent("SESSION_END");
        CloseLog();
    }

    void OnDestroy()
    {
        CloseLog();
    }

    // Logging

    void InitLog()
    {
        sessionStartTime = Time.realtimeSinceStartup;
        logTimer = 0f;

        string folder = Path.Combine("/sdcard/Documents", Application.productName + "_Logs");
        Directory.CreateDirectory(folder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"locomotion_{participantId}_{timestamp}.txt";
        logFilePath = Path.Combine(folder, filename);

        logWriter = new StreamWriter(logFilePath, append: true) { AutoFlush = true };

        // CSV header
        logWriter.WriteLine(
            "time,event,stage," +
            "pos_x,pos_y,pos_z," +
            "hmd_yaw,hmd_pitch," +
            "speed,lean_speed,boost_speed," +
            "broom_calibrated," +
            "broom_dir_x,broom_dir_y,broom_dir_z," +
            "portal0_x,portal0_y,portal0_z," +
            "portal1_x,portal1_y,portal1_z," +
            "coins,detail"
        );

        loggingActive = true;
        LogEvent("SESSION_START");
        Debug.Log($"[Logger] Writing to {logFilePath}");
    }

    void UpdateLog()
    {
        if (!loggingActive) return;

        logTimer += Time.deltaTime;
        if (logTimer >= logInterval)
        {
            logTimer -= logInterval;
            LogEvent("SAMPLE");
        }
    }

    void LogEvent(string eventType, string detail = "")
    {
        if (!loggingActive || logWriter == null) return;

        float t = Time.realtimeSinceStartup - sessionStartTime;

        Vector3 pos = transform.position;

        float hmdYaw = 0f, hmdPitch = 0f;
        if (hmd != null)
        {
            Vector3 fwd = hmd.transform.forward;
            hmdYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            hmdPitch = Mathf.Asin(fwd.y) * Mathf.Rad2Deg;
        }

        Vector3 p0 = portals[0] != null ? portals[0].transform.position : Vector3.one * float.NaN;
        Vector3 p1 = portals[1] != null ? portals[1].transform.position : Vector3.one * float.NaN;

        int coins = parkourCounter != null ? parkourCounter.coinCount : 0;

        detail = detail.Replace(",", ";");

        try
        {
            logWriter.WriteLine(string.Format(
                "{0:F3},{1},{2}," +
                "{3:F3},{4:F3},{5:F3}," +
                "{6:F1},{7:F1}," +
                "{8:F3},{9:F3},{10:F3}," +
                "{11}," +
                "{12:F3},{13:F3},{14:F3}," +
                "{15:F3},{16:F3},{17:F3}," +
                "{18:F3},{19:F3},{20:F3}," +
                "{21},{22}",
                t, eventType, stage ?? "",
                pos.x, pos.y, pos.z,
                hmdYaw, hmdPitch,
                currentSpeed, currentLeanSpeed, currentBoostSpeed,
                calibrated ? 1 : 0,
                broomAxisDir.x, broomAxisDir.y, broomAxisDir.z,
                p0.x, p0.y, p0.z,
                p1.x, p1.y, p1.z,
                coins, detail
            ));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Logger] Write failed: {e.Message}");
        }
    }

    void CloseLog()
    {
        if (logWriter == null) return;

        try
        {
            logWriter.Close();
            logWriter = null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Logger] Close failed: {e.Message}");
        }
    }


    void ResetSpeeds()
    {
        currentSpeed = currentLeanSpeed = currentBoostSpeed = 0f;
    }

    void UpdatePortals()
    {
        if (OVRInput.GetDown(placePortalButton))
        {
            isPlacingPortal = true;
            currentPortalDistance = portalMinDistance;
            EnsurePortalPreview();
        }

        if (isPlacingPortal && OVRInput.Get(placePortalButton))
        {
            Vector3 controllerPos = ControllerWorldPos(leftController);
            Vector3 forward = ControllerWorldRot(leftController) * Vector3.forward;

            float verticalAngle = Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            currentPortalDistance = Mathf.Clamp(
                currentPortalDistance + (verticalAngle / 90f) * portalDistanceSpeed * Time.deltaTime,
                portalMinDistance,
                portalMaxDistance
            );

            Vector3 horizontalForward = forward;
            horizontalForward.y = 0;
            horizontalForward.Normalize();

            Vector3 portalPos = controllerPos + horizontalForward * currentPortalDistance;
            portalPos.y = (broom != null) ? broom.position.y : 0;

            Quaternion portalRot = Quaternion.LookRotation(-horizontalForward);

            if (portalPreview != null)
            {
                portalPreview.transform.position = portalPos;
                portalPreview.transform.rotation = portalRot;
            }
            Debug.DrawLine(controllerPos, portalPos, Color.magenta);
        }

        if (OVRInput.GetUp(placePortalButton) && isPlacingPortal)
        {
            isPlacingPortal = false;

            Vector3 controllerPos = ControllerWorldPos(leftController);
            Vector3 forward = ControllerWorldRot(leftController) * Vector3.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 portalPos = controllerPos + forward * currentPortalDistance;
            portalPos.y = (broom != null) ? broom.position.y : 0;

            Quaternion portalRot = Quaternion.LookRotation(-forward);

            if (nextPortalIndex == 0)
            {
                if (portals[0] != null) Destroy(portals[0]);
                portals[0] = Instantiate(portalPrefab, portalPos, portalRot);
                portals[0].name = "Portal_Checkpoint";
                LogEvent("PORTAL_CHECKPOINT", $"pos={portalPos}");
                Debug.Log("Checkpoint placed at " + portalPos);
                nextPortalIndex = 1;
            }
            else if (nextPortalIndex == 1)
            {
                if (portals[1] != null) Destroy(portals[1]);
                portals[1] = Instantiate(portalPrefab, portalPos, portalRot);
                portals[1].name = "Portal_Teleporter";
                LogEvent("PORTAL_TELEPORTER", $"pos={portalPos}");
                Debug.Log("Portal placed at " + portalPos);
                nextPortalIndex = 2;
            }
            else
            {
                if (portals[0] != null) Destroy(portals[0]);
                if (portals[1] != null) Destroy(portals[1]);
                portals[0] = Instantiate(portalPrefab, portalPos, portalRot);
                portals[0].name = "Portal_Checkpoint";
                portals[1] = null;
                LogEvent("PORTAL_RESET", $"pos={portalPos}");
                Debug.Log("Reset. New checkpoint at " + portalPos);
                nextPortalIndex = 1;
            }

            if (portalPreview != null)
            {
                Destroy(portalPreview);
                portalPreview = null;
            }
        }
    }

    void EnsurePortalPreview()
    {
        if (portalPreview != null) return;

        portalPreview = Instantiate(portalPrefab);
        portalPreview.name = "PortalPreview";

        Collider previewCollider = portalPreview.GetComponent<Collider>();
        if (previewCollider != null) previewCollider.enabled = false;
    }

    void UpdateSpeed()
    {
        float distance = Vector3.Distance(OVRInput.GetLocalControllerPosition(rightController), hmd.transform.localPosition);
        float targetLeanSpeed = 0f;
        if (distance < distanceThreshold)
        {
            float t = 1f - (distance / distanceThreshold);
            targetLeanSpeed = maxSpeed * Mathf.Pow(t, responsePower);
        }

        float leanRate = (targetLeanSpeed > currentLeanSpeed ? acceleration : deceleration) * Time.deltaTime;
        currentLeanSpeed = Mathf.MoveTowards(currentLeanSpeed, targetLeanSpeed, leanRate);

        bool boosting = OVRInput.Get(boostButton);
        float boostTarget = boosting ? boostMaxSpeed : 0f;
        float boostRate = (boostTarget > currentBoostSpeed ? boostAcceleration : deceleration) * Time.deltaTime;
        currentBoostSpeed = Mathf.MoveTowards(currentBoostSpeed, boostTarget, boostRate);

        currentSpeed = currentLeanSpeed + currentBoostSpeed;
    }

    void UpdateBroomCalibration()
    {
        if (OVRInput.GetDown(calibrateButton, leftController))
        {
            broomBaseRotation = broom.rotation;
            calibrated = true;
            calibratedNeutralForward = (ControllerWorldRot(rightController) * Vector3.forward).normalized;

            Vector3 calibRightPos = ControllerWorldPos(rightController);
            Vector3 calibLeftPos = ControllerWorldPos(leftController);
            calibratedBroomLength = Vector3.Distance(calibRightPos, calibLeftPos);

            LogEvent("BROOM_CALIBRATE", $"length={calibratedBroomLength:F3}");
        }

        if (!calibrated) return;

        Quaternion rightControllerRot = ControllerWorldRot(rightController);
        Vector3 rightPos = ControllerWorldPos(rightController);

        broomAxisDir = (rightControllerRot * Vector3.forward).normalized;
        broom.position = rightPos;

        Vector3 rightUp = rightControllerRot * Vector3.up;
        broom.rotation = Quaternion.LookRotation(broomAxisDir, rightUp);
    }

    void Move()
    {
        if (currentSpeed <= 0f) return;

        Vector3 moveDir;
        if (calibrated)
        {
            Vector3 currentForward = (ControllerWorldRot(rightController) * Vector3.forward).normalized;

            float neutralPitch = Mathf.Asin(calibratedNeutralForward.y) * Mathf.Rad2Deg;
            float currentPitch = Mathf.Asin(currentForward.y) * Mathf.Rad2Deg;
            float pitchOffset = currentPitch - neutralPitch;

            Vector3 horizontalDir = broomAxisDir;
            horizontalDir.y = 0;
            horizontalDir.Normalize();

            float verticalComponent = Mathf.Sin(pitchOffset * Mathf.Deg2Rad);
            moveDir = (horizontalDir + Vector3.up * verticalComponent).normalized;
        }
        else
        {
            moveDir = hmd.transform.forward;
        }

        transform.position += moveDir.normalized * currentSpeed * Time.deltaTime;
    }

    // void HandleRespawn()
    // {
    //     if ((OVRInput.Get(OVRInput.Button.Two) || OVRInput.Get(OVRInput.Button.Four)) && parkourCounter.parkourStart)
    //     {
    //         LogEvent("RESPAWN", $"target={parkourCounter.currentRespawnPos}");
    //         transform.position = parkourCounter.currentRespawnPos;
    //     }
    // }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("banner"))
        {
            stage = other.gameObject.name;
            parkourCounter.isStageChange = true;
            LogEvent("STAGE_CHANGE", stage);
        }
        else if (other.CompareTag("objectInteractionTask"))
        {
            selectionTaskMeasure.isTaskStart = true;
            selectionTaskMeasure.scoreText.text = "";
            selectionTaskMeasure.partSumErr = 0f;
            selectionTaskMeasure.partSumTime = 0f;

            float tempValueY = other.transform.position.y > 0 ? 12 : 0;
            Vector3 tmpTarget = new Vector3(hmd.transform.position.x, tempValueY, hmd.transform.position.z);
            selectionTaskMeasure.taskUI.transform.LookAt(tmpTarget);
            selectionTaskMeasure.taskUI.transform.Rotate(new Vector3(0, 180f, 0));
            selectionTaskMeasure.taskStartPanel.SetActive(true);

            LogEvent("TASK_START", other.gameObject.name);
        }
        else if (other.CompareTag("coin"))
        {
            parkourCounter.coinCount += 1;
            GetComponent<AudioSource>().Play();
            other.gameObject.SetActive(false);
            LogEvent("COIN", $"total={parkourCounter.coinCount}");
        }
        else if (other.CompareTag("portal"))
        {
            if (portals[0] != null && portals[1] != null && other.gameObject == portals[1])
            {
                Vector3 dest = portals[0].transform.position - portals[0].transform.forward * 1.5f;
                LogEvent("PORTAL_RESPAWN", $"dest={dest}");
                transform.position = dest;
            }
        }
    }

    Quaternion ControllerWorldRot(OVRInput.Controller c)
    {
        Transform space = hmd != null ? hmd.transform.parent : null;
        return space == null ? OVRInput.GetLocalControllerRotation(c) : space.rotation * OVRInput.GetLocalControllerRotation(c);
    }

    Vector3 ControllerWorldPos(OVRInput.Controller c)
    {
        Transform space = hmd != null ? hmd.transform.parent : null;
        Vector3 localPos = OVRInput.GetLocalControllerPosition(c);
        return space == null ? localPos : space.TransformPoint(localPos);
    }
}