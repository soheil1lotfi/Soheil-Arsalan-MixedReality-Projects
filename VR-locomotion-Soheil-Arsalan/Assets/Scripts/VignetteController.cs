using UnityEngine;

using System.Collections;

/// <summary>
/// Bridges OVR-based locomotion to the XR Interaction Toolkit's TunnelingVignetteController.
/// 
/// DIAGNOSTIC VERSION — includes:
///   - Force Test: activates the vignette 3s after startup (no movement needed)
///   - On-screen debug overlay: shows speed, timers, and state in the headset
/// 
/// If the force test shows the vignette → rendering works, problem is detection.
/// If the force test does NOT show it → the vignette itself can't render on Quest.
/// </summary>
[AddComponentMenu("VR/OVR Locomotion Vignette Driver")]
public class OVRLocomotionVignetteDriver : MonoBehaviour, UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.ITunnelingVignetteProvider
{
    // ─────────────────────────────────────────────
    //  Inspector Fields
    // ─────────────────────────────────────────────

    [Header("References")]

    [Tooltip("The TunnelingVignetteController to drive. Auto-detected if not assigned.")]
    [SerializeField]
    private UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController m_VignetteController;

    [Tooltip("The transform to track for movement. Should be whatever your locomotion moves.")]
    [SerializeField]
    private Transform m_TrackedTransform;

    [Header("Movement Detection")]

    [SerializeField]
    [Range(0.01f, 0.5f)]
    private float m_SpeedThreshold = 0.05f;

    [SerializeField]
    private bool m_HorizontalOnly = true;

    [Header("Rotation Detection")]

    [SerializeField]
    private bool m_DetectRotation = true;

    [SerializeField]
    [Range(1f, 90f)]
    private float m_RotationSpeedThreshold = 10f;

    [SerializeField]
    private bool m_YawOnly = true;

    [Header("Timing")]

    [SerializeField]
    [Range(0f, 5f)]
    private float m_MovementDurationThreshold = 2.0f;

    [SerializeField]
    [Range(0f, 1f)]
    private float m_MovementGracePeriod = 0.3f;

    [SerializeField]
    [Range(0f, 1f)]
    private float m_StillnessDelay = 0.1f;

    [Header("Optional: Custom Vignette Parameters")]

    [SerializeField]
    private bool m_OverrideParameters = false;

    [SerializeField]
    private UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.VignetteParameters m_CustomParameters;

    [Header("────── DIAGNOSTICS ──────")]

    [Tooltip("Forces the vignette ON 3 seconds after startup, holds for 4 seconds. " +
             "If you see it → rendering works. If not → vignette itself is broken.")]
    [SerializeField]
    private bool m_ForceTestOnStartup = true;

    [Tooltip("Shows real-time debug info as text overlay in the headset.")]
    [SerializeField]
    private bool m_ShowDebugOverlay = true;

    // ─────────────────────────────────────────────
    //  ITunnelingVignetteProvider
    // ─────────────────────────────────────────────

    public UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.VignetteParameters vignetteParameters =>
        m_OverrideParameters ? m_CustomParameters : null;

    // ─────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────

    private Vector3 _previousPosition;
    private Quaternion _previousRotation;
    private bool _isVignetteActive;
    private float _stillnessTimer;
    private float _movementTimer;
    private float _movementPauseTimer;

    // Debug
    private float _lastSpeed;
    private float _lastAngularSpeed;
    private bool _lastIsMoving;
    private string _status = "Initializing...";
    private bool _forceTestActive;

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        if (m_TrackedTransform == null)
        {
            m_TrackedTransform = transform;
            _status = "WARN: TrackedTransform null → using self";
            Debug.LogWarning($"[Vignette] {_status} ({gameObject.name})");
        }

        if (m_VignetteController == null)
            m_VignetteController = GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>();
        if (m_VignetteController == null)
            m_VignetteController = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>();

        if (m_VignetteController == null)
        {
            _status = "ERROR: No TunnelingVignetteController!";
            Debug.LogError($"[Vignette] {_status}");
            enabled = false;
            return;
        }

        _previousPosition = GetTrackedPosition();
        _previousRotation = m_TrackedTransform.rotation;
        _isVignetteActive = false;
        _stillnessTimer = 0f;
        _movementTimer = 0f;
        _movementPauseTimer = 0f;

