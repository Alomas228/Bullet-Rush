using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Разделяемые ресурсы VFX: материалы и сетки на весь забег.
///
/// Всё создаётся лениво, ровно один раз, и дальше только переиспользуется.
/// Ни один эффект не создаёт себе Material/mesh в рантайме.
///
/// Почему материалов несколько, а не один:
///   трассер игрока — вытянутый импульс (спад по длине), оранжевый,
///   трассер врага   — тот же импульс, но красный (тот же шейдер,
///                    разный цвет: глаз различает «своих» и «чужих»
///                    пуль на лету),
///   вспышка         — круглое пятно (радиальный спад),
///   шар пули        — настоящая сфера с френелем, отдельный
///                    шейдер: UV-формы трассеров на сфере дают
///                    ромб, а не шар,
///   кровь           — единственный неаддитивный: альфа-смешивание,
///                    тёмно-красный, читает vertex color,
///   молния          — вспышка и рикошет: голубой или жёлтый цвет
///                    приходит из vertex color, поэтому один
///                    материал обслуживает все болты кадра,
///   взрыв           — шар-огонь отдельным шейдером (ему нужен
///                    объём через N·V) и плоские слои углей и дыма
///                    общим шейдером; различает их _Additive,
///   пламя на мобе   — тот же плоский шейдер, форма языка задана
///                    градиентом альфы по вершинам квада.
/// Одиннадцать экземпляров на игру = ноль влияния на GPU, зато
/// эффекты настраиваются независимо. Новых материалов в рантайме
/// не появляется никогда.
/// </summary>
public static class VfxSharedAssets
{
    private const string ShaderName =
        "Custom/Bullet Rush VFX Additive";

    private const string GlowBallShaderName =
        "Custom/Bullet Rush Glow Ball";

    private const string BloodShaderName =
        "Custom/Bullet Rush VFX Blood";

    private const string TraceBoltShaderName =
        "Custom/Bullet Rush VFX Trace Bolt";

    private const string BlastShaderName =
        "Custom/Bullet Rush VFX Blast";

    private const string ExplosionSphereShaderName =
        "Custom/Bullet Rush VFX Explosion Sphere";

    private const string DangerZoneShaderName =
        "Custom/Bullet Rush VFX Danger Zone";

    private const string FallbackShaderName =
        "Universal Render Pipeline/Unlit";

    // =========================================================
    // MATERIALS
    // =========================================================

    // Цвета трассеров. У игрока ядро белое-подкрашенное, поэтому
    // пуля выглядит раскалённой; у врага ядро само по себе
    // насыщенно-красное - иначе аддитив со свечением и тонмаппинг
    // съедают красный и трассер читается розовым.
    // Значения HDR - выше порога Bloom (0.9), иначе свечения не будет.
    private static readonly Color TracerGlowColor =
        new Color(1f, 0.62f, 0.22f, 1f);

    private static readonly Color TracerCoreColor =
        new Color(1f, 0.95f, 0.8f, 1f);

    private static readonly Color EnemyTracerGlowColor =
        new Color(1f, 0.02f, 0.01f, 1f);

    private static readonly Color EnemyTracerCoreColor =
        new Color(1f, 0.06f, 0.03f, 1f);

    private const float DefaultIntensity = 3f;

    // У врага яркость ниже: при 3.0 красный канал упирается в
    // клиппинг, ACES/Neutral тональная компрессия уводит его в
    // белое, и хвост снова становится розовым. 2.2 - это всё ещё
    // ~2.4x порога Bloom (0.9), то есть ореол остаётся.
    private const float EnemyIntensity = 2.2f;

    // Цвета шаров (тело снаряда). У пули игрока жёлтое ядро с
    // почти белой кромкой - «горящая спичка», у снаряда врага
    // насыщенно-красное с тёплой кромкой, тот же приём, что и у
    // трассеров: иначе шар выглядит розовым шариком.
    private static readonly Color BulletBallColor =
        new Color(1f, 0.88f, 0.32f, 1f);

    private static readonly Color BulletRimColor =
        new Color(1f, 0.97f, 0.85f, 1f);

    private static readonly Color EnemyBulletBallColor =
        new Color(1f, 0.12f, 0.05f, 1f);

    private static readonly Color EnemyBulletRimColor =
        new Color(1f, 0.32f, 0.16f, 1f);

