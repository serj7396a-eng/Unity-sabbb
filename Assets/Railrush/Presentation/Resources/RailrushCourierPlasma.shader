Shader "Railrush/Courier Plasma"
{
    Properties
    {
        [MainTexture] _MainTex ("Plasma falloff", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half axial = input.uv.y;
                half turbulence = sin(axial * 22.0h - _Time.y * 37.0h) * axial * 0.07h;
                half mask = tex2D(_MainTex, float2(input.uv.x + turbulence, axial)).a;
                half flicker = 0.77h + 0.16h * sin(_Time.y * 41.0h - axial * 20.0h)
                    + 0.07h * sin(_Time.y * 29.0h + axial * 26.0h);
                return half4(input.color.rgb * (1.1h + 0.53h * mask), input.color.a * mask * flicker);
            }
            ENDCG
        }
    }
}
