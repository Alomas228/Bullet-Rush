using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Batched, texture-free dash silhouettes, speed marks and launch bursts.
/// Samples the real rendered positions rather than assuming a straight path.
/// No colliders, lights, particle systems or objects are created per dash.
/// </summary>
[DefaultExecutionOrder(350)]
public sealed class DashVfxBatcher : MonoBehaviour
{
    private const int MaxEmitters = 96;
    private const int MaxGhosts = 320;
    private const int MaxBursts = 96;
    private const int MaxWindups = 96;
    private const float PlayerSpacing = 0.56f;
    private const float EnemySpacing = 0.76f;
    private const float PlayerGhostLife = 0.23f;
    private const float EnemyGhostLife = 0.19f;
    private const float BurstLife = 0.21f;

    private struct Emitter
    {
        public Transform Target;
        public Vector3 PreviousPosition, Direction;
        public Quaternion PreviousRotation;
        public Vector3 Size;
        public float Remainder, EndTime;
        public bool Enemy, Ending;
    }

    private struct Ghost
    {
        public Vector3 Position, Direction, Size;
        public Quaternion Rotation;
        public float Born;
        public bool Enemy;
    }

    private struct Burst
    {
        public Vector3 Position, Direction, Size;
        public float Born;
        public bool Enemy;
    }

    private struct Windup
    {
        public Transform Target;
        public Vector3 Direction;
        public float Start, Duration;
    }

    private static DashVfxBatcher instance;
    private readonly List<Emitter> emitters = new List<Emitter>(MaxEmitters);
    private readonly List<Ghost> ghosts = new List<Ghost>(MaxGhosts);
    private readonly List<Burst> bursts = new List<Burst>(MaxBursts);
    private readonly List<Windup> windups = new List<Windup>(MaxWindups);
    private readonly List<Vector3> vertices = new List<Vector3>(16000);
    private readonly List<Color32> colors = new List<Color32>(16000);
    private readonly List<int> indices = new List<int>(24000);
    private Mesh mesh;
    private Material material;
    private MeshRenderer surface;
    private Camera view;

    private static readonly Color32 PlayerBody = new Color32(238, 185, 105, 255);
    private static readonly Color32 PlayerTop = new Color32(255, 239, 191, 255);
    private static readonly Color32 PlayerShade = new Color32(192, 129, 63, 255);
    private static readonly Color32 EnemyBody = new Color32(83, 183, 170, 255);
    private static readonly Color32 EnemyTop = new Color32(191, 247, 229, 255);
    private static readonly Color32 EnemyShade = new Color32(42, 119, 123, 255);

    public static void BeginDash(Transform target, Vector3 direction, float duration, bool enemy)
    {
        if (target == null || duration <= 0f) return;
        EnsureExists();
        if (instance != null) instance.StartDash(target, direction, duration, enemy);
    }

    public static void EndDash(Transform target)
    {
        if (instance == null || target == null) return;
        for (int i = instance.emitters.Count - 1; i >= 0; i--)
        {
            Emitter e = instance.emitters[i];
            if (e.Target != target) continue;
            // Keep the final rendered pose on this frame (including Rigidbody
            // interpolation), then cease emitting; old ghosts fade normally.
            e.Ending = true;
            instance.emitters[i] = e;
        }
        instance.RemoveWindup(target);
    }

    public static void BeginWindup(Transform target, Vector3 direction, float duration)
    {
        if (target == null || duration <= 0f) return;
        EnsureExists();
        if (instance == null) return;
        instance.RemoveWindup(target);
        if (instance.windups.Count >= MaxWindups) instance.windups.RemoveAt(0);
        instance.windups.Add(new Windup
        {
            Target = target,
            Direction = FlatDirection(direction),
            Start = Time.time,
            Duration = duration
        });
    }

    public static void UpdateWindupDirection(Transform target, Vector3 direction)
    {
        if (instance == null || target == null) return;
        for (int i = instance.windups.Count - 1; i >= 0; i--)
        {
            Windup w = instance.windups[i];
            if (w.Target != target) continue;
            w.Direction = FlatDirection(direction);
            instance.windups[i] = w;
            break;
        }
    }

    public static void ClearAll()
    {
        if (instance != null) instance.ClearEffects();
    }