    // Цвет крови. Тёмно-красный, НЕ HDR: кровь не светится,
    // и после тонмаппинга она должна остаться кровавой, а не
    // розовой. Светлое пятно внутри капли и прозрачность
    // приходят из vertex color, который ставит BloodEffect.
    private static readonly Color BloodColor =
        new Color(0.42f, 0.02f, 0.02f, 1f);

    // Яркость шара держится чуть выше порога Bloom (0.9 в
    // SampleSceneProfile). Раньше здесь стояло 2.6 - шар светился
    // втрое выше порога, Bloom получал всю избыточную энергию и
    // пуля выглядела лампочкой. Теперь ореол есть, но он
    // в несколько раз меньше.
    private const float BulletIntensity = 1.2f;
    private const float EnemyBulletIntensity = 1.1f;

    private static Material tracerMaterial;
    private static Material enemyTracerMaterial;
    private static Material impactMaterial;
    private static Material bulletMaterial;
    private static Material enemyBulletMaterial;
    private static Material bloodMaterial;
    private static Material traceBoltMaterial;
    private static Material explosionSphereMaterial;
    private static Material burnFlameMaterial;
    private static Material blastEmberMaterial;
    private static Material blastSmokeMaterial;
    private static Material dangerZoneMaterial;
    private static bool shaderWarningLogged;

    /// <summary>
    /// Материал трассеров: белое ядро + оранжево-жёлтое свечение.
    /// Intensity 3.0 при пороге Bloom 0.9 даёт заметный ореол:
    /// ядро светит в ~3.3 раза выше порога.
    /// </summary>
    public static Material TracerMaterial
    {
        get
        {
            if (tracerMaterial == null)
            {
                tracerMaterial = CreateMaterial(
                    "TracerVfxMat",
                    false,
                    TracerGlowColor,
                    TracerCoreColor,
                    DefaultIntensity);

                EnableInstancing(tracerMaterial);
            }

            return tracerMaterial;
        }
    }

    /// <summary>
    /// Материал трассеров врага: тот же шейдер и та же форма, но
    /// чистый красный - и свечение, и ядро. Снаряд врага должен
    /// читаться как чужой с одного взгляда, без разбора на толпу.
    /// </summary>
    public static Material EnemyTracerMaterial
    {
        get
        {
            if (enemyTracerMaterial == null)
            {
                enemyTracerMaterial = CreateMaterial(
                    "EnemyTracerVfxMat",
                    false,
                    EnemyTracerGlowColor,
                    EnemyTracerCoreColor,
                    EnemyIntensity);

                EnableInstancing(enemyTracerMaterial);
            }

            return enemyTracerMaterial;
        }
    }

    /// <summary>
    /// GPU-инстансинг на материале трассеров. Без этого флага
    /// Graphics.RenderMeshInstanced бросает InvalidOperationException.
    ///
    /// Ставится только на те материалы, которые рисует батчер
    /// (трассеры и три слоя взрыва): остальные VFX по-прежнему
    /// рисуются обычными MeshRenderer, и лишний инстансинг-вариант
    /// им не нужен.
    /// </summary>
    private static void EnableInstancing(Material material)
    {
        if (material == null)
            return;

        material.enableInstancing = true;
    }

    /// <summary>
    /// Материал вспышек и осколков попадания.
    /// </summary>
    public static Material ImpactMaterial
    {
        get
        {
            if (impactMaterial == null)
            {
                impactMaterial = CreateMaterial(
                    "ImpactVfxMat",
                    true,
                    TracerGlowColor,
                    TracerCoreColor,
                    DefaultIntensity);
            }

            return impactMaterial;
        }
    }

    /// <summary>
    /// Материал шара пули игрока: жёлтый. Ставится на MeshRenderer
    /// самого снаряда, чтобы тело пули светилось так же, как
    /// трассер за ней.
    /// </summary>
    public static Material BulletMaterial
    {
        get
        {
            if (bulletMaterial == null)
            {
                bulletMaterial = CreateGlowBallMaterial(
                    "BulletGlowMat",
                    BulletBallColor,
                    BulletRimColor,
                    BulletIntensity);
            }

            return bulletMaterial;
        }
    }

    /// <summary>
    /// Материал шара снаряда врага: красный.
    /// </summary>
    public static Material EnemyBulletMaterial
    {
        get
        {
            if (enemyBulletMaterial == null)
            {
                enemyBulletMaterial = CreateGlowBallMaterial(
                    "EnemyBulletGlowMat",
                    EnemyBulletBallColor,
                    EnemyBulletRimColor,
                    EnemyBulletIntensity);
            }

            return enemyBulletMaterial;
        }
    }

