// ============================================================
// Светящийся шар снаряда: тело пули игрока (жёлтое) и моба
// (красное). Форма считается из нормали меша, а не из UV, -
// в отличие от трассеров, которые рисуются квадом.
//
// ВАЖНО: общий код лежит в BulletGlowBallVfx.hlsl, а не в
// HLSLINCLUDE на уровне SubShader - HLSLINCLUDE в таком виде
// Unity 6 не парсит, и шейдер целиком выпадает из импорта.
// ============================================================

Shader "Custom/Bullet Rush Glow Ball"
{
    Properties
    {
        // Цвет шара. HDR: должен быть выше порога Bloom (0.9),
        // иначе свечения не будет - будет просто цветной шарик.
        [HDR] _BaseColor ("Ball Color (HDR)", Color) = (1.0, 0.9, 0.35, 1.0)

        // Цвет кромки. Чем он белее, тем «горячее» выглядит шар.
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (1.0, 0.97, 0.85, 1.0)

        _RimPower ("Rim Sharpness", Range(0.5, 8.0)) = 2.0
        _RimStrength ("Rim Strength", Range(0.0, 8.0)) = 1.2
        _Intensity ("Intensity", Range(0.0, 24.0)) = 2.6
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
            "PreviewType" = "Sphere"
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
            #include "BulletGlowBallVfx.hlsl"
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
            #include "BulletGlowBallVfx.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
