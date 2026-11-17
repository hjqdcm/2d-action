Shader "SlashDemo/HeroCloth"
{
    Properties
    {
        _MainTex ("Character Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _VolumeStrength ("Painted Volume", Range(0, 1)) = 0.45
        _WalkBlend ("Walking", Range(0, 1)) = 0
        [HideInInspector] _PaintedPortrait ("Painted Portrait", Float) = 0
        [HideInInspector] _ProfileLegMask ("Profile Leg Mask", Float) = 0
        [HideInInspector] _SaberCutMask ("Saber Arm Cutout", 2D) = "black" {}
        [HideInInspector] _SaberCutMode ("Saber Layer", Float) = 0
        [HideInInspector] _SaberGripExtension ("Saber Grip Extension", Float) = 0
        [HideInInspector] _SaberPatchOffset ("Saber Underpaint UV Offset", Float) = 0
        [HideInInspector] _SupportCutMask ("Support Arm Cutout", 2D) = "black" {}
        [HideInInspector] _SupportCut ("Support Arm Cutout Enabled", Float) = 0
        [HideInInspector] _SupportPatchOffset ("Support Arm Underpaint UV Offset", Float) = 0
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
            TEXTURE2D(_SaberCutMask);
            SAMPLER(sampler_SaberCutMask);
            TEXTURE2D(_SupportCutMask);
            SAMPLER(sampler_SupportCutMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _VolumeStrength;
                half _WalkBlend;
                half _PaintedPortrait;
                half _ProfileLegMask;
                half _SaberCutMode;
                half _SaberGripExtension;
                float _SaberPatchOffset;
                half _SupportCut;
                float _SupportPatchOffset;
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
                if (_PaintedPortrait > 0.5h)
                {
                    if (_SaberCutMode > 0.5h)
                    {
                        half4 cut = SAMPLE_TEXTURE2D(_SaberCutMask, sampler_SaberCutMask, input.localUV);
                        if (input.localUV.x < 0)
                            paint.a *= _SaberGripExtension;
                        else if (_SaberCutMode > 1.5h)
                            paint.a *= _SaberCutMode > 2.5h ? cut.a : cut.b;
                        else if (cut.r > 0.001h)
                        {
                            half4 underpaint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                                input.uv + float2(_SaberPatchOffset, 0));
                            underpaint.a *= saturate(cut.g / cut.r);
                            paint = lerp(paint, underpaint, cut.r);
                        }
                    }
                    if (_SupportCut > 0.5h)
                    {
                        half2 cut = SAMPLE_TEXTURE2D(_SupportCutMask, sampler_SupportCutMask, input.localUV).rg;
                        if (cut.r > 0.001h)
                        {
                            half4 underpaint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                                input.uv + float2(_SupportPatchOffset, 0));
                            underpaint.a *= saturate(cut.g / cut.r);
                            paint = lerp(paint, underpaint, cut.r);
                        }
                    }
                    paint.a *= lerp(1.0h, input.binding.a, _ProfileLegMask);
                    return paint * _Color;
                }
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
