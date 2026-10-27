// Процедурный материал волны с длинным скатом и загнутым гребнем.
// UV.y идет вдоль ее асимметричного поперечного профиля.

#ifndef BULLET_RUSH_SURF_WAVE_INCLUDED
#define BULLET_RUSH_SURF_WAVE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _WaveSize;
    float _Intensity;
    float _FoamScale;
    float _FoamSpeed;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float3 normalOS : NORMAL;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float height01 : TEXCOORD3;
};

Varyings Vert(Attributes input)
{
    Varyings output;

    float2 uv = input.uv;
    float time = _Time.y;
    float crest = smoothstep(0.3, 0.39, uv.y) *
                  (1.0 - smoothstep(0.54, 0.67, uv.y));
    float body = smoothstep(0.015, 0.12, uv.y) *
                 (1.0 - smoothstep(0.84, 0.99, uv.y));
    float longWave =
        sin(uv.x * 18.0 + time * 1.7 + sin(uv.x * 6.0) * 0.8);
    float shortWave =
        sin(uv.x * 47.0 - time * 2.6 + uv.y * 5.0);
    float heightVariation = 1.0 + longWave * 0.12 + shortWave * 0.035;

    float3 positionOS = input.positionOS.xyz;
    positionOS.y += body * _WaveSize.z * (heightVariation - 1.0);
    positionOS.z += body *
        (longWave * 0.07 + shortWave * 0.025) * _WaveSize.y;
    positionOS.y += sin(uv.x * 58.0 + time * 2.1) *
        body * 0.02 * _WaveSize.z;

    VertexPositionInputs positionInputs =
        GetVertexPositionInputs(positionOS);
    VertexNormalInputs normalInputs =
        GetVertexNormalInputs(input.normalOS);

    output.positionHCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
    output.uv = uv;
    output.height01 = saturate(
        positionOS.y / max(_WaveSize.z * 1.1, 0.001));

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    float time = _Time.y * _FoamSpeed;
    float2 waterUv = float2(input.uv.x * _WaveSize.x, input.uv.y);
    float profile = input.uv.y;

    float swellA = sin(waterUv.x * 0.32 + time + sin(waterUv.x * 0.09));
    float swellB = sin(waterUv.x * 0.71 - time * 1.35 + input.uv.y * 5.0);
    float swell = saturate(0.5 + swellA * 0.23 + swellB * 0.13);

    float viewFacing = saturate(dot(
        normalize(input.normalWS),
        normalize(_WorldSpaceCameraPos - input.positionWS)));

    float height = input.height01;
    float3 deepWater = float3(0.015, 0.24, 0.39);
    float3 bodyWater = float3(0.025, 0.52, 0.68);
    float3 sunlitWater = float3(0.15, 0.82, 0.88);
    float3 water = lerp(deepWater, bodyWater, smoothstep(0.0, 0.72, height));
    water = lerp(water, sunlitWater, pow(viewFacing, 2.0) * 0.72);

    float glint = pow(saturate(sin(
        waterUv.x * 0.9 - time * 1.6 + sin(input.uv.y * 18.0))), 10.0);

    float crestFoam = smoothstep(0.29, 0.37, profile) *
                      (1.0 - smoothstep(0.51, 0.61, profile));
    float fallingFoam = smoothstep(0.66, 0.73, profile) *
                        (1.0 - smoothstep(0.84, 0.91, profile));
    float foamLine = saturate(crestFoam + fallingFoam * 0.85);
    float brokenFoam =
        0.5 + 0.5 * sin(waterUv.x * _FoamScale * 2.4 + time * 1.7 +
                         sin(waterUv.x * 0.57) * 2.0);
    float foamNoise =
        0.5 + 0.5 * sin(waterUv.x * 5.1 - time * 2.1 +
                         sin(waterUv.x * 0.84) * 2.4);
    float foam = smoothstep(0.46, 0.78, brokenFoam * 0.58 + foamNoise * 0.42);
    foam *= foamLine;
    float foamAlpha = foam * foamLine;

    float3 color = water * _Color.rgb;
    color += float3(0.14, 0.65, 0.78) * glint * (0.25 + swell * 0.4);
    color = lerp(color, float3(0.78, 0.98, 1.0), foam * 0.94);

    float alpha = lerp(0.72, 0.91, smoothstep(0.0, 1.0, height));
    alpha = lerp(alpha, 0.98, foamAlpha);

    return half4(color * _Intensity, alpha * _Color.a);
}

#endif
