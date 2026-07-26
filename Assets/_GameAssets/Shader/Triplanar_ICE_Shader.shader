Shader "My Shaders/TriplanarRampURP_WorldSpace"
{
    Properties
    {
        [Header(Triplanar Mapping)]
        _BaseMap("Albedo (RGB)", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Tiling("Tiling (World Scale)", Float) = 1.0
        _TriplanarBlend("Triplanar Blend Sharpness", Range(1, 10)) = 4.0
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0

        [Header(Ramp Shading)]
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)
        _HighlightColor("Highlight Color", Color) = (1,1,1,1)
        _ShadowColor("Shadow Color", Color) = (0.5,0.5,0.5,1)
        _RampThreshold("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmoothing("Ramp Smoothing", Range(0.01, 1)) = 0.1

        [Header(Extra Effects)]
        _DarkenIntensity("Darken Intensity", Range(0, 1)) = 0
        _DarkenColor("Darken Color", Color) = (0,0,0,1)
        _UseEmission("Use Emission (Glow)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // =========================================================
        // PASS 1: HIỂN THỊ MÀU SẮC, ÁNH SÁNG & NHẬN BÓNG ĐỔ (RECEIVE SHADOW)
        // =========================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Các từ khóa biên dịch hệ thống bóng đổ của URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0; 
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD2;
                float3 bitangentWS  : TEXCOORD3;
                half4  color        : COLOR;
                float4 shadowCoord  : TEXCOORD4; // Biến truyền tọa độ bóng đổ xuống Fragment
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Tiling;
            float _TriplanarBlend;
            float _NormalStrength;
            float4 _HighlightColor;
            float4 _ShadowColor;
            float _RampThreshold;
            float _RampSmoothing;
            float _DarkenIntensity;
            float4 _DarkenColor;
            float _UseEmission;
            CBUFFER_END

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            
            half4 TriplanarSample(TEXTURE2D_PARAM(tex, smp), float3 position, float3 normal, float tiling, float blend)
            {
                half3 weights = abs(normal);
                weights = pow(weights, blend);
                weights /= (weights.x + weights.y + weights.z);
                
                half2 uvX = position.zy * tiling;
                half2 uvY = position.xz * tiling;
                half2 uvZ = position.xy * tiling;
                
                half4 sampleX = SAMPLE_TEXTURE2D(tex, smp, uvX);
                half4 sampleY = SAMPLE_TEXTURE2D(tex, smp, uvY);
                half4 sampleZ = SAMPLE_TEXTURE2D(tex, smp, uvZ);
                return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                OUT.color = IN.color;

                // ĐÃ FIX CHO UNITY 6: Sử dụng TransformWorldToShadowCoord thay cho ComputeShadowCoord
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = TriplanarSample(_BaseMap, sampler_BaseMap, IN.positionWS, normalize(IN.normalWS), _Tiling, _TriplanarBlend);
                albedo *= _BaseColor;

                half4 packedNormal = TriplanarSample(_BumpMap, sampler_BumpMap, IN.positionWS, normalize(IN.normalWS), _Tiling, _TriplanarBlend);
                half3 normalTS = UnpackNormalScale(packedNormal, _NormalStrength);

                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                half3 normalWS = TransformTangentToWorld(normalTS, TBN);

                // Lấy thông tin ánh sáng kèm dữ liệu suy hao bóng đổ (Shadow Attenuation)
                Light mainLight = GetMainLight(IN.shadowCoord);
                
                half dotNL = saturate(dot(normalWS, mainLight.direction));
                half ramp = smoothstep(_RampThreshold - _RampSmoothing * 0.5, _RampThreshold + _RampSmoothing * 0.5, dotNL);
                
                // Áp dụng bóng đổ từ vật thể khác vào dải màu nhận ánh sáng
                ramp *= mainLight.shadowAttenuation;

                half3 rampColor = lerp(_ShadowColor.rgb, _HighlightColor.rgb, ramp);
                half3 finalColor = albedo.rgb * rampColor * mainLight.color;
                finalColor *= IN.color.a;
                finalColor = lerp(finalColor, _DarkenColor.rgb, _DarkenIntensity);
                half3 emission = albedo.rgb * _UseEmission;
                finalColor += emission;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // =========================================================
        // PASS 2: SHADOW CASTER (ĐỔ BÓNG LÊN VẬT KHÁC)
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}