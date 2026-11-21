Shader "Railrush/Night Sky"
{
    Properties { _District ("District", Color) = (0.13,0.15,0.25,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _District;
            struct SkyVertex { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            SkyVertex Vert(float4 position : POSITION)
            {
                SkyVertex output;
                output.vertex = UnityObjectToClipPos(position);
                output.direction = position.xyz;
                return output;
            }
            fixed4 Frag(SkyVertex input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float horizon = exp(-max(ray.y, 0) * 6.4);
                float3 sky = lerp(float3(.034,.031,.086), _District.rgb, horizon);
                float azimuth = atan2(ray.x, ray.z);
                float bands = sin(azimuth * 9.0 + ray.y * 18.0 + sin(ray.y * 17.0 - azimuth * 3.1));
                float clouds = smoothstep(.12,.84,bands) * smoothstep(.04,.18,ray.y) * (1-smoothstep(.38,.59,ray.y));
                sky = lerp(sky, sky * float3(.64,.67,.95), clouds * .54);
                float halo = saturate(dot(ray, normalize(float3(.085,.155,1))));
                float moon = smoothstep(.9992,.99945,halo);
                sky += float3(.27,.11,.28) * pow(halo, 56);
                sky += float3(.96,.27,.50) * moon * (1 - .17 * smoothstep(.39,.70,frac(ray.y*181)));
                return fixed4(sky,1);
            }
            ENDCG
        }
    }
}
