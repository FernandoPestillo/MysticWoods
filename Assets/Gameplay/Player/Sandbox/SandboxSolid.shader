Shader "Mystic Woods/Player Sandbox Solid"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; };
            v2f vert(appdata input) { v2f output; output.position = UnityObjectToClipPos(input.vertex); return output; }
            fixed4 frag(v2f input) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
