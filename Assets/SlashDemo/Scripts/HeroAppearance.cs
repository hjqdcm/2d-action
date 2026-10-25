using UnityEngine;

namespace SlashDemo
{
    public static class HeroFacingMath
    {
        // Clockwise on the atlas: S, SE, E, NE, N, NW, W, SW.
        public static int Direction(Vector2 facing, int previous = -1, float elevation = 45)
        {
            if (facing.sqrMagnitude < 0.0001f) return previous >= 0 ? previous : 0;
            Vector2 screen = ObliqueProjection.ProjectGround(facing, elevation);
            float angle = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg + 90;
            if (previous >= 0 && previous < 8 && Mathf.Abs(Mathf.DeltaAngle(previous * 45, angle)) <= 27.5f)
                return previous;
            return Mathf.RoundToInt(Mathf.Repeat(angle, 360) / 45) % 8;
        }

        // v5 cloth attachment points, in visual-local space after boot registration.
        // Painted three-quarter/profile views have asymmetric shoulders, so a symmetric
        // sinusoidal approximation can place an arm outside the actual torso.
        static readonly Vector2[] LeftShoulders = {
            new Vector2(-0.1410f, 0.5540f), new Vector2(-0.1093f, 0.5411f),
            new Vector2(-0.0646f, 0.5058f), new Vector2(-0.0596f, 0.5469f),
            new Vector2(-0.1698f, 0.5358f), new Vector2(-0.0913f, 0.5218f),
            new Vector2(-0.0264f, 0.5406f), new Vector2(-0.0511f, 0.5235f)
        };
        static readonly Vector2[] RightShoulders = {
            new Vector2(0.1547f, 0.5540f), new Vector2(0.1448f, 0.5575f),
            new Vector2(0.0124f, 0.5388f), new Vector2(0.0873f, 0.5524f),
            new Vector2(0.1670f, 0.5358f), new Vector2(0.0542f, 0.5561f),
            new Vector2(0.0903f, 0.4906f), new Vector2(0.1698f, 0.4876f)
        };

        public static Vector3 Shoulder(int direction, bool left)
        {
            return (left ? LeftShoulders : RightShoulders)[Mathf.Clamp(direction, 0, 7)];
        }
    }

    public static class HeroArmMath
    {
        // Left/right here names the screen shoulder, not the character's anatomical hand.
        public static bool RightHandOnScreenLeft(int facingIndex) => facingIndex <= 2 || facingIndex >= 7;

        public static float SingleHandWeight(bool attacking, int combo, bool heavy, bool ready,
            float progress, float windup)
        {
            if (!attacking || combo < 0 || combo > 1 || heavy || !ready) return 0;
            float enter = Mathf.SmoothStep(0, 1, progress / Mathf.Max(0.01f, windup * 0.65f));
            float leave = Mathf.SmoothStep(0, 1, (1 - progress) / 0.12f);
            return enter * leave;
        }

        public static Vector3 RelaxedOffHand(Vector3 shoulder, Vector3 up, bool left, float scale)
        {
            // Let the arm hang beside the robe, with a small gap from the body.
            return shoulder - up * (0.40f * scale) + Vector3.right * ((left ? -0.035f : 0.035f) * scale);
        }

        public struct Orders
        {
            public int sword, upperArm, forearm, hand, trail;
        }

        public static Orders Sorting(bool back, int bodyOrder, int weaponCount)
        {
            int foreground = Mathf.Max(18, bodyOrder + 1);
            int upper = foreground + weaponCount;
            return new Orders {
                sword = back ? bodyOrder - weaponCount - 8 : foreground,
                upperArm = upper,
                forearm = back ? bodyOrder - 6 : upper + 2,
                hand = back ? bodyOrder - 2 : upper + 4,
                trail = back ? bodyOrder - 8 : upper + 6
            };
        }

