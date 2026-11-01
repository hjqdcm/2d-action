Shader "SlashDemo/SerpentGaze"
{
    Properties { _Progress ("Progress", Float)=0 _Mode ("0 windup, 1 blast",Float)=0 }
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
                float _Progress;
                float _Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes i)
            { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float t=saturate(_Progress);
                float3 jade=float3(.16,.78,.43), lime=float3(.63,1,.56), gold=float3(1,.91,.55);
                float r=i.uv.x, side=i.uv.y;
                float edges=pow(saturate(sin(side*3.14159265)),.38);
                float noise=Noise(float2(r*14-t*7,side*18));
                float outer=exp(-pow((r-.985)*70,2));
                float sideRim=exp(-pow(min(side,1-side)*95,2))*smoothstep(.12,.32,r);
                float radialFade=smoothstep(.025,.14,r)*(1-smoothstep(.985,1,r));
                if(_Mode<.5)
                {
                    float gather=exp(-pow((r-(1-t)*.33)*13,2));
                    float rays=pow(saturate(sin(side*67+r*9+t*8)),12);
                    float flow=pow(saturate(sin(r*24+side*13+t*14)),9);
                    float alpha=edges*(gather*.25+rays*.09+flow*.10+.035)*t*radialFade;
                    alpha+=(outer*edges*.15+sideRim*.12)*t*t;
                    return half4(lerp(jade,lime,saturate(gather+flow*.5)),saturate(alpha));
                }
                float front=lerp(.03,1,saturate(t/.36));
                float wave=exp(-pow((r-front)*19,2));
                float core=exp(-pow((r-front)*60,2));
                float veil=(1-smoothstep(front-.24,front+.01,r))*(1-smoothstep(.62,1,t));
                float strands=pow(saturate(sin(side*83+noise*5-r*17+t*13)),13);
                float curl=pow(saturate(sin(r*31+side*23+noise*7-t*8)),7);
                float rim=outer*smoothstep(.08,.28,t);
                float fade=1-smoothstep(.38,1,t);
                float ignition=exp(-t*22);
                // Two trailing arcs and travelling veins keep the filled cone readable after the flash.
                float echo=exp(-pow((r-front+.16)*32,2))*.55+exp(-pow((r-front+.31)*36,2))*.30;
                float veins=pow(saturate(sin(side*53+sin(r*13-t*12)*1.6-r*8+t*10)),18);
                float alpha=edges*(ignition*(.48+noise*.16)+fade*(wave*.62+core*.42+echo*.32
                    +veil*(.21+noise*.16+strands*.18+veins*.23)+curl*wave*.23+rim*.50));
                alpha+=sideRim*fade*(.22+wave*.32);
                float3 color=lerp(jade,lime,saturate(wave*.8+strands*.5+veins*.4));
                color=lerp(color,gold,saturate(core*.8+rim*.8+echo*.35+sideRim*.55+ignition*.4));
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
