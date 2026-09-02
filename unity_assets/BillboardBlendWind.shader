// BillboardBlendWind.shader
// --------------------------------------------------------------------------
// Smooth "3D-feeling" billboard character shader for the Stone-Age caveman.
//
//  WHAT IT FIXES
//  1. SMOOTH ORBIT  - instead of snapping between 8/16 discrete direction
//     sprites, it CROSS-FADES between the two neighbouring direction sprites
//     using the exact camera<->character angle. Orbiting the camera no longer
//     feels like flipping paper cut-outs.
//  2. LIVING CHARACTER - per-pixel, GPU-side animation driven by sway masks:
//       * hair strands drift in a gusty breeze          (mask R)
//       * the fur loincloth / hem flutters              (mask G, OFF by default)
//       * visible breathing: chest expands, shoulders
//         and head rise on the inhale, subtle exhale
//         shading pulse + idle body bob                 (mask B)
//       * idle head GLANCES (look left/right) + blinks    (mask A)
//       * slow finger-curl fist clench
//       * optional soft contact-shadow blob under the feet
//       * walk bob + weight-shift while moving (script driven)
//
//  The two direction sprites are:
//     _MainTex -> set automatically by the SpriteRenderer (direction A)
//     _TexB    -> the next direction sprite              (direction B)
//     _Blend   -> 0..1 cross-fade between them
//  BillboardCharacter.cs drives everything; you normally never touch these
//  values by hand.
//
//  Use this file with the BUILT-IN render pipeline. For URP projects use
//  BillboardBlendWindURP.shader instead (same properties, same name suffix).
// --------------------------------------------------------------------------

