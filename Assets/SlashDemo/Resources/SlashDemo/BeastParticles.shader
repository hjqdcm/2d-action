Shader "SlashDemo/BeastParticles"
{
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
            struct A { float4 p:POSITION; float3 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float3 uv:TEXCOORD0; half4 color:COLOR; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv.xy*2-1; float radius=length(p),kind=i.uv.z;
                float alpha;
                if(kind<.5 || (kind>2.5 && kind<3.5))
                {
                    float angle=atan2(p.y,p.x);
                    float edge=.78+.08*sin(angle*5)+.04*sin(angle*9);
                    alpha=(1-smoothstep(.12,edge,radius))*.8;
                }
                else if(kind<1.5) alpha=1-smoothstep(.61,.72,abs(p.x)*.9+abs(p.y));
                else if(kind<2.5) alpha=(1-smoothstep(.42,.92,radius))*(.75+.25*exp(-dot(p-float2(-.2,.2),p-float2(-.2,.2))*24));
                else if(kind<4.5) alpha=exp(-p.x*p.x*5-p.y*p.y*15)*(1-smoothstep(.65,1,radius));
                else if(kind<5.5) alpha=exp(-pow((radius-.62)*16,2))*(1-smoothstep(.8,1,radius));
                else if(kind<6.5)
                {
                    // Warm paper with a cinnabar spine and short cross strokes.
                    alpha=1-smoothstep(.68,.85,max(abs(p.x),abs(p.y)));
                    float ink=(1-smoothstep(.07,.13,abs(p.x))) * (1-smoothstep(.52,.65,abs(p.y)));
                    ink=max(ink,(1-smoothstep(.05,.12,abs(abs(p.y)-.24))) * (1-smoothstep(.37,.49,abs(p.x))));
                    return half4(lerp(i.color.rgb,half3(.72,.09,.025),ink),i.color.a*alpha);
                }
                else
                {
                    float2 a=abs(p);
                    float rays=max(exp(-a.x*15)*max(0,1-a.y),exp(-a.y*15)*max(0,1-a.x));
                    alpha=max(rays,exp(-radius*7))*(1-smoothstep(.67,1,radius));
                }
                return half4(i.color.rgb,i.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