    private static void EnsureExists()
    {
        if (instance != null) return;
        new GameObject("Dash VFX Batch").AddComponent<DashVfxBatcher>();
    }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        Shader shader = Resources.Load<Shader>("DashVfx");
        if (shader == null)
        {
            Debug.LogError("[DashVfx] Missing DashVfx shader.");
            enabled = false;
            return;
        }
        material = new Material(shader) { name = "Dash afterimages" };
        mesh = new Mesh { name = "Batched dash effects" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        surface = gameObject.AddComponent<MeshRenderer>();
        surface.sharedMaterial = material;
        surface.shadowCastingMode = ShadowCastingMode.Off;
        surface.receiveShadows = false;
        surface.lightProbeUsage = LightProbeUsage.Off;
        surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
        surface.enabled = false;
    }

    private void StartDash(Transform target, Vector3 direction, float duration, bool enemy)
    {
        RemoveWindup(target);
        for (int i = emitters.Count - 1; i >= 0; i--)
            if (emitters[i].Target == target) emitters.RemoveAt(i);
        if (emitters.Count >= MaxEmitters)
        {
            int drop = 0;
            while (drop < emitters.Count && !emitters[drop].Enemy) drop++;
            emitters.RemoveAt(drop < emitters.Count ? drop : 0);
        }

        Vector3 size = AbsSize(target.lossyScale);
        direction = FlatDirection(direction);
        float now = Time.time;
        emitters.Add(new Emitter
        {
            Target = target,
            PreviousPosition = target.position,
            PreviousRotation = target.rotation,
            Direction = direction,
            Size = size,
            EndTime = now + duration + 0.065f,
            Enemy = enemy
        });
        AddGhost(target.position, target.rotation, direction, size, now, enemy);
        if (bursts.Count >= MaxBursts) bursts.RemoveAt(0);
        bursts.Add(new Burst
        {
            Position = target.position,
            Direction = direction,
            Size = size,
            Born = now,
            Enemy = enemy
        });
    }

    private void RemoveWindup(Transform target)
    {
        for (int i = windups.Count - 1; i >= 0; i--)
            if (windups[i].Target == target) windups.RemoveAt(i);
    }

    private void AddGhost(Vector3 pos, Quaternion rotation, Vector3 direction, Vector3 size, float now, bool enemy)
    {
        if (ghosts.Count >= MaxGhosts)
        {
            // When an unusually large crowd dashes, discard old enemy ghosts
            // before the player's more important readability feedback.
            int drop = 0;
            while (drop < ghosts.Count && !ghosts[drop].Enemy) drop++;
            if (drop == ghosts.Count) drop = 0;
            ghosts.RemoveAt(drop);
        }
        ghosts.Add(new Ghost
        {
            Position = pos, Rotation = rotation, Direction = direction,
            Size = size, Born = now, Enemy = enemy
        });
    }

    private void LateUpdate()
    {
        if (surface == null) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            ClearEffects();
            return;
        }
        if (emitters.Count == 0 && ghosts.Count == 0 && bursts.Count == 0 && windups.Count == 0)
        {
            surface.enabled = false;
            return;
        }

        float now = Time.time;
        SampleEmitters(now);
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        if (view == null) { surface.enabled = false; return; }

        vertices.Clear(); colors.Clear(); indices.Clear();
        // Oldest silhouettes first; newer ones layer above them.
        for (int i = 0; i < ghosts.Count;)
        {
            Ghost ghost = ghosts[i];
            float life = ghost.Enemy ? EnemyGhostLife : PlayerGhostLife;
            float t = Mathf.Clamp01((now - ghost.Born) / life);
            if (t >= 1f) { ghosts.RemoveAt(i); continue; }
            DrawGhost(ghost, t);
            i++;
        }
        for (int i = 0; i < bursts.Count;)
        {
            Burst burst = bursts[i];
            float t = Mathf.Clamp01((now - burst.Born) / BurstLife);
            if (t >= 1f) { bursts.RemoveAt(i); continue; }
            DrawBurst(burst, t);
            i++;
        }
        for (int i = 0; i < windups.Count;)
        {
            Windup windup = windups[i];
            if (windup.Target == null || !windup.Target.gameObject.activeInHierarchy || now >= windup.Start + windup.Duration + 0.035f)
            {
                windups.RemoveAt(i);
                continue;
            }
            DrawWindup(windup, Mathf.Clamp01((now - windup.Start) / windup.Duration));
            i++;
        }

