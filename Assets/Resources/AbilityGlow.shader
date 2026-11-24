Shader "BulletRush/AbilityGlow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Input { float4 position : POSITION; half2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; half2 uv : TEXCOORD0; half4 color : COLOR; };
            Output Vert(Input v)
            {
                Output o;
                o.position = TransformObjectToHClip(v.position.xyz);
                o.uv = v.uv;
                o.color = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                    o.color.rgb = SRGBToLinear(o.color.rgb);
                #endif
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half d = length((i.uv - 0.5h) * 2.0h);
                half soft = saturate(1.0h - d);
                soft = soft * soft * (3.0h - 2.0h * soft);
                return half4(i.color.rgb * (soft * soft * i.color.a), 0);
            }
            ENDHLSL
        }
    }
}
