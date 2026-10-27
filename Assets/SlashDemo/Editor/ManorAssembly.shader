Shader "Hidden/SlashDemo/ManorAssembly"
{
    Properties
    {
        _MainTex ("Registered layout", 2D) = "white" {}
        _NorthWest ("North west detail", 2D) = "white" {}
        _NorthEast ("North east detail", 2D) = "white" {}
        _SouthWest ("South west detail", 2D) = "white" {}
        _SouthEast ("South east detail", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _NorthWest, _NorthEast, _SouthWest, _SouthEast;
            fixed4 Frag(v2f_img input) : SV_Target
            {
                float2 local = frac(input.uv * 2);
                fixed4 detail;
                if (input.uv.y >= 0.5)
                    detail = input.uv.x < 0.5 ? tex2D(_NorthWest, local) : tex2D(_NorthEast, local);
                else
                    detail = input.uv.x < 0.5 ? tex2D(_SouthWest, local) : tex2D(_SouthEast, local);
                // Keep every internal seam registered to the layout and feather its detail transition.
                float2 distanceToEdge = min(local, 1 - local);
                float blend = smoothstep(0, 0.025, min(distanceToEdge.x, distanceToEdge.y));
                return lerp(tex2D(_MainTex, input.uv), detail, blend);
            }
            ENDCG
        }
    }
}
