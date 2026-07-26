Shader "WaterFlow/3D/Outline"
{
    Properties
    {
        _ColorOutline("Main Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness", Float) = 1
        _Bias ("Bias", Float) = 0.0
		[Enum(Off,0,On,1)] _ZWriteMode ("ZWrite Mode", Float) = 1
    }
    SubShader
    {
        ZWrite [_ZWriteMode]
        Cull Front
        Pass
        {

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                half3 normal : NORMAL;
                half4 vertex : POSITION;
            };

            struct v2f
            {
                half4 vertex : SV_POSITION;
            };

            sampler2D _MainTex, _MatCap;
            CBUFFER_START (UnityPerMaterial)
            half4 _ColorOutline;
            half _OutlineThickness, _Bias;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;

                half3 pos = v.vertex.xyz;
                pos += v.normal * 0.01 * _OutlineThickness;

                o.vertex = TransformObjectToHClip(pos);
                o.vertex.z -= _Bias * 0.002 * 0.5 * _ProjectionParams.y;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return _ColorOutline;
            }
            ENDHLSL
        }
    }
}