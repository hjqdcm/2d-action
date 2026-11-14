using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlashDemo.Editor
{
    public static class UltimateValidation
    {
        const string Pending = "SlashDemo.UltimateValidation";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int stage;
        static double next, deadline;
        static Vector2 castPosition;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(SlashDemoTools.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".Launch", true);
            Application.runInBackground = true;
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.update += Tick;

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Pending + ".Launch", false))
            {
                SessionState.SetBool(Pending + ".Launch", false);
                var view = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
                view.Show();
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            var arena = UnityEngine.Object.FindAnyObjectByType<SlashArena>();
            if (arena == null || arena.Player == null) return;
            try
            {
                if (!VerifyLiveCast(arena)) return;
                Verify(arena);
                Debug.Log("PLAYER_ULTIMATE_PASS: live cast and movement recovery, charge gates, five slashes, immunity and cooldown.");
                Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        static void Finish(int code)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }

        static bool VerifyLiveCast(SlashArena arena)
        {
            var player = arena.Player;
            if (stage == 0)
            {
                if (Time.timeSinceLevelLoad < .5f || Time.deltaTime <= 0 || arena.InputBlocked || arena.Effects.Frozen) return false;
                Application.runInBackground = true;
                player.Teleport(ManorEnvironment.SpawnPoint);
                arena.Tuning.Settings.infiniteUltimateForDebug = true;
                arena.Tuning.ApplyPlayer(player);
                Check(player.TryUltimateSkill(Vector2.right), "The live cast was rejected"
                    + $" (alive: {player.IsAlive}, blocked: {arena.InputBlocked}, frozen: {arena.Effects.Frozen},"
                    + $" dashing: {player.IsDashing}, attacking: {player.IsAttacking}, recovering: {player.IsComboRecovering},"
                    + $" heavy: {player.IsHeavyBusy}, debug: {typeof(SlashPlayer).GetProperty("InfiniteUltimateForDebug", Private).GetValue(player)},"
                    + $" ultimate exists: {typeof(SlashPlayer).GetProperty("Ultimate", Private).GetValue(player) != null}).");
                castPosition = player.transform.position;
                deadline = EditorApplication.timeSinceStartup + 8;
                stage = 1;
                next = EditorApplication.timeSinceStartup + .02;
                return false;
            }
            Check(EditorApplication.timeSinceStartup < deadline, "Live R cast stopped advancing at stage " + stage
                + $" (input blocked: {arena.InputBlocked}, time scale: {Time.timeScale}).");
            if (stage == 1 && player.IsUltimateActive)
            {
                var frame = ManorOcclusionValidation.Capture(arena.WorldCamera);
                UnityEngine.Object.Destroy(frame);
                stage = 2;
            }
            else if (stage == 2 && !player.IsUltimateActive)
            {
                Check(player.TryDash(Vector2.right), "The player could not dash after the live cast.");
                stage = 3;
            }
            return stage == 3 && !player.IsDashing && Vector2.Distance(castPosition, player.transform.position) > .25f;
        }

        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static MethodInfo Method(string name) => typeof(SlashPlayer).GetMethod(name, Private);
        static FieldInfo Field(string name) => typeof(SlashPlayer).GetField(name, Private);

        static void Verify(SlashArena arena)
        {
            var player = arena.Player;
            var settings = arena.Tuning.Settings;
            settings.infiniteUltimateForDebug = false;
            settings.invincible = false;
            arena.Tuning.ApplyPlayer(player);
            var ultimate = typeof(SlashPlayer).GetProperty("Ultimate", Private).GetValue(player);
            object Read(string name) => ultimate.GetType().GetProperty(name).GetValue(ultimate);
            int Progress() => (int)Read("Progress");
            float Cooldown() => (float)Read("CooldownLeft");
            void Advance(float dt) => ultimate.GetType().GetMethod("Advance").Invoke(ultimate, new object[] { dt });
            var hud = arena.GetComponent<SlashHUD>();
            void CheckProgressBadge(int stacks, int required)
            {
                foreach (var buff in hud.VisibleBuffs)
                {
                    if (buff.id != "five-star-sword-progress") continue;
                    Check(buff.stacks == stacks && buff.showZeroStacks && buff.cropIconToSquare
                        && buff.iconResource == "SlashDemo/FiveStarSwordSlashIcon" && buff.description.Contains(stacks + " / " + required),
                        "The ultimate's passive badge did not show the current progress and square icon.");
                    return;
                }
                throw new Exception("The ultimate's passive badge was missing.");
            }
            var start = ManorEnvironment.SpawnPoint;

            player.EquipTechnique(TechniqueCatalog.FlowingCloud);
            player.Teleport(start);
            CheckProgressBadge(0, 3);
            Check(player.Character == PlayableCharacter.Swordsman && !player.TryUltimateSkill(Vector2.right),
                "The swordsman's ultimate did not start locked.");
            CloudCast(player, full: false, finish: true);
            CloudCast(player, full: true, finish: false);
            Check(Progress() == 0, "A partial or cancelled Q counted toward the ultimate.");
            for (int i = 1; i <= 3; i++)
            {
                CloudCast(player, full: true, finish: true);
                Check(Progress() == i, "A completed full-stack Q did not count exactly once.");
                CheckProgressBadge(i, 3);
            }
            var floatingText = (System.Collections.ICollection)typeof(SlashEffects)
                .GetField("texts", Private).GetValue(arena.Effects);
            int textCount = floatingText.Count;
            Check(player.TryUltimateSkill(Vector2.right) && Progress() == 0 && Cooldown() == 30,
                "Three full-stack casts did not unlock a 30-second ultimate.");
            Check(floatingText.Count == textCount, "Casting the ultimate displayed floating text.");
            CheckProgressBadge(0, 3);
            var visualRoot = (Transform)ultimate.GetType().GetField("visuals", Private).GetValue(ultimate);
            var bladeMesh = visualRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach (var line in visualRoot.GetComponentsInChildren<LineRenderer>())
                Check(!line.enabled, "The ultimate drew a line before the first slash.");
            Check(bladeMesh.vertexCount == 0, "Blade facets appeared before the first slash.");
            int playerHealth = player.Health;
            Check(!player.TakeDamage(10, Vector2.left) && !player.TakePoisonDamage(10) && player.Health == playerHealth,
                "The ultimate did not protect the player during windup.");
            var target = SlashArena.CreateEnemy(player.transform.parent, start + Vector2.right * .06f, true);
            arena.RegisterEnemy(target);
            Physics2D.SyncTransforms();
            int health = target.Health;
            var strokes = (LineRenderer[])ultimate.GetType().GetField("cores", Private).GetValue(ultimate);
            var halos = (LineRenderer[])ultimate.GetType().GetField("halos", Private).GetValue(ultimate);
            float fullLength = 2 * 2.75f * 1.25f * Mathf.Sin(72 * Mathf.Deg2Rad);
            float Length(LineRenderer stroke) => Vector3.Distance(stroke.GetPosition(0), stroke.GetPosition(1));
            Advance(.108f);
            Check(visualRoot.GetComponentsInChildren<LineRenderer>().Length == 10,
                "The ultimate created extra lines beyond its sword light and halos.");
            Check(strokes[0].enabled && Length(strokes[0]) > 0 && Length(strokes[0]) < fullLength && !strokes[1].enabled,
                "The first edge did not sweep across the star before the second edge appeared.");
            Check(bladeMesh.vertexCount == 9, "The first slash did not render its three blade facets.");
            bool HasUniformWidth(int i) => Mathf.Abs(strokes[i].widthMultiplier - .22f) < .001f
                && Mathf.Abs(halos[i].widthMultiplier - .52f) < .001f;
            Check(HasUniformWidth(0), "The first slash did not use twice the original afterglow width.");
            Advance(.112f);
            Check(strokes[1].enabled && HasUniformWidth(0) && HasUniformWidth(1),
                "The second slash and the first slash's afterglow had different widths.");
            Advance(.347f);
            Check(health - target.Health == 180, "The ultimate did not resolve all five slashes exactly once.");
            foreach (var stroke in strokes)
                Check(stroke.enabled && Mathf.Abs(Length(stroke) - fullLength) < .001f, "The star edge did not grow by 25%.");
            for (int i = 0; i < strokes.Length; i++)
                Check(HasUniformWidth(i), "The final flash changed the slash width.");
            Check(bladeMesh.vertexCount > 0 && bladeMesh.vertexCount <= 5 * 9, "The final slash's facets were missing or unbounded.");
            Check(!player.TakeDamage(10, Vector2.left) && !player.TakePoisonDamage(10) && player.Health == playerHealth,
                "The ultimate lost immunity during recovery.");
            player.Teleport(start);
            Check(!player.IsUltimateActive && Cooldown() > 29, "Teleport did not cancel the animation or preserve cooldown.");
            Check(!visualRoot.gameObject.activeInHierarchy, "Teleport left the sword light visible.");
            Check(player.TakePoisonDamage(1), "Teleport left the ultimate's immunity active.");
            player.Heal(1);

            player.EquipTechnique(TechniqueCatalog.IronSword);
            CheckProgressBadge(0, 12);
            IronRelease(player, .3f, emit: true);
            IronRelease(player, 10, emit: false);
            Check(Progress() == 0, "A partial or un-emitted heavy attack counted.");
            for (int i = 1; i <= 12; i++)
            {
                IronRelease(player, 10, emit: true);
                Check(Progress() == i, "A fully emitted heavy attack did not count exactly once.");
                CheckProgressBadge(i, 12);
            }
            Check(!player.TryUltimateSkill(Vector2.right), "Progress bypassed the cooldown.");
            Advance(30);
            Check(player.TryUltimateSkill(Vector2.right), "Twelve full heavy attacks did not unlock the ultimate.");
            player.EquipTechnique(TechniqueCatalog.FlowingCloud);
            Check(!player.IsUltimateActive && Progress() == 0 && Cooldown() == 30, "Changing technique bypassed cooldown.");

            settings.infiniteUltimateForDebug = true;
            arena.Tuning.ApplyPlayer(player);
            target.gameObject.SetActive(false);
            int Impacts() => Array.FindAll(arena.Effects.GetComponentsInChildren<Renderer>(),
                renderer => renderer.name == "Spark" || renderer.name == "Damage spark" || renderer.name == "Damage impact ring").Length;
            int SwordFlecks() => Array.FindAll(arena.Effects.GetComponentsInChildren<SpriteRenderer>(),
                sprite => sprite.name == "Sword slash fleck").Length;
            for (int i = 0; i < 2; i++)
            {
                arena.Effects.Clear();
                float remaining = Cooldown();
                Check(player.TryUltimateSkill(Vector2.right) && !player.TryUltimateSkill(Vector2.right),
                    "Unlimited ultimate failed to bypass the gates or allowed overlapping casts.");
                Check(Progress() == 0 && Cooldown() == remaining, "A debug cast changed normal ultimate state.");
                int impactCount = Impacts(), fleckCount = SwordFlecks();
                Advance(.76f);
                Check(Impacts() == impactCount, "An empty ultimate produced impact feedback.");
                Check(SwordFlecks() - fleckCount == 120, "The five cuts did not emit their blade and side particles.");
                Check(player.TakePoisonDamage(1), "The ultimate left immunity active after normal recovery.");
                player.Heal(1);
            }
            settings.infiniteUltimateForDebug = false;
            arena.Tuning.ApplyPlayer(player);
            Check(Cooldown() > 0 && !player.TryUltimateSkill(Vector2.right),
                "Normal requirements did not resume when unlimited ultimate was turned off.");
        }

        static void CloudCast(SlashPlayer player, bool full, bool finish)
        {
            player.ResetTechniqueCombo();
            player.ResetTechniqueCooldown();
            if (full) player.FillTechniqueStacksForDebug();
            Check(player.TryTechniqueSkill(Vector2.right), "Could not begin Q.");
            Field("skillRushRemaining").SetValue(player, 0f);
            Method("UpdateTechniqueRush").Invoke(player, null);
            if (finish)
            {
                Field("attackAge").SetValue(player, 1f);
                Method("Update").Invoke(player, null);
            }
            else player.ResetTechniqueCombo();
        }

        static void IronRelease(SlashPlayer player, float chargeTime, bool emit)
        {
            Check(player.RequestHeavyAttack(), "Could not charge a heavy attack.");
            ((ChargedAttackState)Field("heavyState").GetValue(player)).Advance(chargeTime);
            Check(player.ReleaseHeavyAttack(), "Could not release a heavy attack.");
            if (emit) Method("UpdateHeavyAttack").Invoke(player, new object[] { .5f });
            else player.CancelHeavyAttack();
        }
    }
}
