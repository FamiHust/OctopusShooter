Shader "Custom/MobileCartoonWater_Masked"
{
    Properties
    {
        [Header(Gradient Colors)]
        _ShallowColor ("Shallow Color (Màu gần bờ)", Color) = (0.35, 0.93, 0.85, 0.6)
        _DeepColor ("Deep Color (Màu xa bờ)", Color) = (0.1, 0.55, 0.85, 0.8)
        _CausticsColor ("Caustics Color (Màu vân nước)", Color) = (1, 1, 1, 0.4)

        [Header(Textures)]
        _NoiseTex ("Noise Texture (Distortion)", 2D) = "white" {}
        _CausticsTex ("Caustics Texture (Vân nước)", 2D) = "white" {}
        _MaskTex ("Mask Texture (Màu Trắng = Hiện Vân, Đen = Ẩn Vân)", 2D) = "white" {}

        [Header(Movement)]
        _DistortionStrength ("Distortion Strength (Độ méo)", Range(0, 0.1)) = 0.015
        _NoiseSpeed ("Noise Speed (Tốc độ cuộn Noise)", Vector) = (0.04, 0.04, 0, 0)
        _CausticsSpeed ("Caustics Speed (Tốc độ cuộn Vân)", Vector) = (0.015, 0.015, 0, 0)

        [Header(Toon Edge)]
        _Threshold ("Toon Threshold (Độ dày mảnh vân)", Range(0, 1)) = 0.45
        _Smoothing ("Edge Smoothing (Độ mượt viền)", Range(0.001, 0.2)) = 0.02
        
        [Header(Shore Slice Slider)]
        _ShoreCutoff ("Shore Cutoff (Kéo để chỉnh giới hạn sóng)", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            sampler2D _CausticsTex;
            float4 _CausticsTex_ST;
            sampler2D _MaskTex;

            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _CausticsColor;
            half _DistortionStrength;
            half4 _NoiseSpeed;
            half4 _CausticsSpeed;
            half _Threshold;
            half _Smoothing;
            half _ShoreCutoff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. Màu nền Gradient theo trục Y của UV
                half4 backgroundGradient = lerp(_ShallowColor, _DeepColor, i.uv.y);

                // 2. Độ méo UV từ Noise Texture
                float2 noiseUV = TRANSFORM_TEX(i.uv, _NoiseTex) + _NoiseSpeed.xy * _Time.y;
                half4 noiseSample = tex2D(_NoiseTex, noiseUV);
                float2 distortionOffset = (noiseSample.rg * 2.0 - 1.0) * _DistortionStrength;

                // 3. UV Vân nước cuộn
                float2 causticsUV = TRANSFORM_TEX(i.uv, _CausticsTex) + distortionOffset + _CausticsSpeed.xy * _Time.y;
                half causticsSample = tex2D(_CausticsTex, causticsUV).r;

                // 4. Tạo mặt nạ vân nước sắc nét
                half causticsMask = smoothstep(_Threshold, _Threshold + _Smoothing, causticsSample);

                // ================= BỘ LỌC GIỚI HẠN VÂN NƯỚC (NEW) =================
                
                // Ô Texture Mask: Bạn bỏ ảnh vẽ vùng mong muốn vào đây (Kênh R)
                half texMask = tex2D(_MaskTex, i.uv).r;

                // Thanh Slice kéo tay bằng toán: Tự động triệt tiêu vân nước dựa theo chiều cao UV.y
                half proceduralShoreMask = smoothstep(_ShoreCutoff, 0.0, i.uv.y);

                // Nhân toàn bộ mask lại với nhau
                causticsMask *= texMask * proceduralShoreMask;

                // =================================================================

                // 5. Trộn vân nước lên nền Gradient
                half4 finalColor = lerp(backgroundGradient, _CausticsColor, causticsMask);
                return finalColor;
            }
            ENDCG
        }
    }
}