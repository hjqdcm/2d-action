Shader "SlashDemo/HeroCloth"
{
    Properties
    {
        _MainTex ("Character Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _VolumeStrength ("Painted Volume", Range(0, 1)) = 0.45
        _WalkBlend ("Walking", Range(0, 1)) = 0
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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _VolumeStrength;
                half _WalkBlend;
            CBUFFER_END
            float4 _MainTex_TexelSize;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 localUV : TEXCOORD1; float4 binding : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 localUV : TEXCOORD1; float4 binding : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.localUV = input.localUV;
                output.binding = input.binding;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half clothWhite = 1 - smoothstep(0.40h, 0.60h, (paint.b - paint.r) / max(0.001h, paint.b));
                clothWhite = max(clothWhite, input.binding.a);
                half left = input.binding.r * clothWhite, right = input.binding.g * clothWhite;
                if (input.binding.b > 0.75) clip(right - 0.5h);
                else if (input.binding.b > 0.4) clip(left - 0.5h);
                else if (input.binding.b < 0.1) clip(0.5h - max(left, right));
                else paint.a *= _WalkBlend;
                // Gentle cool underside shading preserves the highlights painted into every view.
                half light = saturate(0.7h - (input.localUV.x - 0.5h) * 0.65h + (input.localUV.y - 0.5h) * 0.18h);
                half band = smoothstep(0.35h, 0.78h, light);
                half3 volume = lerp(half3(0.72h, 0.80h, 0.93h), half3(1.03h, 1.02h, 1.0h), band);
                paint.rgb *= lerp(half3(1, 1, 1), volume, _VolumeStrength);
                float2 pixel = _MainTex_TexelSize.xy;
                half edge = min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(pixel.x, 0)).a,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(pixel.x, 0)).a);
                edge = min(edge, min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, pixel.y)).a,
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, pixel.y)).a));
                paint.rgb = lerp(paint.rgb, half3(0.12h, 0.18h, 0.24h), (1 - edge) * paint.a * 0.24h);
                return paint * _Color;
            }
            ENDHLSL
        }
    }
}
