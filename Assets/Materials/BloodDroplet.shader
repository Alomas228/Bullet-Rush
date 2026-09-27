// ============================================================
// Кровь при попадании пули в моба: капли разлетаются от точки
// попадания и гаснут на лету.
//
// Чем отличается от остальных VFX этого проекта:
//   - НЕ аддитивная. Кровь не светится: на чёрном фоне аддитив
//     даёт розово-белые искры, и моб выглядит как новогодняя
//     гирлянда. Здесь обычное альфа-смешивание
//     (SrcAlpha OneMinusSrcAlpha), цвет тёмно-красный.
//   - Читает vertex color. Все капли одного эффекта собраны
//     в ОДИН меш (8-12 кватов), и у каждой своя прозрачность
//     и свой оттенок - иначе затухание пришлось бы делать
//     через MaterialPropertyBlock (он ломает SRP Batcher) или
//     через отдельный рендерер на каждую каплю (12 draw call
//     вместо одного).
//
// Стоимость на GPU:
//   - 1 активный pass, 1 draw call на попадание;
//   - 0 текстурных сэмплов, 0 освещения, 0 теней, 0 тумана;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - ZWrite Off, поэтому порядок отрисовки капель не важен.
//
// ВАЖНО: общий код лежит в BloodDropletVfx.hlsl, а не в
// HLSLINCLUDE на уровне SubShader - HLSLINCLUDE в таком виде
// Unity 6 не парсит, и шейдер целиком выпадает из импорта.
// ============================================================

Shader "Custom/Bullet Rush VFX Blood"
{
    Properties
    {
        // Тёмно-красный. Не HDR: свечения быть не должно,
        // цвет должен оставаться кровавым после тонмаппинга.
        _Color ("Blood Color", Color) = (0.42, 0.02, 0.02, 1.0)

        // Мягкость края капли. Больше - круглее и размытее.
        // Капли крупные (0.16 единицы ~ 7 пикселей), поэтому
        // край держим жёстче, чем у маленьких брызг: иначе
        // пятно читается как туман, а не как кровь.
        _EdgePower ("Edge Falloff", Range(0.5, 6.0)) = 1.9

        // Плотность середины капли.
        _CorePower ("Core Falloff", Range(0.5, 6.0)) = 2.0
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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            #include "BloodDropletVfx.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnlyUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation
            #include "BloodDropletVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
