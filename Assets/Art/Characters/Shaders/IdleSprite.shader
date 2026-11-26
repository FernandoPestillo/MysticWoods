// URP 2D character sprite. Lit by Light2D, with procedural breathing/attack deformation
// around the feet and a post-lighting hit flash so feedback reads even in dark areas.
Shader "Mystic Woods/Idle Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _IdleStretch ("Idle Stretch", Vector) = (0,0,0,0)
        [PerRendererData] _AttackStretch ("Attack Stretch", Vector) = (0,0,0,0)
        [PerRendererData] _AttackTint ("Attack Tint", Color) = (0,0,0,0)
        [PerRendererData] _FlashColor ("Flash Color", Color) = (1,1,1,0)
        [PerRendererData] _Fade ("Dissolve", Range(0,1)) = 0
        [PerRendererData] _VisualOffset ("Visual offset (local units)", Vector) = (0,0,0,0)
        _RimLight ("Ambient floor", Range(0,1)) = 0.18
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        float4 _IdleStretch;
        float4 _AttackStretch;
        half4 _AttackTint;
        half4 _FlashColor;
        half _Fade;
        half _RimLight;
        float4 _VisualOffset;
    CBUFFER_END

    float3 Deform(float3 positionOS)
    {
        float2 anchor = float2(_IdleStretch.w, _IdleStretch.z);
        positionOS.xy = anchor + (positionOS.xy - anchor) * (1 + _IdleStretch.xy);
        float2 attackAnchor = float2(_AttackStretch.w, _AttackStretch.z);
        positionOS.xy = attackAnchor + (positionOS.xy - attackAnchor) * (1 + _AttackStretch.xy);
        // Hops and leaps lift only the drawing; physics and colliders stay on the ground.
        positionOS.xy += _VisualOffset.xy;
        return positionOS;
    }

    // Ordered pixel dissolve keeps the pixel-art look while a body fades out.
    half Dither(float2 uv)
    {
        float2 cell = floor(uv * 64);
        return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
    }

    half4 Finish(half4 lit, half4 albedo, float2 uv)
    {
        // Characters must never vanish in the darkest corners: keep a small floor of their own color.
        lit.rgb = max(lit.rgb, albedo.rgb * _RimLight);
        lit.rgb = lerp(lit.rgb, _AttackTint.rgb, _AttackTint.a);
        lit.rgb = lerp(lit.rgb, _FlashColor.rgb, _FlashColor.a);
        if (_Fade > 0 && Dither(uv) < _Fade) lit.a = 0;
        return lit;
    }
    ENDHLSL

    SubShader
    {
        // Local vertex coordinates are required to keep the feet anchored.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"
               "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex LitVertex
            #pragma fragment LitFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color : COLOR; };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                input.positionOS = Deform(input.positionOS);
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 albedo = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return Finish(CommonLitFragment(input, input.color), albedo, input.uv);
            }
            ENDHLSL
        }

        Pass
        {
            // Used by cameras that do not run the 2D renderer (scene previews, tests).
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma multi_compile_instancing

            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            Varyings UnlitVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                input.positionOS = Deform(input.positionOS);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                return Finish(color, color, input.uv);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
