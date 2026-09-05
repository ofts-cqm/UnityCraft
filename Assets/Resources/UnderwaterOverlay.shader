Shader "UnityCraft/Underwater Overlay"
{
    Properties
    {
        _WaterAtlas ("Water Atlas", 2DArray) = "" {}
        _Frame ("Water Frame", Float) = 32
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_WaterAtlas);
            float _Frame;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return UNITY_SAMPLE_TEX2DARRAY(_WaterAtlas, float3(input.uv, _Frame)) * input.color;
            }
            ENDCG
        }
    }
}
