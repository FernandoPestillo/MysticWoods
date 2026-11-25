// URP 2D terrain for the forest Tilemaps. World-space mirrored repetition hides cell seams;
// path tiles encode their neighbours in vertex colour to soften borders. Lit by Light2D.
Shader "Mystic Woods/Forest Ground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Generated terrain", 2D) = "white" {}
        _TextureWorldSize ("Texture world size", Float) = 4
        _Transitions ("Neighbor transitions", Float) = 0
        _Tint ("Terrain tint", Color) = (1,1,1,1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float _TextureWorldSize;
        float _Transitions;
        half4 _Tint;
    CBUFFER_END

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    float Bit(float mask, float value) { return fmod(floor(mask / value), 2); }

    float3 ToGamma(float3 c)
    {
        float3 hi = 1.055 * pow(max(c, 0), 1.0 / 2.4) - 0.055;
        return lerp(hi, c * 12.92, step(c, 0.0031308));
    }

    half4 Terrain(float2 world, half4 neighbors)
    {
        float2 uv = 1 - abs(frac(world / (_TextureWorldSize * 2)) * 2 - 1);
        half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Tint;
        if (_Transitions > .5)
        {
            float2 cellUV = frac(world);
            // Tilemap converts vertex colours to linear; restore the encoded neighbour bits.
            float3 encoded = neighbors.rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
            encoded = ToGamma(encoded);
            #endif
            float mask = floor(encoded.r * 15 + .5);
            float diagonal = floor(encoded.g * 15 + .5);
            float left = Bit(mask, 1), right = Bit(mask, 2), down = Bit(mask, 4), up = Bit(mask, 8);
            float distance = min(min(lerp(cellUV.x, 1, left), lerp(1 - cellUV.x, 1, right)),
                                 min(lerp(cellUV.y, 1, down), lerp(1 - cellUV.y, 1, up)));
            float alpha = smoothstep(.01, .27, distance);
            if (left * down > .5 && Bit(diagonal, 1) < .5) alpha *= smoothstep(.02, .27, length(cellUV));
            if (right * down > .5 && Bit(diagonal, 2) < .5) alpha *= smoothstep(.02, .27, length(float2(1 - cellUV.x, cellUV.y)));
            if (left * up > .5 && Bit(diagonal, 4) < .5) alpha *= smoothstep(.02, .27, length(float2(cellUV.x, 1 - cellUV.y)));
            if (right * up > .5 && Bit(diagonal, 8) < .5) alpha *= smoothstep(.02, .27, length(1 - cellUV));
            color.a *= alpha;
        }
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                half4 neighbors : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.world = TransformObjectToWorld(input.positionOS).xy;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.neighbors = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = Terrain(input.world, input.neighbors);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(color.rgb, color.a, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
                InitializeInputData(input.world, input.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float3 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; half4 neighbors : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.world = TransformObjectToWorld(input.positionOS).xy;
                o.neighbors = input.color;
                return o;
            }
            half4 frag(Varyings input) : SV_Target { return Terrain(input.world, input.neighbors); }
            ENDHLSL
        }
    }
}