Shader "Game/BillboardBlendWind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Direction A", 2D) = "white" {}

        _TexB   ("Direction B", 2D) = "white" {}
        _MaskA  ("Sway Mask A (R hair G cloth B torso)", 2D) = "black" {}
        _MaskB  ("Sway Mask B", 2D) = "black" {}
        _Blend  ("Direction Blend", Range(0,1)) = 0
        _Color  ("Tint", Color) = (1,1,1,1)

        [Header(Wind)]
        _WindDirX    ("Wind Screen X (-1..1)", Float) = 0.75
        _WindSpeed   ("Wind Speed", Float) = 1.6
        _HairAmp     ("Hair Sway Amplitude (px)", Float) = 3.0
        _ClothAmp    ("Cloth Flutter Amplitude (px)", Float) = 0

        [Header(Breathing)]
        _BreathRate  ("Breaths Per Second", Float) = 0.22
        _BreathAmp   ("Breathing Amount", Range(0,2)) = 1.0
        _BreathTint  ("Exhale Shading Pulse", Range(0,0.2)) = 0.045
        _BobAmp      ("Idle Body Bob", Range(0,3)) = 1.0

        [Header(Head)]
        _HeadTurn    ("Head Turn (rad, driven by BillboardCharacter)", Float) = 0
        _NeckY       ("Neck Pivot Y (uv)", Float) = 0.845
        _Blink       ("Blink (script driven)", Range(0,1)) = 0

        [Header(Hands)]
        _ClenchAmp   ("Finger Curl Amount", Range(0,2)) = 1.0


        [Header(Walking)]
        _MoveBlend   ("Move Blend (script)", Range(0,1)) = 0
        _MovePhase   ("Stride Phase (script)", Float) = 0
        _StrideAmp   ("Walk Bob Amount", Range(0,3)) = 1.0

        [Header(Contact Shadow)]
        _ShadowStrength ("Shadow Strength (0 = off)", Range(0,1)) = 0.38
        _ShadowSizeX    ("Shadow Half Width (0..0.5)", Float) = 0.26
        _ShadowY        ("Shadow Centre Y (0..0.2)", Float) = 0.025
        _ShadowSizeY    ("Shadow Half Height", Float) = 0.045

        [Header(Internal)]
        _TexSize      ("Texture Size (px)", Vector) = (176, 392, 0, 0)
        _BodyCentreX  ("Body Centre X (0..1)", Float) = 0.5
        _Phase        ("Per-Instance Random Phase", Float) = 0
        _FallbackMask ("Use Procedural Mask Fallback (0/1)", Float) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil     ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp   ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask   ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            // Important: never pack these sprites into an atlas, the blend
            // samples the full textures with matching UVs.
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _TexB;
            sampler2D _MaskA;
            sampler2D _MaskB;

            float4 _Color;
            float  _Blend;

            float _WindDirX, _WindSpeed, _HairAmp, _ClothAmp;
        float _BreathRate, _BreathAmp, _BreathTint, _BobAmp;
        float _HeadTurn, _Blink, _ClenchAmp;
            float _MoveBlend, _MovePhase, _StrideAmp;
            float _ShadowStrength, _ShadowSizeX, _ShadowY, _ShadowSizeY;
            float4 _TexSize;
            float _BodyCentreX, _Phase, _FallbackMask;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv0 = i.texcoord;

                // ---------- sway masks (sampled at the rest pose) ----------
                float4 mask = lerp(tex2D(_MaskA, uv0), tex2D(_MaskB, uv0), _Blend);
                float hairW  = mask.r;
                float clothW = mask.g;
                float torsoW = mask.b;

                // procedural fallback if no masks were supplied
                float headW = mask.a;
                if (_FallbackMask > 0.5)
                {
                    float hairF  = smoothstep(0.78, 0.88, uv0.y);
                    float clothF = smoothstep(0.60, 0.52, uv0.y) * smoothstep(0.20, 0.32, uv0.y);
                    float torsoF = smoothstep(0.52, 0.62, uv0.y) * smoothstep(0.92, 0.78, uv0.y);
                    hairW  = max(hairW,  hairF);
                    clothW = max(clothW, clothF);
                    torsoW = max(torsoW, torsoF);
                    headW  = max(headW,  smoothstep(0.80, 0.84, uv0.y) * smoothstep(0.985, 0.945, uv0.y));
                }

                // ---------- gusty breeze ----------
                float t = _Time.y + _Phase;
                // slow swell: wind breathes between calm and gusty
                float gust = 0.72 + 0.48 * pow(0.5 + 0.5 * sin(0.61 * t + 1.7 * sin(0.23 * t)), 2.0);
                gust *= 0.88 + 0.12 * sin(2.9 * t);

                // hair: waves travel from the scalp down the strands
                float hx = uv0.x * 7.0;
                float hairWave = sin(t * _WindSpeed * 1.9 + (0.92 - uv0.y) * 4.5) * 0.60
                               + sin(t * _WindSpeed * 3.1 + hx * 1.7)             * 0.25
                               + sin(t * _WindSpeed * 5.3 + hx * 3.1)             * 0.15;
                float px = 1.0 / max(_TexSize.x, 1.0);
                float2 hairOff = float2(_WindDirX * hairWave,
                                        0.16 * abs(hairWave) - 0.08)
                               * (_HairAmp * px) * hairW * gust;

                // loincloth: hem-weighted flutter, coupled to the same breeze
                float flutter = sin(t * _WindSpeed * 2.2 + (0.55 - uv0.y) * 9.0) * 0.60
                              + sin(t * _WindSpeed * 3.7 + uv0.x * 14.0)        * 0.40;
                float2 clothOff = float2(_WindDirX * (flutter + 0.35 * hairWave),
                                         abs(flutter) * 0.45)
                                * (_ClothAmp * px) * clothW * gust * (1.0 + 0.7 * _MoveBlend);

                // ---------- breathing ----------
                float brPhase = t * _BreathRate * 6.28318530;
                float br      = sin(brPhase);                 // -1 exhale .. +1 inhale
                float inhale  = max(br, 0.0);
                float exhale  = max(-br, 0.0);

                float2 uvB = uv0;
                // chest expands sideways on the inhale (content pushes outward)
                float e = 0.016 * inhale * _BreathAmp * torsoW;
                uvB.x = _BodyCentreX + (uvB.x - _BodyCentreX) / (1.0 + e);
                // shoulders + head rise a couple of pixels
                float rise = (0.55 * torsoW + 0.45 * smoothstep(0.45, 0.80, uv0.y))
                           * 0.009 * inhale * _BreathAmp;
                uvB.y -= rise;
                // gentle whole-body idle bob (offset phase -> settle between breaths)
                uvB.y -= 0.0022 * sin(brPhase - 1.5708) * _BobAmp;

                // ---------- walking bob / weight shift ----------
                float stride = _MovePhase * 6.28318530;
                uvB.y += sin(stride * 2.0) * 0.0045 * _StrideAmp * _MoveBlend;
                uvB.x += sin(stride)       * 0.0035 * _StrideAmp * _MoveBlend;

                // ---------- head: idle GLANCES (looking left / right a little) ----------
                // _HeadTurn is eased between small look angles by the script
                // (BillboardCharacter): turn -> hold -> turn back, like a person
                // idly glancing around. There is NO perpetual wobble and NO
                // free-floating drift (that read as sliding): the head only
                // moves while it is actually turning, and the tiny side shift
                // below is LOCKED to the turn angle so the chin/neck seam
                // stays planted while the upper head leads the look.
                float headRot = _HeadTurn;                    // rad, script-driven
                float2 pivot = float2(_BodyCentreX, _NeckY);
                float asp = _TexSize.x / max(_TexSize.y, 1.0);        // texel aspect
                float2 hp = (uv0 - pivot) * float2(1.0, asp);
                float csr = cos(headRot), snr = sin(headRot);
                float2 hrot = float2(hp.x * csr - hp.y * snr, hp.x * snr + hp.y * csr) / float2(1.0, asp);
                float2 headOff = (hrot - hp) * headW;
                headOff.x -= sin(headRot) * 40.0 * px * headW;   // yaw fake, top-led

                // eyes = dark pixels of the face (head zone minus hair)
                float4 rest = lerp(tex2D(_MainTex, uv0), tex2D(_TexB, uv0), _Blend);
                float restLum = dot(rest.rgb, float3(0.299, 0.587, 0.114));
                float faceW = saturate(headW - hairW);
                float eyeW = faceW * smoothstep(0.42, 0.16, restLum) * rest.a;
                float py = 1.0 / max(_TexSize.y, 1.0);
                // closed lids: sample the lower lid / cheek just below the eye
                float2 blinkOff = float2(0.0, -2.6 * py) * eyeW * _Blink;

                // ---------- hands: slow fist clench (finger curl) ----------
                float lat = uv0.x - _BodyCentreX;
                float handBand = smoothstep(0.44, 0.47, uv0.y) * smoothstep(0.60, 0.55, uv0.y);
                float handW = handBand * smoothstep(0.075, 0.11, abs(lat)) * (1.0 - clothW) * (1.0 - headW);
                float clench = smoothstep(0.2, 0.8, sin(t * 0.43 + _Phase * 3.1)) * _ClenchAmp;
                float2 handOff = float2(sign(lat), -0.25) * (0.8 * px) * clench * handW;

                // ---------- sample + direction cross-fade ----------
                float2 duv = uvB + hairOff + clothOff + headOff + blinkOff + handOff;
                float4 cA = tex2D(_MainTex, duv);
                float4 cB = tex2D(_TexB,   duv);
                float4 col = lerp(cA, cB, _Blend) * _Color * i.color;

                // very subtle exhale shading pulse on the chest
                col.rgb *= 1.0 - _BreathTint * exhale * torsoW * _BreathAmp;
                // closed-lid shading while blinking, faint clench shadow on the fists
                col.rgb *= 1.0 - 0.22 * eyeW * _Blink;
                col.rgb *= 1.0 - 0.05 * clench * handW;

                // ---------- soft contact shadow under the feet ----------
                if (_ShadowStrength > 0.001)
                {
                    float2 sp = float2((uv0.x - _BodyCentreX) / max(_ShadowSizeX, 1e-4),
                                       (uv0.y - _ShadowY)      / max(_ShadowSizeY, 1e-4));
                    float sh = saturate(1.0 - dot(sp, sp));
                    sh = sh * sh;                       // softer falloff
                    float shA = sh * _ShadowStrength;
                    col.rgb *= 1.0 - 0.55 * shA;        // darken whatever is there
                    col.a    = max(col.a, shA * saturate(1.0 - col.a));
                }

                // premultiplied-alpha output (blend mode set above)
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
