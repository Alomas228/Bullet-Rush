// ============================================================
// Гребень волны прибоя (Пляж).
//
// Отдельный простой шейдер без инстансинга: волна рисуется
// обычным MeshRenderer на растянутом диске. Цвет приходит через
// MaterialPropertyBlock (_Color), поэтому один материал можно
// переиспользовать на все волны.
//
// Пена процедурная и живёт в SurfWaveVfx.hlsl, который делят оба
// pass-а. Текстуру (_WaveTex) можно подложить свою; по умолчанию
// она белая и на вид не влияет.
//
// Лента лежит чуть выше пола, чтобы её не перекрывал ground-меш,
// и рисуется альфа-блендом (не аддитивом): на светлом песке
// аддитивная подсветка сливалась с фоном.
// ============================================================

Shader "Custom/Bullet Rush Surf Wave"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.9, 1.0, 0.95)
        _WaveSize ("Wave Size (W, D)", Vector) = (69, 6, 0, 0)
        _WaveTex ("Wave Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0.0, 8.0)) = 1.6
        _EdgePower ("Edge Falloff", Range(0.5, 8.0)) = 1.3
        _RingInner ("Ring Inner", Range(0.0, 1.0)) = 0.58
        _RingOuter ("Ring Outer", Range(0.0, 1.0)) = 0.99
        _FoamScale ("Foam Scale", Range(0.1, 4.0)) = 0.6
        _FoamSpeed ("Foam Speed", Range(0.0, 6.0)) = 1.6
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
            Cull Off
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
            Cull Off
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
