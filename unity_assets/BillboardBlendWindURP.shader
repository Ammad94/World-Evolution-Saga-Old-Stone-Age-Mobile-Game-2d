// BillboardBlendWindURP.shader
// --------------------------------------------------------------------------
// URP version of BillboardBlendWind.shader (identical behaviour).
// Smooth camera-orbit cross-fade between neighbouring direction sprites +
// hair / loincloth wind sway + breathing + contact shadow, all GPU-side.
//
// Use this file when your project uses the UNIVERSAL RENDER PIPELINE.
// --------------------------------------------------------------------------

Shader "Game/BillboardBlendWindURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Direction A", 2D) = "white" {}

        _TexB   ("Direction B", 2D) = "white" {}
        [PerRendererData] _TexHead ("Head Glance Dir (previous view)", 2D) = "white" {}
        _MaskA  ("Sway Mask A (R hair G cloth B torso)", 2D) = "black" {}
        _MaskB  ("Sway Mask B", 2D) = "black" {}
        _Blend  ("Direction Blend", Range(0,1)) = 0
        [Header(Cross fade quality)]
        _BlendSharp ("Direction Blend Sharpness (1 = no ghosting)", Range(0,1)) = 0.75
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
            "RenderPipeline" = "UniversalPipeline"
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
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_TexB);       SAMPLER(sampler_TexB);
            TEXTURE2D(_TexHead);    SAMPLER(sampler_TexHead);
            TEXTURE2D(_MaskA);      SAMPLER(sampler_MaskA);
            TEXTURE2D(_MaskB);      SAMPLER(sampler_MaskB);

            float4 _Color;
            float  _Blend;
            float  _BlendSharp, _BlendAlphaUnion;

            float _WindDirX, _WindSpeed, _HairAmp, _ClothAmp;
        float _BreathRate, _BreathAmp, _BreathTint, _BobAmp;
        float _HeadGlance, _Blink, _ClenchAmp;
            float _MoveBlend, _MovePhase, _StrideAmp;
            float _ShadowStrength, _ShadowSizeX, _ShadowY, _ShadowSizeY;
            float4 _TexSize;
            float _BodyCentreX, _Phase, _FallbackMask;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }


            // Sharpened, alpha-weighted cross-fade between two direction sprites.
            float4 BlendDirs(float4 a, float4 b, float w)
            {
                float hw = lerp(0.5, 0.06, saturate(_BlendSharp));   // fade half-width
                float t  = smoothstep(0.5 - hw, 0.5 + hw, w);
                float wa = a.a * (1.0 - t);
                float wb = b.a * t;
                float sum = wa + wb;
                float3 rgb = sum > 1e-5 ? (a.rgb * wa + b.rgb * wb) / sum : lerp(a.rgb, b.rgb, t);
                float al = lerp(lerp(a.a, b.a, t), max(a.a, b.a), saturate(_BlendAlphaUnion));
                return float4(rgb, al);
            }
            half4 frag (v2f i) : SV_Target
            {
                float2 uv0 = i.texcoord;

                // ---------- sway masks (sampled at the rest pose) ----------
                float4 mask = lerp(SAMPLE_TEXTURE2D(_MaskA, sampler_MaskA, uv0),
                                   SAMPLE_TEXTURE2D(_MaskB, sampler_MaskB, uv0), _Blend);
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
                float e = 0.016 * inhale * _BreathAmp * torsoW;
                uvB.x = _BodyCentreX + (uvB.x - _BodyCentreX) / (1.0 + e);
                float rise = (0.55 * torsoW + 0.45 * smoothstep(0.45, 0.80, uv0.y))
                           * 0.009 * inhale * _BreathAmp;
                uvB.y -= rise;
                uvB.y -= 0.0022 * sin(brPhase - 1.5708) * _BobAmp;

                // ---------- walking bob / weight shift ----------
                float stride = _MovePhase * 6.28318530;
                uvB.y += sin(stride * 2.0) * 0.0045 * _StrideAmp * _MoveBlend;
                uvB.x += sin(stride)       * 0.0035 * _StrideAmp * _MoveBlend;

                // ---------- head: idle GLANCES (looking left / right a little) ----------
                // _HeadGlance (eased by BillboardCharacter) cross-fades the HEAD
                // REGION toward a neighbouring view: + blends toward direction
                // B, - toward the previous view (_TexHead). That is a REAL head
                // turn — the artist's own pixels: face, eyes and hair
                // silhouette actually change — instead of sliding or rotating
                // a flat sprite (which always reads as fake). The feathered
                // head mask fades the turn out at the neck seam and the body
                // never moves. The script fades the glance out while the
                // character turns or walks, so it never fights the orbit
                // cross-fade.
                float g = _HeadGlance;                         // -1 .. 1

                // eyes = dark pixels of the face (head zone minus hair)
                float4 rA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv0);
                float4 rB = SAMPLE_TEXTURE2D(_TexB,    sampler_TexB,    uv0);
                float4 rest = lerp(BlendDirs(rA, rB, _Blend), rB, saturate(g));
                rest = lerp(rest, SAMPLE_TEXTURE2D(_TexHead, sampler_TexHead, uv0), saturate(-g));
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
                float2 duv = uvB + hairOff + clothOff + blinkOff + handOff;
                float4 cA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, duv);
                float4 cB = SAMPLE_TEXTURE2D(_TexB,    sampler_TexB,    duv);
                
                // ---- ghost-free direction cross-fade -------------------------
                // A plain lerp() of two sprites makes BOTH of them semi-transparent
                // in the middle of the fade, which reads as a faint "second
                // caveman" showing through / a blurry double image.
                // 1) _BlendSharp compresses the fade into a narrow window so most
                //    of the time exactly ONE sprite is on screen.
                // 2) the alpha is taken as the UNION of the two silhouettes and the
                //    colour is alpha-weighted, so the body never goes see-through.
                float4 bodyCol = BlendDirs(cA, cB, _Blend);
                // glance: cross-fade the head region toward the neighbouring view
                float4 headCol = lerp(bodyCol, cB, saturate(g));
                headCol = lerp(headCol, SAMPLE_TEXTURE2D(_TexHead, sampler_TexHead, duv), saturate(-g));
                float4 col = lerp(bodyCol, headCol, headW) * _Color * i.color;

                col.rgb *= 1.0 - _BreathTint * exhale * torsoW * _BreathAmp;

                // ---------- soft contact shadow under the feet ----------
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

                col.rgb *= col.a;
                return col;
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
