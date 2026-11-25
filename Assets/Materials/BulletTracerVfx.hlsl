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
    float _VertexTheme;

CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float shapeOverride : TEXCOORD1;
    half4 color : COLOR;
    float style : TEXCOORD2;
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
    output.color = input.color;
    output.style = input.uv1.y;

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
        tail = saturate(input.uv.x);
        // Only the actual projectile-tail quad narrows toward its rear.
        // Other streak/impact effects sharing this shader keep their shape.
        float widthFactor = input.shapeOverride < -0.5
            ? lerp(0.18, 1.0, sqrt(tail))
            : 1.0;
        if (_VertexTheme > 0.5)
        {
            float style = input.style;
            if (style > 0.5 && style < 1.5)
                p.y += (abs(frac(tail * 3.5 + _Time.y * 11.0) * 2.0 - 1.0) * 2.0 - 1.0) * 0.35 * (1.0 - tail);
            else if (style > 1.5 && style < 2.5)
                p.y += sin(tail * 18.0 - _Time.y * 16.0) * 0.24 * (1.0 - tail);
            else if (style > 7.5 && style < 8.5)
                p.y = abs(p.y) - 0.17 * (1.0 - tail);
            if (style > 2.5 && style < 5.5)
                widthFactor *= 0.8 + 0.2 * sin(tail * 12.0 + _Time.y * 6.0);
        }
        shape = saturate(abs(p.y) / widthFactor);
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
    if (_VertexTheme > 0.5)
    {
        half3 tint = input.color.rgb;
        half3 hot = lerp(tint, half3(1,1,1), input.style > 8.5 ? 0.14 : 0.55);
        color = (tint * glow + hot * core) * tailFade * _Intensity * input.color.a;
        if (radialMode < 0.5 && input.style > 5.5 && input.style < 6.5)
            color *= 0.55 + 0.45 * smoothstep(0.15, 0.6, frac(tail * 4.0));
        if (radialMode < 0.5 && input.style > 3.5 && input.style < 4.5)
            color *= 0.55 + 0.45 * sin(tail * 13.0 + _Time.y * 7.0) * sin(tail * 13.0 + _Time.y * 7.0);
    }

    return half4(color, 1.0);
}

#endif
