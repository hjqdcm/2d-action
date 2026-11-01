Shader "SlashDemo/SerpentVenom"
{
    Properties { _Age ("Age",Float)=0 _Mode ("0 pool, 1 glob",Float)=0 _Fade ("Fade",Float)=1 }
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
                float _Age, _Mode, _Fade;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv*2-1; float r=length(p),angle=atan2(p.y,p.x);
                if(_Mode>.5)
                {
                    float edge=1-smoothstep(.55,.96,r);
                    float shine=exp(-dot(p-float2(-.23,.25),p-float2(-.23,.25))*26);
                    return half4(lerp(float3(.12,.45,.07),float3(.8,1,.25),shine+.4*(1-r)),edge*.95);
                }
                float edge=1-smoothstep(.84,.99,r);
                float ripple=.5+.5*sin(r*28-_Age*5+sin(angle*7)*.8);
                float swirl=.5+.5*sin(p.x*13+p.y*10+_Age*2+sin(p.y*8-_Age*3)*2);
                float bubbles=pow(saturate(sin(p.x*28+sin(p.y*9))*sin(p.y*31+_Age*2)),12);
                float rim=exp(-pow((r-.88)*24,2));
                float3 color=lerp(float3(.035,.14,.025),float3(.25,.49,.045),swirl*.65+ripple*.18);
                color=lerp(color,float3(.65,.83,.16),rim*.6+bubbles*.8);
                return half4(color,edge*(.71+rim*.15+bubbles*.1)*_Fade);
            }
            ENDHLSL
        }
    }
}
