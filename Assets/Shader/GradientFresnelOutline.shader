Shader "Custom/GradientFresnelOutline"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.4,0.4,0.8,1)
        _GradColor ("Gradient Color", Color) = (0.7,0.7,1,1)
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,1)

        _MeshHeight ("Mesh Height", Float) = 1
        _RimPower ("Rim Power", Range(0,8)) = 2

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness", Range(0,0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // ===== MAIN PASS =====
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _MainColor;
            float4 _GradColor;
            float4 _FresnelColor;

            float _MeshHeight;
            float _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float height : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                o.height = v.vertex.y / _MeshHeight;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);

                // Gradient
                float t = saturate(i.height);
                float3 color = lerp(_MainColor.rgb, _GradColor.rgb, t);

                // Fresnel
                float fresnel = pow(1 - saturate(dot(normal, i.viewDir)), _RimPower);
                color += fresnel * _FresnelColor.rgb;

                return float4(color,1);
            }

            ENDCG
        }

        // ===== OUTLINE PASS =====
        Pass
        {
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _OutlineThickness;
            float4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float3 normal = UnityObjectToWorldNormal(v.normal);
                float3 pos = mul(unity_ObjectToWorld, v.vertex).xyz;

                pos += normal * _OutlineThickness;

                o.pos = UnityWorldToClipPos(pos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }

            ENDCG
        }
    }
}