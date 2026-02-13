using UnityEngine;

/// <summary>
/// Swaps the Meta XR / XR Interaction Toolkit tunneling vignette material to
/// use a cage-grid shader instead of solid black, then manages cage-specific
/// properties at runtime.
///
/// ──────────────────────────────────────────────────────────────────────
/// WHY A CAGE?
/// ──────────────────────────────────────────────────────────────────────
/// Research shows that a plain black vignette removes peripheral optical
/// flow (good) but also removes all spatial grounding (bad). Users report
/// it feels like "running in the dark," which undermines comfort and
/// often causes them to disable the feature entirely.
///
/// A cage grid in the periphery provides a static visual rest frame —
/// like looking at a TV mounted in a room. The brain accepts the
/// stationary grid as the "real" world, which significantly reduces
/// the visual–vestibular conflict that causes cybersickness.
///
/// ──────────────────────────────────────────────────────────────────────
/// SETUP  (assumes OVRVignetteDriver is already installed)
/// ──────────────────────────────────────────────────────────────────────
///
///  1. Copy TunnelingVignetteCage.shader into Assets/Shaders/ (or anywhere
///     inside Assets). Unity will compile it automatically.
///
///  2. Copy CageVignetteSetup.cs into Assets/Scripts/.
///
///  3. Select the TunnelingVignette GameObject in the hierarchy.
///
///  4. Confirm these components are present and configured:
///       ✓ MeshRenderer (enabled)
///       ✓ MeshFilter   (TunnelingVignetteMeshFilter)
///       ✓ TunnelingVignetteController  — DISABLED (unchecked)
///       ✓ OVRVignetteDriver            — enabled, configured
///
///  5. Add Component → CageVignetteSetup.
///
///  6. (Recommended) Drag the Cage Shader asset into the "Cage Shader"
///     field in the Inspector. If left empty the script uses
///     Shader.Find("VR/TunnelingVignetteCage") at runtime.
///
///  7. (Optional) Drag your active OVRCameraRig into "Camera Rig" to
///     enable rotation stabilization (keeps the cage fixed during
///     snap/smooth virtual turns).
///
///  8. Tune colors and grid density in the Inspector. The defaults are
///     a good starting point (soft gray lines on very dark background).
///
///  9. Build to Quest and test.
///
/// ──────────────────────────────────────────────────────────────────────
/// COMPONENT ORDER
/// ──────────────────────────────────────────────────────────────────────
/// This component runs at execution order −50 so that it swaps the
/// material BEFORE OVRVignetteDriver initializes (order 0). Both
/// components then share the same material instance at runtime:
///   • CageVignetteSetup  → writes cage colors, grid spacing, yaw offset
///   • OVRVignetteDriver  → writes _ApertureSize, _FeatheringEffect
///
/// ──────────────────────────────────────────────────────────────────────
/// CULL DIRECTION NOTE
/// ──────────────────────────────────────────────────────────────────────
/// The shader defaults to Cull Front (renders back faces), which is
/// correct for a standard hemisphere whose normals point outward and
/// whose camera sits inside. If the vignette appears inverted (visible
/// in the center, transparent at the edges), the mesh has inverted
/// normals. Open TunnelingVignetteCage.shader and change:
///     Cull Front   →   Cull Back
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MeshRenderer))]
public class CageVignetteSetup : MonoBehaviour
{
    // ================================================================
    // Inspector fields
    // ================================================================

    [Header("Shader")]
    [Tooltip(
        "Direct reference to TunnelingVignetteCage.shader. If empty, " +
        "the script searches by name at runtime.")]
    [SerializeField] private Shader _cageShader;

    [Header("Grid Appearance")]
    [Tooltip("Color of the cage grid lines. Aim for medium contrast — " +
             "bright enough to perceive peripherally, dim enough not to distract.")]
    [SerializeField] private Color _gridColor = new Color(0.376f, 0.376f, 0.376f, 1f);

    [Tooltip("Background color behind the grid lines (the 'walls' of the cage). " +
             "Very dark but NOT pure black — a touch of brightness feels more natural.")]
    [SerializeField] private Color _backgroundColor = new Color(0.102f, 0.102f, 0.102f, 1f);

