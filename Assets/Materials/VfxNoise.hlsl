// Value-noise для VFX-шейдеров. Общий файл, потому что его
// используют и шар взрыва, и угли с дымом.
//
// Смысл шума один: край у огня и у дыма не идеально круглый.
// Без него шар выглядит геометрической фигурой (диском или
// сферой из CAD), а с ним - огнём.

#ifndef BULLET_RUSH_NOISE_INCLUDED
#define BULLET_RUSH_NOISE_INCLUDED

float VfxHash21(float2 p)
{
    p = frac(p * float2(123.34f, 456.21f));
    p += dot(p, p + 45.32f);
    return frac(p.x * p.y);
}

float VfxValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float2 curve = local * local * (3.0f - 2.0f * local);

    float a = VfxHash21(cell);
    float b = VfxHash21(cell + float2(1.0f, 0.0f));
    float c = VfxHash21(cell + float2(0.0f, 1.0f));
    float d = VfxHash21(cell + float2(1.0f, 1.0f));

    return lerp(
        lerp(a, b, curve.x),
        lerp(c, d, curve.x),
        curve.y
    );
}

// Две октавы и медленный дрейф по времени. Возвращает 0..1.
float VfxFlameNoise(float2 uv, float scale, float time)
{
    float noise = VfxValueNoise(
        uv * scale + float2(time * 0.9f, -time * 0.6f)
    );

    noise = noise * 0.65f + VfxValueNoise(
        uv * scale * 2.7f + float2(-time * 1.3f, time * 0.8f)
    ) * 0.35f;

    return noise;
}

#endif
