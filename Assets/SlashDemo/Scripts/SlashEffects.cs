using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public sealed class SlashEffects : MonoBehaviour
    {
        const float SwordWaveGlowThickness = 0.48f, SwordWaveCoreThickness = 0.156f;
        sealed class Fleck
        {
            public SpriteRenderer renderer;
            public Vector3 velocity;
            public float age, life, spin, angle;
            public Color color;
            public Vector3 scale;
            public bool gathering, grounded;
            public SlashPlayer scarOwner;
            public Vector2 ground;
        }
        readonly List<Fleck> particles = new List<Fleck>();
        readonly List<Hero360Model.Echo> modelEchoes = new List<Hero360Model.Echo>(16);
        readonly List<FloatingText> texts = new List<FloatingText>();
        sealed class ImpactRing
        {
            public LineRenderer line;
            public Vector2 position;
            public Color color;
            public float age, life, size;
        }
        readonly List<ImpactRing> rings = new List<ImpactRing>(24);
        sealed class WhirlwindRing
        {
            public SlashPlayer owner;
            public Vector2 origin;
            public float age, previousAge = -1, reach, knockback;
            public int damage, createdFrame;
            public bool notified;
            public System.Action onFirstHit;
            public readonly HashSet<SlashEnemy> hit = new HashSet<SlashEnemy>();
        }
        readonly List<WhirlwindRing> whirlwinds = new List<WhirlwindRing>(4);
        readonly List<Collider2D> whirlwindOverlaps = new List<Collider2D>(32);
        public int ActiveWhirlwindCount => whirlwinds.Count;
        sealed class SwordWave
        {
            public SlashPlayer normalAttackOwner;
            public SlashPlayer heavyOwner;
            public float scarHeavyBonus;
            public SwordScar scar;
            public bool normalAttack, notified;
            public Transform root;
            public LineRenderer outer, core;
            public Vector2 position, direction;
            public float traveled, age;
            public SlashTuningSettings.HeavyAttackSettings settings;
            public readonly HashSet<SlashEnemy> hit = new HashSet<SlashEnemy>();
        }
        readonly List<SwordWave> waves = new List<SwordWave>();
        readonly List<Collider2D> waveOverlaps = new List<Collider2D>(32);
        public int ActiveSwordWaveCount => waves.Count;
        sealed class SwordScar
        {
            public SlashPlayer owner;
            public BladeLightVisual visual, dark;
            public Vector2 origin, direction;
            public float length, reach, width, age, nextTick;
            public int damage, seed, debrisSpawned;
            public bool explodes;
            public SlashTuningSettings.IronSwordSettings settings;
        }
        readonly List<SwordScar> swordScars = new List<SwordScar>(8);
        readonly HashSet<SlashEnemy> scarTargets = new HashSet<SlashEnemy>();
        readonly List<Collider2D> scarOverlaps = new List<Collider2D>(32);
        public int ActiveSwordScarCount => swordScars.Count;
        public int TotalScarTicks { get; private set; }
        public int TotalScarExplosions { get; private set; }
        sealed class BladeLightVisual
        {
            public Transform root;
            public Mesh mesh;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public float crescentPower, glowScale, coreFraction = 0.10f;
        }
        sealed class SwordScarBurst
        {
            public SwordScar scar;
            public BladeLightVisual visual;
            public float age;
            public int createdFrame;
        }
        const float ScarBurstLife = 0.34f;
        readonly List<SwordScarBurst> swordScarBursts = new List<SwordScarBurst>(8);
        readonly List<Vector3> scarVertices = new List<Vector3>(1800);
        readonly List<Color> scarColors = new List<Color>(1800);
        readonly List<Vector2> scarUVs = new List<Vector2>(1800);
        readonly List<int> scarTriangles = new List<int>(9000);
        Material scarMaterial, scarDarkMaterial;
        int scarSequence;
        public int ActiveSwordScarBurstCount => swordScarBursts.Count;
        sealed class CloudLight
        {
            public Transform root;
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector2 from, to, direction;
            public float width, age, life;
            public bool path;
        }
        readonly List<CloudLight> cloudLights = new List<CloudLight>(32);
        readonly List<Vector3> cloudVertices = new List<Vector3>(240);
        readonly List<Color> cloudColors = new List<Color>(240);
        readonly List<Vector2> cloudUVs = new List<Vector2>(240);
        readonly List<int> cloudTriangles = new List<int>(1100);
        Material cloudMaterial;
        public int ActiveCloudLightCount => cloudLights.Count;
        public int ActiveCloudTrailCount
        {
            get { int count = 0; foreach (var light in cloudLights) if (light.path) count++; return count; }
        }
        public struct FloatingText { public Vector3 position; public string text; public Color color; public float born; }
        public IReadOnlyList<FloatingText> Texts => texts;
        float freezeRemaining;
        float savedScale = 1;
        public bool Frozen => freezeRemaining > 0;
        [Min(0)] public float hitStopMultiplier = 1;
        [Range(0, 1)] public float volume = 0.18f;
        AudioSource audioSource;
        AudioClip swing, heavySwing, hit, heavyHit, killingHit;
        Material gatherMaterial;
        bool gathering;
        Vector2 chargeTarget;
        float chargePower, gatherTimer;
        public int ActiveChargeMotes
        {
            get { int count = 0; foreach (var p in particles) if (p.gathering) count++; return count; }
        }
        public int TotalChargeMotes { get; private set; }
        ObliqueView obliqueView;
        ObliqueView View
        {
            get
            {
                if (obliqueView == null) obliqueView = GetComponentInParent<SlashArena>().View;
                return obliqueView;
            }
        }

        void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            audioSource.volume = volume;
            swing = MakeSound("Sword swish", 0.13f, 700, false);
            heavySwing = MakeSound("Heavy swish", 0.19f, 380, false);
            hit = MakeSound("Impact", 0.10f, 110, true);
            heavyHit = MakeSound("Heavy impact", 0.16f, 72, true);
            killingHit = MakeSound("Final impact", 0.22f, 48, true);
        }

        static AudioClip MakeSound(string name, float duration, float frequency, bool impact)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var random = new System.Random(17);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                float noise = (float)random.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, impact ? 0.8f : 0.22f);
                float envelope = Mathf.Pow(1 - t, impact ? 3 : 1.8f) * Mathf.Min(t * 35, 1);
                samples[i] = envelope * (filtered * 0.7f + Mathf.Sin(i / (float)rate * frequency * (1 - t * 0.6f) * Mathf.PI * 2) * (impact ? 0.45f : 0.06f));
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlaySwing(bool heavy) { if (audioSource != null) audioSource.PlayOneShot(heavy ? heavySwing : swing); }
        public void PlayHit(bool heavy = false, bool lethal = false)
        {
            if (audioSource != null) audioSource.PlayOneShot(lethal ? killingHit : heavy ? heavyHit : hit,
                lethal ? 1 : heavy ? 0.9f : 0.8f);
        }
        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (audioSource != null) audioSource.volume = volume;
        }

        public void Clear()
        {
            RestoreTime();
            foreach (var particle in particles)
                if (particle.renderer != null) Destroy(particle.renderer.gameObject);
            particles.Clear();
            foreach (var echo in modelEchoes) echo.Dispose();
            modelEchoes.Clear();
            gathering = false; gatherTimer = 0;
            texts.Clear();
            foreach (var ring in rings)
                if (ring.line != null) Destroy(ring.line.gameObject);
            rings.Clear();
            CancelWhirlwinds(null);
            ClearSwordWaves();
            ClearSwordScars();
            foreach (var light in cloudLights) DisposeCloudLight(light);
            cloudLights.Clear();
        }

        // Share the ordinary blade trail's additive HDR shader and soft blue/white cross section.
        public void DrawCloudRush(Vector2 from, Vector2 to, Vector2 direction, float width, float intensity, float trailLife)
        {
            CreateCloudLight(from, to, direction, width, false, intensity, 0.14f);
            if ((to - from).sqrMagnitude > 0.0001f)
                CreateCloudLight(from, to, direction, width, true, intensity, trailLife);
        }

        void CreateCloudLight(Vector2 from, Vector2 to, Vector2 direction, float width, bool path, float intensity, float life)
        {
            if (cloudLights.Count >= 96) { DisposeCloudLight(cloudLights[0]); cloudLights.RemoveAt(0); }
            if (cloudMaterial == null)
                cloudMaterial = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive")) { name = "Flowing cloud sword light" };
            cloudMaterial.SetFloat("_Intensity", intensity);
            var root = new GameObject(path ? "Lingering cloud sword path" : "Cloud rush sword light").transform;
            root.SetParent(transform, false);
            var mesh = new Mesh { name = root.name }; mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = cloudMaterial;
            var light = new CloudLight { root = root, mesh = mesh, renderer = renderer, from = from, to = to,
                direction = direction, width = width, path = path, life = life };
            cloudLights.Add(light);
            DrawCloudLight(light);
        }

        void DrawCloudLight(CloudLight light)
        {
            cloudVertices.Clear(); cloudColors.Clear(); cloudUVs.Clear(); cloudTriangles.Clear();
            float progress = Mathf.Clamp01(light.age / light.life);
            float alpha = (1 - progress) * (1 - progress);
            Vector2 side = new Vector2(-light.direction.y, light.direction.x);
            float radius = light.width * 0.5f;
            light.renderer.sortingOrder = ObliqueProjection.DepthOrder(light.to.y) + 32;
            if (light.path)
            {
                // World-fixed ribbons cover every actual movement segment and fade independently.
                // Keep segment ends open at equal width so consecutive steps join without gaps.
                for (int lane = -1; lane <= 1; lane++)
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        Vector2 edge = Vector2.Lerp(light.from, light.to, t) + side * (radius * 0.66f * lane);
                        float thickness = radius * (lane == 0 ? 0.30f : 0.10f) * Mathf.Lerp(0.35f, 1, 1 - progress);
                        AddCloudSection(light, edge, side, thickness, alpha * (lane == 0 ? 0.50f : 0.24f), i > 0);
                    }
            }
            else
            {
                const int segments = 40;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments, lateral = t * 2 - 1;
                    float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), 0.65f);
                    float forward = Mathf.Sqrt(Mathf.Max(0, 1 - lateral * lateral));
                    Vector2 edge = light.to + (light.direction * forward + side * lateral) * radius;
                    AddCloudSection(light, edge, -light.direction, radius * 0.40f * taper, alpha, i > 0);
                }
            }
            light.mesh.Clear(); light.mesh.SetVertices(cloudVertices); light.mesh.SetColors(cloudColors);
            light.mesh.SetUVs(0, cloudUVs); light.mesh.SetTriangles(cloudTriangles, 0); light.mesh.RecalculateBounds();
        }

        void AddCloudSection(CloudLight light, Vector2 edge, Vector2 inward, float width, float alpha, bool connect)
        {
            int first = cloudVertices.Count;
            for (int col = 0; col < 5; col++)
            {
                float offset = col == 0 ? -0.3f : col == 1 ? -0.12f : col == 2 ? 0 : col == 3 ? 0.3f : 1;
                Vector2 point = edge + inward * (width * offset);
                cloudVertices.Add(light.root.InverseTransformPoint(View.LiftPoint(point, View.WeaponHeight)));
                Color color = col == 2 ? new Color(0.95f, 0.99f, 1, alpha * 0.95f)
                    : new Color(0.35f, 0.75f, 1, alpha * (col == 1 ? 0.25f : col == 3 ? 0.7f : 0));
                cloudColors.Add(color);
                cloudUVs.Add(new Vector2(col == 2 ? 1 : col == 3 ? 0.5f : 0, 0));
                if (!connect || col == 4) continue;
                int a = first - 5 + col, b = first + col;
                cloudTriangles.Add(a); cloudTriangles.Add(b); cloudTriangles.Add(a + 1);
                cloudTriangles.Add(a + 1); cloudTriangles.Add(b); cloudTriangles.Add(b + 1);
            }
        }

        void DisposeCloudLight(CloudLight light)
        {
            if (light.mesh != null) Destroy(light.mesh);
            if (light.root != null) Destroy(light.root.gameObject);
        }

        void UpdateCloudLights(float dt)
        {
            for (int i = cloudLights.Count - 1; i >= 0; i--)
            {
                var light = cloudLights[i]; light.age += dt;
                if (light.root == null || light.age >= light.life)
                { DisposeCloudLight(light); cloudLights.RemoveAt(i); }
                else DrawCloudLight(light);
            }
        }

        public bool HasWhirlwind(SlashPlayer owner)
        {
            foreach (var ring in whirlwinds) if (ring.owner == owner) return true;
            return false;
        }

        public void CancelWhirlwinds(SlashPlayer owner)
        {
            for (int i = whirlwinds.Count - 1; i >= 0; i--)
                if (owner == null || whirlwinds[i].owner == owner)
                {
                    whirlwinds.RemoveAt(i);
                }
        }

        public void LaunchWhirlwind(SlashPlayer owner, Vector2 origin, SlashPlayer.Strike strike,
            float initialAge, System.Action onFirstHit)
        {
            CancelWhirlwinds(owner);
            // Apply the complete circle once, in the same update that draws the full sword-light ribbon.
            var ring = new WhirlwindRing { owner = owner, origin = origin, age = Mathf.Max(0, initialAge),
                reach = strike.reach, damage = strike.damage, knockback = strike.knockback,
                createdFrame = Time.frameCount, onFirstHit = onFirstHit };
            whirlwinds.Add(ring);
            ResolveWhirlwind(ring);
        }

        void ResolveWhirlwind(WhirlwindRing ring)
        {
            float radius = SwordMotion.WhirlwindRadius(ring.age, ring.reach);
            Physics2D.OverlapCircle(ring.origin, radius + 0.7f, ContactFilter2D.noFilter, whirlwindOverlaps);
            foreach (var collider in whirlwindOverlaps)
            {
                var enemy = collider.GetComponent<SlashEnemy>();
                if (enemy == null || !enemy.Alive || ring.hit.Contains(enemy)
                    || !SwordMotion.WhirlwindHit(ring.origin, ring.previousAge, ring.age, ring.reach,
                        enemy.transform.position, enemy.Radius)) continue;
                ring.hit.Add(enemy);
                Vector2 direction = (Vector2)enemy.transform.position - ring.origin;
                enemy.TakeHit(ring.damage, direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right, ring.knockback, true);
                if (!ring.notified) { ring.notified = true; ring.onFirstHit?.Invoke(); }
                HitStop(0.033f);
            }
            ring.previousAge = ring.age;
        }

        void UpdateWhirlwinds(float dt)
        {
            for (int i = whirlwinds.Count - 1; i >= 0; i--)
            {
                var ring = whirlwinds[i];
                if (ring.owner == null || !ring.owner.IsAlive)
                { whirlwinds.RemoveAt(i); continue; }
                if (dt > 0 && ring.createdFrame != Time.frameCount)
                {
                    ring.age += dt;
                }
                if (ring.age >= SwordMotion.WhirlwindRingLife + SwordMotion.WhirlwindInnerDelay)
                { whirlwinds.RemoveAt(i); continue; }
            }
        }

        public void Impact(Vector2 position, bool heavy, bool lethal)
        {
            if (rings.Count >= 24)
            {
                if (rings[0].line != null) Destroy(rings[0].line.gameObject);
                rings.RemoveAt(0);
            }
            Color color = heavy || lethal ? new Color(0.9f, 0.98f, 1, 0.85f) : new Color(0.55f, 0.85f, 1, 0.45f);
            var line = DemoArt.Ring("Sword impact ring", transform, 0.5f, color, 0.045f,
                ObliqueProjection.DepthOrder(position.y) + 35, 40);
            var ring = new ImpactRing { line = line, position = position, color = color,
                life = heavy || lethal ? 0.14f : 0.10f, size = lethal ? 1.25f : heavy ? 1.05f : 0.65f };
            rings.Add(ring);
            DrawImpactRing(ring);
            GetComponentInParent<SlashArena>().CameraRig.Punch(heavy || lethal ? 1 : 0.35f);
        }

        void DrawImpactRing(ImpactRing ring)
        {
            float t = Mathf.Clamp01(ring.age / ring.life);
            ring.line.transform.position = View.LiftPoint(ring.position, View.WeaponHeight);
            ring.line.transform.rotation = View.Rotation;
            ring.line.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, ring.size, 1 - (1 - t) * (1 - t));
            Color c = ring.color;
            c.a *= (1 - t) * (1 - t);
            ring.line.startColor = ring.line.endColor = c;
        }

        public void SetChargeTarget(Vector2 position, float progress)
        {
            gathering = true;
            chargeTarget = position;
            chargePower = Mathf.Clamp01(progress);
        }

        public void StopChargeGather()
        {
            gathering = false; gatherTimer = 0;
            for (int i = particles.Count - 1; i >= 0; i--)
                if (particles[i].gathering)
                {
                    if (particles[i].renderer != null) Destroy(particles[i].renderer.gameObject);
                    particles.RemoveAt(i);
                }
        }

        void SpawnChargeMote()
        {
            if (particles.Count >= 180 || ActiveChargeMotes >= 16) return;
            if (gatherMaterial == null)
            {
                gatherMaterial = new Material(DemoArt.Material) { name = "Charge mote HDR" };
                gatherMaterial.SetColor("_Color", new Color(2.2f, 2.4f, 2.5f, 1));
            }
            Vector2 position = chargeTarget + DemoArt.Direction(Random.Range(0f, 360f)) * Random.Range(0.4f, 0.85f);
            var color = new Color(0.65f, 0.88f, 1, 0.8f);
            var r = DemoArt.Shape("Gathering sword light", transform, "Disk", Vector2.zero,
                Vector2.one * Random.Range(0.035f, 0.065f), color, ObliqueProjection.DepthOrder(position.y) + 30);
            r.sharedMaterial = gatherMaterial;
            r.transform.position = View.LiftPoint(position, View.WeaponHeight);
            r.transform.rotation = View.Rotation;
            particles.Add(new Fleck { renderer = r, gathering = true, ground = position, life = 0.45f,
                color = color, scale = r.transform.localScale });
            TotalChargeMotes++;
        }

        public void ClearSwordWaves()
        {
            foreach (var wave in waves)
                if (wave.root != null) Destroy(wave.root.gameObject);
            waves.Clear();
        }

        public void LaunchSwordWave(Vector2 position, Vector2 direction, SlashTuningSettings.HeavyAttackSettings settings,
            SlashPlayer normalAttackOwner = null, SlashPlayer heavyOwner = null, float scarHeavyBonus = 0,
            SlashTuningSettings.IronSwordSettings scarSettings = null, bool scarExplodes = false)
        {
            var root = new GameObject("Charged sword wave").transform;
            root.SetParent(transform, false);
            var wave = new SwordWave { root = root, position = position,
                direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right,
                settings = settings.Clone(), normalAttackOwner = normalAttackOwner, normalAttack = normalAttackOwner != null,
                heavyOwner = heavyOwner, scarHeavyBonus = scarHeavyBonus };
            if (scarSettings != null && heavyOwner != null)
                wave.scar = CreateSwordScar(heavyOwner, position, wave.direction, settings, scarSettings, scarExplodes);
            int points = SwordMotion.WaveArcSegments + 1;
            wave.outer = DemoArt.Line("Ice glow", root, new Vector3[points], new Color(0.3f, 0.73f, 1, 0.55f), SwordWaveGlowThickness, 0);
            wave.core = DemoArt.Line("White edge", root, new Vector3[points], new Color(0.9f, 0.98f, 1), SwordWaveCoreThickness, 1);
            var taper = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 0.7f),
                new Keyframe(0.5f, 1), new Keyframe(0.8f, 0.7f), new Keyframe(1, 0));
            wave.outer.widthCurve = wave.core.widthCurve = taper;
            wave.outer.widthMultiplier = SwordWaveGlowThickness;
            wave.core.widthMultiplier = SwordWaveCoreThickness;
            waves.Add(wave);
            DrawSwordWave(wave);
        }

        void DrawSwordWave(SwordWave wave)
        {
            float progress = Mathf.Clamp01(wave.traveled / Mathf.Max(0.01f, wave.settings.distance));
            float fade = CombatFeedbackMath.WaveFade(progress);
            wave.outer.widthMultiplier = SwordWaveGlowThickness * Mathf.Lerp(1, 1.35f, progress);
            wave.core.widthMultiplier = SwordWaveCoreThickness * Mathf.Lerp(1, 1.10f, progress);
            wave.outer.startColor = wave.outer.endColor = new Color(0.3f, 0.73f, 1, 0.55f * fade);
            wave.core.startColor = wave.core.endColor = new Color(0.9f, 0.98f, 1, fade);
            wave.root.position = View.LiftPoint(wave.position, View.WeaponHeight);
            Vector2 side = new Vector2(-wave.direction.y, wave.direction.x);
            int order = ObliqueProjection.DepthOrder(wave.position.y) + 30;
            wave.outer.sortingOrder = order;
            wave.core.sortingOrder = order + 1;
            for (int i = 0; i <= SwordMotion.WaveArcSegments; i++)
            {
                Vector2 arc = SwordMotion.WaveArcPoint(wave.settings.width, 2f * i / SwordMotion.WaveArcSegments - 1);
                Vector3 point = wave.direction * arc.x + side * arc.y;
                wave.outer.SetPosition(i, point);
                wave.core.SetPosition(i, point);
            }
        }

        void UpdateSwordWaves(float dt)
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var wave = waves[i];
                if (wave.root == null) { waves.RemoveAt(i); continue; }
                if (dt > 0)
                {
                    wave.age += dt;
                    float step = Mathf.Max(0, SwordMotion.WaveTravel(wave.age, wave.settings.speed, wave.settings.distance) - wave.traveled);
                    Vector2 next = wave.position + wave.direction * step;
                    Physics2D.OverlapCircle((wave.position + next) * 0.5f,
                        step * 0.5f + wave.settings.width * 0.5f + 0.7f, ContactFilter2D.noFilter, waveOverlaps);
                    foreach (var collider in waveOverlaps)
                    {
                        var enemy = collider.GetComponent<SlashEnemy>();
                        if (enemy == null || !enemy.Alive || wave.hit.Contains(enemy)) continue;
                        if (!SwordMotion.WaveHit(wave.position, wave.direction, step, wave.settings.width,
                            enemy.transform.position, enemy.Radius)) continue;
                        wave.hit.Add(enemy);
                        int damage = wave.settings.damage;
                        if (wave.heavyOwner != null && wave.scarHeavyBonus > 0
                            && IsOnSwordScar(wave.heavyOwner, enemy.transform.position, enemy.Radius))
                            damage = Mathf.RoundToInt(damage * (1 + wave.scarHeavyBonus));
                        enemy.TakeHit(damage, wave.direction, wave.settings.knockback, !wave.normalAttack);
                        if (!wave.notified)
                        {
                            wave.notified = true;
                            if (wave.normalAttackOwner != null) wave.normalAttackOwner.OnSwordTideHit();
                        }
                    }
                    wave.position = next;
                    wave.traveled += step;
                    // The scar follows the traveled sword edge, so a new scar cannot precede its wave's hit.
                    if (wave.scar != null && wave.scar.visual.root != null) wave.scar.length = wave.traveled;
                    if (wave.traveled >= wave.settings.distance - 0.00001f)
                    {
                        Destroy(wave.root.gameObject);
                        waves.RemoveAt(i);
                        continue;
                    }
                }
                DrawSwordWave(wave);
            }
        }

        SwordScar CreateSwordScar(SlashPlayer owner, Vector2 origin, Vector2 direction,
            SlashTuningSettings.HeavyAttackSettings wave, SlashTuningSettings.IronSwordSettings settings, bool explodes)
        {
            var scar = new SwordScar { owner = owner, origin = origin, direction = direction,
                width = wave.width, reach = wave.distance, damage = wave.damage, seed = ++scarSequence,
                settings = settings.Clone(), explodes = explodes, nextTick = settings.scarTickInterval,
                visual = CreateBladeLight(owner, "玄铁剑痕 · 裂口亮芯"),
                dark = CreateBladeLight(owner, "玄铁剑痕 · 翻土暗底", true) };
            // Persistent energy is dimmer than a swing or explosion, leaving the dark rim readable through bloom.
            scar.visual.properties.SetFloat("_Intensity", Mathf.Clamp(owner.trailHDRIntensity * 0.65f, 0.8f, 1.8f));
            scar.visual.properties.SetFloat("_GlowIntensity", 0.8f);
            scar.visual.renderer.SetPropertyBlock(scar.visual.properties);
            swordScars.Add(scar);
            DrawSwordScar(scar);
            return scar;
        }

        public void ClearSwordScars(SlashPlayer owner = null)
        {
            for (int i = swordScars.Count - 1; i >= 0; i--)
            {
                if (owner != null && swordScars[i].owner != owner) continue;
                DisposeBladeLight(swordScars[i].visual);
                DisposeBladeLight(swordScars[i].dark);
                swordScars.RemoveAt(i);
            }
            for (int i = swordScarBursts.Count - 1; i >= 0; i--)
            {
                if (owner != null && swordScarBursts[i].scar.owner != owner) continue;
                DisposeBladeLight(swordScarBursts[i].visual); swordScarBursts.RemoveAt(i);
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                if (!particles[i].grounded || (owner != null && particles[i].scarOwner != owner)) continue;
                if (particles[i].renderer != null) Destroy(particles[i].renderer.gameObject);
                particles.RemoveAt(i);
            }
        }

        static bool ScarContains(SwordScar scar, Vector2 position, float radius)
        {
            if (scar.length <= 0 || scar.visual.root == null) return false;
            Vector2 delta = position - scar.origin;
            float along = Vector2.Dot(delta, scar.direction);
            return along >= -radius && along <= scar.length + radius
                && Mathf.Abs(delta.x * scar.direction.y - delta.y * scar.direction.x) <= scar.width * 0.5f + radius;
        }

        public bool IsOnSwordScar(SlashPlayer owner, Vector2 position, float radius = 0)
        {
            foreach (var scar in swordScars)
                if (scar.owner == owner && scar.age < scar.settings.scarDuration && ScarContains(scar, position, radius)) return true;
            return false;
        }

        void DamageSwordScar(SwordScar scar, float ratio, bool explosion = false)
        {
            if (scar.length <= 0 || ratio <= 0) return;
            Physics2D.OverlapCircle(scar.origin + scar.direction * (scar.length * 0.5f),
                scar.length * 0.5f + scar.width * 0.5f + 0.7f, ContactFilter2D.noFilter, scarOverlaps);
            scarTargets.Clear();
            foreach (var collider in scarOverlaps)
            {
                var enemy = collider.GetComponentInParent<SlashEnemy>();
                if (enemy == null || !enemy.Alive || !ScarContains(scar, enemy.transform.position, enemy.Radius)
                    || !scarTargets.Add(enemy)) continue;
                enemy.TakeHit(Mathf.Max(1, Mathf.RoundToInt(scar.damage * ratio)), scar.direction, 0, explosion, interrupt: explosion);
            }
        }

        void DrawSwordScar(SwordScar scar)
        {
            float remaining = scar.settings.scarDuration - scar.age;
            float gather = scar.explodes ? Mathf.Clamp01(1 - remaining / 0.20f) : 0;
            float fade = scar.explodes ? 1 : Mathf.Clamp01(remaining / 0.30f);
            float energy = Mathf.Lerp(0.5f, 1, Mathf.Clamp01(scar.age / scar.settings.scarDuration));
            int order = ObliqueProjection.DepthOrder(scar.origin.y + scar.direction.y * scar.length * 0.5f) - 12;
            // Dark earth uses ordinary alpha blending; both meshes lie exactly on the ground plane.
            BeginScarMesh();
            AddScarFractures(scar, scar.dark, 1.55f, 0.42f * fade, true);
            EndScarMesh(scar.dark, order - 1);
            BeginScarMesh();
            AddScarFractures(scar, scar.visual, 1, energy * fade * (1 + gather * 0.18f), false);
            AddScarRangeLines(scar, fade * Mathf.Lerp(0.26f, 0.34f, energy));
            EndScarMesh(scar.visual, order);
        }

        void AddScarRangeLines(SwordScar scar, float alpha)
        {
            if (scar.length <= 0.001f) return;
            Vector2 side = new Vector2(-scar.direction.y, scar.direction.x);
            float reveal = Mathf.Clamp01(scar.length / scar.reach);
            // Straight, quiet lines mark the actual half-width; they reveal with the damaging scar.
            for (int sign = -1; sign <= 1; sign += 2)
                for (int row = 0; row <= 24; row++)
                {
                    float t = Mathf.Min(row / 24f, reveal);
                    float endFade = Mathf.Min(1, t * 16, (1 - t) * 16);
                    Vector2 center = scar.origin + scar.direction * (scar.reach * t) + side * (sign * scar.width * 0.5f);
                    AddScarSection(scar.visual, center, side, 0.045f, alpha * endFade, 0, row > 0);
                    if (t >= reveal) break;
                }
        }

        // Stable knots every 2-3 sections give sharp bends without changing shape as the wave advances.
        static float ScarNoise(int seed, int knot)
        {
            unchecked
            {
                uint value = (uint)(seed * 73856093 ^ knot * 19349663);
                value ^= value >> 13; value *= 1274126177u; value ^= value >> 16;
                return (value & 65535) / 32767.5f - 1;
            }
        }

        static Vector2 ScarFracturePoint(Vector2 origin, Vector2 direction, float length, float width, int seed, float t)
        {
            float knot = t * 10;
            int index = Mathf.FloorToInt(knot);
            float bend = Mathf.Lerp(ScarNoise(seed, index), ScarNoise(seed, index + 1), knot - index);
            Vector2 side = new Vector2(-direction.y, direction.x);
            return origin + direction * (length * t) + side * (bend * width * 1.20f * Mathf.Min(1, t * 12));
        }

        void AddScarFractures(SwordScar scar, BladeLightVisual visual, float widthScale, float alpha, bool dark)
        {
            float width = 0.19f * scar.visual.glowScale;
            AddScarFracture(visual, scar.origin, scar.direction, scar.reach, width, scar.seed,
                Mathf.Clamp01(scar.length / scar.reach), widthScale, alpha, dark);
            Vector2 side = new Vector2(-scar.direction.y, scar.direction.x);
            for (int branch = 0; branch < 7; branch++)
            {
                float t = 0.12f + branch * 0.115f;
                float available = scar.length - scar.reach * t;
                if (available <= 0) continue;
                float sign = branch % 2 == 0 ? 1 : -1;
                Vector2 direction = (scar.direction * 0.72f + side * sign * 0.70f).normalized;
                // Keep decorative branches inside the existing damaging footprint.
                float length = Mathf.Min(scar.reach * (0.25f + branch % 3 * 0.045f), scar.width * 0.52f);
                Vector2 origin = ScarFracturePoint(scar.origin, scar.direction, scar.reach, width, scar.seed, t);
                AddScarFracture(visual, origin, direction, length, width * 0.35f, scar.seed + 11 + branch,
                    Mathf.Clamp01(available / length), widthScale, alpha * 0.8f, dark);
            }
        }

        void AddScarFracture(BladeLightVisual visual, Vector2 origin, Vector2 direction, float length,
            float width, int seed, float reveal, float widthScale, float alpha, bool dark)
        {
            if (reveal <= 0 || length <= 0.001f) return;
            Vector2 side = new Vector2(-direction.y, direction.x);
            for (int row = 0; row <= 24; row++)
            {
                float t = Mathf.Min(row / 24f, reveal);
                float taper = Mathf.Min(1, t * 18) * (0.25f + 0.75f * (1 - Mathf.Pow(t, 1.6f)));
                if (t >= 1) taper = 0;
                float jagged = 1 + 0.15f * ScarNoise(seed + 71, row / 2);
                Vector2 center = ScarFracturePoint(origin, direction, length, width, seed, t);
                AddScarSection(visual, center, side, width * widthScale * taper * jagged, alpha, 0, row > 0, dark);
                if (t >= reveal) break;
            }
        }

        BladeLightVisual CreateBladeLight(SlashPlayer owner, string name, bool dark = false)
        {
            if (scarMaterial == null)
                scarMaterial = new Material(Resources.Load<Shader>("SlashDemo/BladeTrailAdditive")) { name = "Sword scar blade glow" };
            if (dark && scarDarkMaterial == null)
                scarDarkMaterial = new Material(Resources.Load<Shader>("SlashDemo/BladeTrail")) { name = "Sword scar dark earth" };
            var root = new GameObject(name).transform; root.SetParent(transform, false);
            var mesh = new Mesh { name = name }; mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = dark ? scarDarkMaterial : scarMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_Intensity", owner.trailHDRIntensity); properties.SetFloat("_GlowIntensity", 2);
            renderer.SetPropertyBlock(properties);
            return new BladeLightVisual { root = root, mesh = mesh, renderer = renderer, properties = properties,
                crescentPower = owner.trailCrescentPower, glowScale = Mathf.Clamp(owner.trailGlowWidth / 0.10f, 0.5f, 1.8f) };
        }

        void DisposeBladeLight(BladeLightVisual visual)
        {
            if (visual.mesh != null) Destroy(visual.mesh);
            if (visual.root != null) Destroy(visual.root.gameObject);
        }

        void BeginScarMesh()
        { scarVertices.Clear(); scarColors.Clear(); scarUVs.Clear(); scarTriangles.Clear(); }

        void EndScarMesh(BladeLightVisual visual, int order)
        {
            visual.mesh.Clear(); visual.mesh.SetVertices(scarVertices); visual.mesh.SetColors(scarColors);
            visual.mesh.SetUVs(0, scarUVs); visual.mesh.SetTriangles(scarTriangles, 0); visual.mesh.RecalculateBounds();
            visual.renderer.sortingOrder = order;
        }

        void AddScarRibbon(BladeLightVisual visual, Vector2 origin, Vector2 direction, Vector2 side,
            float length, float thickness, float alpha, float bow, float height)
        {
            if (length <= 0.001f || thickness <= 0 || alpha <= 0) return;
            const int segments = 24;
            for (int row = 0; row <= segments; row++)
            {
                float t = row / (float)segments;
                float taper = CombatFeedbackMath.TrailCrescent(t, visual.crescentPower);
                Vector2 center = origin + direction * (length * t) + side * (Mathf.Sin(t * Mathf.PI) * bow);
                AddScarSection(visual, center, side, thickness * taper, alpha, height, row > 0);
            }
        }

        void AddScarSection(BladeLightVisual visual, Vector2 center, Vector2 side, float thickness,
            float alpha, float height, bool connect, bool dark = false)
        {
            int first = scarVertices.Count;
            for (int col = 0; col < 7; col++)
            {
                float offset = col == 0 ? -1 : col == 1 ? -0.60f : col == 2 ? -visual.coreFraction : col == 3 ? 0
                    : col == 4 ? visual.coreFraction : col == 5 ? 0.60f : 1;
                float opacity = col == 0 || col == 6 ? 0 : col == 1 || col == 5 ? 0.18f : col == 3 ? 0.95f : 0.52f;
                Color tint = col >= 2 && col <= 4 ? new Color(0.92f, 0.98f, 1, opacity * alpha)
                    : new Color(0.30f, 0.73f, 1, opacity * alpha);
                if (dark)
                {
                    offset = col == 0 ? -1 : col == 1 ? -0.85f : col == 2 ? -0.45f : col == 3 ? 0
                        : col == 4 ? 0.45f : col == 5 ? 0.85f : 1;
                    float coverage = col == 0 || col == 6 ? 0 : col == 1 || col == 5 ? 0.8f : 1;
                    tint = col == 1 || col == 5 ? new Color(0.09f, 0.11f, 0.13f, coverage * alpha)
                        : new Color(0.02f, 0.03f, 0.05f, coverage * alpha);
                }
                scarVertices.Add(visual.root.InverseTransformPoint(View.LiftPoint(center + side * (offset * thickness), height)));
                scarColors.Add(tint); scarUVs.Add(new Vector2(col >= 2 && col <= 4 ? 1 : 0, 0));
                if (!connect || col == 6) continue;
                int a = first - 7 + col, b = first + col;
                scarTriangles.Add(a); scarTriangles.Add(b); scarTriangles.Add(a + 1);
                scarTriangles.Add(a + 1); scarTriangles.Add(b); scarTriangles.Add(b + 1);
            }
        }

        void CreateSwordScarBurst(SwordScar scar)
        {
            var burst = new SwordScarBurst { scar = scar, createdFrame = Time.frameCount,
                visual = CreateBladeLight(scar.owner, "万钧 · 裂地剑爆") };
            burst.visual.coreFraction = 0.42f;
            burst.visual.properties.SetFloat("_Intensity", Mathf.Clamp(scar.owner.trailHDRIntensity * 1.3f, 1, 3));
            burst.visual.renderer.SetPropertyBlock(burst.visual.properties);
            swordScarBursts.Add(burst); DrawSwordScarBurst(burst);
            if (audioSource != null) audioSource.PlayOneShot(heavyHit, 0.9f);
            var camera = GetComponentInParent<SlashArena>().CameraRig;
            camera.Punch(0.85f); camera.Shake(0.10f);
            HitStop(0.03f);
        }

        void DrawSwordScarBurst(SwordScarBurst burst)
        {
            BeginScarMesh();
            var scar = burst.scar;
            float p = Mathf.Clamp01(burst.age / ScarBurstLife);
            // Most displacement happens in the first 65 ms; the remainder is a short debris tail.
            float launch = Mathf.Clamp01(burst.age / 0.065f);
            float expansion = 1 - Mathf.Pow(1 - launch, 3);
            float fade = Mathf.Pow(1 - p, 1.6f);
            float flash = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(burst.age / 0.075f));
            Vector2 side = new Vector2(-scar.direction.y, scar.direction.x);
            float radius = scar.width * 0.5f;
            AddScarRibbon(burst.visual, scar.origin, scar.direction, side, scar.length,
                radius * 0.82f, flash * 0.95f, 0, 0.055f);
            AddScarRibbon(burst.visual, scar.origin, scar.direction, side, scar.length,
                0.30f * burst.visual.glowScale, flash, 0, 0.08f);
            // Broken, outward cleaves replace the closed oval: each fracture strikes across the scar.
            for (int station = 0; station < 5; station++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector2 axis = (side * sign + scar.direction * (station % 2 == 0 ? 0.32f : -0.24f)).normalized;
                    Vector2 cross = new Vector2(-axis.y, axis.x);
                    Vector2 origin = scar.origin + scar.direction * (scar.length * (0.09f + station * 0.20f))
                        + axis * (radius * 0.12f * expansion);
                    AddScarRibbon(burst.visual, origin, axis, cross, radius * Mathf.Lerp(0.18f, 0.78f, expansion),
                        0.20f * burst.visual.glowScale * Mathf.Lerp(1, 0.35f, p), fade * 0.95f,
                        sign * 0.06f, 0.065f);
                }
            // Wedges rise from the ground with a hard leading edge instead of floating gently outward.
            for (int i = 0; i < 12; i++)
            {
                float sign = i % 2 == 0 ? -1 : 1;
                float t = 0.10f + (i / 2) * 0.16f;
                Vector2 point = scar.origin + scar.direction * (scar.length * t)
                    + side * (sign * radius * 0.48f * expansion);
                Vector2 axis = (side * sign + scar.direction * (i % 3 == 0 ? -0.65f : 0.45f)).normalized;
                Vector2 cross = new Vector2(-axis.y, axis.x);
                float length = Mathf.Lerp(0.16f, 0.75f, expansion);
                float rise = Mathf.Sin(Mathf.Min(1, p * 1.35f) * Mathf.PI) * (i % 3 == 0 ? 0.95f : 0.60f);
                for (int row = 0; row <= 8; row++)
                {
                    float u = row / 8f;
                    AddScarSection(burst.visual, point + axis * (length * u), cross,
                        (1 - u) * 0.12f * burst.visual.glowScale, fade * (1 - u * 0.2f),
                        0.06f + rise * u, row > 0);
                }
            }
            EndScarMesh(burst.visual, ObliqueProjection.DepthOrder(scar.origin.y + scar.direction.y * scar.length * 0.5f) + 28);
        }

        void UpdateSwordScarBursts(float dt)
        {
            if (dt <= 0) return;
            for (int i = swordScarBursts.Count - 1; i >= 0; i--)
            {
                var burst = swordScarBursts[i];
                if (burst.createdFrame != Time.frameCount) burst.age += dt;
                if (burst.visual.root == null || burst.scar.owner == null || !burst.scar.owner.IsAlive
                    || !burst.scar.owner.HasIronSword || burst.age >= ScarBurstLife)
                {
                    DisposeBladeLight(burst.visual); swordScarBursts.RemoveAt(i); continue;
                }
                DrawSwordScarBurst(burst);
            }
        }

        void UpdateSwordScars(float dt)
        {
            if (dt <= 0) return;
            for (int i = swordScars.Count - 1; i >= 0; i--)
            {
                var scar = swordScars[i];
                if (scar.visual.root == null || scar.owner == null || !scar.owner.IsAlive || !scar.owner.HasIronSword)
                {
                    DisposeBladeLight(scar.visual);
                    DisposeBladeLight(scar.dark);
                    swordScars.RemoveAt(i); continue;
                }
                scar.age += dt;
                SpawnScarDebris(scar);
                while (scar.nextTick <= Mathf.Min(scar.age, scar.settings.scarDuration) + 0.00001f)
                {
                    DamageSwordScar(scar, scar.settings.scarTickDamageRatio);
                    TotalScarTicks++;
                    scar.nextTick += scar.settings.scarTickInterval;
                }
                if (scar.age >= scar.settings.scarDuration)
                {
                    if (scar.explodes)
                    {
                        DamageSwordScar(scar, scar.settings.scarExplosionRatio, true);
                        CreateSwordScarBurst(scar);
                        TotalScarExplosions++;
                    }
                    DisposeBladeLight(scar.visual); DisposeBladeLight(scar.dark); swordScars.RemoveAt(i); continue;
                }
                DrawSwordScar(scar);
            }
        }

        void SpawnScarDebris(SwordScar scar)
        {
            Vector2 side = new Vector2(-scar.direction.y, scar.direction.x);
            while (scar.debrisSpawned < 7 && scar.length >= scar.reach * (0.08f + scar.debrisSpawned * 0.14f))
            {
                int index = scar.debrisSpawned++;
                if (particles.Count >= 180) continue;
                float sign = index % 2 == 0 ? -1 : 1;
                float t = 0.08f + index * 0.14f;
                Vector2 point = ScarFracturePoint(scar.origin, scar.direction, scar.reach,
                    0.19f * scar.visual.glowScale, scar.seed, t) + side * (sign * 0.22f);
                Color color = new Color(0.15f, 0.18f, 0.20f, 0.95f);
                float angle = ScarNoise(scar.seed, index + 50) * 180;
                var r = DemoArt.Shape("Sword scar earth chip", transform, "Blade", point,
                    new Vector2(0.12f + index % 3 * 0.025f, 0.07f), color, ObliqueProjection.DepthOrder(point.y) - 10);
                r.transform.position = View.LiftPoint(point, 0);
                r.transform.rotation = Quaternion.Euler(0, 0, angle);
                particles.Add(new Fleck { renderer = r, grounded = true, scarOwner = scar.owner, ground = point,
                    velocity = side * (sign * 0.7f), life = 0.30f, angle = angle, color = color, scale = r.transform.localScale });
            }
        }

        public void HitStop(float duration)
        {
            duration *= hitStopMultiplier;
            if (duration <= 0) return;
            if (!Frozen) savedScale = Time.timeScale;
            freezeRemaining = Mathf.Max(freezeRemaining, duration);
            Time.timeScale = 0;
        }

        public void RestoreTime()
        {
            freezeRemaining = 0;
            Time.timeScale = 1;
        }

        public void Burst(Vector2 position, Color color, int count = 10, float power = 1)
        {
            // Hard cap keeps held attacks and later waves from growing transient objects without bound.
            count = Mathf.Min(count, 180 - particles.Count);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 360f);
                var r = DemoArt.Shape("Spark", transform, "Blade", position,
                    new Vector2(Random.Range(0.08f, 0.22f), 0.045f), color, ObliqueProjection.DepthOrder(position.y) + 30);
                r.transform.position = View.LiftPoint(position, View.WeaponHeight);
                r.transform.rotation = View.Rotation * Quaternion.Euler(0, 0, angle);
                particles.Add(new Fleck { renderer = r, velocity = View.Rotation * (Vector3)(DemoArt.Direction(angle) * Random.Range(1.8f, 5f) * power),
                    life = Random.Range(0.16f, 0.40f), spin = Random.Range(-220f, 220f), angle = angle, color = color, scale = r.transform.localScale });
            }
        }

        public void Ghost(Transform player)
        {
            var appearance = player.GetComponent<HeroAppearance>();
            if (appearance != null && appearance.Continuous3D)
            {
                if (modelEchoes.Count < 16) modelEchoes.Add(new Hero360Model.Echo(appearance.ContinuousModel, transform));
                return;
            }
            if (particles.Count >= 175) return;
            var r = DemoArt.Shape("DashAfterimage", transform, "Robe", player.position,
                new Vector2(0.75f, 0.9f) * View.characterScale, new Color(0.3f, 0.94f, 0.84f, 0.3f), ObliqueProjection.DepthOrder(player.position.y) - 1);
            r.transform.position = View.LiftPoint(player.position, View.BodyLift);
            r.transform.rotation = View.Rotation;
            if (appearance != null && appearance.Ready)
            {
                r.sprite = appearance.BodySprite;
                r.flipX = appearance.Flipped;
                r.transform.localScale = Vector3.one * (HeroAppearance.BodyHeight * View.characterScale);
                r.transform.position = View.LiftPoint(player.position, View.BodyLift + HeroAppearance.FootOffset * View.characterScale);
                r.color = new Color(0.55f, 0.83f, 1f, 0.28f);
            }
            particles.Add(new Fleck { renderer = r, life = 0.24f, color = r.color, scale = r.transform.localScale });
        }

        public void Number(Vector2 position, string text, Color color)
        {
            if (texts.Count > 35) texts.RemoveAt(0);
            texts.Add(new FloatingText { position = position, text = text, color = color, born = Time.time });
        }

        void Update()
        {
            if (Frozen)
            {
                freezeRemaining -= Time.unscaledDeltaTime;
                if (freezeRemaining <= 0) Time.timeScale = savedScale;
            }
            float dt = Time.deltaTime;
            for (int i = modelEchoes.Count - 1; i >= 0; i--)
                if (!modelEchoes[i].Advance(dt)) { modelEchoes[i].Dispose(); modelEchoes.RemoveAt(i); }
            if (gathering && dt > 0)
            {
                gatherTimer -= dt;
                for (int n = 0; gatherTimer <= 0 && n < 4; n++)
                { SpawnChargeMote(); gatherTimer += 0.08f; }
                gatherTimer = Mathf.Max(0, gatherTimer);
            }
            UpdateSwordWaves(dt);
            UpdateSwordScars(dt);
            UpdateSwordScarBursts(dt);
            UpdateWhirlwinds(dt);
            UpdateCloudLights(dt);
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var ring = rings[i];
                ring.age += dt;
                if (ring.line == null || ring.age >= ring.life)
                {
                    if (ring.line != null) Destroy(ring.line.gameObject);
                    rings.RemoveAt(i);
                    continue;
                }
                DrawImpactRing(ring);
            }
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.age += dt;
                if (p.age >= p.life || p.renderer == null)
                {
                    if (p.renderer != null) Destroy(p.renderer.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                if (p.gathering)
                {
                    p.ground = Vector2.MoveTowards(p.ground, chargeTarget, (3.8f + 2 * chargePower) * dt);
                    if (!gathering || Vector2.Distance(p.ground, chargeTarget) < 0.025f)
                    {
                        Destroy(p.renderer.gameObject); particles.RemoveAt(i); continue;
                    }
                    p.renderer.transform.position = View.LiftPoint(p.ground, View.WeaponHeight);
                    p.renderer.transform.rotation = View.Rotation;
                }
                else if (p.grounded)
                {
                    p.ground += (Vector2)p.velocity * dt;
                    p.velocity *= Mathf.Exp(-10 * dt);
                    p.renderer.transform.position = View.LiftPoint(p.ground, 0);
                    p.renderer.transform.rotation = Quaternion.Euler(0, 0, p.angle);
                }
                else
                {
                    p.renderer.transform.position += p.velocity * dt;
                    p.velocity *= Mathf.Exp(-5 * dt);
                    p.angle += p.spin * dt;
                    p.renderer.transform.rotation = View.Rotation * Quaternion.Euler(0, 0, p.angle);
                }
                float t = 1 - p.age / p.life;
                p.renderer.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * t);
                p.renderer.transform.localScale = p.scale * Mathf.Lerp(0.4f, 1, t);
            }
            for (int i = texts.Count - 1; i >= 0; i--)
                if (Time.time - texts[i].born > 0.65f) texts.RemoveAt(i);
        }

        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (gatherMaterial != null) Destroy(gatherMaterial);
            if (cloudMaterial != null) Destroy(cloudMaterial);
            if (scarMaterial != null) Destroy(scarMaterial);
            if (scarDarkMaterial != null) Destroy(scarDarkMaterial);
            if (swing != null) Destroy(swing);
            if (heavySwing != null) Destroy(heavySwing);
            if (hit != null) Destroy(hit);
            if (heavyHit != null) Destroy(heavyHit);
            if (killingHit != null) Destroy(killingHit);
        }
    }
}
