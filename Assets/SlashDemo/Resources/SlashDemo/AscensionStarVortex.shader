Shader "SlashDemo/AscensionStarVortex"
{
    Properties
    {
        _Age ("Age", Float) = 0
        _Fade ("Fade", Float) = 1
        _Collapse ("Collapse", Float) = 0
    }
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
                float _Age, _Fade, _Collapse;
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
                float2 centeredUV = input.uv * 2 - 1;
                float radius = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x + .00001);
                float edge = 1 - smoothstep(.97, 1, radius);
                float rim = 1 - smoothstep(.008, .025, abs(radius - .947));
                float ticks = pow(saturate(.5 + .5 * cos(angle * 36 + _Age)), 12);
                ticks *= smoothstep(.865, .892, radius) * (1 - smoothstep(.924, .941, radius));

                float r = radius / lerp(1, .30, _Collapse);
                float flow = angle * 3 + r * 15 + _Age * 7;
                float nebula = pow(saturate(.5 + .5 * cos(flow + .65 * sin(r * 18 + _Age))), 3);
                float filament = pow(saturate(.5 + .5 * cos(flow + .4)), 13);
                float innerEdge = 1 - smoothstep(.82, 1, r);
                float dustAngle = angle / TWO_PI * 24 + r * 5 - _Age * 2.8;
                float dustRadius = r * 7 + _Age * 1.95 + .63 * sin(floor(dustAngle) * 2.41);
                float2 dustCell = float2(frac(dustAngle) - .5, frac(dustRadius) - .5);
                float stars = (1 - smoothstep(.07, .24, length(dustCell))) * innerEdge;

                float core = exp(-r * r * 110);
                float collar = 1 - smoothstep(.010, .036, abs(r - .19));
                float3 color = float3(.10, .045, .29);
                color += float3(.29, .09, .69) * nebula * innerEdge;
                color += float3(.20, .52, .86) * filament * innerEdge;
                color += float3(.62, .61, .98) * (stars + .66 * rim + .55 * ticks);
                color += float3(.76, .82, 1) * (core + .52 * collar);
                float alpha = (.31 + nebula * innerEdge * .24 + filament * innerEdge * .27
                    + stars * .35 + rim * .54 + ticks * .34 + core * .68 + collar * .34) * edge;
                return half4(saturate(color), saturate(alpha) * _Fade);
            }
            ENDHLSL
        }
    }
}
