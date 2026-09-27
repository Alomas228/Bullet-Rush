// ============================================================
// Additive unlit VFX для пулевых трассеров и вспышек попадания.
//
// Почему не Universal Render Pipeline/Unlit:
//   URP/Unlit НЕ читает vertex color, поэтому Color Gradient
//   у LineRenderer просто игнорируется (нет плавного затухания
//   хвоста). Здесь форма считается в фрагментном шейдере прямо
//   из интерполированных UV - это даёт тонкое белое ядро и
//   мягкое оранжевое свечение вокруг него ОДНИМ квадом.
//
// Стоимость на GPU:
//   - 1 активный pass (второй объявлен на случай смены
//     режима рендерера);
//   - 0 текстурных сэмплов, 0 освещения, 0 теней, 0 тумана;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - Additive (One One) + ZWrite Off => не нужен сортировочный
//     список прозрачных, порядок отрисовки не важен.
//
// ВАЖНО: общий код лежит в BulletTracerVfx.hlsl, а не в
// HLSLINCLUDE на уровне SubShader. HLSLINCLUDE в таком виде
// Unity 6 не парсит, и шейдер целиком выпадает из импорта.
// ============================================================

Shader "Custom/Bullet Rush VFX Additive"
{
    Properties
    {
        // Оранжево-жёлтое свечение (снаружи ядра). HDR: должен быть
        // выше порога Bloom, иначе свечения не будет.
        [HDR] _EmissionColor ("Glow Color (HDR)", Color) = (1.0, 0.52, 0.12, 1.0)

        // Бело-жёлтое ядро. Тоже HDR.
        [HDR] _CoreColor ("Core Color (HDR)", Color) = (1.0, 0.95, 0.78, 1.0)

        _Intensity ("Intensity", Range(0.0, 24.0)) = 3.0
        _CoreSharpness ("Core Sharpness", Range(1.0, 12.0)) = 5.0
        _EdgePower ("Edge Falloff", Range(1.0, 6.0)) = 1.6
        _TailPower ("Tail Falloff", Range(0.25, 6.0)) = 1.5

        // 0 = вытянутый импульс (хвост по U, спад по V)
        // 1 = круглая вспышка (радиальный спад)
        _RadialMode ("Radial Mode", Float) = 0.0
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

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            #include "BulletTracerVfx.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnlyUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            #include "BulletTracerVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