    /// <summary>
    /// Материал капель крови: тёмно-красный, альфа-смешивание.
    /// Единственный VFX-материал без свечения.
    /// </summary>
    public static Material BloodMaterial
    {
        get
        {
            if (bloodMaterial == null)
            {
                bloodMaterial = CreateBloodMaterial();
            }

            return bloodMaterial;
        }
    }

    private static Material CreateBloodMaterial()
    {
        Shader shader = ResolveShader(
            BloodShaderName,
            "Assets/Materials/BloodDroplet.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "BloodVfxMat",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", BloodColor);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", BloodColor);
        if (material.HasProperty("_EdgePower"))
            material.SetFloat("_EdgePower", 1.9f);
        if (material.HasProperty("_CorePower"))
            material.SetFloat("_CorePower", 2f);

        ConfigureAlphaBlended(material);
        return material;
    }

    /// <summary>
    /// Материал молний и рикошетов: один на все болты кадра.
    /// Цвет приходит из vertex color, поэтому материал белый, а
    /// _Intensity = 3 повторяет старую схему «цвет + emission 2x»
    /// и сохраняет яркость свечения под Bloom.
    /// </summary>
    public static Material TraceBoltMaterial
    {
        get
        {
            if (traceBoltMaterial == null)
            {
                traceBoltMaterial = CreateTraceBoltMaterial();
            }

            return traceBoltMaterial;
        }
    }

    private static Material CreateTraceBoltMaterial()
    {
        Shader shader = ResolveShader(
            TraceBoltShaderName,
            "Assets/Materials/TraceBoltVfx.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "TraceBoltVfxMat",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", 3f);

        return material;
    }

    /// <summary>
    /// Материал огненного шара взрыва. Отдельный шейдер:
    /// сфере нужен настоящий объём (N·V, горячая кромка,
    /// Cull Off), а не мягкий круг. Цвет не задан - он
    /// приходит из vertex color (стадия остывания у каждого
    /// взрыва своя).
    /// </summary>
    public static Material ExplosionSphereMaterial
    {
        get
        {
            if (explosionSphereMaterial == null)
            {
                explosionSphereMaterial =
                    CreateExplosionSphereMaterial();

                EnableInstancing(explosionSphereMaterial);
            }

            return explosionSphereMaterial;
        }
    }

    /// <summary>
    /// Материал пламени на горящем мобе. Тот же шейдер, что у
    /// углей взрыва: мягкий круг с шумом по краю. Форму языка
    /// делает не шейдер, а градиент альфы по вершинам квада -
    /// основание яркое и плотное, вершина сходит в ноль.
    /// Аддитивный, яркость выше порога Bloom, поэтому пламя
    /// светится, а не выглядит как оранжевая наклейка.
    /// </summary>
    public static Material BurnFlameMaterial
    {
        get
        {
            if (burnFlameMaterial == null)
            {
                burnFlameMaterial = CreateBlastMaterial(
                    "BurnFlameMat",
                    Color.white,
                    true,
                    1.1f,
                    2f,
                    0.6f,
                    4f,
                    0.35f);
            }

            return burnFlameMaterial;
        }
    }

    /// <summary>
    /// Материал разлетающихся углей: аддитивный, плотное ядро.
    /// </summary>
    public static Material BlastEmberMaterial
    {
        get
        {
            if (blastEmberMaterial == null)
            {
                blastEmberMaterial = CreateBlastMaterial(
                    "BlastEmberMat",
                    Color.white,
                    true,
                    1.8f,
                    2.5f,
                    1.2f,
                    2f,
                    0.12f);

                EnableInstancing(blastEmberMaterial);
            }

            return blastEmberMaterial;
        }
    }

    /// <summary>
    /// Материал дыма: единственный неаддитивный слой взрыва.
    /// Тёмный тёплый серый, очень мягкий край, выбранный так,
    /// чтобы клуб дыма читался поверх тёмного пола арены.
    /// </summary>
    public static Material BlastSmokeMaterial
    {
        get
        {
            if (blastSmokeMaterial == null)
            {
                blastSmokeMaterial = CreateBlastMaterial(
                    "BlastSmokeMat",
                    new Color(0.17f, 0.14f, 0.13f, 1f),
                    false,
                    0.8f,
                    1.2f,
                    0.5f,
                    2.5f,
                    0.2f);

                EnableInstancing(blastSmokeMaterial);
            }

            return blastSmokeMaterial;
        }
    }

