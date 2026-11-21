Shader "Railrush/NightTrack"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 paint : COLOR;
                float2 surface : TEXCOORD1;
            };

            struct Pixel
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half2 surface : TEXCOORD2;
                fixed4 paint : COLOR;
                UNITY_FOG_COORDS(3)
            };

            Pixel vert(Input input)
            {
                Pixel output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.paint = input.paint;
                output.surface = input.surface;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 frag(Pixel input) : SV_Target
            {
                half3 normal = normalize(input.normal);
                half3 towardCamera = normalize(_WorldSpaceCameraPos - input.world);
                half rim = 1.0h - abs(dot(normal, towardCamera));
                half top = max(0.0h, normal.y);
                half cool = max(0.0h, dot(normal, half3(-0.38h, 0.82h, -0.42h)));
                half warm = max(0.0h, dot(normal, half3(0.55h, 0.25h, 0.72h)));
                half3 base = input.paint.rgb * (0.49h + cool * 0.66h + warm * 0.18h);
                base += half3(0.033h, 0.038h, 0.082h) * (0.48h + top * 0.5h);
                half frost = input.surface.y * (rim * rim) * (0.36h + top * 0.64h);
                float2 track = input.world.xz;
                half detail = 0.53h + 0.26h * sin(track.x * 26.5 + sin(track.y * 3.2) * 2.5)
                             + 0.21h * sin(track.x * 7.7 + track.y * 8.9);
                half pools = 0.36h + 0.64h * smoothstep(-0.42h, 0.68h,
                    sin(track.x * 1.56 + track.y * 0.32) * sin(track.x * 3.6 - track.y * 0.57));
                half lamp = 0.32h + 0.68h * pow(0.5h + 0.5h * cos(track.y * 0.54 + track.x * 0.42), 4.0h);
                half3 wetColor = lerp(half3(0.52h, 0.19h, 0.66h), half3(0.11h, 0.57h, 0.72h),
                    smoothstep(-2.4h, 2.4h, track.x));
                base += wetColor * frost * pools * detail * lamp * 0.56h;
                base = lerp(base, input.paint.rgb * (0.92h + input.surface.x * 0.32h),
                    saturate(input.surface.x));
                fixed4 result = fixed4(base, 1);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
