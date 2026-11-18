Shader "SlashDemo/BloodTideMist"
{
    Properties
    {
        _Age ("Age", Float) = 0
        _Fade ("Opacity", Range(0, 1)) = 1
        [HideInInspector] _BorderMask ("Arena border", 2D) = "white" {}
        [HideInInspector] _UseBorderMask ("Use arena border", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BorderMask);
            SAMPLER(sampler_BorderMask);
            CBUFFER_START(UnityPerMaterial)
                float _Age, _Fade, _UseBorderMask;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 samplePosition)
            {
                float3 value = frac(samplePosition.xyx * .1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float Noise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 blend = frac(samplePosition);
                blend = blend * blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * 2 - 1;
                float2 patch = uv * lerp(1, 4, _UseBorderMask);
                float coarse = Noise(patch * 3.6 + float2(-.35, .18) * _Age);
                float wisps = Noise(patch * float2(6, 10) + coarse * 1.7 + float2(.46, -.31) * _Age);
                float edge = _UseBorderMask > .5
                    ? SAMPLE_TEXTURE2D(_BorderMask, sampler_BorderMask, input.uv).r
                    : 1 - smoothstep(.76 + .15 * coarse, 1, length(uv));
                float density = smoothstep(.17, .84, .65 * coarse + .35 * wisps);
                float3 color = lerp(float3(.18, .012, .035), float3(.58, .08, .12), density);
                return half4(color, edge * (.18 + density * .54) * _Fade);
            }
            ENDHLSL
        }
    }
}
