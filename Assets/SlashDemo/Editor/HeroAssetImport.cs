using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlashDemo.Editor
{
    // Validate the actual imported texture and assembled sprites, beyond C# compilation.
    public static class HeroAssetImport
    {
        const string AtlasPath = "Assets/SlashDemo/Resources/SlashDemo/WhiteSwordsmanAtlas.png";
        const string DirectionalAtlasPath = "Assets/SlashDemo/Resources/SlashDemo/WhiteSwordsmanDirections-v5.png";

        [InitializeOnLoadMethod]
        static void ScheduleValidation()
        {
            EditorApplication.delayCall += ValidateWhenIdle;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ValidateWhenIdle;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ValidateLivePlayer;
        }

        static void ValidateLivePlayer()
        {
            if (!EditorApplication.isPlaying) return;
            var live = UnityEngine.Object.FindAnyObjectByType<SlashPlayer>();
            if (live == null) return; // The user may be playing another scene.
            if (!live.IsAlive || live.GetComponent<Rigidbody2D>() == null ||
                live.transform.Find("Weapon Arms") == null || live.transform.Find("Blade Trail Mesh") == null)
            {
                Debug.LogError("SLASH_PLAYER_INIT_FAIL: player initialization did not finish; inspect the first exception in Console.");
                return;
            }
            try { ValidateArmRig(live); }
            catch (Exception error)
            {
                Debug.LogError("SLASH_ARM_RIG_FAIL: " + error.Message);
                return;
            }
            Debug.Log("SLASH_PLAYER_INIT_OK: health, physics body and weapon initialization completed.");
        }

        static void ValidateArmRig(SlashPlayer player)
        {
            var hero = player.GetComponent<HeroAppearance>();
            if (hero == null || !hero.Ready) return;
            if (hero.Continuous3D)
            {
                var mesh = hero.ContinuousModel.Mesh;
                Require(mesh != null && mesh.vertexCount > 100 && mesh.bounds.size.z > 0.1f,
                    "Continuous hero has no volumetric geometry.");
                Require(hero.ContinuousModel.Renderer.enabled && !hero.HasDirectionalArt,
                    "Continuous hero is still using directional sprites.");
                foreach (var sprite in player.transform.Find("Weapon Arms").GetComponentsInChildren<SpriteRenderer>())
                    Require(!sprite.enabled, "Flat arms must be hidden when the 3D mesh is active.");
                Require(!ShaderUtil.ShaderHasError(hero.ContinuousModel.Renderer.sharedMaterial.shader),
                    "360 hero shader has compilation errors.");
                return;
            }
            var rig = player.transform.Find("Weapon Arms");
            Require(rig != null, "Missing arm rig.");
            int bodyOrder = hero.BodySortingOrder;
            foreach (string name in new[] { "Left upper arm", "Right upper arm", "Left forearm", "Right forearm", "Grip hand", "Support hand" })
            {
                var part = rig.Find(name);
                Require(part != null, "Missing arm part: " + name);
                var renderer = part.GetComponent<SpriteRenderer>();
                Require(renderer != null && renderer.enabled && part.gameObject.activeInHierarchy,
                    "Arm part is disabled: " + name);
                bool inFront = !hero.IsBackFacing || name.EndsWith("upper arm");
                Require(inFront ? renderer.sortingOrder > bodyOrder : renderer.sortingOrder < bodyOrder,
                    "Arm part has the wrong body occlusion for this view: " + name);
            }
        }

        static void ValidateWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { ValidateHero(); }
            catch (Exception error) { Debug.LogError("SLASH_HERO_IMPORT_FAIL: " + error.Message); }
        }

        [MenuItem("Tools/Slash Demo/Validate Hero Appearance")]
        public static void ValidateHero()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var live = UnityEngine.Object.FindAnyObjectByType<SlashPlayer>();
                Require(live != null && live.GetComponent<HeroAppearance>() != null && live.GetComponent<HeroAppearance>().Ready,
                    "The live hero is still using its fallback appearance. Stop Play and run this check again.");
                ValidateArmRig(live);
                Debug.Log("SLASH_HERO_LIVE_OK: white-haired swordsman is active.");
                return;
            }
            Require(File.Exists(AtlasPath), "Missing atlas: " + AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            Require(importer != null, "Atlas import failed. Check the .meta YAML in Console.");
            if (!importer.isReadable || importer.npotScale != TextureImporterNPOTScale.None || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency)
            {
                importer.isReadable = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var texture = Resources.Load<Texture2D>("SlashDemo/WhiteSwordsmanAtlas");
            Require(texture != null && texture.isReadable && texture.width == 1254 && texture.height == 1254,
                "Atlas could not load with its original dimensions and readable pixels.");
            Require(File.Exists(DirectionalAtlasPath), "Missing eight-direction character atlas.");
            AssetDatabase.ImportAsset(DirectionalAtlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var directionImporter = AssetImporter.GetAtPath(DirectionalAtlasPath) as TextureImporter;
            Require(directionImporter != null, "Directional atlas failed to import.");
            if (!directionImporter.isReadable || directionImporter.npotScale != TextureImporterNPOTScale.None ||
                directionImporter.mipmapEnabled || directionImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                directionImporter.isReadable = true;
                directionImporter.npotScale = TextureImporterNPOTScale.None;
                directionImporter.mipmapEnabled = false;
                directionImporter.textureCompression = TextureImporterCompression.Uncompressed;
                directionImporter.alphaIsTransparency = true;
                directionImporter.SaveAndReimport();
            }
            var directionTexture = Resources.Load<Texture2D>(HeroAppearance.DirectionalAtlasResource);
            Require(directionTexture != null && directionTexture.isReadable && directionTexture.width == 2048 && directionTexture.height == 1280,
                "Directional atlas is missing, downscaled or not readable.");
            var root = new GameObject("Hero import validation") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var legacy = DemoArt.Shape("Old robe", visual, "Robe", Vector2.zero, Vector2.one, Color.white);
                var hero = root.AddComponent<HeroAppearance>();
                Require(hero.Initialize(visual) && hero.Ready, "Hero body sprites could not initialize.");
                Require(!legacy.gameObject.activeSelf, "The legacy hero was not hidden.");
                var pivot = new GameObject("SwordPivot").transform;
                pivot.SetParent(root.transform, false);
                var oldSword = DemoArt.Shape("Old blade", pivot, "Blade", Vector2.zero, Vector2.one, Color.white);
                var sleeve = DemoArt.Shape("Sleeve", root.transform, "Square", Vector2.zero, Vector2.one, Color.white);
                hero.ConfigureWeapon(pivot, sleeve);
                Require(!oldSword.enabled && sleeve.sprite != null && sleeve.sprite.name == "White silk shoulder",
                    "Weapon and sleeve replacement failed.");
                Require(hero.HasDirectionalArt, "Eight-direction appearance did not initialize.");
                var directionFrames = new System.Collections.Generic.HashSet<Sprite>();
                for (int i = 0; i < 8; i++)
                {
                    Vector2 facing = ObliqueProjection.UnprojectGround(DemoArt.Direction(i * 45 - 90), 45).normalized;
                    hero.SetFacing(facing, 1.2f);
                    Require(hero.FacingIndex == i && !hero.Flipped && directionFrames.Add(hero.BodySprite),
                        "A direction is missing, mirrored or using another direction's sprite: " + i);
                    Require(hero.IsBackFacing == (i >= 3 && i <= 5), "Arm occlusion does not match the displayed body direction.");
                    Require(hero.BodySprite.texture == directionTexture && hero.BodySprite.bounds.min.y >= -0.02f,
                        "A directional sprite has an invalid texture or foot pivot.");
                }
                hero.SetFacing(Vector2.right, 1.2f);
                var blade = pivot.Find("Ice-blue jian").GetComponent<SpriteRenderer>();
                Require(Mathf.Abs(blade.sprite.bounds.max.x * blade.transform.localScale.x - 1.2f) < 0.001f,
                    "Visible sword tip does not match the combat blade length.");
                var view = root.AddComponent<ObliqueView>();
                Require(Mathf.Abs(view.WeaponHeight / view.characterScale - 0.84f) < 0.001f,
                    "Weapon plane is not at the hero's waist.");
                HeroAppearance.FitSleeve(sleeve, Vector3.zero, new Vector3(0.4f, 0.3f, 0), view, 0.23f);
                Require(sleeve.transform.localScale.x > 0 && sleeve.transform.localScale.y > 0, "Sleeve pose is invalid.");
                var gait = hero.Locomotion;
                Require(gait != null && gait.DeformedMesh != null, "Walking mesh did not initialize.");
                Require(gait.LayerCount == 3 && gait.WalkingRenderer.sharedMaterials.Length == 3,
                    "Lower robe, torso and head rendering layers did not initialize.");
                Require(gait.WalkingRenderer != null && gait.WalkingRenderer.gameObject != gait.gameObject,
                    "Walking mesh must render on a separate child from SpriteRenderer.");
                Require(gait.GetComponent<MeshFilter>() == null && gait.WalkingRenderer.GetComponent<MeshFilter>() != null,
                    "SpriteRenderer and MeshFilter were placed on the same GameObject.");
                gait.Refresh();
                var idle = gait.DeformedMesh.vertices;
                var originalUV = gait.DeformedMesh.uv;
                var regionUV = gait.DeformedMesh.uv2;
                for (int i = 0; i < 12; i++) gait.Advance(new Vector2(0.05f, 0), 1f / 60, false, view);
                gait.Refresh();
                var walking = gait.DeformedMesh.vertices;
                var walkingUV = gait.DeformedMesh.uv;
                bool feetMoved = false;
                for (int i = 0; i < walking.Length; i++)
                {
                    if (idle[i].y < 0.2f && Vector3.Distance(idle[i], walking[i]) > 0.01f) feetMoved = true;
                    if (regionUV[i].y >= 0.59f)
                        Require(Vector3.Distance(idle[i], walking[i]) < 0.00001f, "Walking deformed the upper body.");
                    Require(Vector2.Distance(originalUV[i], walkingUV[i]) < 0.000001f,
                        "Walking moved texture coordinates instead of the painted leg geometry.");
                }
                Require(feetMoved, "Walking did not animate the boots.");
                float frozenPhase = gait.Phase;
                gait.Advance(Vector2.one, 0, false, view);
                Require(gait.Phase == frozenPhase, "Hit stop advanced the walk cycle.");
                gait.Advance(new Vector2(0.2f, 0), 1f / 60, true, view);
                Require(gait.Phase == frozenPhase, "Dash advanced the walking cycle.");
                for (int i = 0; i < 160; i++) gait.Advance(Vector2.zero, 1f / 60, false, view);
                gait.Refresh();
                var stopped = gait.DeformedMesh.vertices;
                for (int i = 0; i < stopped.Length; i++)
                    Require(Vector3.Distance(stopped[i], idle[i]) < 0.00001f, "Stopping did not restore the standing pose.");
                gait.SetCombatPose(12, 0.08f, false, 1f / 60);
                gait.Refresh();
                var attacking = gait.DeformedMesh.vertices;
                bool upperMoved = false;
                for (int i = 0; i < attacking.Length; i++)
                {
                    if (regionUV[i].y > 0.75f && Vector3.Distance(attacking[i], stopped[i]) > 0.001f) upperMoved = true;
                    Require(Vector2.Distance(originalUV[i], gait.DeformedMesh.uv[i]) < 0.000001f,
                        "Layered combat motion moved the painted texture UVs.");
                }
                Require(upperMoved, "Combat did not animate the upper-body layer.");
                var strike = SwordMotion.Defaults()[0];
                var powerPose = SwordMotion.Evaluate(strike, strike.windupRatio + strike.activeRatio);
                gait.SetCombatPose(powerPose, new Vector2(powerPose.bodyOffset.x, powerPose.sink) / HeroAppearance.BodyHeight, 1, 1f / 60);
                gait.Refresh();
                var driven = gait.DeformedMesh.vertices;
                bool hemInherited = false, headInherited = false;
                for (int i = 0; i < driven.Length; i++)
                {
                    if (regionUV[i].y > 0.30f && regionUV[i].y < 0.50f && Vector3.Distance(driven[i], stopped[i]) > 0.01f) hemInherited = true;
                    if (regionUV[i].y > 0.85f && Vector3.Distance(driven[i], stopped[i]) > 0.01f) headInherited = true;
                    if (regionUV[i].y == 0)
                        Require(Vector3.Distance(driven[i], stopped[i]) < 0.00001f, "Weight transfer moved the planted feet.");
                    if (regionUV[i].y > 0.59f)
                        Require(Vector3.Distance(gait.PosePoint(stopped[i]), driven[i]) < 0.00001f,
                            "Shoulder anchor deformation does not match rendered upper-body vertices.");
                    Require(Vector2.Distance(originalUV[i], gait.DeformedMesh.uv[i]) < 0.000001f,
                        "Kinetic motion changed painted UV coordinates.");
                }
                Require(hemInherited && headInherited, "Hem and head did not inherit waist deformation.");
                gait.SetCombatPose(SwordMotion.Evaluate(strike, 1), Vector2.zero, 1, 0);
                gait.Refresh();
                var heldPose = gait.DeformedMesh.vertices;
                for (int i = 0; i < heldPose.Length; i++)
                    Require(Vector3.Distance(heldPose[i], driven[i]) < 0.000001f, "Hit stop advanced the kinetic pose.");
                var clothShader = Resources.Load<Shader>("SlashDemo/HeroCloth");
                Require(clothShader != null && !ShaderUtil.ShaderHasError(clothShader), "Walking shader failed to import.");
                var bladeShader = Resources.Load<Shader>("SlashDemo/BladeTrailAdditive");
                Require(bladeShader != null && !ShaderUtil.ShaderHasError(bladeShader), "Additive blade shader failed to import.");
                Require(DemoArt.LineMaterial.shader == Resources.Load<Shader>("SlashDemo/BladeTrail"),
                    "Blade glow replaced the shared line shader.");
                gait.UseStaticBody();
                Require(!gait.enabled && !gait.WalkingRenderer.enabled && !gait.GetComponent<SpriteRenderer>().forceRenderingOff,
                    "Walking failure does not restore the static character renderer.");
                Debug.Log("SLASH_HERO_WALK_OK: waist-level sword, fixed UVs, walking, kinetic waist deformation, inherited cloth/head, shoulder anchors and frozen pose verified.");
                Debug.Log("SLASH_HERO_IMPORT_OK: eight distinct directions, registered feet, three body layers, assembled sleeves/sword and blade alignment verified.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
