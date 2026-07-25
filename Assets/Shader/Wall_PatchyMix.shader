Shader "Custom/Wall_PatchyMix"
{
    Properties
    {
        _BaseTex ("Base Albedo", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _PatchTex ("Patch Albedo", 2D) = "white" {}
        _PatchColor ("Patch Color", Color) = (0.8,0.8,0.8,1)

        _PatchAmount ("Patch Amount", Range(0,1)) = 0.65
        _PatchStrength ("Patch Strength", Range(0,1)) = 1.0
        _PatchWorldScale ("Patch World Scale", Range(0.1, 10.0)) = 1.2
        _PatchSpacing ("Patch Spacing (Cells)", Range(1.0, 8.0)) = 2.0
        _PatchSize ("Patch Size", Range(0.1, 0.95)) = 0.58
        _PatchRoundness ("Patch Roundness", Range(0.0, 0.45)) = 0.18
        _PatchFeather ("Patch Feather", Range(0.001, 0.2)) = 0.04

        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BaseTex;
        sampler2D _PatchTex;

        fixed4 _BaseColor;
        fixed4 _PatchColor;

        half _PatchAmount;
        half _PatchStrength;
        half _PatchWorldScale;
        half _PatchSpacing;
        half _PatchSize;
        half _PatchRoundness;
        half _PatchFeather;

        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float2 uv_BaseTex;
            float2 uv_PatchTex;
            float3 worldPos;
            float3 worldNormal;
        };

        float SdRoundedBox(float2 p, float2 b, float r)
        {
            float2 q = abs(p) - b + r;
            return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 baseCol = tex2D(_BaseTex, IN.uv_BaseTex) * _BaseColor;
            fixed4 patchCol = tex2D(_PatchTex, IN.uv_PatchTex) * _PatchColor;

            // Choose projection plane from dominant world normal for stable world-space tiling.
            float3 n = abs(normalize(IN.worldNormal));
            float2 worldUV = (n.y >= n.x && n.y >= n.z) ? IN.worldPos.xz : (n.x >= n.z ? IN.worldPos.zy : IN.worldPos.xy);
            float2 gridUV = worldUV * _PatchWorldScale;

            float2 cell = floor(gridUV);
            float2 local = frac(gridUV) - 0.5;

            // Deterministic patch anchors on a global world-space grid.
            float spacing = max(1.0, round(_PatchSpacing));
            float2 cellScaled = cell / spacing;
            float2 deltaToAnchor = abs(cellScaled - round(cellScaled));
            float hasPatch = (1.0 - step(0.001, deltaToAnchor.x)) * (1.0 - step(0.001, deltaToAnchor.y));

            // Deterministic (non-random) density gate so Patch Amount remains useful.
            float pattern = frac(cell.x * 0.61803398875 + cell.y * 0.41421356237);
            hasPatch *= step(1.0 - _PatchAmount, pattern);

            // Rounded-square mask inside each patch cell.
            float patchSize = saturate(_PatchSize);
            float roundness = min(_PatchRoundness, patchSize * 0.49);

            float halfSize = patchSize * 0.5;
            float2 boxHalf = float2(halfSize, halfSize);
            float sdf = SdRoundedBox(local, boxHalf, roundness);

            float shapeMask = 1.0 - smoothstep(0.0, _PatchFeather, sdf);
            float mask = saturate(shapeMask * hasPatch * _PatchStrength);

            // Patch inherits base by tinting base color instead of replacing it.
            fixed3 patchTintedBase = baseCol.rgb * patchCol.rgb;
            fixed3 finalAlbedo = lerp(baseCol.rgb, patchTintedBase, mask);

            o.Albedo = finalAlbedo;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Standard"
}