    /// <summary>
    /// Материал опасных зон: элита, аое босса, событие волны.
    /// Один на все зоны - цвет приходит per-instance массивом
    /// _ZoneColor, поэтому элита, босс и волна делят один draw call.
    /// Аддитивный: зона читается как энергия и не темнит пол.
    /// </summary>
    public static Material DangerZoneMaterial
    {
        get
        {
            if (dangerZoneMaterial == null)
            {
                dangerZoneMaterial =
                    CreateDangerZoneMaterial();

                EnableInstancing(dangerZoneMaterial);
            }

            return dangerZoneMaterial;
        }
    }

    private static Material CreateDangerZoneMaterial()
    {
        Shader shader = ResolveShader(
            DangerZoneShaderName,
            "Assets/Materials/DangerZoneVfx.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "DangerZoneVfxMat",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", 1.25f);
        if (material.HasProperty("_FillPower"))
            material.SetFloat("_FillPower", 1.5f);
        if (material.HasProperty("_RingInner"))
            material.SetFloat("_RingInner", 0.78f);
        if (material.HasProperty("_RingOuter"))
            material.SetFloat("_RingOuter", 0.9f);
        if (material.HasProperty("_DashCount"))
            material.SetFloat("_DashCount", 24f);
        if (material.HasProperty("_StripeScale"))
            material.SetFloat("_StripeScale", 7f);
        if (material.HasProperty("_SpinSpeed"))
            material.SetFloat("_SpinSpeed", 1.6f);
        if (material.HasProperty("_PulseSpeed"))
            material.SetFloat("_PulseSpeed", 6f);
        if (material.HasProperty("_ShockWidth"))
            material.SetFloat("_ShockWidth", 0.12f);

        ConfigureAdditive(material);

        return material;
    }

