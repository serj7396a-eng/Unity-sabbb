Shader "Railrush/Rain"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off Cull Off Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Input { float4 position : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Output Vert(Input data)
            {
                Output output;
                output.position = UnityObjectToClipPos(data.position);
                output.color = data.color;
                output.uv = data.uv;
                return output;
            }
            fixed4 Frag(Output input) : SV_Target
            {
                float pin = (1 - smoothstep(0.0, 0.5, abs(input.uv.x - 0.5))) * sin(input.uv.y * 3.14159);
                return fixed4(input.color.rgb, input.color.a * pin);
            }
            ENDCG
        }
    }
}
