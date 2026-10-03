// ============================================================
// Пузырные слои взрыва: угли и дым.
//
// Один шейдер на оба слоя - различает их только
// _Additive (светится/дым) и настройки края.
// Огонь-шар - отдельный ExplosionSphereVfx, ему нужен
// настоящий объём. Подробности в BlastVfx.hlsl.
//
// Стоимость на GPU:
//   - 1 активный pass, а угли и дым со всех взрывов сцены
//     уходят в батчер по одному вызову на слой (BlastBatcher);
//   - 0 текстурных сэмплов: форма и край считаются
//     в фрагментном шейдере из UV;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - ZWrite Off => порядок отрисовки слоёв не важен.
//
// Общий код лежит в BlastVfx.hlsl и подключается внутри pass:
// этот же шейдер носит BurnFlameEffect, который рисуется обычным
// MeshRenderer без инстансинга, и путь через атрибут COLOR должен
// остаться рабочим.
// ============================================================

Shader "Custom/Bullet Rush VFX Blast"
{
    Properties
    {
        // Базовый цвет слоя. У аддитивных слоёв он белый:
        // весь цвет и вся HDR-яркость приходят из vertex color,
        // который пишет ExplosionEffect (у каждого взрыва своя
        // стадия остывания, а материал общий на все взрывы).
        _Color ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)

        // Общий множитель яркости.
        _Intensity ("Intensity", Range(0.0, 16.0)) = 1.0

        // Мягкость края: больше - круглее и размытее.
        _EdgePower ("Edge Falloff", Range(0.25, 8.0)) = 1.6

        // Насколько плотное ядро (экспонента) и насколько оно
        // ярче остального пятна.
        _CorePower ("Core Falloff", Range(0.25, 8.0)) = 3.0
        _CoreGain ("Core Gain", Range(0.0, 6.0)) = 0.8

        // 1 = аддитивное смешивание, 0 = альфа (дым)
        _Additive ("Additive", Float) = 1.0

        // Шум края: без него круг выглядит геометрической фигурой.
        _NoiseScale ("Noise Scale", Float) = 3.5
        _NoiseAmount ("Noise Amount", Range(0.0, 1.0)) = 0.28

        // Режим смешивания задаёт материал (VfxSharedAssets):
        // 1/1 - аддитивный шар, SrcAlpha/OneMinusSrcAlpha - дым.
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

            // Режим смешивания берётся из материала: аддитивный
            // шейдер сам ставит One One, альфа-дым - себя.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // БЕЗ "#pragma target 4.5":StructuredBuffer в
            // вершинном шейдере требует SM 4.5, и эта строка
            // отключала инстансинг - шейдер уходил на вариант без
            // UNITY_INSTANCING_ON. Цвет берётся из instanced-свойства
            // и SM 4.5 не нужен.
            //
            // Угли и дым рисуются пачкой через
            // Graphics.RenderMeshInstanced, цвет квада приходит
            // per-instance из структуры инстанса. Тот же шейдер носит
            // BurnFlameEffect - у него инстансинг выключен, и он
            // берёт цвет из атрибута COLOR.
            #pragma multi_compile_instancing
            #pragma editor_sync_compilation
            #include "BlastVfx.hlsl"
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
            // Без "#pragma target 4.5" - см. первый pass.
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma editor_sync_compilation
            #include "BlastVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
