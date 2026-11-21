Shader "Railrush/Imported Surface"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glow ("Neon", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Glow;
            struct App { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct Varying
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            Varying Vert(App input)
            {
                Varying output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 Frag(Varying input) : SV_Target
            {
                half3 n = normalize(input.normal);
                half3 v = normalize(_WorldSpaceCameraPos - input.world);
                half diffuse = saturate(dot(n, normalize(half3(-.40, .81, -.34))));
                half rim = pow(1.0h - saturate(dot(n, v)), 2.6h);
                half3 reflected = lerp(half3(.25,.12,.47), half3(.07,.39,.53), saturate(n.x * .5h + .5h));
                fixed4 albedo = tex2D(_MainTex, input.uv) * _Color;
                fixed4 color = fixed4(albedo.rgb * (.39h + diffuse * .66h + _Glow) + reflected * rim * .26h, 1);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
