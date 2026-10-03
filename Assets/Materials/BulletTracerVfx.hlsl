// Общий HLSL для трассеров и вспышек попадания.
//
// Лежит отдельным файлом, а не HLSLINCLUDE на уровне SubShader:
// такой блок Unity 6 не переваривает ("Parse error: syntax error,
// unexpected '{'"), и весь шейдер молча не импортируется -
// Shader.Find возвращает null, а материал не создаётся.
//
// Include подключается дважды, по одному разу в каждый Pass
// (Forward и ForwardOnly). Активен всегда только один Pass,
// поэтому на GPU это не удваивает работу.

#ifndef BULLET_RUSH_VFX_INCLUDED
#define BULLET_RUSH_VFX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)

    half4 _EmissionColor;
    half4 _CoreColor;
    half _Intensity;
    half _CoreSharpness;
    half _EdgePower;
    half _TailPower;
    float _RadialMode;

CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float shapeOverride : TEXCOORD1;
};

Varyings Vert(Attributes input)
{
    // Трассеры рисуются пачкой через RenderMeshInstanced, и матрица
    // каждого приходит из instanceID. Без этой строки все пули
    // получили бы transform нулевого инстанса и легли в одну точку.
    // Без инстансинга (вспышки попадания на MeshRenderer) макрос
    // раскрывается в ничто, поэтому путь остаётся прежним.
    UNITY_SETUP_INSTANCE_ID(input);

    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.uv = input.uv;

    // Переключатель формы на квад: 0 - как задано материалом,
    // 1 - круглое свечение, -1 - вытянутый хвост.
    //
    // Раньше по нему в ОДНОМ меше держались и хвост, и круглое
    // ядро пули - так требовал старый общий меш на трассер.
    // С переходом на GPU-инстансинг хвост и ядро стали двумя
    // разными квадами с разными мешами (см. TracerBatcher),
    // поэтому теперь UV1 просто помечает, какой это квад:
    // хвост -1, ядро +1. Значения прежние, фрагментный шейдер
    // их не различает.
    //
    // Взят UV1, а не COLOR: у меша без этого канала Unity
    // подставляет 0, то есть «как в материале», и все старые
    // меши (вспышка попадания, осколки, брызги крови) продолжают
    // вести себя ровно как раньше. У COLOR дефолт неоднозначен.
    output.shapeOverride = input.uv1.x;

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    // Центр квада в (0,0), края в ±1.
    float2 p = input.uv * 2.0 - 1.0;

    float radialMode = _RadialMode;

    if (input.shapeOverride > 0.5)
        radialMode = 1.0;
    else if (input.shapeOverride < -0.5)
        radialMode = 0.0;

    float shape;
    float tail;

    if (radialMode > 0.5)
    {
        // Вспышка попадания: настоящий круг, спад по расстоянию.
        shape = saturate(length(p));
        tail = 1.0;
    }
    else
    {
        // Трассер: спад по ширине, хвост по длине.
        shape = saturate(abs(p.y));
        tail = saturate(input.uv.x);
    }

    // Один pow на пиксель, остальное - умножения.
    float inv = saturate(1.0 - shape);
    float core = pow(inv, _CoreSharpness);
    float glow = core * inv;
    float tailFade = pow(tail, _TailPower);

    half3 color =
        _EmissionColor.rgb * glow * tailFade +
        _CoreColor.rgb * core * tailFade;

    color *= (half)_Intensity;

    return half4(color, 1.0);
}

#endif
