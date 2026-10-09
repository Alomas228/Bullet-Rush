// Общий HLSL опасных зон (элита, босс, событие волны).
//
// Одна плоская лежащая на земле мандрина рисует ВСЁ состояние
// зоны процедурно, без единого дочернего объекта:
//   - фаза предупреждения: дышащая заливка и бегущая дуга;
//   - активная фаза: заливка в опасную полоску и яркий бордюр;
//   - момент срабатывания: расходящееся наружу кольцо-вспышка.
//
// Раньше каждая зона собирала диск, 16 кубиков бордюра и сферу
// вспышки - до 18 объектов и столько же вызовов на зону. Теперь
// зон может быть сколько угодно, а рисует их один инстансный
// вызов (DangerZoneBatcher).
//
// Per-instance данные приходят двумя float4-массивами через
// MaterialPropertyBlock, тем же instanceID, что и матрица:
//   _ZoneColor  = (r, g, b, baseAlpha)
//   _ZoneParams = (warnProgress, spinPhase, active, flash)
//
// Длину массивов MPB менять после первой записи нельзя, поэтому
// DangerZoneBatchBuffer всегда отдаёт массивы ровно из 511
// элементов - как и BlastBatchBuffer.
//
// #pragma target 4.5 здесь нет: StructuredBuffer в вершинном
// шейдере требует SM 4.5 и отключает инстансинг.

#ifndef BULLET_RUSH_DANGER_ZONE_INCLUDED
#define BULLET_RUSH_DANGER_ZONE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _Intensity;
    half _FillPower;
    half _RingInner;
    half _RingOuter;
    half _DashCount;
    half _StripeScale;
    half _SpinSpeed;
    half _PulseSpeed;
    half _ShockWidth;
CBUFFER_END

UNITY_INSTANCING_BUFFER_START(DangerZonePerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float4, _ZoneColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _ZoneParams)
UNITY_INSTANCING_BUFFER_END(DangerZonePerInstance)

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : TEXCOORD1;
    float4 params : TEXCOORD2;
};

Varyings Vert(Attributes input)
{
    // Instance ID нужен и матрице, и обоим per-instance массивам.
    UNITY_SETUP_INSTANCE_ID(input);

    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.uv = input.uv;

    #if defined(UNITY_INSTANCING_ENABLED)
        output.color = UNITY_ACCESS_INSTANCED_PROP(
            DangerZonePerInstance, _ZoneColor);

        output.params = UNITY_ACCESS_INSTANCED_PROP(
            DangerZonePerInstance, _ZoneParams);
    #else
        output.color = float4(1.0, 0.0, 0.0, 1.0);
        output.params = float4(1.0, 0.0, 0.0, 0.0);
    #endif

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    // Центр диска в (0,0), край в 1 по осям.
    float2 p = input.uv * 2.0f - 1.0f;

    float r = length(p);
    float ang = atan2(p.y, p.x);

    float warn = input.params.x;
    float phase = input.params.y;
    float active = input.params.z;
    float flash = input.params.w;

    float time = _Time.y;

    // Спин у каждой зоны свой (phase), поэтому залп зон не
    // крутится в идеальном такте.
    float a = ang + time * _SpinSpeed + phase * 6.2831853f;

    // Мягкая заливка: центр ярче края.
    float fillMask = pow(saturate(1.0f - r), _FillPower);

    float pulse =
        0.5f + 0.5f * sin(time * _PulseSpeed + phase * 6.2831853f);

    // Бордюр: узкое кольцо у самого края.
    float ring =
        smoothstep(_RingInner, _RingOuter, r) *
        (1.0f - smoothstep(_RingOuter, 1.0f, r));

    // Пунктир по бордюру: крутится вместе с a.
    float dash = 0.5f + 0.5f * sin(a * _DashCount);
    dash = smoothstep(0.15f, 0.75f, dash);

    // Диагональная опасная полоска для активной фазы.
    float stripe =
        step(0.5f, frac((p.x + p.y) * _StripeScale + time * 0.35f));

    float fill;
    float rim;

    if (active > 0.5f)
    {
        fill = 0.10f + 0.10f * stripe;
        rim = 0.9f + 0.5f * dash;
    }
    else
    {
        // Предупреждение: дышит и подсвечивается бегущей дугой.
        float sweep =
            pow(saturate(0.5f + 0.5f * cos(a - time * 2.0f)), 8.0f);

        fill = (0.05f + 0.20f * pulse) * (0.6f + 0.4f * warn);

        rim =
            (0.35f + 0.55f * pulse) *
            (0.35f + 0.65f * dash) *
            (0.6f + 0.6f * sweep);
    }

    // Вспышка срабатывания: кольцо бежит от центра к краю.
    float shockRadius = 1.0f - flash;
    float shock = saturate(
        1.0f - abs(r - shockRadius) / max(_ShockWidth, 0.001f));

    shock *= flash * flash;

    // Мягкий срез у самой границы, чтобы диск не обрывался кромкой.
    float fade = 1.0f - smoothstep(0.98f, 1.0f, r);

    float intensity = (fill * fillMask + rim * ring) * fade + shock;

    half3 rgb =
        input.color.rgb * _Color.rgb * intensity * _Intensity;

    rgb += input.color.rgb * shock * 2.0f;

    return half4(rgb, saturate(intensity));
}

#endif