        public static Vector3 Elbow(Vector3 shoulder, Vector3 hand, Vector3 bodyCenter, Vector3 up,
            bool left, bool back, float scale)
        {
            Vector3 elbow = Vector3.Lerp(shoulder, hand, 0.52f) - up * (0.025f * scale)
                + Vector3.right * ((left ? -0.035f : 0.035f) * scale);
            if (back)
            {
                // Bend around the sides of the ribcage; only the inner forearms are hidden by the back.
                float clearance = Mathf.Max(Mathf.Abs(shoulder.x - bodyCenter.x) + 0.10f * scale, 0.26f * scale);
                elbow.x = left ? Mathf.Min(elbow.x, bodyCenter.x - clearance) : Mathf.Max(elbow.x, bodyCenter.x + clearance);
                float drop = Vector3.Dot(shoulder - elbow, up);
                if (drop < 0.09f * scale) elbow -= up * (0.09f * scale - drop);
            }
            return elbow;
        }
    }

    // Slice the intact transparent atlas in Unity; combat still uses SwordMotion coordinates.
    [DisallowMultipleComponent]
    public sealed class HeroAppearance : MonoBehaviour
    {
        public const float BodyHeight = 1.35f, FootOffset = -0.44f;
        public const string DirectionalAtlasResource = "SlashDemo/WhiteSwordsmanDirections-v5";
        const float SwordPivotX = 0.25f;
        static Sprite front, back, sleeve, shoulderCloth, sword;
        static Sprite[] directions;
        static bool warned;
        SpriteRenderer body, weapon;
        Material weaponMaterial;
        Hero360Model continuousModel;
        HeroWalkMesh locomotion;
        Vector2 previousGround;
        bool facingBack;
        public bool Ready => continuousModel != null || body != null;
        public bool Continuous3D => continuousModel != null;
        public Hero360Model ContinuousModel => continuousModel;
        public Sprite BodySprite => body != null ? body.sprite : null;
        public bool Flipped => body != null && body.flipX;
        public HeroWalkMesh Locomotion => locomotion;
        public float WalkBob => continuousModel != null ? continuousModel.Bob : locomotion != null ? locomotion.Bob : 0;
        public bool HasDirectionalArt => !Continuous3D && directions != null && directions.Length == 8;
        public int FacingIndex { get; private set; } = -1;
        public bool IsBackFacing => continuousModel != null ? continuousModel.BackFacing : HasDirectionalArt ? FacingIndex >= 3 && FacingIndex <= 5 : facingBack;
        public int BodySortingOrder => continuousModel != null ? continuousModel.Renderer.sortingOrder : body != null ? body.sortingOrder : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            // Runs for every Play session, including when domain reload is disabled.
            if (front != null) Destroy(front);
            if (back != null) Destroy(back);
            if (sleeve != null) Destroy(sleeve);
            if (shoulderCloth != null) Destroy(shoulderCloth);
            if (sword != null) Destroy(sword);
            if (directions != null)
                foreach (var frame in directions) if (frame != null) Destroy(frame);
            front = back = sleeve = shoulderCloth = sword = null;
            directions = null;
            warned = false;
        }

