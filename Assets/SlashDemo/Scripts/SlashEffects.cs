using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    public sealed class SlashEffects : MonoBehaviour
    {
        sealed class Fleck
        {
            public SpriteRenderer renderer;
            public Vector3 velocity;
            public float age, life, spin, angle;
            public Color color;
            public Vector3 scale;
            public bool gathering;
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
        sealed class SwordWave
        {
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
            ClearSwordWaves();
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

        public void LaunchSwordWave(Vector2 position, Vector2 direction, SlashTuningSettings.HeavyAttackSettings settings)
        {
            var root = new GameObject("Charged sword wave").transform;
            root.SetParent(transform, false);
            var wave = new SwordWave { root = root, position = position,
                direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right,
                settings = settings.Clone() };
            int points = SwordMotion.WaveArcSegments + 1;
            wave.outer = DemoArt.Line("Ice glow", root, new Vector3[points], new Color(0.3f, 0.73f, 1, 0.55f), 0.20f, 0);
            wave.core = DemoArt.Line("White edge", root, new Vector3[points], new Color(0.9f, 0.98f, 1), 0.065f, 1);
            var taper = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 0.7f),
                new Keyframe(0.5f, 1), new Keyframe(0.8f, 0.7f), new Keyframe(1, 0));
            wave.outer.widthCurve = wave.core.widthCurve = taper;
            wave.outer.widthMultiplier = 0.20f;
            wave.core.widthMultiplier = 0.065f;
            waves.Add(wave);
            DrawSwordWave(wave);
        }

        void DrawSwordWave(SwordWave wave)
        {
            float progress = Mathf.Clamp01(wave.traveled / Mathf.Max(0.01f, wave.settings.distance));
            float fade = CombatFeedbackMath.WaveFade(progress);
            wave.outer.widthMultiplier = 0.20f * Mathf.Lerp(1, 1.35f, progress);
            wave.core.widthMultiplier = 0.065f * Mathf.Lerp(1, 1.10f, progress);
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
                        enemy.TakeHit(wave.settings.damage, wave.direction, wave.settings.knockback, true);
                    }
                    wave.position = next;
                    wave.traveled += step;
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
            if (swing != null) Destroy(swing);
            if (heavySwing != null) Destroy(heavySwing);
            if (hit != null) Destroy(hit);
            if (heavyHit != null) Destroy(heavyHit);
            if (killingHit != null) Destroy(killingHit);
        }
    }
}
