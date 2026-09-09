// BillboardBlendWind.shader
// --------------------------------------------------------------------------
// Smooth "3D-feeling" billboard character shader for the Stone-Age caveman.
// --------------------------------------------------------------------------

Shader "Game/BillboardBlendWind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Direction A", 2D) = "white" {}

        _TexB   ("Direction B", 2D) = "white" {}
        [PerRendererData] _TexHead ("Head Glance Dir (previous view)", 2D) = "white" {}
        [Toggle] _AlphaClip ("Cut transparent green edge pixels", Float) = 1
        _AlphaClipThreshold ("Transparent Edge Cutoff", Range(0,0.5)) = 0.10
        _MaskA  ("Sway Mask A (R hair G cloth B torso)", 2D) = "black" {}
        _MaskB  ("Sway Mask B", 2D) = "black" {}
        _Blend  ("Direction Blend", Range(0,1)) = 0
        [Header(Cross fade quality)]
        _BlendSharp ("Direction Blend Sharpness (1 = no ghosting)", Range(0,1)) = 1.0
        _BlendAlphaUnion ("Keep Silhouette Solid While Blending", Range(0,1)) = 1.0
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
        _HeadGlance  ("Head Glance Blend (-1..1, driven by BillboardCharacter)", Range(-1,1)) = 0
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

        [Header(Environment)]
        _Daylight       ("Daylight (0..2)", Range(0,2)) = 1.0
        _AmbientTint    ("Ambient Tint (RGB)", Color) = (1,1,1,1)
        _Wetness        ("Wetness (0..1)", Range(0,1)) = 0
        _WetShine       ("Wet Shine Strength", Range(0,0.5)) = 0.18
        _Darkness       ("Darkness (0..1)", Range(0,1)) = 0

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
            sampler2D _TexHead;
            sampler2D _MaskA;
            sampler2D _MaskB;

            float4 _Color;
            float  _Blend;
            float  _BlendSharp, _BlendAlphaUnion;
            float  _AlphaClip;
            float  _AlphaClipThreshold;

            float2 SafeUV(float2 uv)
            {
                return clamp(uv, float2(0.0005, 0.0005), float2(0.9995, 0.9995));
            }

            float4 Despill(float4 c)
            {
                float maxRB = max(c.r, c.b);
                float gDom = c.g - maxRB;
                c.a *= 1.0 - saturate(gDom * 24.0);
                c.g = min(c.g, maxRB + 0.004);
                float keep = step(0.02, c.a);
                c.rgb *= keep;
                c.a *= keep;
                return c;
            }

            float _WindDirX, _WindSpeed, _HairAmp, _ClothAmp;
            float _BreathRate, _BreathAmp, _BreathTint, _BobAmp;
            float _HeadGlance, _Blink, _ClenchAmp;
            float _MoveBlend, _MovePhase, _StrideAmp;
            float _ShadowStrength, _ShadowSizeX, _ShadowY, _ShadowSizeY;
            float4 _TexSize;
            float _BodyCentreX, _Phase, _FallbackMask;
            float _Daylight, _Wetness, _WetShine, _Darkness;
            float4 _AmbientTint;

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
                float2 maskUV = SafeUV(uv0);
                float4 mask = lerp(tex2D(_MaskA, maskUV), tex2D(_MaskB, maskUV), _Blend);
                float hairW  = mask.r;
                float clothW = mask.g;
                float torsoW = mask.b;
                float headW  = mask.a;

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

                // loincloth: hem-weighted flutter
                float flutter = sin(t * _WindSpeed * 2.2 + (0.55 - uv0.y) * 9.0) * 0.60
                              + sin(t * _WindSpeed * 3.7 + uv0.x * 14.0)        * 0.40;
                float2 clothOff = float2(_WindDirX * (flutter + 0.35 * hairWave),
                                         abs(flutter) * 0.45)
                                * (_ClothAmp * px) * clothW * gust * (1.0 + 0.7 * _MoveBlend);

                // ---------- breathing ----------
                float brPhase = t * _BreathRate * 6.28318530;
                float br      = sin(brPhase);
                float inhale  = max(br, 0.0);
                float exhale  = max(-br, 0.0);

                float2 uvB = uv0;
                float e = 0.016 * inhale * _BreathAmp * torsoW;
                uvB.x = _BodyCentreX + (uvB.x - _BodyCentreX) / (1.0 + e);
                float rise = (0.55 * torsoW + 0.45 * smoothstep(0.45, 0.80, uv0.y))
                           * 0.009 * inhale * _BreathAmp;
                uvB.y -= rise;
                uvB.y -= 0.0022 * sin(brPhase - 1.5708) * _BobAmp;

                // ---------- walking bob ----------
                float stride = _MovePhase * 6.28318530;
                uvB.y += sin(stride * 2.0) * 0.0045 * _StrideAmp * _MoveBlend;
                uvB.x += sin(stride)       * 0.0035 * _StrideAmp * _MoveBlend;

                // ---------- head glances ----------
                float g = _HeadGlance;
                float gPos = saturate(g);
                float gNeg = saturate(-g);

                // eyes detection
                float4 rA = Despill(tex2D(_MainTex, maskUV));
                float4 rB = Despill(tex2D(_TexB,   maskUV));
                float4 rest = lerp(rA, rB, gPos);
                rest = lerp(rest, Despill(tex2D(_TexHead, maskUV)), gNeg);
                float restLum = dot(rest.rgb, float3(0.299, 0.587, 0.114));
                float faceW = saturate(headW - hairW);
                float eyeW = faceW * smoothstep(0.42, 0.16, restLum) * rest.a;
                float py = 1.0 / max(_TexSize.y, 1.0);
                float2 blinkOff = float2(0.0, -2.6 * py) * eyeW * _Blink;

                // ---------- hands ----------
                float lat = uv0.x - _BodyCentreX;
                float handBand = smoothstep(0.44, 0.47, uv0.y) * smoothstep(0.60, 0.55, uv0.y);
                float handW = handBand * smoothstep(0.075, 0.11, abs(lat)) * (1.0 - clothW) * (1.0 - headW);
                float clench = smoothstep(0.2, 0.8, sin(t * 0.43 + _Phase * 3.1)) * _ClenchAmp;
                float2 handOff = float2(sign(lat), -0.25) * (0.8 * px) * clench * handW;

                // ---------- SINGLE-SPRITE SAMPLE ----------
                // To eliminate the ghost double-image, we use ONLY _MainTex
                // (the dominant direction sprite). The script snaps _MainTex
                // to the nearest of the 16 directions, so there is never a
                // cross-fade to produce a ghost.
                float2 duv = SafeUV(uvB + hairOff + clothOff + blinkOff + handOff);
                float4 col = Despill(tex2D(_MainTex, duv));

                col *= _Color * i.color;

                // very subtle exhale shading pulse on the chest
                col.rgb *= 1.0 - _BreathTint * exhale * torsoW * _BreathAmp;
                col.rgb *= 1.0 - 0.22 * eyeW * _Blink;
                col.rgb *= 1.0 - 0.05 * clench * handW;

                // ---------- environment tinting ----------
                col.rgb *= _AmbientTint.rgb * _Daylight;
                col.rgb *= saturate(1.0 - _Darkness * 0.97);
                float wetMask = saturate(hairW + clothW) * _Wetness;
                col.rgb *= 1.0 - 0.25 * wetMask;
                float wetShine = 0.5 + 0.5 * sin(t * 1.3 + uv0.x * 12.0 + uv0.y * 5.0);
                col.rgb += wetShine * _WetShine * wetMask * col.a;

                // ---------- soft contact shadow under the feet ----------
                float bodyAlpha = col.a;
                if (_ShadowStrength > 0.001)
                {
                    float2 sp = float2((uv0.x - _BodyCentreX) / max(_ShadowSizeX, 1e-4),
                                       (uv0.y - _ShadowY)      / max(_ShadowSizeY, 1e-4));
                    float sh = saturate(1.0 - dot(sp, sp));
                    sh = sh * sh;
                    float shA = sh * _ShadowStrength;
                    col.rgb *= 1.0 - 0.55 * shA;
                    col.a    = max(col.a, shA * saturate(1.0 - col.a));
                }
                if (bodyAlpha < _AlphaClipThreshold)
                    col.rgb = 0;

                // HARD alpha clip: any pixel below the cutoff is fully
                // transparent. This kills the soft fringe that was the
                // source of the ghost double-image.
                if (_AlphaClip > 0.5)
                    clip(col.a - _AlphaClipThreshold);

                if (col.a > 0.001 && col.a < _AlphaClipThreshold + 0.001)
                    col.rgb = 0;

                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
