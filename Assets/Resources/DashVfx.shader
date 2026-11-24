Shader "BulletRush/DashVfx"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Input { float4 position : POSITION; half4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; half4 color : COLOR; };
            Output Vert(Input v)
            {
                Output o;
                o.position = TransformObjectToHClip(v.position.xyz);
                o.color = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                    o.color.rgb = SRGBToLinear(o.color.rgb);
                #endif
                return o;
            }
            half4 Frag(Output i) : SV_Target { return i.color; }
            ENDHLSL
        }
    }
}
