Shader "Custom/MobileCartoonBuoy_SmoothSpecular_Fixed"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.5, 0.5, 0.5, 1)

        _Threshold ("Threshold", Range(0, 1)) = 0.5
        _Smoothing ("Smoothing", Range(0.001, 1)) = 0.1

        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Glossiness", Range(1, 128)) = 32.0
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 1.0

        _BobSpeed ("Bob Speed", Range(0, 10)) = 2.5
        _BobHeight ("Bob Height", Range(0, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD3;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            half4 _BaseColor;
            half4 _HighlightColor;
            half4 _ShadowColor;
            half _Threshold;
            half _Smoothing;

            half4 _SpecColor;
            half _Glossiness;
            half _SpecularIntensity;

            half _BobSpeed;
            half _BobHeight;

            v2f vert (appdata v)
            {
                v2f o;
                float t = _Time.y;
                
                float4 localPos = v.vertex;

                // Hoat anh chi len xuong theo truc Y thuon tuy
                localPos.y += sin(t * _BobSpeed) * _BobHeight;

                o.vertex = UnityObjectToClipPos(localPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                // Tinh huong nhin tu Camera de lam bong sang muot ma
                float3 worldPos = mul(unity_ObjectToWorld, localPos).xyz;
                o.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Màu gốc từ Texture và Base Color
                half4 texColor = tex2D(_MainTex, i.uv) * _BaseColor;

                float3 normal = normalize(i.worldNormal);
                float3 fakeLightDir = normalize(float3(0.5, 1.0, -0.4)); 
                half ndl = dot(normal, fakeLightDir) * 0.5 + 0.5;

                // Phân mảng khối sáng tối (Toon Diffuse)
                half lowBound = _Threshold - _Smoothing * 0.5;
                half highBound = _Threshold + _Smoothing * 0.5;
                half toonMask = smoothstep(lowBound, highBound, ndl);
                half3 diffuseColor = lerp(_ShadowColor.rgb, _HighlightColor.rgb, toonMask);

                // Thuat toan pow tao vet sang loang muot (Smooth Specular Gradient)
                float3 viewDir = normalize(i.viewDir);
                float3 halfDir = normalize(fakeLightDir + viewDir);
                float ndh = max(0.0, dot(normal, halfDir));
                
                float smoothSpec = pow(ndh, _Glossiness) * _SpecularIntensity;
                half3 specularColor = _SpecColor.rgb * smoothSpec;

                // Gộp kết quả
                half3 finalRGB = (texColor.rgb * diffuseColor) + specularColor;

                return half4(finalRGB, texColor.a);
            }
            ENDCG
        }
    }
}