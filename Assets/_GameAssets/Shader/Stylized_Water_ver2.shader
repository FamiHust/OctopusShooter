Shader "Custom/MobileCartoonWater_CausticsShoreCutoff"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.93, 0.85, 0.6)
        _DeepColor ("Deep Color", Color) = (0.1, 0.55, 0.85, 0.8)
        _CausticsColor ("Caustics Color", Color) = (1, 1, 1, 0.4)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 0.9)

        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _CausticsTex ("Caustics Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _FoamTex ("Foam Texture", 2D) = "white" {}

        _DistortionStrength ("Distortion Strength", Range(0, 0.1)) = 0.015
        _NoiseSpeed ("Noise Speed", Vector) = (0.04, 0.04, 0, 0)
        _CausticsSpeed ("Caustics Speed", Vector) = (0.015, 0.015, 0, 0)
        _FoamSpeed ("Foam Speed", Vector) = (-0.02, 0.02, 0, 0)

        _Threshold ("Toon Threshold", Range(0, 1)) = 0.45
        _Smoothing ("Edge Smoothing", Range(0.001, 0.2)) = 0.02
        _ShoreCutoff ("Deep Water Cutoff", Range(0.0, 1.0)) = 0.5
        _CausticsShoreCutoff ("Caustics Shore Cutoff (Day van ra xa mep nuoc)", Range(0.0, 0.5)) = 0.05
        _AlphaSlice ("Alpha Slice", Range(0.01, 1.0)) = 0.5

        _WaveSpeed ("Wave Speed", Range(0, 10)) = 3.0
        _WaveScale ("Wave Scale", Range(0, 50)) = 10.0
        _WaveStrength ("Wave Strength", Range(0, 0.5)) = 0.08

        _FoamSize ("Foam Size", Range(0.01, 0.5)) = 0.1
        _FoamNoiseFactor ("Foam Noise Factor", Range(0.0, 1.0)) = 0.4
        _FoamWaterFade ("Foam Water-Side Fade", Range(0.001, 0.4)) = 0.05
        _FoamShoreFade ("Foam Shore-Side Fade", Range(0.001, 0.4)) = 0.05

        [Header(Shadow Refraction)]
        _ShadowColor ("Shadow Color (Mau bong)", Color) = (0.08, 0.18, 0.35, 1.0)
        _ShadowStrength ("Shadow Strength (Cuong do bong)", Range(0, 1)) = 0.85
        _ShadowDistortion ("Shadow Distortion (Do meo va di chuyen bong theo song)", Range(0, 5)) = 1.5
    }
    SubShader
    {
        // Queue AlphaTest+50 (2450) cho phep Unity Render Pipeline thu nhan Screen Space Shadows
        // trong khi van giu Blend SrcAlpha OneMinusSrcAlpha & ZWrite Off cho mat nuoc trong suot.
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest+50" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase_fullshadows

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                UNITY_SHADOW_COORDS(2)
            };

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            sampler2D _CausticsTex;
            float4 _CausticsTex_ST;
            sampler2D _MaskTex;
            sampler2D _FoamTex;
            float4 _FoamTex_ST;

            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _CausticsColor;
            half4 _FoamColor;
            
            half _DistortionStrength;
            half4 _NoiseSpeed;
            half4 _CausticsSpeed;
            half4 _FoamSpeed;
            
            half _Threshold;
            half _Smoothing;
            half _ShoreCutoff;
            half _CausticsShoreCutoff;
            half _AlphaSlice;

            half _WaveSpeed;
            half _WaveScale;
            half _WaveStrength;

            half _FoamSize;
            half _FoamNoiseFactor;
            half _FoamWaterFade;
            half _FoamShoreFade;

            half4 _ShadowColor;
            half _ShadowStrength;
            half _ShadowDistortion;

            v2f vert (appdata v)
            {
                v2f o;
                float4 localPos = v.vertex;

                float shoreFactor = 1.0 - v.uv.y; 
                float wave = sin(v.uv.x * _WaveScale + _Time.y * _WaveSpeed) * _WaveStrength * shoreFactor;
                localPos.z += wave;

                o.pos = UnityObjectToClipPos(localPos);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, localPos).xyz;

                TRANSFER_SHADOW(o);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. Mau nen va Alpha Gradient
                half3 rgbGradient = lerp(_ShallowColor.rgb, _DeepColor.rgb, i.uv.y);
                half alphaFactor = smoothstep(0.0, _AlphaSlice, i.uv.y);
                half alphaGradient = lerp(_ShallowColor.a, _DeepColor.a, alphaFactor);
                half4 backgroundGradient = half4(rgbGradient, alphaGradient);

                // 2. UV Distortion tu Noise Tex
                float2 noiseUV = TRANSFORM_TEX(i.uv, _NoiseTex) + _NoiseSpeed.xy * _Time.y;
                float2 distortionOffset = (tex2D(_NoiseTex, noiseUV).rg * 2.0 - 1.0) * _DistortionStrength;

                // --- BONG DI CHUYEN & KHUC XA THEO SONG NUOC (Shadow Moving with Wave & Noise) ---
                float shoreFactor = 1.0 - i.uv.y;
                float wavePhase = i.uv.x * _WaveScale + _Time.y * _WaveSpeed;
                float waveHeight = sin(wavePhase) * _WaveStrength * shoreFactor;
                float waveSlope = cos(wavePhase) * _WaveStrength * shoreFactor;

                // Vector di chuyen bong theo nhip cuon cua song va do doc mat nuoc
                float2 waveMotionOffset = float2(waveSlope, waveHeight) * 3.0;

                // Tong hop dich chuyen bong
                float2 shadowRefraction = (waveMotionOffset + distortionOffset * 10.0) * _ShadowDistortion;

                v2f shadowInput = i;
                #if defined(SHADOWS_SCREEN)
                    shadowInput._ShadowCoord.xy += shadowRefraction * shadowInput._ShadowCoord.w;
                #else
                    shadowInput.worldPos.xz += shadowRefraction;
                #endif

                UNITY_LIGHT_ATTENUATION(atten, shadowInput, shadowInput.worldPos);
                half shadowMask = (1.0 - saturate(atten)) * _ShadowStrength;
                // --------------------------------------------------------------------------------

                // 3. Van nuoc Caustics cuon
                float2 causticsUV = TRANSFORM_TEX(i.uv, _CausticsTex) + distortionOffset + _CausticsSpeed.xy * _Time.y;
                half causticsSample = tex2D(_CausticsTex, causticsUV).r;
                half causticsMask = smoothstep(_Threshold, _Threshold + _Smoothing, causticsSample);

                // --- BO LOC GIOI HAN VAN CAUSTICS HAI DAU (NONG VA SAU) ---
                half deepMask = smoothstep(_ShoreCutoff, 0.0, i.uv.y); // An van o vung nuoc sau
                
                // NEW: Day van nuoc cach ly khoi mep bo sat uv.y = 0
                half shallowMask = smoothstep(_CausticsShoreCutoff, _CausticsShoreCutoff + 0.08, i.uv.y); 
                
                half proceduralCausticsMask = deepMask * shallowMask;

                half texMask = tex2D(_MaskTex, i.uv).r;
                causticsMask *= texMask * proceduralCausticsMask;
                // ---------------------------------------------------------

                half4 waterColor = lerp(backgroundGradient, _CausticsColor, causticsMask);

                // 4. Bot song hai mep (Dual-Edge Textured Foam)
                float2 foamUV = TRANSFORM_TEX(i.uv, _FoamTex) + _FoamSpeed.xy * _Time.y;
                foamUV += distortionOffset * 1.5;
                half foamNoiseSample = tex2D(_FoamTex, foamUV).r;

                half foamWavePush = sin(_Time.y * _WaveSpeed) * 0.015;
                half foamBaseLine = i.uv.y + distortionOffset.y + foamWavePush;

                half finalFoamCoord = foamBaseLine - (foamNoiseSample * _FoamNoiseFactor);

                half waterEdge = smoothstep(_FoamSize, _FoamSize - _FoamWaterFade, finalFoamCoord);
                half shoreEdge = smoothstep(0.0, _FoamShoreFade, finalFoamCoord);

                half foamMask = saturate(waterEdge * shoreEdge);

                // 5. De lop bot trang len tren cung
                half4 finalColor = lerp(waterColor, _FoamColor, foamMask * _FoamColor.a);

                // 6. Ap dung bong khuc xa (Refracted Shadow Tint)
                half3 shadedRGB = lerp(finalColor.rgb, finalColor.rgb * _ShadowColor.rgb, _ShadowColor.a);
                finalColor.rgb = lerp(finalColor.rgb, shadedRGB, shadowMask);

                return finalColor;
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}