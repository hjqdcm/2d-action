Shader "SlashDemo/ManorSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Manor Paint", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SceneEdgeFade ("Scene edge fade", Range(0, 0.1)) = 0
        _SceneEdgeColor ("Scene edge color", Color) = (0.7, 0.74, 0.7, 1)
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
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            int _ManorProtectCount;
            float4 _ManorProtectRects[16];
            float4 _ManorProtectRight, _ManorProtectUp;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _SceneEdgeColor;
                float _SceneEdgeFade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Do not repaint an actor with a foreground crop when their feet are beside it.
                float2 projected = float2(dot(input.positionWS, _ManorProtectRight.xyz), dot(input.positionWS, _ManorProtectUp.xyz));
                [loop] for (int index = 0; index < _ManorProtectCount; index++)
                {
                    float4 bounds = _ManorProtectRects[index];
                    if (all(projected >= bounds.xy) && all(projected <= bounds.zw)) clip(-1);
                }
                // Nine bilinear taps reconstruct a Catmull-Rom surface when enlarged.
                // Background and registered foreground cutouts sample the same atlas UVs.
                float2 position = input.uv * _MainTex_TexelSize.zw;
                float2 center = floor(position - 0.5) + 0.5;
                float2 f = position - center;
                float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
                float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
                float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
                float2 w3 = f * f * (-0.5 + 0.5 * f);
                float2 w12 = w1 + w2;
                float2 middle = center + w2 / w12;
                float3 u = float3(center.x - 1, middle.x, center.x + 2) * _MainTex_TexelSize.x;
                float3 v = float3(center.y - 1, middle.y, center.y + 2) * _MainTex_TexelSize.y;
                float3 weightX = float3(w0.x, w12.x, w3.x);
                float3 weightY = float3(w0.y, w12.y, w3.y);
                half4 paint = 0;
                half4 darkest = 65504, brightest = -65504;
                [unroll] for (int y = 0; y < 3; y++)
                {
                    [unroll] for (int x = 0; x < 3; x++)
                    {
                        half4 tap = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(u[x], v[y]), 0);
                        paint += tap * (weightX[x] * weightY[y]);
                        darkest = min(darkest, tap);
                        brightest = max(brightest, tap);
                    }
                }
                // Bound reconstruction to the local palette to suppress ringing at tile edges.
                paint = clamp(paint, darkest, brightest);
                if (_SceneEdgeFade > 0)
                {
                    float edge = min(min(input.uv.x, input.uv.y), min(1 - input.uv.x, 1 - input.uv.y));
                    paint.rgb = lerp(_SceneEdgeColor.rgb, paint.rgb, smoothstep(0, _SceneEdgeFade, edge));
                }
                return paint * input.color;
            }
            ENDHLSL
        }
    }
}
