Shader "Mystic Woods/Forest Ground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Generated terrain", 2D) = "white" {}
        _TextureWorldSize ("Texture world size", Float) = 4
        _Transitions ("Neighbor transitions", Float) = 0
        _Tint ("Terrain tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 neighbors:COLOR; };
            sampler2D _MainTex;
            float _TextureWorldSize, _Transitions;
            fixed4 _Tint;
            v2f vert(appdata input)
            {
                v2f output;
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.world=mul(unity_ObjectToWorld,input.vertex).xy;
                output.uv=input.uv;
                output.neighbors=input.color;
                return output;
            }
            float bit(float mask,float value) { return fmod(floor(mask/value),2); }
            fixed4 frag(v2f input):SV_Target
            {
                // Shared world-space texels and mirrored repetition prevent cell seams.
                float2 uv=1-abs(frac(input.world/(_TextureWorldSize*2))*2-1);
                fixed4 color=tex2D(_MainTex,uv)*_Tint;
                if (_Transitions>.5)
                {
                    float2 cellUV=frac(input.world);
                    // Tilemap converts vertex colors to linear; restore the encoded neighbor bits.
                    float3 encoded=input.neighbors.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    encoded=LinearToGammaSpace(encoded);
                    #endif
                    float mask=floor(encoded.r*15+.5);
                    float diagonal=floor(encoded.g*15+.5);
                    float left=bit(mask,1),right=bit(mask,2),down=bit(mask,4),up=bit(mask,8);
                    float distance=min(min(lerp(cellUV.x,1,left),lerp(1-cellUV.x,1,right)),
                                       min(lerp(cellUV.y,1,down),lerp(1-cellUV.y,1,up)));
                    float alpha=smoothstep(.01,.27,distance);
                    if(left*down>.5 && bit(diagonal,1)<.5) alpha*=smoothstep(.02,.27,length(cellUV));
                    if(right*down>.5 && bit(diagonal,2)<.5) alpha*=smoothstep(.02,.27,length(float2(1-cellUV.x,cellUV.y)));
                    if(left*up>.5 && bit(diagonal,4)<.5) alpha*=smoothstep(.02,.27,length(float2(cellUV.x,1-cellUV.y)));
                    if(right*up>.5 && bit(diagonal,8)<.5) alpha*=smoothstep(.02,.27,length(1-cellUV));
                    color.a*=alpha;
                }
                return color;
            }
            ENDCG
        }
    }
}