    /// <summary>
    /// Один из плоских слоёв взрыва (угли, дым). Один шейдер на
    /// оба, различает их _Additive: светится или дым.
    /// </summary>
    private static Material CreateBlastMaterial(
        string name,
        Color color,
        bool additive,
        float edgePower,
        float corePower,
        float coreGain,
        float noiseScale,
        float noiseAmount)
    {
        Shader shader = ResolveShader(
            BlastShaderName,
            "Assets/Materials/BlastVfx.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = name,
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", 1f);
        if (material.HasProperty("_EdgePower"))
            material.SetFloat("_EdgePower", edgePower);
        if (material.HasProperty("_CorePower"))
            material.SetFloat("_CorePower", corePower);
        if (material.HasProperty("_CoreGain"))
            material.SetFloat("_CoreGain", coreGain);
        if (material.HasProperty("_NoiseScale"))
            material.SetFloat("_NoiseScale", noiseScale);
        if (material.HasProperty("_NoiseAmount"))
            material.SetFloat("_NoiseAmount", noiseAmount);

        if (additive)
        {
            ConfigureAdditive(material);
        }
        else
        {
            ConfigureAlphaBlended(material);
        }

        // Флаг режима смешивания для фрагментного шейдера: у
        // аддитивного слоя маска уходит в цвет, у дыма - в альфу.
        if (material.HasProperty("_Additive"))
            material.SetFloat("_Additive", additive ? 1f : 0f);

        return material;
    }

    /// <summary>
    /// Материал сферы-огня. Яркость целиком в vertex color,
    /// поэтому _Intensity держим единицей, а сам материал отвечает
    /// только за форму объёма: мягкое тело, горячую кромку и
    /// белое ядро первых кадров.
    /// </summary>
    private static Material CreateExplosionSphereMaterial()
    {
        Shader shader = ResolveShader(
            ExplosionSphereShaderName,
            "Assets/Materials/ExplosionSphereVfx.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "ExplosionSphereMat",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", 1f);

        // Тело: пологая кривая, чтобы шар не выглядел пятном.
        if (material.HasProperty("_BodyPower"))
            material.SetFloat("_BodyPower", 1.5f);
        if (material.HasProperty("_BodyGain"))
            material.SetFloat("_BodyGain", 0.9f);

        // Кромка: узкая и яркая - именно она делает шар шаром.
        if (material.HasProperty("_RimPower"))
            material.SetFloat("_RimPower", 2.5f);
        if (material.HasProperty("_RimGain"))
            material.SetFloat("_RimGain", 1.15f);

        // Ядро вспышки. Держим его скромным и тонированным:
        // чистое белое ядро выбивало весь шар в белый шар.
        if (material.HasProperty("_CorePower"))
            material.SetFloat("_CorePower", 7f);
        if (material.HasProperty("_CoreGain"))
            material.SetFloat("_CoreGain", 0.9f);
        if (material.HasProperty("_CoreSharp"))
            material.SetFloat("_CoreSharp", 0.6f);

        if (material.HasProperty("_NoiseScale"))
            material.SetFloat("_NoiseScale", 2.4f);
        if (material.HasProperty("_NoiseAmount"))
            material.SetFloat("_NoiseAmount", 0.2f);
        if (material.HasProperty("_NoiseSpeed"))
            material.SetFloat("_NoiseSpeed", 1.1f);

        // Деформация поверхности: превращает идеальную сферу
        // в кипящий пузырь. Больше 0.35 - шар начинает терять
        // читаемость как «зона поражения».
        if (material.HasProperty("_Bulge"))
            material.SetFloat("_Bulge", 0.3f);

        ConfigureAdditive(material);

        return material;
    }

    private static Material CreateMaterial(        string name,
        bool radial,
        Color glow,
        Color core,
        float intensity)
    {
        Shader shader = ResolveShader(
            ShaderName,
            "Assets/Materials/BulletTracer.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = name,
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", glow);
        if (material.HasProperty("_CoreColor"))
            material.SetColor("_CoreColor", core);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", intensity);
        if (material.HasProperty("_CoreSharpness"))
            material.SetFloat("_CoreSharpness", 5f);
        if (material.HasProperty("_EdgePower"))
            material.SetFloat("_EdgePower", 1.6f);
        if (material.HasProperty("_TailPower"))
            material.SetFloat("_TailPower", 1.5f);
        if (material.HasProperty("_RadialMode"))
            material.SetFloat("_RadialMode", radial ? 1f : 0f);

        ConfigureAdditive(material);
        return material;
    }

    /// <summary>
    /// Материал светящегося шара: тело снаряда. Отдельный шейдер,
    /// потому что форма берётся из нормали сферы, а не из UV квада.
    /// </summary>
    private static Material CreateGlowBallMaterial(
        string name,
        Color ball,
        Color rim,
        float intensity)
    {
        Shader shader = ResolveShader(
            GlowBallShaderName,
            "Assets/Materials/BulletGlowBall.shader");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = name,
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", ball);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", ball);
        if (material.HasProperty("_RimColor"))
            material.SetColor("_RimColor", rim);
        if (material.HasProperty("_RimPower"))
            material.SetFloat("_RimPower", 2f);
        if (material.HasProperty("_RimStrength"))
            material.SetFloat("_RimStrength", 0.6f);
        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", intensity);

        ConfigureAdditive(material);
        return material;
    }

    /// <summary>
    /// Шейдер VFX или запасной URP/Unlit. Молча пропасть нельзя:
    /// без материала эффект просто невидим, а Shader.Find в сборке
    /// возвращает null ещё и из-за вырезания шейдера.
    /// </summary>
    private static Shader ResolveShader(
        string shaderName,
        string assetPath)
    {
        Shader shader = Shader.Find(shaderName);

        if (shader != null)
            return shader;

        if (!shaderWarningLogged)
        {
            shaderWarningLogged = true;

            Debug.LogError(
                "Bullet Rush VFX: shader \"" + shaderName +
                "\" не найден. Проверь консоль на \"Shader error\" " +
                "при импорте " + assetPath +
                " и наличие шейдера в Project Settings > Graphics > " +
                "Always Included Shaders."
            );
        }

        shader = Shader.Find(FallbackShaderName);

        if (shader == null)
        {
            Debug.LogError(
                "Bullet Rush VFX: не найден даже запасной шейдер " +
                "\"" + FallbackShaderName + "\". VFX не будут " +
                "отрисовываться вообще."
            );

            return null;
        }

        Debug.LogWarning(
            "Bullet Rush VFX: используется запасной " +
            FallbackShaderName + " вместо \"" + shaderName +
            "\". Форма эффекта будет неверной."
        );

        return shader;
    }

    // =========================================================
    // MESHES
    // =========================================================

    private static Mesh streakMesh;
    private static Mesh centeredMesh;
    private static Mesh blastFireballMesh;
    private static Mesh blastQuadMesh;
    private static Mesh dangerDiscMesh;

    /// <summary>
    /// Квад 1x1, pivot у головы: X от -1 до 0, Y от -0.5 до 0.5.
    /// U = 1 в голове (ярко) и 0 в хвосте (гаснет).
    /// Растягивается по X через transform.localScale.
    /// </summary>
    public static Mesh StreakMesh
    {
        get
        {
            if (streakMesh == null)
                streakMesh = BuildQuad(
                    "VfxStreakQuad",
                    -1f,
                    0f
                );

            return streakMesh;
        }
    }

    /// <summary>
    /// Квад 1x1 с центром в pivot: X и Y от -0.5 до 0.5.
    /// Используется вспышкой попадания и осколками.
    /// </summary>
    public static Mesh CenteredMesh
    {
        get
        {
            if (centeredMesh == null)
                centeredMesh = BuildQuad(
                    "VfxCenteredQuad",
                    -0.5f,
                    0.5f
                );

            return centeredMesh;
        }
    }

    // Единичный квад со стороной 1: X и Y от -0.5 до 0.5, центр в
    // pivot. Общий для углей и дыма - форму круга рисует
    // BlastVfx.hlsl по UV, а размер приходит из матрицы инстанса,
    // а per-instance цвет - из массива _BlastColor, который ведёт
    // батчер через MaterialPropertyBlock.
    public static Mesh BlastQuadMesh
    {
        get
        {
            if (blastQuadMesh == null)
                blastQuadMesh = BuildQuad(
                    "VfxBlastQuad",
                    -0.5f,
                    0.5f
                );

            return blastQuadMesh;
        }
    }

    /// <summary>
    /// Сфера радиуса 0.5 для огненного шара: 14x9 сегментов,
    /// 150 вершин. Нормали шейдер берёт из самой позиции вершины,
    /// поэтому атрибут normal не нужен - на сфере радиус-вектор
    /// и есть нормаль.
    ///
    /// Меш общий на все взрывы: без инстансинга у каждого свой
    /// меш означал бы свой draw call на взрыв, а стадия остывания
    /// теперь приходит per-instance цветом.
    /// </summary>
    public static Mesh BlastFireballMesh
    {
        get
        {
            if (blastFireballMesh == null)
                blastFireballMesh = BuildSphereMesh(
                    "VfxBlastFireball",
                    14,
                    9);

            return blastFireballMesh;
        }
    }

    /// <summary>
    /// Диск радиуса 0.5, лежащий в плоскости XZ, с центром в pivot.
    /// Общий для всех зон: размер приходит из матрицы инстанса
    /// (scale.x = scale.z = диаметр), а UV так раскладываются в
    /// 0..1, чтобы шейдер посчитал радиус как length(uv*2-1).
    ///
    /// Веер из segments треугольников: центр - одна вершина, край -
    /// segments+1 вершин. 64 сегмента дают ровный круг на любом
    /// радиусе зон.
    /// </summary>
    public static Mesh DangerDiscMesh
    {
        get
        {
            if (dangerDiscMesh == null)
                dangerDiscMesh = BuildDiscMesh(
                    "VfxDangerDisc",
                    64);

            return dangerDiscMesh;
        }
    }

    private static Mesh BuildDiscMesh(string name, int segments)
    {
        int vertexCount = segments + 2;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[segments * 3];

        // Центр веера.
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;

            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);

            if (i == segments)
                break;

            int t = i * 3;

            triangles[t] = 0;
            triangles[t + 1] = i + 1;
            triangles[t + 2] = i + 2;
        }

