Shader "SlashDemo/BladeTrailAdditive"
{
    Properties
    {
        _Intensity ("HDR blade core intensity", Range(1, 3)) = 2.2
        _GlowIntensity ("Outer glow intensity", Range(0, 2)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _GlowIntensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; half emission : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.emission = saturate(input.uv.x);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Boost only RGB. Multiplying alpha would thicken and overexpose the whole ribbon.
                return half4(input.color.rgb * lerp(_GlowIntensity, _Intensity, input.emission), input.color.a);
            }
            ENDHLSL
        }
    }
}
