Shader "SlashDemo/Hero360Toon"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Flash ("Hit flash", Range(0,1)) = 0
        _DetailAtlas ("Original painted fabric atlas", 2D) = "white" {}
        _ViewDirection ("View direction", Vector) = (0,-0.707,-0.707,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity;
                half _Flash;
                half4 _ViewDirection;
            CBUFFER_END
            TEXTURE2D(_DetailAtlas); SAMPLER(sampler_DetailAtlas);
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; float2 surface : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; half4 color : COLOR; float2 uv : TEXCOORD1; half2 surface : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.surface = input.surface;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half light = dot(normal, normalize(half3(-0.45, 0.65, -0.65)));
                half band = 0.72 + 0.16 * smoothstep(-0.15, 0.10, light) + 0.12 * smoothstep(0.45, 0.60, light);
                half3 shade = lerp(half3(0.83, 0.88, 0.98), half3(1,1,1), band);
                half4 painted = SAMPLE_TEXTURE2D(_DetailAtlas, sampler_DetailAtlas, input.uv);
                half ink = dot(painted.rgb, half3(0.299, 0.587, 0.114));
                // Painting supplies folds and embroidery; its transparent pixels never cut holes in the mesh.
                half fabric = lerp(1, clamp(ink / 0.94, 0.65, 1.07), painted.a * input.surface.x);
                half hairMask = step(2.5, input.surface.y) * step(input.surface.y, 3.5);
                half strands = 1 - hairMask * 0.025 * (0.5 + 0.5 * sin(input.uv.x * 420 + input.uv.y * 16));
                half edge = abs(dot(normal, normalize(_ViewDirection.xyz)));
                half outline = lerp(0.78, 1, smoothstep(0.06, 0.27, edge));
                half3 color = input.color.rgb * band * shade * fabric * strands * outline * _Tint.rgb;
                color = lerp(color, half3(1,1,1), _Flash);
                return half4(color, _Opacity * _Tint.a);
            }
            ENDHLSL
        }
    }
}