        _status = "Ready";
        Debug.Log($"[Vignette] Init OK. Controller={m_VignetteController.gameObject.name}, " +
                  $"Tracked={m_TrackedTransform.gameObject.name}");
    }

    private void Start()
    {
        if (m_ForceTestOnStartup && m_VignetteController != null)
            StartCoroutine(ForceTestCoroutine());
    }

    private IEnumerator ForceTestCoroutine()
    {
        _status = "Force test: waiting 3s...";
        Debug.Log("[Vignette] Force test in 3s...");
        yield return new WaitForSeconds(3f);

        _status = ">>> FORCE TEST: VIGNETTE ON <<<";
        Debug.Log("[Vignette] >>> FORCE ACTIVATING <<<");
        _forceTestActive = true;
        m_VignetteController.BeginTunnelingVignette(this);

        yield return new WaitForSeconds(4f);

        _status = "Force test: turning OFF";
        Debug.Log("[Vignette] >>> FORCE DEACTIVATING <<<");
        m_VignetteController.EndTunnelingVignette(this);
        _forceTestActive = false;

        _status = "Force test done. Normal mode.";
    }

    private void OnDisable()
    {
        if (_isVignetteActive && m_VignetteController != null)
        {
            m_VignetteController.EndTunnelingVignette(this);
            _isVignetteActive = false;
        }
    }

    private void Update()
    {
        if (m_VignetteController == null || m_TrackedTransform == null)
        {
            _status = $"NULL REF! ctrl={m_VignetteController != null} trk={m_TrackedTransform != null}";
            return;
        }

        if (_forceTestActive)
            return;

        // --- Positional speed ---
        Vector3 currentPosition = GetTrackedPosition();
        float distance = Vector3.Distance(currentPosition, _previousPosition);
        _previousPosition = currentPosition;

        float speed = Time.deltaTime > Mathf.Epsilon ? distance / Time.deltaTime : 0f;
        bool isTranslating = speed > m_SpeedThreshold;

        // --- Rotational speed ---
        bool isRotating = false;
        float angularSpeed = 0f;
        if (m_DetectRotation)
        {
            Quaternion currentRotation = m_TrackedTransform.rotation;
            float angleDelta = GetAngularDelta(currentRotation);
            _previousRotation = currentRotation;

            angularSpeed = Time.deltaTime > Mathf.Epsilon ? angleDelta / Time.deltaTime : 0f;
            isRotating = angularSpeed > m_RotationSpeedThreshold;
        }
        else
        {
            _previousRotation = m_TrackedTransform.rotation;
        }

        bool isMoving = isTranslating || isRotating;

        _lastSpeed = speed;
        _lastAngularSpeed = angularSpeed;
        _lastIsMoving = isMoving;

        if (isMoving)
        {
            _movementPauseTimer = 0f;
            _stillnessTimer = 0f;
            _movementTimer += Time.deltaTime;

            if (!_isVignetteActive && _movementTimer >= m_MovementDurationThreshold)
            {
                _isVignetteActive = true;
                m_VignetteController.BeginTunnelingVignette(this);
                _status = ">>> VIGNETTE ON <<<";
                Debug.Log("[Vignette] ACTIVATED by movement");
            }
            else if (!_isVignetteActive)
            {
                _status = $"Moving {_movementTimer:F1}/{m_MovementDurationThreshold:F1}s";
            }
        }
        else
        {
            _movementPauseTimer += Time.deltaTime;

            if (_movementPauseTimer >= m_MovementGracePeriod)
            {
                _movementTimer = 0f;

                if (_isVignetteActive)
                {
                    _stillnessTimer += Time.deltaTime;
                    if (_stillnessTimer >= m_StillnessDelay)
                    {
                        _isVignetteActive = false;
                        m_VignetteController.EndTunnelingVignette(this);
                        _status = "VIGNETTE OFF";
                        Debug.Log("[Vignette] DEACTIVATED");
                    }
                }
                else
                {
                    _status = "Idle";
                }
            }
            else if (!_isVignetteActive)
            {
                _status = $"Grace {_movementPauseTimer:F2}/{m_MovementGracePeriod:F2}s (timer kept at {_movementTimer:F1}s)";
            }
        }
    }

    // ─────────────────────────────────────────────
    //  On-Screen Debug Overlay
    // ─────────────────────────────────────────────

    private void OnGUI()
    {
        if (!m_ShowDebugOverlay) return;

        float s = Screen.height / 1000f;
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(18 * s);
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.green;
        style.padding = new RectOffset(10, 10, 10, 10);

        float w = 550 * s;
        float h = 300 * s;

        string text =
            "=== VIGNETTE DEBUG ===\n" +
            $"Status: {_status}\n" +
            $"Speed: {_lastSpeed:F3} m/s (>{m_SpeedThreshold}?)\n" +
            $"RotSpeed: {_lastAngularSpeed:F1} d/s (>{m_RotationSpeedThreshold}?)\n" +
            $"IsMoving: {_lastIsMoving}\n" +
            $"MoveTimer: {_movementTimer:F2}/{m_MovementDurationThreshold:F2}s\n" +
            $"Active: {_isVignetteActive} | ForceTest: {_forceTestActive}\n" +
            $"Controller: {(m_VignetteController ? m_VignetteController.gameObject.name : "NULL")}\n" +
            $"Tracking: {(m_TrackedTransform ? m_TrackedTransform.gameObject.name : "NULL")}";

        GUI.Box(new Rect(10, 10, w, h), text, style);
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private Vector3 GetTrackedPosition()
    {
        Vector3 pos = m_TrackedTransform.position;
        if (m_HorizontalOnly) pos.y = 0f;
        return pos;
    }

    private float GetAngularDelta(Quaternion currentRotation)
    {
        if (m_YawOnly)
        {
            float prevYaw = _previousRotation.eulerAngles.y;
            float curYaw = currentRotation.eulerAngles.y;
            return Mathf.Abs(Mathf.DeltaAngle(prevYaw, curYaw));
        }
        else
        {
            Quaternion delta = Quaternion.Inverse(_previousRotation) * currentRotation;
            delta.ToAngleAxis(out float angle, out _);
            if (angle > 180f) angle = 360f - angle;
            return angle;
        }
    }

    // ─────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────

    public void ForceBeginVignette()
    {
        if (m_VignetteController != null && !_isVignetteActive)
        {
            _isVignetteActive = true;
            _stillnessTimer = 0f;
            m_VignetteController.BeginTunnelingVignette(this);
        }
    }

    public void ForceEndVignette()
    {
        if (m_VignetteController != null && _isVignetteActive)
        {
            _isVignetteActive = false;
            m_VignetteController.EndTunnelingVignette(this);
        }
    }

    public bool IsVignetteActive => _isVignetteActive;
}