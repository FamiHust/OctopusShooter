Shader "Custom/S_GradientOutLine_MultiPass" {
	Properties {
		_MainColor ("MainColor", Vector) = (1,0,0.05476761,1)
		_GradColor ("GradColor", Vector) = (1,0.477572,0,1)
		_FresnelColor ("FresnelColor", Vector) = (1,1,1,1)
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_RimPower ("RimPower", Range(0, 10)) = 0
		_MeshHeight ("MeshHeight", Float) = 0.4
		_OutlineColor ("Outline Color", Vector) = (0,0,0,1)
		_OutlineThickness ("Outline Thickness", Range(0, 0.1)) = 0.02
		_OutlinePower ("Outline Power", Range(0.1, 10)) = 4
		[HideInInspector] __dirty ("", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Standard"
}