        Mesh mesh = new Mesh
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Бокс задаётся руками: диск плоский, а по высоте оставляем
        // запас, чтобы кольцо вспышки не срезалось у самой земли.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(1f, 0.25f, 1f));

        mesh.UploadMeshData(true);

        return mesh;
    }

    private static Mesh BuildSphereMesh(
        string name,
        int segments,
        int rings)
    {
        Vector3[] vertices =
            new Vector3[(segments + 1) * (rings + 1)];

        Vector2[] uvs = new Vector2[(segments + 1) * (rings + 1)];
        int[] triangles = new int[segments * rings * 6];

        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float phi = v * Mathf.PI;

            float sin = Mathf.Sin(phi);
            float cos = Mathf.Cos(phi);

            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float theta = u * Mathf.PI * 2f;

                int index = ring * (segments + 1) + segment;

                vertices[index] = new Vector3(
                    sin * Mathf.Cos(theta),
                    cos,
                    sin * Mathf.Sin(theta)
                ) * 0.5f;

                uvs[index] = new Vector2(u, 1f - v);
            }
        }

        int cursor = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int bottomLeft = ring * (segments + 1) + segment;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + segments + 1;
                int topRight = topLeft + 1;

                triangles[cursor++] = bottomLeft;
                triangles[cursor++] = topLeft;
                triangles[cursor++] = bottomRight;

                triangles[cursor++] = topLeft;
                triangles[cursor++] = topRight;
                triangles[cursor++] = bottomRight;
            }
        }

        Mesh mesh = new Mesh
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Меш не меняется после создания: ни цвета, ни вершин
        // effect больше в него не пишет. Поэтому границы задаются
        // руками и UploadMeshData(true) - без CPU-копии.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(1f, 1f, 1f)
        );

        mesh.UploadMeshData(true);

        return mesh;
    }

    private static Mesh BuildQuad(string name, float minX, float maxX)
    {
        Vector3[] vertices =
        {
            new Vector3(minX, -0.5f, 0f),
            new Vector3(minX,  0.5f, 0f),
            new Vector3(maxX, -0.5f, 0f),
            new Vector3(maxX,  0.5f, 0f)
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        Mesh mesh = new Mesh
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        // Сетка неизменяемая: убираем копию в CPU-памяти.
        mesh.UploadMeshData(true);

        return mesh;
    }

    // =========================================================
    // TRACER QUADS (ОБЩИЕ, ДЛЯ ИНСТАНСИНГА)
    // =========================================================

    private static Mesh tracerTailMesh;
    private static Mesh tracerCoreMesh;

    /// <summary>
    /// Единичный квад хвоста: X от -1 (хвост) до 0 (голова),
    /// Y от -0.5 до 0.5. Форма зашита в UV1.x = -1, поэтому шейдер
    /// берёт спад по длине, а не по радиусу.
    ///
    /// Геометрия та же, что у StreakMesh, но это ДРУГой меш: у
    /// StreakMesh канала UV1 нет, и форма приходит из материала
    /// (_RadialMode). Для инстансов материал общий на всех, а
    /// хвост и ядро лежат в разных вызовах отрисовки, поэтому
    /// форму приходится задавать на самом меше.
    ///
    /// Масштаб приходит из матрицы инстанса: X - длина хвоста,
    /// Y - её ширина.
    /// </summary>
    public static Mesh TracerTailMesh
    {
        get
        {
            if (tracerTailMesh == null)
                tracerTailMesh = BuildShapeQuad(
                    "VfxTracerTailQuad",
                    -1f,
                    0f,
                    -1f);

            return tracerTailMesh;
        }
    }

    /// <summary>
    /// Единичный квад ядра: X и Y от -0.5 до 0.5, то есть квад
    /// квадратный - масштабируется одинаково по обеим осям и
    /// остаётся круглым после поворота объекта к камере.
    /// Форма зашита в UV1.x = +1 (радиальный спад).
    /// </summary>
    public static Mesh TracerCoreMesh
    {
        get
        {
            if (tracerCoreMesh == null)
                tracerCoreMesh = BuildShapeQuad(
                    "VfxTracerCoreQuad",
                    -0.5f,
                    0.5f,
                    1f);

            return tracerCoreMesh;
        }
    }

    private static Mesh BuildShapeQuad(
        string name,
        float minX,
        float maxX,
        float shape)
    {
        Vector3[] vertices =
        {
            new Vector3(minX, -0.5f, 0f),
            new Vector3(minX,  0.5f, 0f),
            new Vector3(maxX, -0.5f, 0f),
            new Vector3(maxX,  0.5f, 0f)
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        Vector2[] shapes = new Vector2[4];

        for (int i = 0; i < shapes.Length; i++)
            shapes[i] = new Vector2(shape, 0f);

        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        Mesh mesh = new Mesh
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetUVs(1, shapes);
        mesh.triangles = triangles;

        // Бокс задаётся руками, а не RecalculateBounds: квад плоский,
        // и по нему считается отсечение всего батча целиком.
        // Нулевая толщина по Z отсекала бы инстансы у камеры,
        // поэтому Z задаётся с запасом.
        mesh.bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, 0f, 0f),
            new Vector3(maxX - minX, 1f, 2f));

        // Сетка неизменяемая: убираем копию в CPU-памяти.
        mesh.UploadMeshData(true);

        return mesh;
    }

    // =========================================================
    // RENDERER SETUP
    // =========================================================

    /// <summary>
    /// Общая настройка VFX-рендерера: без теней, без
    /// light probes, без motion vectors, без occlusion culling.
    /// Материал здесь может быть любым - метод задаёт только
    /// флаги рендерера, меш и материал.
    /// </summary>
    public static void SetupRenderer(
        MeshRenderer renderer,
        Mesh mesh,
        Material material)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();

        if (filter == null)
            filter = renderer.gameObject.AddComponent<MeshFilter>();

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
    }

    /// <summary>
    /// Историческое имя: осталось от первой версии, когда
    /// все VFX были аддитивными. Само настройка - общая.
    /// </summary>
    public static void SetupAdditiveRenderer(
        MeshRenderer renderer,
        Mesh mesh,
        Material material)
    {
        SetupRenderer(renderer, mesh, material);
    }

    /// <summary>
    /// Альфа-смешивание вместо аддитивного: для крови и всего,
    /// что не должно светиться.
    /// </summary>
    private static void ConfigureAlphaBlended(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (int)CullMode.Off);

        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void ConfigureAdditive(Material material)
    {
        // Ставим и через теги, и через свойства: набор свойств
        // зависит от того, какой шейдер реально подхватился.
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (int)BlendMode.One);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (int)CullMode.Off);

        if (material.HasProperty("_EmissionColor"))
            material.EnableKeyword("_EMISSION");

        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    // =========================================================
    // CAMERA / BILLBOARD
    // =========================================================

    private static Camera cachedCamera;

    /// <summary>
    /// Камера кэшируется: Camera.main нельзя дёргать на каждый
    /// выстрел (это поиск по тегу).
    /// </summary>
    public static Camera MainCamera
    {
        get
        {
            if (cachedCamera == null)
                cachedCamera = Camera.main;

            return cachedCamera;
        }
    }

    public static void ClearCameraCache()
    {
        cachedCamera = null;
    }

    public static void ResetStatics()
    {
        tracerMaterial = null;
        enemyTracerMaterial = null;
        impactMaterial = null;
        bulletMaterial = null;
        enemyBulletMaterial = null;
        bloodMaterial = null;
        traceBoltMaterial = null;
        explosionSphereMaterial = null;
        burnFlameMaterial = null;
        blastEmberMaterial = null;
        blastSmokeMaterial = null;
        dangerZoneMaterial = null;
        shaderWarningLogged = false;
        streakMesh = null;
        centeredMesh = null;
        blastFireballMesh = null;
        blastQuadMesh = null;
        dangerDiscMesh = null;
        tracerTailMesh = null;
        tracerCoreMesh = null;
        cachedCamera = null;
    }

    /// <summary>
    /// Поворот, при котором локальная ось X смотрит вдоль
    /// direction, а квад развёрнут лицом к камере.
    ///
    /// Нужен вместо "повернуть в сторону камеры": у LineRenderer
    /// лента становится ребром к камере и пропадает, когда пуля
    /// летит вверх экрана. Квад с таким билбордом виден всегда.
    ///
    /// Считается один раз при спавне, не каждый кадр.
    /// </summary>
    public static Quaternion FaceDirection(Vector3 direction)
    {
        Vector3 forward = direction;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Camera camera = MainCamera;
        Vector3 view = camera != null
            ? camera.transform.forward
            : Vector3.up;

        // Проекция направления взгляда на плоскость, перпендикулярную
        // направлению полёта: это нормаль квада.
        Vector3 normal =
            view - forward * Vector3.Dot(view, forward);

        if (normal.sqrMagnitude < 0.0001f)
        {
            normal =
                Vector3.up -
                forward * Vector3.Dot(Vector3.up, forward);
        }

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.right;

        normal.Normalize();

        // Правый базис: X = forward, Y = up, Z = normal.
        Vector3 up = Vector3.Cross(normal, forward);

        return Quaternion.LookRotation(normal, up);
    }
}
