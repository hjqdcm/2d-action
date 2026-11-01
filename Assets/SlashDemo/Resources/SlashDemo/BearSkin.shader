Shader "SlashDemo/BearSkin"
{
    Properties
    {
        _MainTex ("Bear artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PartIndex ("Rigid part", Float) = -1
        _JointRect ("Joint paint crop", Vector) = (.5,.4,.2,.15)
        _JointOpacity ("Joint cover opacity", Float) = 1
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PartIndex;
                float4 _JointRect;
                float _JointOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Edge(float2 a,float2 b,float2 p)
            { return (b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x); }
            float Quad(float2 p,float2 a,float2 b,float2 c,float2 d)
            { return step(0,min(min(Edge(a,b,p),Edge(b,c,p)),min(Edge(c,d,p),Edge(d,a,p)))); }
            float4 LimbMasks(float2 p)
            {
                float nearHind=Quad(p,float2(-.03,-.03),float2(.29,-.03),float2(.24,.40),float2(-.03,.38));
                float farHind=Quad(p,float2(.30,.12),float2(.465,.12),float2(.43,.31),float2(.31,.30));
                float nearFore=Quad(p,float2(.43,-.03),float2(.80,-.03),float2(.64,.44),float2(.405,.40));
                float farFore=Quad(p,float2(.78,.045),float2(1.03,.045),float2(1.03,.44),float2(.80,.42));
                farHind *= 1-nearFore;
                return float4(nearHind,farHind,nearFore,farFore);
            }
            half4 Frag(Varyings input):SV_Target
            {
                if (_PartIndex >= 3.5)
                {
                    float2 local=(input.uv-_JointRect.xy)/_JointRect.zw*2;
                    float radius=length(local);
                    // A soft irregular fur/stone edge conceals the cut line.
                    float edge=.98+.025*sin(local.x*28+local.y*17);
                    half cover=(1-smoothstep(edge-.15,edge,radius))*_JointOpacity;
                    clip(cover-.005);
                    half4 paint=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                    // Paint missing undercoat from the solid center of the same joint.
                    float2 safeUV=lerp(_JointRect.xy,input.uv,smoothstep(.2,.9,paint.a));
                    paint=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,safeUV);
                    return half4(paint.rgb,cover)*_Color;
                }
                float4 masks=LimbMasks(input.uv);
                float mask=0;
                if(_PartIndex < -.5)
                {
                    // Stationary overlapping shoulder caps cover the rigid joints.
                    float4 cuts=masks*step(input.uv.y,float4(.325,.255,.355,.355));
                    mask=1-max(max(cuts.x,cuts.y),max(cuts.z,cuts.w));
                }
                else if(_PartIndex < .5) mask=masks.x;
                else if(_PartIndex < 1.5) mask=masks.y;
                else if(_PartIndex < 2.5) mask=masks.z;
                else mask=masks.w;
                clip(mask-.5);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv) * _Color;
            }
            ENDHLSL
        }
    }
}
