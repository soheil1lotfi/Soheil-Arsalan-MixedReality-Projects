// ============================================================================
// TunnelingVignetteCage.shader
//
// Drop-in replacement for the XR Interaction Toolkit tunneling vignette shader.
// Instead of filling the periphery with solid black, it renders a world-space
// projected grid "cage" that acts as a static visual rest frame — dramatically
// improving the vignette's effectiveness against cybersickness.
//
// Designed for Meta Quest (single-pass instanced stereo, mobile GPU).
// Works with the built-in render pipeline.
//
// PROPERTIES (kept compatible with TunnelingVignetteController / OVRVignetteDriver):
//   _ApertureSize      — driven by your vignette controller (0 = closed, 1 = open)
//   _FeatheringEffect   — softness of the transition edge
//   _GridColor          — color of the cage grid lines
//   _BackColor          — color behind the grid (the "walls" of the cage)
//   _GridSpacing        — distance between grid lines in cube-projected UV space
//   _GridLineWidth      — thickness of each grid line (relative to spacing)
//   _VirtualYawOffset   — radians of virtual yaw to subtract for cage stability
// ============================================================================

Shader "VR/TunnelingVignetteCage"
{
    Properties
    {
        [Header(Vignette Mask)]
        _ApertureSize      ("Aperture Size",     Range(0, 1)) = 1.0
        _FeatheringEffect  ("Feathering Effect",  Range(0, 1)) = 0.26

        [Header(Cage Grid)]
        _GridColor     ("Grid Line Color",    Color) = (0.376, 0.376, 0.376, 1.0)
        _BackColor     ("Background Color",   Color) = (0.102, 0.102, 0.102, 1.0)
        _GridSpacing   ("Grid Spacing",       Float) = 0.2
        _GridLineWidth ("Grid Line Width",    Range(0.001, 0.06)) = 0.012

        [Header(Rotation Stabilization)]
        _VirtualYawOffset ("Virtual Yaw Offset (rad)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"              = "Overlay+100"
            "RenderType"         = "Transparent"
            "IgnoreProjector"    = "True"
            "ForceNoShadowCasting" = "True"
        }

        ZWrite Off
        ZTest   Always
        Blend   SrcAlpha OneMinusSrcAlpha
        Cull    Front   // Camera is inside the hemisphere — render inner faces

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // -----------------------------------------------------------------
            // Uniforms
            // -----------------------------------------------------------------
            float  _ApertureSize;
            float  _FeatheringEffect;
            float4 _GridColor;
            float4 _BackColor;
            float  _GridSpacing;
            float  _GridLineWidth;
            float  _VirtualYawOffset;

            // -----------------------------------------------------------------
            // Structures
            // -----------------------------------------------------------------
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float3 viewSpacePos : TEXCOORD0;   // For aperture mask
                float3 worldDir     : TEXCOORD1;   // For cage grid
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -----------------------------------------------------------------
            // Vertex shader
            // -----------------------------------------------------------------
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);

                // View-space position (for computing angular distance from center)
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.viewSpacePos  = mul(UNITY_MATRIX_V, worldPos).xyz;

                // World-space direction from camera to vertex (for cage grid)
                o.worldDir = worldPos.xyz - _WorldSpaceCameraPos;

                return o;
            }

            // -----------------------------------------------------------------
            // Cage grid helper — projects direction onto a virtual cube and
            // draws anti-aliased grid lines on each face.
            // -----------------------------------------------------------------
            float cageGrid(float3 dir)
            {
                float3 a = abs(dir);
                float2 uv;

                // Select the dominant cube face and compute planar UV
                if (a.x >= a.y && a.x >= a.z)
                    uv = dir.yz / (a.x + 0.0001);
                else if (a.y >= a.x && a.y >= a.z)
                    uv = dir.xz / (a.y + 0.0001);
                else
                    uv = dir.xy / (a.z + 0.0001);

                // uv is now in [-1, 1] on the dominant face.
                // Compute grid lines with screen-space anti-aliasing.
                float2 gridCoord = uv / _GridSpacing;
                float2 gridFrac  = abs(frac(gridCoord) - 0.5);      // 0 at line, 0.5 between
                float2 fw        = fwidth(gridCoord);                // Screen-space derivative
                float2 aa        = smoothstep(fw * 0.5, fw * 2.5, gridFrac);

                return 1.0 - min(aa.x, aa.y);                       // 1 on a line, 0 between
            }

            // -----------------------------------------------------------------
            // Fragment shader
            // -----------------------------------------------------------------
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // -------- Aperture mask --------
                // Angle from the center of the eye's view (0 = dead center, pi/2 = 90 deg off)
                float angFromCenter = atan2(
                    length(i.viewSpacePos.xy),
                    -i.viewSpacePos.z              // View space: camera looks along -Z
                );
                float normAngle = angFromCenter / (UNITY_PI * 0.5); // 0..1 for 0..90 deg

                // _ApertureSize = 1 → fully open (transparent); 0 → fully closed
                float mask = smoothstep(
                    _ApertureSize,
                    _ApertureSize + _FeatheringEffect,
                    normAngle
                );

                // Early out — nothing to draw in the clear zone
                if (mask < 0.002)
                    discard;

                // -------- Cage grid --------
                float3 dir = normalize(i.worldDir);

                // Undo virtual yaw rotation so the cage stays fixed during snap/smooth turn
                float s, c;
                sincos(-_VirtualYawOffset, s, c);
                float3 stableDir = float3(
                    dir.x * c - dir.z * s,
                    dir.y,
                    dir.x * s + dir.z * c
                );

                float grid = cageGrid(stableDir);

                // Composite: grid lines on top of background color
                float3 color = lerp(_BackColor.rgb, _GridColor.rgb, grid);

                return float4(color, mask);
            }
            ENDCG
        }
    }

    // No fallback — if this shader can't compile, something is very wrong
    Fallback Off
}
