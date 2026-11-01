Shader "SlashDemo/SnakeSkin"
{
    Properties { _MainTex ("Snake artwork", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) _PartIndex ("Body section", Float) = 0 }
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
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv;
                // The recurved tip crosses the diagonal root cut. It belongs only to
                // the animated tail, including where the neck mask used to retain it.
                // Include the LOWER side of the recurved segment as well as its tip.
                // The previous y=.46 cut left that lower arc attached to the coil.
                float tailTip=step(.735,p.x)*step(.405,p.y)*step(p.y,.565);
                float neck=max(step(.57,p.y),step(.78-.8*p.y,p.x)*step(p.x,.76)*step(.445,p.y));
                float tail=max(step(1.275,p.x+p.y),tailTip)*step(p.y,.565);
                float mask;
                if(_PartIndex<.5)
                {
                    float neckCut=max(step(.60,p.y),step(.78-.8*p.y,p.x)*step(p.x,.76)*step(.48,p.y));
                    mask=(1-neckCut)*(1-tail);
                }
                else if(_PartIndex<1.5)
                    mask=max(step(.57,p.y),step(.78-.8*p.y,p.x)*step(p.x,.76)*step(.415,p.y))*(1-tail);
                // One exact partition for the WHOLE tail, including its root.
                // The mesh keeps the root attached; duplicated paint is unnecessary.
                else mask=tail;
                clip(mask-.5);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,p)*_Color;
            }
            ENDHLSL
        }
    }
}
