Shader "BulletRush/AbilityCore"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Input { float4 position : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct Output
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                half4 color : COLOR;
            };
            Output Vert(Input v)
            {
                Output o;
                o.world = TransformObjectToWorld(v.position.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.color = v.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                    o.color.rgb = SRGBToLinear(o.color.rgb);
                #endif
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 view = normalize(GetWorldSpaceViewDir(i.world));
                half rim = 1.0h - saturate(abs(dot(n, view)));
                rim *= rim;
                half light = saturate(abs(dot(n, half3(-0.38h,0.85h,-0.36h))));
                // Restrained HDR highlight: top facets and silhouettes catch
                // light, while darker sides retain their saturated colour.
                half3 rgb = i.color.rgb * (0.91h + light * 0.20h + rim * 0.36h);
                rgb += lerp(i.color.rgb, half3(1,1,1), 0.35h) * (rim * 0.17h);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