        surface.enabled = vertices.Count > 0;
        if (!surface.enabled) return;
        mesh.Clear(false);
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0, true);
    }

    private void SampleEmitters(float now)
    {
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            Emitter e = emitters[i];
            if (e.Target == null || !e.Target.gameObject.activeInHierarchy)
            {
                emitters.RemoveAt(i);
                continue;
            }
            Vector3 position = e.Target.position;
            Quaternion rotation = e.Target.rotation;
            Vector3 delta = position - e.PreviousPosition;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            // Never paint a long trail across an arena if a system teleports
            // the unit or the run is reset during a dash.
            if (distance > 8f)
            {
                emitters.RemoveAt(i);
                continue;
            }
            if (distance > 0.001f)
            {
                Vector3 dir = flat / distance;
                float spacing = e.Enemy ? EnemySpacing : PlayerSpacing;
                float next = spacing - e.Remainder;
                int samples = 0;
                while (next <= distance && samples++ < 12)
                {
                    float ratio = next / distance;
                    // Snapshots follow the actual path, including wall slides.
                    AddGhost(Vector3.Lerp(e.PreviousPosition, position, ratio),
                        Quaternion.Slerp(e.PreviousRotation, rotation, ratio),
                        dir, e.Size, now, e.Enemy);
                    next += spacing;
                }
                e.Direction = dir;
                e.Remainder = (e.Remainder + distance) % spacing;
            }
            e.PreviousPosition = position;
            e.PreviousRotation = rotation;
            if (e.Ending || now >= e.EndTime)
            {
                if (e.Remainder > 0.18f)
                    AddGhost(position, rotation, e.Direction, e.Size, now, e.Enemy);
                emitters.RemoveAt(i);
            }
            else emitters[i] = e;
        }
    }

    private void DrawGhost(Ghost g, float t)
    {
        float life = 1f - t;
        float opacity = (g.Enemy ? 0.27f : 0.36f) * life * life;
        Vector3 half = g.Size * (0.5f * Mathf.Lerp(0.98f, 0.69f, t));
        Vector3 cameraDir = view.transform.position - g.Position;
        Color32 body = WithAlpha(g.Enemy ? EnemyBody : PlayerBody, opacity);
        Color32 top = WithAlpha(g.Enemy ? EnemyTop : PlayerTop, opacity * 1.18f);
        Color32 shade = WithAlpha(g.Enemy ? EnemyShade : PlayerShade, opacity * 0.82f);
        // Emit only the visible faces of each translucent cuboid, avoiding
        // doubled alpha where the rear faces would overlap the front faces.
        Vector3 p000 = Corner(g, half, -1,-1,-1), p001 = Corner(g, half, -1,-1,1);
        Vector3 p010 = Corner(g, half, -1,1,-1), p011 = Corner(g, half, -1,1,1);
        Vector3 p100 = Corner(g, half, 1,-1,-1), p101 = Corner(g, half, 1,-1,1);
        Vector3 p110 = Corner(g, half, 1,1,-1), p111 = Corner(g, half, 1,1,1);
        if (Vector3.Dot(cameraDir, g.Rotation * Vector3.right) > 0f) Quad(p101,p100,p110,p111,shade,shade,body,body);
        else Quad(p000,p001,p011,p010,shade,shade,body,body);
        if (Vector3.Dot(cameraDir, g.Rotation * Vector3.forward) > 0f) Quad(p001,p101,p111,p011,body,body,top,top);
        else Quad(p100,p000,p010,p110,body,body,top,top);
        Quad(p011,p111,p110,p010,top,top,top,top);

        // Two short, ground-level tapering streaks alongside each snapshot.
        Vector3 dir = g.Direction;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 floor = g.Position - Vector3.up * (g.Size.y * 0.5f - 0.065f);
        float gap = g.Size.x * 0.36f;
        float length = (g.Enemy ? 0.8f : 1.03f) * Mathf.Lerp(1f, 0.56f, t);
        Color32 tip = WithAlpha(g.Enemy ? EnemyTop : PlayerTop, life * 0.72f);
        Color32 tail = WithAlpha(g.Enemy ? EnemyBody : PlayerBody, 0f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 center = floor + side * (s * gap);
            Strip(center - dir * length, center + dir * 0.12f, 0.012f, 0.075f * life, tail, tip);
        }
    }

    private void DrawBurst(Burst b, float t)
    {
        float life = 1f - t;
        float spread = Mathf.Lerp(0.36f, 0.94f, t) * b.Size.x;
        Vector3 dir = b.Direction;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 floor = b.Position - Vector3.up * (b.Size.y * 0.5f - 0.07f);
        Color32 shine = WithAlpha(b.Enemy ? EnemyTop : PlayerTop, life * 0.96f);
        Color32 dust = WithAlpha(b.Enemy ? EnemyBody : PlayerBody, life * 0.46f);
        Color32 clear = WithAlpha(b.Enemy ? EnemyBody : PlayerBody, 0f);
        // A broken fan of compact speed wedges at takeoff, rather than a
        // persistent ring or a large screen-obscuring particle cloud.
        for (int j = -2; j <= 2; j++)
        {
            Vector3 heading = FlatDirection(-dir + side * (j * 0.42f));
            Vector3 near = floor - dir * (0.14f + t * 0.30f) + side * (j * spread * 0.17f);
            Vector3 far = near + heading * ((0.48f + Mathf.Abs(j) * 0.11f) * (0.75f + t));
            Strip(far, near, 0.012f, 0.13f * life, clear, shine);
            if (j != 0)
            {
                Vector3 p = near + heading * (0.13f + t * 0.35f);
                float s = 0.085f * life;
                Tri(p + side*s, p - side*s, p + heading*s*2f, dust, dust, shine);
            }
        }
    }

    private void DrawWindup(Windup w, float progress)
    {
        Vector3 size = AbsSize(w.Target.lossyScale);
        Vector3 dir = w.Direction;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 floor = w.Target.position - Vector3.up * (size.y * 0.5f - 0.055f);
        float alpha = 0.20f + progress * 0.65f;
        Color32 color = WithAlpha(EnemyTop, alpha);
        Color32 outline = WithAlpha(EnemyShade, alpha * 0.64f);
        // Two compact forward-pointing chevrons at the feet show the charge
        // without confusing it with a collision zone or a status halo.
        for (int i = 0; i < 2; i++)
        {
            Vector3 tip = floor + dir * (size.z * 0.58f + 0.24f + i * 0.24f - progress * 0.10f);
            Vector3 back = tip - dir * 0.20f;
            float width = size.x * 0.25f;
            Strip(back + side * width, tip, 0.078f, 0.078f, outline, outline);
            Strip(back - side * width, tip, 0.078f, 0.078f, outline, outline);
            Strip(back + side * width, tip, 0.054f, 0.054f, color, color);
            Strip(back - side * width, tip, 0.054f, 0.054f, color, color);
        }
    }

    private static Vector3 Corner(Ghost g, Vector3 half, int x, int y, int z) =>
        g.Position + g.Rotation * new Vector3(half.x*x, half.y*y, half.z*z);

    private void Strip(Vector3 a, Vector3 b, float widthA, float widthB, Color32 ca, Color32 cb)
    {
        Vector3 side = Vector3.Cross(Vector3.up, b - a);
        if (side.sqrMagnitude < 0.000001f) return;
        side.Normalize();
        Quad(a - side*widthA, a + side*widthA, b + side*widthB, b - side*widthB, ca,ca,cb,cb);
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
    {
        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        colors.Add(ca); colors.Add(cb); colors.Add(cc); colors.Add(cd);
        indices.Add(i); indices.Add(i+1); indices.Add(i+2);
        indices.Add(i); indices.Add(i+2); indices.Add(i+3);
    }

    private void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc)
    {
        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        colors.Add(ca); colors.Add(cb); colors.Add(cc);
        indices.Add(i); indices.Add(i+1); indices.Add(i+2);
    }

    private static Color32 WithAlpha(Color32 color, float alpha)
    {
        color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
        return color;
    }

    private static Vector3 FlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static Vector3 AbsSize(Vector3 size) => new Vector3(
        Mathf.Max(0.05f, Mathf.Abs(size.x)), Mathf.Max(0.05f, Mathf.Abs(size.y)), Mathf.Max(0.05f, Mathf.Abs(size.z)));

    private void ClearEffects()
    {
        emitters.Clear(); ghosts.Clear(); bursts.Clear(); windups.Clear();
        if (surface != null) surface.enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;
}