    [Tooltip("Distance between grid lines in cube-projected UV space. " +
             "0.2 ≈ 10 lines per cube face ≈ every ~9°. " +
             "Smaller = denser grid. Range 0.08–0.5.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float _gridSpacing = 0.2f;

    [Tooltip("Thickness of each grid line relative to spacing. " +
             "Keep thin for subtlety.")]
    [Range(0.001f, 0.06f)]
    [SerializeField] private float _gridLineWidth = 0.012f;

    [Header("Rotation Stabilization")]
    [Tooltip(
        "When enabled, the cage grid counter-rotates to stay fixed during " +
        "virtual yaw rotation (snap/smooth turn). This makes the cage a true " +
        "rest frame. Requires Camera Rig to be assigned.")]
    [SerializeField] private bool _stabilizeRotation = true;

    [Tooltip(
        "The OVRCameraRig whose root transform rotation is tracked. " +
        "Virtual yaw rotation is assumed to rotate this transform. " +
        "If empty, auto-detected on Awake.")]
    [SerializeField] private OVRCameraRig _cameraRig;

    // ================================================================
    // Private state
    // ================================================================

    private MeshRenderer _meshRenderer;
    private Material _runtimeMaterial;
    private float _initialRigYaw;
    private bool _initialized;

    // Shader property IDs (cached for performance)
    private static readonly int PropGridColor = Shader.PropertyToID("_GridColor");
    private static readonly int PropBackColor = Shader.PropertyToID("_BackColor");
    private static readonly int PropGridSpacing = Shader.PropertyToID("_GridSpacing");
    private static readonly int PropGridLineWidth = Shader.PropertyToID("_GridLineWidth");
    private static readonly int PropVirtualYaw = Shader.PropertyToID("_VirtualYawOffset");
    private static readonly int PropApertureSize = Shader.PropertyToID("_ApertureSize");
    private static readonly int PropFeatheringEffect = Shader.PropertyToID("_FeatheringEffect");

    // ================================================================
    // MonoBehaviour lifecycle
    // ================================================================

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
        {
            Debug.LogError("[CageVignetteSetup] No MeshRenderer found.", this);
            return;
        }

        // ── Resolve shader ──────────────────────────────────────────
        Shader shader = _cageShader;
        if (shader == null)
        {
            shader = Shader.Find("VR/TunnelingVignetteCage");
            if (shader == null)
            {
                Debug.LogError(
                    "[CageVignetteSetup] Could not find shader 'VR/TunnelingVignetteCage'. " +
                    "Make sure TunnelingVignetteCage.shader is in your project, or drag it " +
                    "into the 'Cage Shader' field in the Inspector.", this);
                return;
            }
        }

        if (!shader.isSupported)
        {
            Debug.LogWarning(
                $"[CageVignetteSetup] Shader '{shader.name}' is not supported on this GPU. " +
                "The vignette will fall back to the original material.", this);
            return;
        }

        // ── Create cage material ────────────────────────────────────
        Material cageMat = new Material(shader);
        cageMat.name = "TunnelingVignetteCage (Instance)";

        // Copy aperture values from the original material so the initial
        // state matches what the designer set up in the Inspector.
        Material original = _meshRenderer.sharedMaterial;
        if (original != null)
        {
            CopyFloatIfExists(original, cageMat, PropApertureSize);
            CopyFloatIfExists(original, cageMat, PropFeatheringEffect);
        }

        // Apply cage visual properties
        ApplyCageProperties(cageMat);

        // Assign as the renderer's shared material. When OVRVignetteDriver
        // later accesses .material, Unity creates an instance from THIS
        // material, preserving the cage shader.
        _meshRenderer.sharedMaterial = cageMat;

        // ── Resolve camera rig ──────────────────────────────────────
        if (_cameraRig == null && _stabilizeRotation)
        {
            _cameraRig = FindObjectOfType<OVRCameraRig>();
            if (_cameraRig != null)
            {
                Debug.Log(
                    $"[CageVignetteSetup] Auto-assigned rig: {_cameraRig.name}", this);
            }
        }

        if (_cameraRig != null)
        {
            _initialRigYaw = _cameraRig.transform.eulerAngles.y;
        }

        _initialized = true;

        Debug.Log(
            $"[CageVignetteSetup] Cage shader applied. Grid spacing={_gridSpacing}, " +
            $"stabilize rotation={_stabilizeRotation}.", this);
    }

    private void Start()
    {
        if (!_initialized) return;

        // By Start(), OVRVignetteDriver has already created its material
        // instance via OnEnable → Initialize(). Grab that instance so we
        // write to the same object.
        _runtimeMaterial = _meshRenderer.material;
    }

    private void LateUpdate()
    {
        if (_runtimeMaterial == null) return;

        // ── Update cage visual properties (supports live Inspector tweaks) ──
        ApplyCageProperties(_runtimeMaterial);

        // ── Update rotation stabilization ───────────────────────────
        if (_stabilizeRotation && _cameraRig != null)
        {
            // The rig root's Y rotation changes due to virtual locomotion
            // (snap turn, smooth turn, steering). Physical head rotation
            // is tracked separately by CenterEyeAnchor.
            float currentYaw = _cameraRig.transform.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(_initialRigYaw, currentYaw);
            float yawRadians = deltaYaw * Mathf.Deg2Rad;

            _runtimeMaterial.SetFloat(PropVirtualYaw, yawRadians);
        }
    }

    private void OnDestroy()
    {
        // The MeshRenderer's material instance will be cleaned up by
        // OVRVignetteDriver (which also calls Destroy on the material).
        // We only need to clean up if we created a separate reference.
        // In practice, _runtimeMaterial is the same instance
        // OVRVignetteDriver holds, so avoid double-destroy.
        _runtimeMaterial = null;
    }

    // ================================================================
    // Helpers
    // ================================================================

    private void ApplyCageProperties(Material mat)
    {
        mat.SetColor(PropGridColor, _gridColor);
        mat.SetColor(PropBackColor, _backgroundColor);
        mat.SetFloat(PropGridSpacing, _gridSpacing);
        mat.SetFloat(PropGridLineWidth, _gridLineWidth);
    }

    private static void CopyFloatIfExists(Material src, Material dst, int propID)
    {
        if (src.HasFloat(propID))
        {
            dst.SetFloat(propID, src.GetFloat(propID));
        }
    }

    // ================================================================
    // Public API  (call from game code if needed)
    // ================================================================

    /// <summary>
    /// Resets the rotation baseline to the rig's current yaw.
    /// Call this after a scene transition or teleport that intentionally
    /// changes the player's facing direction.
    /// </summary>
    public void ResetRotationBaseline()
    {
        if (_cameraRig != null)
        {
            _initialRigYaw = _cameraRig.transform.eulerAngles.y;
        }
    }

    /// <summary>
    /// Allows runtime adjustment of grid density.
    /// </summary>
    public void SetGridSpacing(float spacing)
    {
        _gridSpacing = Mathf.Clamp(spacing, 0.05f, 0.6f);
    }

    /// <summary>
    /// Allows runtime adjustment of grid line / background colors.
    /// </summary>
    public void SetGridColors(Color lineColor, Color bgColor)
    {
        _gridColor = lineColor;
        _backgroundColor = bgColor;
    }
}