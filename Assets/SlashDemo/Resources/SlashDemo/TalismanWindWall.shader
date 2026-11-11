Shader "SlashDemo/TalismanWindWall"
{
    Properties { _Age ("Age", Float) = 0 _Fade ("Fade", Float) = 1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Age, _Fade;
            CBUFFER_END

            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float x = uv.x - _Age * .48;
                float billow = sin(x * 21 + uv.y * 5) * .05 + sin(x * 53 - uv.y * 13) * .022;
                float height = uv.y + billow;
                float ends = smoothstep(0, .10, uv.x) * smoothstep(0, .10, 1 - uv.x);
                float crest = .82 + .09 * sin(x * 29) + .035 * sin(x * 61);
                float curtain = 1 - smoothstep(crest - .16, crest, height);
                float stream = pow(saturate(.5 + .5 * sin(height * 56 + sin(x * 18) * 1.7)), 7);
                stream *= .35 + .65 * smoothstep(-.35, .9, sin(x * 45 + height * 8));
                float wisps = pow(saturate(.5 + .5 * sin(height * 83 + x * 28)), 11);
                float foot = exp(-uv.y * 13);
                float3 color = lerp(float3(.32, .66, .62), float3(1, .98, .86), saturate(stream + .4 * wisps + foot));
                float alpha = ends * curtain * saturate(.34 + .46 * stream + .14 * wisps + .28 * foot) * _Fade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
