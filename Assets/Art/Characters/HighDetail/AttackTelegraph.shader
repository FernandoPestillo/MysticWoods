// Unlit vertex-coloured warning shapes (enemy attack sectors, slam rings). Intentionally not lit
// by Light2D: a telegraph must stay readable in the darkest parts of the forest.
Shader "Mystic Woods/Attack Telegraph"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
        Varyings vert(Attributes input)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(input.positionOS);
            o.color = input.color;
            return o;
        }
        half4 frag(Varyings input) : SV_Target { return input.color; }
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
