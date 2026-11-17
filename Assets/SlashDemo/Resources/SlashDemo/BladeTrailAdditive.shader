Shader "SlashDemo/BladeTrailAdditive"
{
    Properties
    {
        _Intensity ("HDR blade core intensity", Range(1, 3)) = 2.2
        _GlowIntensity ("Outer glow intensity", Range(0, 2)) = 2
        [HideInInspector] _EdgeHighlight ("Edge highlight", Range(0, 1)) = 0
        [HideInInspector] _SoftEdge ("Soft edge", Range(0, 1)) = 0
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
                half _EdgeHighlight;
                half _SoftEdge;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; half2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = saturate(input.uv);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Boost only RGB. Multiplying alpha would thicken and overexpose the whole ribbon.
                half across = abs(input.uv.y * 2 - 1);
                half edge = _EdgeHighlight * smoothstep(.74h, .92h, across);
                half3 rgb = lerp(input.color.rgb, half3(.93h, .23h, .25h), edge);
                half alpha = input.color.a * (1 - _SoftEdge * smoothstep(.22h, 1, across));
                return half4(rgb * lerp(_GlowIntensity, _Intensity, input.uv.x), alpha);
            }
            ENDHLSL
        }
    }
}
