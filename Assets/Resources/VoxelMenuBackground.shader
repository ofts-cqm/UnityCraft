Shader "UnityCraft/UI/Atlas Tile"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("UI Main Texture", 2D) = "white" {}
        [NoScaleOffset] _Atlas ("Atlas", 2DArray) = "white" {}
        _Slice ("Atlas Slice", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            UNITY_DECLARE_TEX2DARRAY(_Atlas);
            float _Slice;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 uiTexture = tex2D(_MainTex, frac(input.uv));
                fixed4 atlasTexture = UNITY_SAMPLE_TEX2DARRAY(_Atlas, float3(frac(input.uv), _Slice));
                return uiTexture * atlasTexture * input.color;
            }
            ENDCG
        }
    }
}
