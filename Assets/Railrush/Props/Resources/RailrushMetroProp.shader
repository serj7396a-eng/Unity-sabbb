Shader "Railrush/MetroProp"
{
    Properties
    {
        _Color ("Surface", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Light", Color) = (0, 0, 0, 1)
        _Metallic ("Metal", Range(0, 1)) = 0
        _Glossiness ("Polish", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            half4 _EmissionColor;
            half _Metallic;
            half _Glossiness;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FragmentInput
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                half3 normal : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FragmentInput vert(VertexInput v)
            {
                FragmentInput o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(FragmentInput, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(FragmentInput i) : SV_Target
            {
                half3 normal = normalize(i.normal);
                half3 lightDirection = normalize(UnityWorldSpaceLightDir(i.worldPos));
                half3 view = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half3 halfway = normalize(lightDirection + view);
                half shadow = SHADOW_ATTENUATION(i);
                half direct = saturate(dot(normal, lightDirection));
                half3 ambient = max(half3(0.21h, 0.24h, 0.3h), ShadeSH9(half4(normal, 1)) * 0.7h);
                half shine = pow(saturate(dot(normal, halfway)), lerp(8.0h, 72.0h, _Glossiness));
                half grazing = 1.0h - saturate(dot(normal, view));
                half3 metalColor = lerp(half3(0.33h, 0.39h, 0.46h), _Color.rgb, _Metallic);
                half3 outputColor = _Color.rgb * (ambient + _LightColor0.rgb * (direct * shadow));
                outputColor += metalColor * (shine * shadow * lerp(0.17h, 0.66h, _Glossiness) + grazing * grazing * 0.12h);
                outputColor += _EmissionColor.rgb;
                half4 output = half4(outputColor, 1.0h);
                UNITY_APPLY_FOG(i.fogCoord, output);
                return output;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "Diffuse"
}
