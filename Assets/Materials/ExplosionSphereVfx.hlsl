// Огонь-шар (сфера) для взрыва.
//
// Не билборд: реальная сфера, поэтому с любого ракурса
// видно объём, а не плоский диск. Именно это и нужно для
// «области поражения».
//
// Как читается объём:
//   ndv = abs(N·V). В центре диска 1, у силуэта 0.
//   - тело   = ndv^_BodyPower    → светлое ядро, мягкий край
//   - кромка  = (1-ndv)^_RimPower → горячее кольцо у
//     силуэта: мгновенно узнаваемый признак шара
//   - шум двигает ndv, поэтому кромка рваная и «горит».
//
// Почему Cull Off: задние грани тоже рисуются и складываются
// аддитивно. В центре диска получается два слоя (перед +
// зад), у края - один. Этот перепад и создаёт объём.
//
// ВАЖНО: материал белый (_Color = white), и весь цвет, вся
// стадия остывания и вся HDR-яркость приходят из vertex
// color. Поэтому ни одна ветка не должна брать от
// vertex color только альфу - иначе сфера будет гореть
// постоянной яркостью, пока её геометрия сжимается.

#ifndef BULLET_RUSH_EXPLOSION_SPHERE_INCLUDED
#define BULLET_RUSH_EXPLOSION_SPHERE_INCLUDED

float4 ExplosionSphereFragment(
    float3 positionOS,
    float3 positionWS,
    half4 vertexColor,
    float4 color,
    float bodyPower,
    float bodyGain,
    float rimPower,
    float rimGain,
    float corePower,
    float coreGain,
    float coreSharp,
    float noiseScale,
    float noiseAmount,
    float intensity,
    float time)
{
    float3 viewDir =
        normalize(_WorldSpaceCameraPos - positionWS);

    // Нормаль берём как радиус-вектор от центра шара, а не из
    // атрибута: вершины всё равно смещены шумом (пузырь), и
    // радиус-вектор остаётся правильной нормалью деформированной
    // сферы. Именно по нему находится силуэт.
    float3 centerWS = TransformObjectToWorld(float3(0.0f, 0.0f, 0.0f));
    float3 normalWS = normalize(positionWS - centerWS);

    // abs: изнанка даёт то же, что и лицевая сторона, иначе
    // мягкий край обрывался бы в половину яркости.
    float ndv = abs(dot(normalWS, viewDir));

    float noise = VfxFlameNoise(positionOS.xz, noiseScale, time);

    ndv = saturate(ndv + (noise - 0.5f) * noiseAmount);

    // Основная масса огня, гаснет к силуэту.
    float body = pow(ndv, bodyPower) * bodyGain;

    // Горячая кромка - даёт объём.
    float rim = pow(1.0f - ndv, rimPower) * rimGain;

    // Белое ядро - только первые мгновения вспышки.
    float core = pow(ndv, corePower) * coreGain;

    // ВАЖНО: остывание приходит в vertex color и лежит в RGB
    // (это цвет, умноженный на HDR-яркость). Если брать от
    // vertex color только альфу, шар останется белым и
    // ярким, пока гаснет его геометрия: сфера просто
    // уменьшается на месте и не пропадает.
    float3 tint = color.rgb * vertexColor.rgb;

    float mask = (body + rim) * vertexColor.a;

    float3 rgb = tint * mask;

    // Ядро тонируется тем же tint, а не чистым белым: иначе
    // центр выбивался в белый круг.
    rgb += tint * core * coreSharp;

    rgb *= intensity;

    return half4(rgb, mask);
}

#endif
