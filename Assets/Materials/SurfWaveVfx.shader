// ============================================================
// Гребень волны прибоя (Пляж).
//
// Объемная процедурная волна на собственном меше. Вершины образуют
// вытянутый вал с загнутым вперед гребнем, а шейдер добавляет
// движение воды, бирюзовую глубину и пену.
//
// Пена процедурная и живёт в SurfWaveVfx.hlsl, который делят оба
// pass-а; внешняя текстура не требуется.
//
// Полупрозрачная вода сохраняет объем и не превращается в светящийся
// плоский маркер на песке.
// ============================================================

Shader "Custom/Bullet Rush Surf Wave"
{
    Properties
    {
        _Color ("Water Tint", Color) = (0.65, 0.88, 1.0, 0.9)
        _WaveSize ("Wave Size (Length, Thickness, Height)", Vector) = (69, 3, 2, 0)
        _Intensity ("Water Intensity", Range(0.0, 3.0)) = 1.0
        _FoamScale ("Foam Detail", Range(0.1, 4.0)) = 0.6
        _FoamSpeed ("Water Speed", Range(0.0, 6.0)) = 1.6
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
            Name "SurfWave"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation

            #include "SurfWaveVfx.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SurfWaveOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma editor_sync_compilation

            #include "SurfWaveVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