        static bool LoadAtlas()
        {
            if (front != null && back != null && sleeve != null && shoulderCloth != null && sword != null) return true;
            var texture = Resources.Load<Texture2D>("SlashDemo/WhiteSwordsmanAtlas");
            if (texture == null || !texture.isReadable)
            {
                if (!warned)
                    Debug.LogWarning("WhiteSwordsmanAtlas could not load. Check Console for asset import errors and enable Read/Write on the texture. The next Play session will retry automatically.");
                warned = true;
                return false;
            }
            warned = false;
            var pixels = texture.GetPixels32();
            front = Slice(texture, pixels, new Rect(0.055f, 0.005f, 0.445f, 0.66f), new Vector2(0.60f, 0), false, "Hero front");
            back = Slice(texture, pixels, new Rect(0.56f, 0.005f, 0.38f, 0.66f), new Vector2(0.51f, 0), false, "Hero back");
            sleeve = Slice(texture, pixels, new Rect(0.035f, 0.715f, 0.44f, 0.25f), new Vector2(0.5f, 0.71f), true, "White silk sleeve");
            if (sleeve != null)
            {
                // Use the continuous embroidered cloth between the sleeve's two painted hollow cuffs.
                Rect cloth = sleeve.rect;
                float left = Mathf.Round(cloth.xMin + cloth.width * 0.22f);
                float right = Mathf.Round(cloth.xMin + cloth.width * 0.78f);
                shoulderCloth = Sprite.Create(texture, new Rect(left, cloth.y, right - left, cloth.height),
                    new Vector2(0.5f, 0.71f), right - left, 0, SpriteMeshType.FullRect);
                shoulderCloth.name = "White silk shoulder";
            }
            sword = Slice(texture, pixels, new Rect(0.505f, 0.78f, 0.485f, 0.13f), new Vector2(SwordPivotX, 0.5f), true, "Ice jian");
            LoadDirections();
            return front != null && back != null && sleeve != null && sword != null;
        }

