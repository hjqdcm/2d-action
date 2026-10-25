#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SlashDemo
{
    // Batch-mode integration check. It uses real scene objects, physics and combat, then exits.
    public sealed class SlashSmokeTest : MonoBehaviour
    {
        int checks;
        Keyboard testKeyboard;
        Mouse testMouse;

        void OnDestroy()
        {
            if (testKeyboard != null && testKeyboard.added) InputSystem.RemoveDevice(testKeyboard);
            if (testMouse != null && testMouse.added) InputSystem.RemoveDevice(testMouse);
        }
        IEnumerator Start()
        {
            IEnumerator test = Run();
            while (true)
            {
                bool next;
                try { next = test.MoveNext(); }
                catch (Exception error)
                {
                    Debug.LogError("SLASH_SMOKE_FAIL: " + error);
                    EditorApplication.Exit(1);
                    yield break;
                }
                if (!next) break;
                yield return test.Current;
            }
            Debug.Log("SLASH_SMOKE_PASS: " + checks + " assertions");
            EditorApplication.Exit(0);
        }

        void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            checks++;
            Debug.Log("SLASH_CHECK: " + message);
        }

        IEnumerator Run()
        {
            yield return null;
            yield return new WaitForFixedUpdate();
            var arena = FindAnyObjectByType<SlashArena>();
            Check(arena != null && arena.Player != null && arena.WorldCamera != null, "scene references exist");
            Check(arena.Tuning != null && arena.Tuning.Settings != null, "runtime tuning initializes automatically");
            var cameraData = arena.WorldCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Check(arena.WorldCamera.allowHDR && cameraData != null && cameraData.renderPostProcessing,
                "main camera enables HDR and URP post processing");
            var glowVolume = arena.transform.Find("Combat Bloom").GetComponent<UnityEngine.Rendering.Volume>();
            Check(glowVolume.isGlobal && glowVolume.sharedProfile != null
                && glowVolume.sharedProfile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom)
                && bloom.active && bloom.intensity.value > 0 && bloom.threshold.value == 0.9f,
                "combat global volume has a live Bloom override");
            arena.Tuning.RestoreDefaults();
            yield return null;
            Check(arena.WorldCamera.orthographic && Vector3.Dot(arena.WorldCamera.transform.forward, ObliqueProjection.Forward(arena.View.elevation)) > 0.999f, "orthographic camera looks down at the configured elevation");
            Check(Quaternion.Angle(arena.Player.transform.rotation, Quaternion.identity) < 0.01f, "player physics root remains on the XY ground");
            Check(Quaternion.Angle(arena.Player.transform.Find("Visual").rotation, arena.View.Rotation) < 0.01f, "standing player visual faces the oblique camera");
            Vector2 aimTarget = new Vector2(2, 1);
            Vector3 aimScreen = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(aimTarget, arena.View.WeaponHeight));
            Check(arena.View.MouseGround(arena.WorldCamera, aimScreen, out Vector2 resolvedAim) && Vector2.Distance(resolvedAim, aimTarget) < 0.001f, "screen aiming matches lifted blade geometry");
            Check(arena.Enemies.Count == 3, "practice contains three dummies");
            foreach (var actor in arena.Enemies)
            {
                var healthUI = actor.transform.Find("Health UI").GetComponent<UnityEngine.Rendering.SortingGroup>();
                Check(healthUI.sortAtRoot && healthUI.sortingOrder == 30000, "health bars sort independently above actors");
                var presentation = actor.GetComponent<ObliqueActor>();
                presentation.Initialize(arena, actor.trainingDummy);
                Check(actor.transform.Find("Health UI/HealthBack") != null
                    && actor.transform.Find("Health UI/Health UI") == null, "reinitialization keeps exactly one health bar group");
            }
            foreach (var sprite in arena.GetComponentsInChildren<SpriteRenderer>())
                Check(sprite.sprite != null && sprite.sharedMaterial != null, "sprite and material: " + sprite.name);
            Check(SlashPlayer.InsideArc(Vector2.zero, Vector2.right, new Vector2(1, 0), 2, 130, 0.34f), "arc includes front");
            Check(!SlashPlayer.InsideArc(Vector2.zero, Vector2.right, new Vector2(-1, 0), 2, 130, 0.34f), "arc excludes rear");
            Check(!SlashPlayer.InsideArc(Vector2.zero, Vector2.right, new Vector2(3, 0), 2, 130, 0.34f), "arc excludes out of reach");
            var player = arena.Player;
            Check(player.SwordSheathed && player.transform.Find("Waist scabbard") != null, "idle starts with a waist scabbard and sheathed sword");
            player.GetComponent<Rigidbody2D>().position = Vector2.zero;
            var dummy = arena.Enemies[0];
            dummy.GetComponent<Rigidbody2D>().position = new Vector2(1.2f, 0);
            var rear = arena.Enemies[1];
            rear.GetComponent<Rigidbody2D>().position = new Vector2(-1.2f, 0);
            arena.Enemies[2].GetComponent<Rigidbody2D>().position = new Vector2(5, 3);
            yield return new WaitForFixedUpdate();
            var playerBody = player.GetComponent<Rigidbody2D>();
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D, Key.UpArrow));
            yield return null;
            yield return new WaitForFixedUpdate();
            playerBody.linearVelocity = Vector2.right * player.moveSpeed;
            Check(player.RequestAttack(Vector2.right), "first slash starts");
            Check(player.IsDrawingSword, "first light attack draws before starting its swing");
            Vector2 attackVelocity = arena.View.Movement(DemoInput.Move) * player.moveSpeed * 0.5f;
            Check(Vector2.Distance(playerBody.linearVelocity, attackVelocity) < 0.001f,
                "starting an attack immediately reduces directional movement speed by 50 percent");
            Vector2 attackOrigin = playerBody.position;
            yield return new WaitForSeconds(player.drawDuration + player.strikes[0].duration * (player.strikes[0].windupRatio + player.strikes[0].activeRatio) + 0.04f);
            Check(DemoInput.Move.sqrMagnitude > 0.9f && Vector2.Distance(playerBody.position, attackOrigin) > 0.03f
                && Mathf.Abs(playerBody.linearVelocity.magnitude - attackVelocity.magnitude) < 0.01f,
                "held WASD and arrow input moves the player at 50 percent speed during a slash");
            Check(dummy.Health == 128, "first slash deals 22 damage");
            Check(!player.ScabbardVisible && !player.transform.Find("Waist scabbard").gameObject.activeSelf,
                "scabbard is hidden when the blade is fully extracted");
            Check(rear.Health == 150, "rear target takes no damage");
            var arms = player.transform.Find("Weapon Arms");
            Check(arms.Find("Support sword fingers") == null,
                "light attacks no longer create a sword finger gesture");
            Check(Vector3.Dot(arms.Find("Support hand").position - player.transform.Find("Visual").position,
                arena.View.Up) < 0.4f * arena.View.characterScale,
                "first cut leaves the left hand hanging beside the body");
            Check(Vector3.Distance(arms.Find("Support hand").position, arms.Find("Grip hand").position) > 0.18f,
                "single handed cut keeps the support hand away from the sword grip");
            var trailShader = Resources.Load<Shader>("SlashDemo/BladeTrailAdditive");
            Check(trailShader != null && !ShaderUtil.ShaderHasError(trailShader), "trail mesh shader imports without errors");
            var trailMesh = player.transform.Find("Blade Trail Mesh").GetComponent<MeshFilter>().sharedMesh;
            Check(trailMesh.vertexCount >= 15 && trailMesh.vertexCount % 5 == 0 && trailMesh.colors.Length == trailMesh.vertexCount,
                "trail contains five gradient columns including a transparent mist boundary");
            Check(trailMesh.triangles.Length == (trailMesh.vertexCount / 5 - 1) * 24,
                "five-column trail connects all four strips without invalid indices");
            Check(trailMesh.uv.Length == trailMesh.vertexCount && trailMesh.uv[2].x == 1
                && trailMesh.uv[3].x == 0.5f && trailMesh.uv[0].x == 0,
                "trail emission coordinates brighten the core independently of its outer glow");
            Check(player.transform.Find("Blade Trail Mesh").GetComponent<MeshRenderer>().sharedMaterial.shader == trailShader
                && DemoArt.LineMaterial.shader == Resources.Load<Shader>("SlashDemo/BladeTrail"),
                "additive blade material is separate from normal wave and environment lines");
            Vector3 trailWorld = player.transform.Find("Blade Trail Mesh").TransformPoint(trailMesh.vertices[0]);
            Check(Mathf.Abs(trailWorld.z - arena.View.Up.z * arena.View.WeaponHeight) < 0.001f,
                "blade trail lies on the same lifted plane as the sword");
            var trailColors = trailMesh.colors;
            Check(trailColors[2].a < trailColors[trailColors.Length - 3].a, "older mesh rows are dimmer than the newest sword sample");
            for (int row = 0; row < trailColors.Length; row += 5)
                Check(trailColors[row].a == 0 && trailColors[row + 4].a == 0
                    && Mathf.Abs(trailColors[row + 1].a - trailColors[row + 2].a * (0.25f / 0.95f)) < 0.00001f,
                    "outer mist fades with sample age and retains transparent boundaries");
            var trailPositions = trailMesh.vertices;
            Check(Vector3.Distance(trailPositions[0], trailPositions[4]) < 0.00001f
                && Vector3.Distance(trailPositions[trailPositions.Length - 5], trailPositions[trailPositions.Length - 1]) < 0.00001f,
                "crescent trail tapers both ends to a sharp point");
            yield return new WaitForSeconds(0.10f);
            Check(dummy.Health == 128, "one target is hit once per slash");
            yield return new WaitForSeconds(0.08f);
            Check(!player.IsAttacking && Mathf.Abs(playerBody.linearVelocity.magnitude - player.moveSpeed) < 0.01f,
                "held movement returns to full speed after recovery ends");
            InputSystem.RemoveDevice(testKeyboard);
            testKeyboard = null;
            playerBody.position = Vector2.zero;
            playerBody.linearVelocity = Vector2.zero;
            Check(player.RequestAttack(Vector2.right) && player.ComboIndex == 1, "second combo slash starts");
            yield return new WaitForSeconds(player.strikes[1].duration * 0.5f);
            Check(Vector3.Dot(arms.Find("Support hand").position - player.transform.Find("Visual").position,
                arena.View.Up) < 0.4f * arena.View.characterScale && Vector3.Distance(arms.Find("Support hand").position,
                arms.Find("Grip hand").position) > 0.18f, "reverse cut keeps the left hand naturally lowered");
            yield return new WaitForSeconds(player.strikes[1].duration * 0.5f + 0.04f);
            Check(dummy.Health == 102, "second slash deals 26 damage");
            var flank = arena.Enemies[2];
            flank.GetComponent<Rigidbody2D>().position = DemoArt.Direction(65) * 1.8f;
            yield return new WaitForFixedUpdate();
            Check(player.RequestAttack(Vector2.right) && player.ComboIndex == 2, "third combo slash starts");
            yield return new WaitForSeconds(player.strikes[2].duration * 0.5f);
            Check(Vector3.Distance(arms.Find("Support hand").position,
                arms.Find("Grip hand").position) < 0.15f, "third cut retains its two handed sword grip");
            yield return new WaitForSeconds(player.strikes[2].duration * 0.5f + 0.04f);
            Check(dummy.Health == 62, "finisher deals 40 damage");
            Check(flank.Health == 110, "wide finisher also hits a second target near the edge of the forward sweep");
            Check(rear.Health == 150, "all three forward cuts exclude rear targets");
            Check(player.IsComboRecovering && !player.RequestHeavyAttack(), "third slash adds recovery that blocks heavy attacks");
            int swingsBeforeRecovery = player.TotalSwings;
            Check(!player.RequestAttack(Vector2.right) && player.TotalSwings == swingsBeforeRecovery,
                "buffered follow-up cannot bypass combo recovery");
            Check(player.TryDash(Vector2.up) && !player.IsComboRecovering, "dash immediately cancels the added combo recovery");
            yield return new WaitForSeconds(player.dashCooldown + 0.03f);
            Check(player.TotalSwings == swingsBeforeRecovery && !player.IsAttacking, "dash discards follow-up buffered during combo recovery");
            Check(player.RequestAttack(Vector2.right), "attack starts before dash cancellation check");
            yield return new WaitForSeconds(player.strikes[0].duration * 0.45f);
            Check(player.IsAttacking, "attack is still in progress before dash");
            // Queue a follow-up, then verify dash cancellation clears it as well as the current cut.
            player.RequestAttack(Vector2.right);
            float oldY = player.GetComponent<Rigidbody2D>().position.y;
            Check(player.TryDash(Vector2.up), "dash starts");
            Check(!player.IsAttacking && player.IsDashing, "dash interrupts the current attack immediately");
            int swingsAtDash = player.TotalSwings;
            Check(!player.TakeDamage(12, Vector2.left), "dash grants invulnerability");
            Check(!player.TryDash(Vector2.right), "dash cannot be repeated during cooldown");
            yield return new WaitForSeconds(0.23f);
            Check(player.GetComponent<Rigidbody2D>().position.y > oldY + 1, "dash moves through physics");
            yield return new WaitForSeconds(player.strikes[0].duration);
            Check(!player.IsAttacking && player.TotalSwings == swingsAtDash, "dash discards the queued combo attack");
            Check(player.TakeDamage(12, Vector2.left) && player.Health == 88, "normal enemy damage reduces health");
            arena.TogglePause();
            Check(arena.Paused && Time.timeScale == 0, "pause stops scaled time");
            arena.TogglePause();
            Check(!arena.Paused && Time.timeScale == 1, "resume restores time");
            arena.practiceMode = false;
            arena.Restart();
            yield return null;
            Check(arena.Player.Health == 100 && arena.Enemies.Count == 0, "restart resets health and clears enemies");
            yield return new WaitForSeconds(2.2f);
            Check(arena.Wave == 1 && arena.Enemies.Count == 4, "first wave spawns four enemies");
            var enemies = new System.Collections.Generic.List<SlashEnemy>(arena.Enemies);
            foreach (var enemy in enemies) enemy.TakeHit(999, Vector2.right, 0);
            Check(enemies[0] != null && !enemies[0].Alive && !enemies[0].GetComponent<Collider2D>().enabled,
                "lethal damage disables combat immediately while retaining the dying visual");
            yield return null;
            Check(arena.Enemies.Count == 0 && arena.Kills == 4, "kills clean up wave membership");
            yield return new WaitForSeconds(0.10f);
            Check(enemies[0] != null && enemies[0].transform.Find("Visual").GetComponentInChildren<SpriteRenderer>().color.a < 1,
                "death visuals fade instead of disappearing instantly");
            arena.TogglePause();
            float deathAlpha = enemies[0].transform.Find("Visual").GetComponentInChildren<SpriteRenderer>().color.a;
            yield return new WaitForSecondsRealtime(0.06f);
            Check(enemies[0] != null && enemies[0].transform.Find("Visual").GetComponentInChildren<SpriteRenderer>().color.a == deathAlpha,
                "pause also freezes death animation");
            arena.TogglePause();
            yield return new WaitForSeconds(0.2f);
            Check(enemies[0] == null, "death visuals are cleaned up after the short fade");
            yield return new WaitForSeconds(3.15f);
            Check(arena.Wave == 2 && arena.Enemies.Count == 5, "cleared wave advances to five enemies");
            yield return new WaitForSeconds(4);
            Check(arena.Player.Health < 100, "hostile enemies approach, telegraph and damage the player");
            arena.Player.TakeDamage(999, Vector2.right);
            yield return new WaitForSeconds(0.85f);
            arena.Player.TakeDamage(999, Vector2.right);
            Check(arena.Defeated && !arena.Player.IsAlive, "lethal damage enters defeat state");
            arena.practiceMode = true;
            arena.Restart();
            yield return null;
            Check(!arena.Defeated && arena.Enemies.Count == 3 && arena.Player.Health == 100, "restart after defeat restores practice");
            var tuning = arena.Tuning;
            tuning.Settings.moveSpeed = 8;
            tuning.Settings.strikes[0].damage = 50;
            tuning.Settings.dummyHealth = 300;
            tuning.Settings.enemyHealthMultiplier = 2;
            tuning.Settings.enemySpeedMultiplier = 2;
            tuning.Settings.hitStopMultiplier = 0;
            tuning.ApplyAll();
            Check(arena.Player.moveSpeed == 8 && arena.Player.strikes[0].damage == 50, "tuning updates current player");
            Check(arena.Enemies[0].maxHealth == 300 && arena.Enemies[0].Health == 300, "tuning updates current dummies and preserves health ratio");
            tuning.ApplyAll();
            Check(arena.Enemies[0].maxHealth == 300, "applying identical tuning does not compound health");
            arena.Effects.HitStop(0.04f);
            Check(!arena.Effects.Frozen && Time.timeScale == 1, "zero hit stop leaves time running");
            arena.Restart();
            yield return null;
            Check(arena.Player.moveSpeed == 8 && arena.Enemies[0].maxHealth == 300, "restart retains runtime tuning");
            arena.practiceMode = false;
            arena.Restart();
            yield return new WaitForSeconds(2.2f);
            Check(arena.Enemies[0].maxHealth == 112, "new enemies receive health multiplier on their wave baseline");
            float tunedSpeed = arena.Enemies[0].moveSpeed;
            tuning.ApplyAll();
            Check(arena.Enemies[0].maxHealth == 112 && arena.Enemies[0].moveSpeed == tunedSpeed, "repeated tuning does not compound enemy multipliers");
            tuning.RestoreDefaults();
            arena.practiceMode = true;
            arena.Restart();
            yield return null;
            Check(arena.Player.moveSpeed == 5.4f && arena.Enemies[0].maxHealth == 150, "restore defaults resets gameplay values");
            var oldSettings = JsonUtility.FromJson<SlashTuningSettings>("{\"version\":3,\"moveSpeed\":7}");
            oldSettings.Validate(new SlashTuningSettings());
            Check(oldSettings.moveSpeed == 7 && oldSettings.heavyAttack.chargeTime == 0.8f,
                "older JSON adds heavy defaults without replacing existing tuning");
            var roundTrip = JsonUtility.FromJson<SlashTuningSettings>(JsonUtility.ToJson(tuning.Settings));
            Check(roundTrip.heavyAttack.width == 1.8f && roundTrip.heavyAttack.distance == 6,
                "heavy settings survive saving and loading JSON");
            player = arena.Player;
            playerBody = player.GetComponent<Rigidbody2D>();
            playerBody.position = Vector2.zero;
            dummy = arena.Enemies[0];
            rear = arena.Enemies[1];
            flank = arena.Enemies[2];
            dummy.GetComponent<Rigidbody2D>().position = new Vector2(2, 0);
            rear.GetComponent<Rigidbody2D>().position = new Vector2(-2, 0);
            flank.GetComponent<Rigidbody2D>().position = new Vector2(4, 0);
            testMouse = InputSystem.AddDevice<Mouse>();
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            Vector2 heavyPointer = arena.WorldCamera.WorldToScreenPoint(arena.View.LiftPoint(new Vector2(4, 0), arena.View.WeaponHeight));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer }.WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D));
            yield return null;
            yield return new WaitForFixedUpdate();
            Check(player.IsCharging && !player.RequestAttack(Vector2.right), "right hold starts charge and blocks overlapping light attacks");
            Vector2 chargeOrigin = playerBody.position;
            yield return new WaitForSeconds(0.15f);
            Check(Vector2.Distance(playerBody.position, chargeOrigin) < 0.002f && playerBody.linearVelocity == Vector2.zero,
                "direction keys cannot move the player while charging");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer });
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            Check(!player.IsHeavyBusy && player.TotalSwordWaves == 0, "early right release cancels without a wave or recovery");

            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer }.WithButton(MouseButton.Right));
            yield return new WaitForSeconds(player.heavyAttack.chargeTime + 0.08f);
            Check(player.HeavyReady && player.TotalSwordWaves == 0, "full charge waits for right release without auto firing");
            Check(Vector3.Distance(arms.Find("Support hand").position,
                arms.Find("Grip hand").position) < 0.15f, "heavy charge retains a two handed sword grip");
            Check(arena.Effects.TotalChargeMotes > 0 && arena.Effects.ActiveChargeMotes <= 16,
                "charge generates bounded inward gathering lights");
            var chargedAppearance = player.GetComponent<HeroAppearance>();
            var chargedBlade = player.transform.Find("SwordPivot/Ice-blue jian")?.GetComponent<SpriteRenderer>();
            Check(chargedAppearance.Continuous3D ? chargedAppearance.ContinuousModel.SwordBrightness >= 1.79f
                : chargedBlade != null && chargedBlade.sharedMaterial.GetColor("_Color").r >= 1.79f,
                "fully charged sword uses HDR brightness beyond ordinary vertex color range");
            var chargeTrail = player.transform.Find("Blade Trail Mesh").GetComponent<MeshFilter>().sharedMesh;
            Check(chargeTrail.vertexCount >= 12, "held charge continuously generates a trembling trail");
            foreach (var color in chargeTrail.colors) Check(color.a <= 0.30001f, "charge trail stays at thirty percent alpha or less");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer });
            yield return null;
            Check(player.IsHeavyRecovering && player.TotalSwordWaves == 0, "full right release holds briefly before launching a wave");
            Check(arena.Effects.ActiveChargeMotes == 0 && (chargedAppearance.Continuous3D
                ? chargedAppearance.ContinuousModel.SwordBrightness == 1 : chargedBlade.sharedMaterial.GetColor("_Color").r == 1),
                "releasing charge clears gathering lights and resets sword HDR tint");
            yield return new WaitForSeconds(HeavyReleaseState.Pause + HeavyReleaseState.Sweep * HeavyReleaseState.WavePoint + 0.02f);
            Check(player.TotalSwordWaves == 1, "forward crossing launches exactly one wave during the heavy sweep");
            Check(player.TryDash(Vector2.up) && !player.IsHeavyBusy, "dash immediately cancels heavy release recovery");
            yield return new WaitForSeconds(0.85f);
            Check(dummy.Health == 95 && flank.Health == 95 && rear.Health == 150,
                "traveling sword wave pierces two forward dummies once each and excludes rear dummy");
            Check(arena.Effects.ActiveSwordWaveCount == 0, "sword wave is removed after its configured distance");

            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer }.WithButton(MouseButton.Right));
            yield return new WaitForSeconds(0.15f);
            Check(player.IsCharging && player.TryDash(Vector2.up) && !player.IsHeavyBusy,
                "dash immediately interrupts an incomplete charge");
            yield return new WaitForSeconds(0.9f);
            Check(!player.IsCharging && player.TotalSwordWaves == 1, "holding right after a dash does not silently restart charging");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer });
            yield return null;
            Check(player.TotalSwordWaves == 1, "releasing the interrupted charge cannot fire a stale wave");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer }.WithButton(MouseButton.Right));
            yield return new WaitForSeconds(0.15f);
            arena.TogglePause();
            yield return new WaitForSecondsRealtime(0.06f);
            Check(!player.IsCharging, "pause safely discards an unfinished charge");
            arena.TogglePause();
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = heavyPointer });
            yield return null;
            Check(player.TotalSwordWaves == 1, "release after a paused charge cannot fire");
            arena.Effects.LaunchSwordWave(Vector2.zero, Vector2.right, player.heavyAttack);
            arena.Restart();
            Check(arena.Effects.ActiveSwordWaveCount == 0, "restart cleans up traveling sword waves");
            yield return null;
            player = arena.Player;
            Check(player.SwordSheathed && player.RequestAttack(Vector2.right), "fresh player starts a new draw from its scabbard");
            yield return new WaitForSeconds(0.06f);
            Check(player.IsDrawingSword && player.TryDash(Vector2.up) && !player.IsAttacking,
                "dash interrupts the draw before a light swing can resolve");
            yield return new WaitForSeconds(0.6f);
            Check(player.SwordSheathed && !player.IsAttacking, "interrupted draw returns to idle scabbard without a stale attack");
            var paths = Environment.GetCommandLineArgs();
            for (int i = 0; i < paths.Length - 1; i++)
            {
                if (paths[i] != "-slashScreenshot") continue;
                string path = Path.GetFullPath(paths[i + 1]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Camera render also works in batch mode; HUD is verified separately in play mode.
                var target = new RenderTexture(1600, 900, 24);
                arena.WorldCamera.targetTexture = target;
                arena.WorldCamera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                RenderTexture.active = previous;
                arena.WorldCamera.targetTexture = null;
                Destroy(image);
                target.Release();
                Destroy(target);
                Debug.Log("SLASH_SCREENSHOT: " + path);
            }
        }
    }
}
#endif
