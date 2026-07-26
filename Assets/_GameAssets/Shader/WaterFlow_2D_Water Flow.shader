Shader "Custom/PerfectWaterFlow_WorldFix"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Pipe Mask (Texture Ống RGB)", 2D) = "black" {}
        _Bubble ("Bubble Texture (Bọt Khí RGB)", 2D) = "black" {}

        [Header(Colors)]
        _ColorOut ("Màu Nước Nông (Viền Ống)", Color) = (0.4, 0.7, 0.9, 1)
        _ColorIn ("Màu Nước Sâu (Tâm Ống)", Color) = (0.1, 0.4, 0.8, 1)
        _ColorHighlight ("Màu Bọt Khí", Color) = (1, 1, 1, 1)

        [Header(Flow Settings)]
        _FlowSpeed ("Tốc Độ Nước Chảy (Uốn lượn)", Float) = 1.5
        
        [Header(Bubble Settings)]
        _BubbleSpeed ("Tốc Độ Bay Lên Của Bọt Khí", Float) = 1.0
        // Mặc định bật chế độ Top-Down (Dùng trục XZ)
        [Toggle] _Is3DTopDown ("Chế độ Top-Down (Dùng mặt phẳng XZ)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

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
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1; // Lưu toàn bộ tọa độ World 3D
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _Bubble;
            float4 _Bubble_ST;

            float4 _ColorOut;
            float4 _ColorIn;
            float4 _ColorHighlight;
            
            float _FlowSpeed;
            float _BubbleSpeed;
            float _Is3DTopDown;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                
                // Lấy tọa độ thật của vật thể trong thế giới (bỏ qua sự bóp méo của Spline)
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. NƯỚC UỐN LƯỢN (DÙNG UV CỦA SPLINE)
                float waterScroll = _Time.y * _FlowSpeed;
                float2 waveUV = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                waveUV.x -= waterScroll; 
                
                fixed4 waveTex = tex2D(_MainTex, waveUV);
                fixed3 waterBaseColor = (waveTex.g * _ColorOut.rgb) + (waveTex.r * _ColorIn.rgb);
                float pipeAlpha = saturate((waveTex.r + waveTex.g + waveTex.b) * 2.0);

                // 2. BỌT KHÍ BAY THẲNG (DÙNG WORLD SPACE)
                float2 projPos;
                if (_Is3DTopDown == 1.0) {
                    projPos = i.worldPos.xz; // Game nhìn từ trên xuống (Trục X và Z)
                } else {
                    projPos = i.worldPos.xy; // Game 2D bình thường (Trục X và Y)
                }

                // Chiếu texture bọt khí lên mặt phẳng
                float2 bubbleUV = projPos * _Bubble_ST.xy + _Bubble_ST.zw;
                
                // Trừ y để bọt khí luôn trôi "lên trên" theo hướng nhìn
                bubbleUV.y -= _Time.y * _BubbleSpeed; 

                fixed4 bubbleTex = tex2D(_Bubble, bubbleUV);
                float totalBubbles = saturate(bubbleTex.r + bubbleTex.g + bubbleTex.b);

                fixed3 finalRGB = waterBaseColor + (totalBubbles * _ColorHighlight.rgb);

                return fixed4(finalRGB, pipeAlpha) * i.color;
            }
            ENDCG
        }
    }
}