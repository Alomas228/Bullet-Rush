Shader "Custom/Bullet Rush Death Pieces"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DeathPieces"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                Light sun = GetMainLight();
                half diffuse = saturate(dot(TransformObjectToWorldNormal(input.normalOS), sun.direction));
                output.color = input.color.rgb * (0.53h + 0.47h * diffuse * sun.color);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return half4(input.color, 1); }
            ENDHLSL
        }
    }
}
