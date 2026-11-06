Shader "Mystic Woods/Idle Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _IdleStretch ("Idle Stretch", Vector) = (0,0,0,0)
        [PerRendererData] _AttackStretch ("Attack Stretch", Vector) = (0,0,0,0)
        [PerRendererData] _AttackTint ("Attack Tint", Color) = (0,0,0,0)
    }
    SubShader
    {
        // Local vertex coordinates are required to keep the feet anchored.
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
               "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex IdleVert
            #pragma fragment CharacterFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _IdleStretch;
            float4 _AttackStretch;
            fixed4 _AttackTint;
            v2f IdleVert(appdata_t input)
            {
                float2 anchor = float2(_IdleStretch.w, _IdleStretch.z);
                input.vertex.xy = anchor + (input.vertex.xy - anchor) * (1 + _IdleStretch.xy);
                float2 attackAnchor = float2(_AttackStretch.w, _AttackStretch.z);
                input.vertex.xy = attackAnchor + (input.vertex.xy - attackAnchor) * (1 + _AttackStretch.xy);
                return SpriteVert(input);
            }
            fixed4 CharacterFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                color.rgb = lerp(color.rgb, _AttackTint.rgb, _AttackTint.a);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
