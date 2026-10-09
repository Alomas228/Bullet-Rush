// Общий HLSL для молний и рикошетов.
//
// Лежит отдельным файлом по той же причине, что и
// BloodDropletVfx.hlsl: HLSLINCLUDE на уровне SubShader в таком
// виде Unity 6 не парсит, и шейдер молча выпадает из импорта.
//
// Вся геометрия всех живых молний собирается TraceBoltBatcher'ом
// в ОДИН меш, поэтому цвет каждого болта приходит через vertex
// color, а не через материал: раньше у каждого цвета был свой
// материал из кэша (молния / рикошет), и каждая молния рисовалась
// отдельным MeshRenderer'ом - N молний = N draw call'ов.
//
// Яркость: раньше базовый цвет materials складывался с emission
// (2x цвета), то есть итог было 3x. Здесь та же формула одним
// множителем _Intensity, чтобы внешний вид и порог Bloom не
// изменились.

#ifndef BULLET_RUSH_TRACE_BOLT_INCLUDED
#define BULLET_RUSH_TRACE_BOLT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)

    half4 _Color;
    half _Intensity;

CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    half4 color : COLOR;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    half4 color : COLOR;
};

Varyings Vert(Attributes input)
{
    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.color = input.color;

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    return half4(
        input.color.rgb * _Color.rgb * _Intensity,
        input.color.a * _Color.a
    );
}

#endif
