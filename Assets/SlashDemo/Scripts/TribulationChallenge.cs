using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SlashDemo
{
    [DefaultExecutionOrder(100)]
    internal sealed class TribulationChallenge : MonoBehaviour
    {
        internal const int RoundCount = 10;
        const int ZonesPerRound = 9;
        const float StartingRadius = 2.2f;
        const float RadiusIncrease = .04f;
        const float WarningDuration = .8f;
        const float ImpactDuration = .38f;
        const float RoundInterval = .55f;
        const int GroundOrder = -65;
        const int SkyOrder = 3000;

        enum Phase { Warning, Impact, BetweenRounds }

        sealed class StrikeZone
        {
            internal readonly Transform Root;
            readonly SpriteRenderer fill;
            readonly Transform impact;
            readonly LineRenderer rim, boltGlow, bolt, shockwave, echoWave;
            readonly LineRenderer[] branches = new LineRenderer[3];
            readonly Vector3[] boltPoints = new Vector3[9];
            float radius;

            internal StrikeZone(Transform parent)
            {
                Root = new GameObject("落雷区域").transform;
                Root.SetParent(parent, false);
                fill = DemoArt.Shape("预警范围", Root, "Disk", Vector2.zero, Vector2.one, Color.clear, GroundOrder);
                rim = DemoArt.Ring("落雷边界", Root, 1, Color.clear, .04f, GroundOrder + 1);

                impact = new GameObject("落雷特效").transform;
                impact.SetParent(Root, false);
                shockwave = DemoArt.Ring("雷击冲击波", impact, 1, Color.clear, .09f, GroundOrder + 3);
                echoWave = DemoArt.Ring("冲击波余纹", impact, 1, Color.clear, .042f, GroundOrder + 2);
                boltGlow = DemoArt.Line("电光", impact, boltPoints, Color.clear, .36f, SkyOrder + 10);
                bolt = DemoArt.Line("雷劫", impact, boltPoints, Color.clear, .12f, SkyOrder + 12);
                var branchPoints = new Vector3[4];
                for (int i = 0; i < branches.Length; i++)
                {
                    branches[i] = DemoArt.Line("分叉电弧", impact, branchPoints, Color.clear, .065f, SkyOrder + 11);
                    branches[i].endWidth = .012f;
                }
            }

            internal void Place(Vector2 point, float strikeRadius)
            {
                radius = strikeRadius;
                Root.localPosition = point;
                float diskDiameter = fill.sprite.bounds.size.x;
                fill.transform.localScale = Vector3.one * (radius * 2 / diskDiameter);
                rim.transform.localScale = new Vector3(radius, radius, 1);
                impact.gameObject.SetActive(false);
                Root.gameObject.SetActive(true);
            }

            internal void Warn(float progress)
            {
                fill.color = new Color(.94f, .22f, .08f, Mathf.Lerp(.14f, .37f, progress));
                SetColor(rim, new Color(1, .47f, .19f, Mathf.Lerp(.55f, .95f, progress)));
            }

            internal void Strike(ObliqueView view)
            {
                for (int i = 1; i < boltPoints.Length; i++)
                {
                    float height = i * 1.05f;
                    boltPoints[i] = view.LiftPoint(new Vector2(Random.Range(-.46f, .46f), 0), height);
                }
                bolt.SetPositions(boltPoints);
                boltGlow.SetPositions(boltPoints);
                for (int i = 0; i < branches.Length; i++)
                {
                    int joint = 2 + i * 2;
                    float side = i % 2 == 0 ? 1 : -1;
                    Vector3 origin = boltPoints[joint];
                    Vector3 down = (boltPoints[joint - 1] - origin) * .55f;
                    Vector3 outward = Vector3.right * side * (.42f + i * .10f);
                    var branch = branches[i];
                    branch.SetPosition(0, origin);
                    branch.SetPosition(1, origin + down + outward);
                    branch.SetPosition(2, origin + down * 2 + outward * .80f);
                    branch.SetPosition(3, origin + down * 3 + outward * 2.1f);
                }
                impact.gameObject.SetActive(true);
            }

            internal void FadeImpact(float strength)
            {
                float progress = 1 - strength;
                float flash = Mathf.Clamp01(1 - progress * 3);
                float flicker = .22f * Mathf.Clamp01(1 - Mathf.Abs(progress - .39f) * 10);
                float arc = flash + flicker;
                fill.color = new Color(.56f, .75f, 1, .13f * strength + .24f * flash);
                SetColor(rim, new Color(.71f, .85f, 1, .60f * strength));
                SetColor(bolt, new Color(.92f, .98f, 1, arc));
                SetColor(boltGlow, new Color(.31f, .56f, 1, .72f * arc));
                foreach (var branch in branches)
                {
                    branch.startColor = new Color(.63f, .83f, 1, .90f * arc);
                    branch.endColor = new Color(.42f, .60f, 1, .14f * arc);
                }

                float waveRadius = Mathf.Lerp(.12f, 1, Mathf.Sqrt(progress)) * radius;
                shockwave.transform.localScale = new Vector3(waveRadius, waveRadius, 1);
                SetColor(shockwave, new Color(.69f, .91f, 1, .94f * strength * strength));

                float echoProgress = Mathf.Clamp01((progress - .18f) / .82f);
                float echoRadius = Mathf.Lerp(.17f, .83f, echoProgress) * radius;
                echoWave.transform.localScale = new Vector3(echoRadius, echoRadius, 1);
                float echoAlpha = Mathf.Sin(echoProgress * Mathf.PI) * .68f;
                SetColor(echoWave, new Color(.49f, .68f, 1, echoAlpha));
            }

            static void SetColor(LineRenderer line, Color color)
                => line.startColor = line.endColor = color;
        }

        readonly StrikeZone[] zones = new StrikeZone[ZonesPerRound];
        readonly SpriteRenderer[] clouds = new SpriteRenderer[10];
        SlashArena arena;
        BeastAttackParticles sparks;
        Action<bool> onFinish;
        Texture2D cloudTexture;
        Sprite cloudSprite;
        Rect strikeGround;
        Vector2 skyCenter;
        Phase phase;
        float remaining;
        float age;
        float radius;
        internal int Round { get; private set; }

        internal static TribulationChallenge Begin(SlashArena arena, TravelMapEnvironment map, Action<bool> onFinish)
        {
            var challenge = new GameObject("渡劫").AddComponent<TribulationChallenge>();
            challenge.transform.SetParent(map.transform, false);
            challenge.arena = arena;
            challenge.onFinish = onFinish;
            challenge.sparks = BeastAttackParticles.For(arena, challenge.transform);
            challenge.strikeGround = map.Definition.GroundRect(new Rect(420, 315, 690, 375));
            var center = TravelMaps.TribulationCenter;
            challenge.skyCenter = map.Definition.Ground(center.x, center.y);
            challenge.CreateClouds();
            for (int i = 0; i < ZonesPerRound; i++)
                challenge.zones[i] = new StrikeZone(challenge.transform);
            challenge.StartRound();
            return challenge;
        }

        internal static float RadiusForRound(int round) => StartingRadius * (1 + (round - 1) * RadiusIncrease);

        void StartRound()
        {
            Round++;
            radius = RadiusForRound(Round);
            phase = Phase.Warning;
            remaining = WarningDuration;
            zones[0].Place(arena.Player.transform.position, radius);
            for (int i = 1; i < zones.Length; i++)
            {
                Vector2 point;
                do
                {
                    point = new Vector2(Random.Range(strikeGround.xMin, strikeGround.xMax),
                        Random.Range(strikeGround.yMin, strikeGround.yMax));
                }
                while (NearOtherCenter(point, i));
                zones[i].Place(point, radius);
            }
            foreach (var zone in zones) zone.Warn(0);
        }

        bool NearOtherCenter(Vector2 point, int placedCount)
        {
            for (int i = 0; i < placedCount; i++)
                if ((point - (Vector2)zones[i].Root.localPosition).sqrMagnitude < 1)
                    return true;
            return false;
        }

        void Update()
        {
            if (!arena.Player.IsAlive)
            {
                Finish(false);
                return;
            }
            if (!arena.Paused && !arena.Effects.Frozen) Advance(Time.deltaTime);
        }

        void Advance(float dt)
        {
            age += dt;
            remaining -= dt;
            switch (phase)
            {
                case Phase.Warning:
                    if (remaining <= 0) Strike();
                    else
                        foreach (var zone in zones) zone.Warn(1 - remaining / WarningDuration);
                    break;
                case Phase.Impact:
                    if (remaining > 0)
                    {
                        foreach (var zone in zones) zone.FadeImpact(remaining / ImpactDuration);
                    }
                    else if (Round == RoundCount)
                    {
                        Finish(true);
                        return;
                    }
                    else
                    {
                        foreach (var zone in zones) zone.Root.gameObject.SetActive(false);
                        phase = Phase.BetweenRounds;
                        remaining = RoundInterval;
                    }
                    break;
                case Phase.BetweenRounds:
                    if (remaining <= 0) StartRound();
                    break;
            }
            UpdateClouds(phase == Phase.Impact ? remaining / ImpactDuration : 0);
        }

        void Strike()
        {
            phase = Phase.Impact;
            remaining = ImpactDuration;
            Vector2 playerPosition = arena.Player.transform.position;
            float nearestStrikeSqr = float.PositiveInfinity;
            foreach (var zone in zones)
            {
                zone.Strike(arena.View);
                zone.FadeImpact(1);
                Vector2 ground = zone.Root.position;
                sparks.LightningImpact(ground, radius);
                nearestStrikeSqr = Mathf.Min(nearestStrikeSqr, (playerPosition - ground).sqrMagnitude);
            }
            if (nearestStrikeSqr <= radius * radius) arena.Player.TakeTribulationDamage();
            else
            {
                float shakeRange = radius * 3;
                arena.CameraRig.Shake(.045f * Mathf.Clamp01(1 - nearestStrikeSqr / (shakeRange * shakeRange)));
            }
        }

        void CreateClouds()
        {
            const int width = 96, height = 48;
            cloudTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var uv = new Vector2(x / (width - 1f), y / (height - 1f));
                    float edge = 1 - ((uv - Vector2.one * .5f) * 2).sqrMagnitude;
                    float mass = Mathf.PerlinNoise(uv.x * 4 + 6, uv.y * 3 + 2) * .7f
                        + Mathf.PerlinNoise(uv.x * 11, uv.y * 9 + 4) * .3f;
                    float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge * 1.6f))
                        * Mathf.Clamp01((mass - .22f) * 1.8f);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            cloudTexture.SetPixels32(pixels);
            cloudTexture.Apply(false, true);
            cloudSprite = Sprite.Create(cloudTexture, new Rect(0, 0, width, height),
                Vector2.one * .5f, 8, 0, SpriteMeshType.FullRect);

            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = new GameObject("雷云").AddComponent<SpriteRenderer>();
                cloud.transform.SetParent(transform, false);
                cloud.transform.rotation = arena.WorldCamera.transform.rotation;
                cloud.transform.localScale = new Vector3(i % 2 == 0 ? -1 : 1, .63f + i % 3 * .12f, 1);
                cloud.sprite = cloudSprite;
                cloud.sharedMaterial = DemoArt.Material;
                cloud.sortingOrder = SkyOrder + i;
                clouds[i] = cloud;
            }
            UpdateClouds(0);
        }

        void UpdateClouds(float lightning)
        {
            Color shade = Color.Lerp(new Color(.16f, .19f, .25f, .81f),
                new Color(.51f, .59f, .77f, .88f), lightning * lightning);
            for (int i = 0; i < clouds.Length; i++)
            {
                float drift = Mathf.Sin(age * .34f + i * 1.63f) * 1.2f;
                Vector2 ground = skyCenter + new Vector2((i % 5 - 2) * 7 + (i / 5) * 2 + drift, i < 5 ? -4 : 2);
                clouds[i].transform.position = arena.View.LiftPoint(ground, 4.7f + i % 3 * .45f);
                clouds[i].color = shade;
            }
        }

        void Finish(bool survived)
        {
            sparks.Clear();
            gameObject.SetActive(false);
            Destroy(gameObject);
            onFinish(survived);
        }

        void OnDestroy()
        {
            if (cloudSprite != null) Destroy(cloudSprite);
            if (cloudTexture != null) Destroy(cloudTexture);
        }
    }
}
