// ============================================================
// Опасные зоны: элита, аое босса, событие волны.
//
// Один шейдер на все зоны. Зона - это плоский диск, лежащий на
// земле; фаза предупреждения, активная фаза и вспышка срабатывания
// целиком считаются во фрагментном шейдере (DangerZoneVfx.hlsl).
//
// Стоимость на GPU:
//   - 1 активный pass;
//   - ВСЕ зоны сцены уходят в один инстансный вызов
//     Graphics.RenderMeshInstanced (DangerZoneBatcher);
//   - 0 текстурных сэмплов: форма и узоры считаются из UV;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - ZWrite Off => порядок зон друг с другом не важен.
//
// Цвет зоны приходит per-instance массивом _ZoneColor, поэтому
// элита, босс и волновое событие делят один материал и один
// вызов отрисовки.
// ============================================================

Shader "Custom/Bullet Rush VFX Danger Zone"
{
    Properties
    {
        // Базовый тон белый: настоящий цвет приходит per-instance.
        _Color ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)

        _Intensity ("Intensity", Range(0.0, 8.0)) = 1.0

        // Мягкость заливки: больше - темнее к краю.
        _FillPower ("Fill Falloff", Range(0.5, 6.0)) = 1.6

        // Границы кольца-бордюра по радиусу (0..1).
        _RingInner ("Ring Inner", Range(0.0, 1.0)) = 0.78
        _RingOuter ("Ring Outer", Range(0.0, 1.0)) = 0.9

        // Сколько сегментов в пунктирном бордюре.
        _DashCount ("Dash Count", Range(2.0, 64.0)) = 24.0

        // Масштаб диагональной опасной полоски активной фазы.
        _StripeScale ("Stripe Scale", Range(1.0, 32.0)) = 7.0

        // Скорость вращения бордюра и дуги.
        _SpinSpeed ("Spin Speed", Range(-4.0, 4.0)) = 1.6

        // Частота дыхания предупреждения.
        _PulseSpeed ("Pulse Speed", Range(0.0, 16.0)) = 6.0

        // Толщина кольца вспышки срабатывания.
        _ShockWidth ("Shock Width", Range(0.01, 0.5)) = 0.12

        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            // Аддитив: зоны читаются как энергия и не темнят пол.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma editor_sync_compilation
            #include "DangerZoneVfx.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnlyUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma editor_sync_compilation
            #include "DangerZoneVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
