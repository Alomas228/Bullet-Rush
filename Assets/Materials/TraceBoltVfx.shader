// Молнии и рикошеты: все живые болты одного кадра рисуются
// ОДНИМ draw call'ом общим мешем TraceBoltBatcher'а.
//
// От URP/Unlit, которым болты рисовались раньше, отличается
// главным: читает vertex color. Цвет каждого болта (голубая
// молния или жёлтый рикошет) и его альфа приходят из вершин,
// поэтому один материал обслуживает любое число болтов. Раньше
// цвет жил в материале из кэша, а у каждой молнии был свой
// MeshRenderer - N молний = N draw call'ов.
//
// Стоимость на GPU:
//   - 1 активный pass, 1 draw call на все болты кадра;
//   - 0 текстурных сэмплов, 0 освещения, 0 теней;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - ZWrite Off, поэтому порядок болтов не важен.
//
// ВАЖНО: общий код лежит в TraceBoltVfx.hlsl, а не в HLSLINCLUDE
// на уровне SubShader - HLSLINCLUDE в таком виде Unity 6 не
// парсит, и шейдер целиком выпадает из импорта.

Shader "Custom/Bullet Rush VFX Trace Bolt"
{
    Properties
    {
        // Белый: цвет болта приходит из vertex color.
        _Color ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)

        // 3x повторяет старую схему «базовый цвет + emission 2x»,
        // чтобы яркость и свечение под Bloom не изменились.
        _Intensity ("Intensity", Float) = 3.0
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
            #include "TraceBoltVfx.hlsl"
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
            #include "TraceBoltVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