        static void LoadDirections()
        {
            var texture = Resources.Load<Texture2D>(DirectionalAtlasResource);
            if (texture == null || !texture.isReadable) return; // The existing two-view hero remains a fallback.
            string[] names = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };
            var pixels = texture.GetPixels32();
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                int column = i % 4, row = i / 4;
                float x = Mathf.Round(column * texture.width / 4f);
                float right = Mathf.Round((column + 1) * texture.width / 4f);
                float bottom = Mathf.Round((1 - row) * texture.height / 2f);
                float top = Mathf.Round((2 - row) * texture.height / 2f);
                frames[i] = SliceDirection(texture, pixels, new Rect(x, bottom, right - x, top - bottom));
                if (frames[i] == null)
                {
                    foreach (var frame in frames) if (frame != null) Destroy(frame);
                    Debug.LogWarning("Directional hero atlas has an empty cell; using the previous appearance.");
                    return;
                }
                frames[i].name = "Hero " + names[i];
            }
            directions = frames;
        }

        static Sprite SliceDirection(Texture2D texture, Color32[] pixels, Rect cell)
        {
            int left = (int)cell.xMin, right = (int)cell.xMax, bottom = (int)cell.yMin, top = (int)cell.yMax;
            int minX = right, maxX = left, minY = top, maxY = bottom;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                    if (pixels[y * texture.width + x].a > 128)
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxX <= minX || maxY <= minY) return null;
            // Register the actual boots, rather than the asymmetrical robe or an approximate grid center.
            int footLeft = maxX, footRight = minX;
            for (int y = minY; y <= minY + (maxY - minY) / 10; y++)
                for (int x = minX; x <= maxX; x++)
                    if (pixels[y * texture.width + x].a > 128)
                    { footLeft = Mathf.Min(footLeft, x); footRight = Mathf.Max(footRight, x); }
            float anchorX = (footLeft + footRight) * 0.5f;
            int height = maxY - minY + 1;
            int rectLeft = Mathf.Max(left, minX - 3), rectBottom = Mathf.Max(bottom, minY - 3);
            var rect = new Rect(rectLeft, rectBottom, Mathf.Min(right, maxX + 4) - rectLeft, Mathf.Min(top, maxY + 4) - rectBottom);
            return Sprite.Create(texture, rect, new Vector2((anchorX - rect.xMin) / rect.width,
                (minY - rect.yMin) / rect.height), height, 0, SpriteMeshType.FullRect);
        }

        static Sprite Slice(Texture2D texture, Color32[] pixels, Rect area, Vector2 pivot, bool unitWidth, string name)
        {
            int left = Mathf.RoundToInt(area.xMin * texture.width), right = Mathf.RoundToInt(area.xMax * texture.width);
            int bottom = Mathf.RoundToInt((1 - area.yMax) * texture.height), top = Mathf.RoundToInt((1 - area.yMin) * texture.height);
            int minX = right, maxX = left, minY = top, maxY = bottom;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxX < minX || maxY < minY) return null;
            var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var sprite = Sprite.Create(texture, rect, pivot, unitWidth ? rect.width : rect.height, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        public bool Initialize(Transform model, bool useContinuous3D = false)
        {
            if (Ready) return true;
            if (useContinuous3D)
            {
                var arena = GetComponentInParent<SlashArena>();
                if (arena != null)
                {
                    try { continuousModel = new Hero360Model(transform, arena.View); }
                    catch (System.Exception error) { Debug.LogError("360 model could not initialize; using the sprite hero. " + error.Message, this); }
                    if (continuousModel != null)
                    {
                        foreach (Transform child in model) if (child.name != "Shadow") child.gameObject.SetActive(false);
                        return true;
                    }
                }
            }
            if (!LoadAtlas()) return false;
            // Also upgrades scenes baked with the original geometric hero.
            foreach (Transform child in model)
                if (child.name != "Shadow") child.gameObject.SetActive(false);
            var go = new GameObject("White-haired swordsman");
            go.transform.SetParent(model, false);
            go.transform.localPosition = new Vector3(0, FootOffset, 0);
            go.transform.localScale = Vector3.one * BodyHeight;
            body = go.AddComponent<SpriteRenderer>();
            body.sprite = HasDirectionalArt ? directions[0] : front;
            body.sharedMaterial = DemoArt.Material;
            locomotion = go.AddComponent<HeroWalkMesh>();
            try { locomotion.Initialize(body); }
            catch (System.Exception error)
            {
                // Walking is optional presentation: its initialization cannot block player controls.
                locomotion.UseStaticBody();
                Debug.LogError("Hero walking could not initialize; using the static character while gameplay continues. " + error.Message, this);
            }
            previousGround = transform.position;
            return true;
        }

        public void AdvanceLocomotion(Vector2 ground, float dt, bool dashing, ObliqueView view)
        {
            if (continuousModel != null) { continuousModel.AdvanceWalk(ground, dt, dashing); return; }
            Vector2 delta = ground - previousGround;
            previousGround = ground;
            if (locomotion != null) locomotion.Advance(delta, dt, dashing, view);
        }

        public void ConfigureWeapon(Transform pivot, params SpriteRenderer[] sleeves)
        {
            if (Continuous3D) return;
            if (!Ready) return;
            foreach (var renderer in pivot.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
            var go = new GameObject("Ice-blue jian");
            go.transform.SetParent(pivot, false);
            weapon = go.AddComponent<SpriteRenderer>();
            weaponMaterial = new Material(DemoArt.Material) { name = "Charged sword HDR tint", hideFlags = HideFlags.DontSave };
            weapon.sharedMaterial = weaponMaterial;
            weapon.sprite = sword;
            for (int i = 0; i < sleeves.Length; i++)
            {
                sleeves[i].sprite = i % 2 == 0 && shoulderCloth != null ? shoulderCloth : sleeve;
                sleeves[i].color = Color.white;
            }
        }

        public void SetWeaponCharge(float progress)
        {
            if (continuousModel != null) { continuousModel.SetCharge(progress); return; }
            if (weaponMaterial == null) return;
            float brightness = Mathf.Lerp(1, 1.8f, Mathf.Clamp01(progress));
            weaponMaterial.SetColor("_Color", new Color(brightness, brightness, brightness, 1));
        }

        void OnDestroy()
        {
            if (continuousModel != null) continuousModel.Dispose();
            if (weaponMaterial == null) return;
            if (Application.isPlaying) Destroy(weaponMaterial); else DestroyImmediate(weaponMaterial);
        }

        public void SetFacing(Vector2 facing, float bladeLength, float elevation = 45)
        {
            if (continuousModel != null) { continuousModel.SetFacing(facing, elevation); return; }
            if (!Ready) return;
            if (HasDirectionalArt)
            {
                FacingIndex = HeroFacingMath.Direction(facing, FacingIndex, elevation);
                body.sprite = directions[FacingIndex];
                body.flipX = false;
            }
            else
            {
                if (facing.y > 0.45f) facingBack = true;
                else if (facing.y < 0.20f) facingBack = false;
                body.sprite = facingBack ? back : front;
                if (Mathf.Abs(facing.x) > 0.05f) body.flipX = facing.x < 0;
            }
            if (weapon != null)
                weapon.transform.localScale = new Vector3(bladeLength / (1 - SwordPivotX), 1.4f, 1);
        }

        public Vector3 Shoulder(bool left)
        {
            if (HasDirectionalArt)
            {
                Vector3 anchor = HeroFacingMath.Shoulder(Mathf.Max(0, FacingIndex), left);
                if (locomotion != null && locomotion.enabled)
                {
                    Vector3 local = body.transform.InverseTransformPoint(body.transform.parent.TransformPoint(anchor));
                    anchor = body.transform.parent.InverseTransformPoint(body.transform.TransformPoint(locomotion.PosePoint(local)));
                }
                return anchor;
            }
            float x = facingBack ? (left ? -0.15f : 0.08f) : (left ? -0.20f : 0.04f);
            return new Vector3(Flipped ? -x : x, 0.61f, 0);
        }

        public void SetCombatPose(SwordMotion.Pose pose, bool attacking, bool dashing, float dt, Vector2 facing, ObliqueView view)
        {
            if (continuousModel != null) { continuousModel.SetBodyPose(pose, attacking, view); return; }
            if (locomotion != null && HasDirectionalArt)
            {
                locomotion.Attacking = attacking;
                if (attacking && pose.kinetic)
                {
                    Vector2 hip = ObliqueProjection.ProjectGround(SwordMotion.ToWorld(pose.bodyOffset, facing), view.elevation);
                    hip.y += pose.sink;
                    locomotion.SetCombatPose(pose, hip / BodyHeight, facing.x < -0.05f ? -1 : 1, dt);
                }
                else locomotion.SetCombatPose(attacking ? pose.lean : 0, attacking ? pose.bodyOffset.x : 0, dashing, dt);
            }
        }

        public static void FitSleeve(SpriteRenderer renderer, Vector3 shoulder, Vector3 hand, ObliqueView view, float width,
            float rootOverlap = 0)
        {
            Vector3 from = renderer.transform.parent.InverseTransformPoint(shoulder);
            Vector3 to = renderer.transform.parent.InverseTransformPoint(hand);
            Vector3 rootDirection = Vector3.right * (to.x - from.x) + view.Up * Vector3.Dot(to - from, view.Up);
            if (rootDirection.sqrMagnitude > 0.000001f) from -= rootDirection.normalized * rootOverlap;
            Vector3 direction = to - from;
            float x = direction.x, y = Vector3.Dot(direction, view.Up);
            float length = new Vector2(x, y).magnitude;
            width = Mathf.Min(width, length * 2);
            // The shoulder strip excludes both dark cuffs; its end-to-end slope spans 56% of the source.
            bool closedShoulder = renderer.sprite == shoulderCloth;
            float socketY = -0.22f * (closedShoulder ? 0.56f / 0.94f : 1) * width;
            float socketX = Mathf.Sqrt(Mathf.Max(0, length * length - socketY * socketY));
            renderer.transform.localPosition = (from + to) * 0.5f;
            renderer.transform.localRotation = view.Rotation * Quaternion.Euler(0, 0,
                (Mathf.Atan2(y, x) - Mathf.Atan2(socketY, socketX)) * Mathf.Rad2Deg);
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(socketX / ((closedShoulder ? 1 : 0.94f) * size.x), width / size.y, 1);
        }
    }
}
