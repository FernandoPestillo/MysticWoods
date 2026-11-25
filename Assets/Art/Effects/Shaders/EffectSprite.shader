// Unlit glow/particle shader for URP 2D: vertex colour * texture, selectable blending.
// Additive (One One) for sparks, fireflies, after-images; Alpha for dust and splashes.
Shader "Mystic Woods/Effect Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Intensity;
            float _SrcBlend;
            float _DstBlend;
            float4 _MainTex_ST;
        CBUFFER_END
        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
        Varyings vert(Attributes input)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(input.positionOS);
            o.uv = input.uv;
            o.color = input.color * _Color;
            return o;
        }
        half4 frag(Varyings input) : SV_Target
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            c.rgb *= _Intensity;
            // Additive materials fade through colour; alpha materials through alpha.
            c.rgb *= _DstBlend > 0.5 && _DstBlend < 1.5 ? c.a : 1.0;
            return c;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
