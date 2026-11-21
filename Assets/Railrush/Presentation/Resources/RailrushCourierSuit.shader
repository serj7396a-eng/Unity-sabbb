Shader "Railrush/Courier Suit"
{
    Properties
    {
        [MainTexture] _MainTex ("Suit paint atlas", 2D) = "white" {}
        _EmissionMap ("Light atlas", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 185

        CGPROGRAM
        #pragma surface surf Standard addshadow nolightmap noforwardadd
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard surface)
        {
            half3 paint = tex2D(_MainTex, input.uv_MainTex).rgb;
            half3 trim = tex2D(_EmissionMap, input.uv_MainTex).rgb;
            half rim = 1.0h - saturate(dot(normalize(input.viewDir), half3(0.0h, 0.0h, 1.0h)));
            half pulse = 1.17h + 0.16h * sin(_Time.y * 5.9h - input.worldPos.y * 14.0h);
            surface.Albedo = paint;
            surface.Metallic = 0.15h;
            surface.Smoothness = 0.28h;
            surface.Emission = trim * pulse + half3(0.018h, 0.026h, 0.070h) * rim * rim;
            surface.Alpha = 1.0h;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
