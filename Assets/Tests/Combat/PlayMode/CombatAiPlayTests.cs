#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Enemies;
using MysticWoods.Gameplay.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MysticWoods.Tests.Combat
{
    /// <summary>
    /// Combat and AI behaviour in the player sandbox (walls + obstacles). Enemies are the real
    /// prefabs and configs used by the level.
    /// </summary>
    public sealed class CombatAiPlayTests
    {
        private const string Slime = "Assets/Gameplay/Enemies/Prefabs/Slime.prefab";
        private const string Skeleton = "Assets/Gameplay/Enemies/Prefabs/Esqueleto.prefab";
        private const string Boss = "Assets/Gameplay/Enemies/Prefabs/SlimeBoss.prefab";
        private PlayerController player;
        private Health playerHealth;
        private Rigidbody2D body;
        private Keyboard keyboard;
        private InputDevice[] disabled;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private readonly List<Object> spawned = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            HitStop.Enabled = false;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            disabled = InputSystem.devices.Where(x => (x is Keyboard || x is Mouse) && x.enabled).ToArray();
            foreach (var device in disabled) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode("Assets/Gameplay/Player/Sandbox/PlayerSandbox.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 5; i++) yield return null;
            player = Object.FindAnyObjectByType<PlayerController>();
            playerHealth = player.GetComponent<Health>();
            body = player.GetComponent<Rigidbody2D>();
            player.Input.SendMessage("OnApplicationFocus", true);
            Teleport(new Vector2(0f, -3f));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            HitStop.Enabled = true;
            foreach (var o in spawned) if (o != null) Object.Destroy(o);
            spawned.Clear();
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            foreach (var device in disabled) if (device.added) InputSystem.EnableDevice(device);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            var sandbox = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Combat Test Cleanup " + Time.frameCount);
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(sandbox);
        }

        // ───────────── helpers ─────────────

        private void Teleport(Vector2 position)
        {
            player.Movement.Stop();
            body.position = position;
            body.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }

        private Health Dummy(Vector2 position, float hp = 10f)
        {
            var go = new GameObject("Dummy");
            go.transform.position = position;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.6f, .6f);
            var health = go.AddComponent<Health>();
            health.Configure(Team.Enemy, hp, 100f, 0f);
            spawned.Add(go);
            return health;
        }

        private EnemyBrain Spawn(string path, Vector2 position, System.Action<EnemyConfig> tune = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var holder = new GameObject("Spawn holder");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, position, Quaternion.identity, holder.transform);
            var brain = go.GetComponent<EnemyBrain>();
            if (tune != null)
            {
                var config = Object.Instantiate(brain.Config);
                tune(config);
                brain.UseConfig(config);
                spawned.Add(config);
            }
            go.transform.SetParent(null, true);
            Object.Destroy(holder);
            go.SetActive(true);
            spawned.Add(go);
            return brain;
        }

        private static DamageInfo Hit(float amount, Vector2 direction, float knockback = 6f, Team team = Team.Enemy) =>
            new DamageInfo(amount, direction, knockback, 1f, null, team, Vector2.zero);

        // ───────────── player combat ─────────────

        [UnityTest]
        public IEnumerator ThreeSwingComboHitsOncePerSwingAndFinisherHitsHarder()
        {
            var dummy = Dummy(new Vector2(0f, -3.75f));
            yield return Tap(Key.Space);
            yield return Wait(.24f);
            yield return Tap(Key.Space);
            yield return Wait(.24f);
            yield return Tap(Key.Space);
            yield return Wait(.8f);
            Assert.That(player.Combat.TotalHits, Is.EqualTo(3), "one hit per swing");
            Assert.That(dummy.Current, Is.EqualTo(10f - 1f - 1f - 2f).Within(.001f), "finisher deals double damage");
            Assert.That(player.Action, Is.Not.EqualTo(PlayerMotionState.Attack));
        }

        [UnityTest]
        public IEnumerator SwingDirectionFollowsHeldInput()
        {
            var right = Dummy(new Vector2(1f, -2.6f));
            var below = Dummy(new Vector2(0f, -3.75f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(.5f);
            Assert.That(right.Current, Is.LessThan(10f));
            Assert.That(below.Current, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator DodgeIsInvulnerableOnlyWhileRolling()
        {
            yield return Tap(Key.K);
            yield return Wait(.06f);
            Assert.That(player.Action, Is.EqualTo(PlayerMotionState.Dodge));
            Assert.That(playerHealth.ReceiveHit(Hit(1f, Vector2.right)), Is.EqualTo(HitResult.Ignored));
            yield return Wait(.5f);
            Assert.That(player.Action, Is.Not.EqualTo(PlayerMotionState.Dodge));
            Assert.That(playerHealth.ReceiveHit(Hit(1f, Vector2.right)), Is.EqualTo(HitResult.Damaged));
            Assert.That(playerHealth.ReceiveHit(Hit(1f, Vector2.right)), Is.EqualTo(HitResult.Ignored), "hurt invulnerability");
            Assert.That(playerHealth.Current, Is.EqualTo(5f));
        }

        [UnityTest]
        public IEnumerator HurtPushesThePlayerAndReturnsControl()
        {
            float x = body.position.x;
            playerHealth.ReceiveHit(Hit(1f, Vector2.right, 8f));
            yield return new WaitForFixedUpdate();
            Assert.That(player.Action, Is.EqualTo(PlayerMotionState.Hurt));
            yield return Wait(.3f);
            Assert.That(body.position.x - x, Is.GreaterThan(.3f));
            Assert.That(player.Action, Is.Not.EqualTo(PlayerMotionState.Hurt));
        }

        [UnityTest]
        public IEnumerator DeathRaisesEventAndIgnoresInput()
        {
            bool died = false;
            player.Died += () => died = true;
            playerHealth.Kill();
            yield return null;
            Assert.That(died, Is.True);
            Assert.That(player.Action, Is.EqualTo(PlayerMotionState.Dead));
            yield return Tap(Key.Space);
            yield return Wait(.1f);
            Assert.That(player.Combat.Swinging, Is.False);
            Assert.That(player.GetComponentInChildren<PlayerAnimationController>().CurrentStateHash,
                Is.EqualTo(PlayerAnimationController.DeathStateHash));
        }

        [UnityTest]
        public IEnumerator HitStopRestoresTimeButNeverOverridesPause()
        {
            HitStop.Enabled = true;
            HitStop.Request(.1f);
            Assert.That(Time.timeScale, Is.EqualTo(HitStop.SlowScale));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            HitStop.Request(.1f);
            Time.timeScale = 0f; // pause menu opens during the freeze
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            HitStop.Request(.1f);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "no hit-stop while paused");
            Time.timeScale = 1f;
        }

        // ───────────── AI ─────────────

        [UnityTest]
        public IEnumerator SlimeReactsChasesAndTelegraphsBeforeHitting()
        {
            var slime = Spawn(Slime, new Vector2(-2.5f, -3f), c => c.wanderRadius = 0f);
            var states = new List<EnemyState>();
            slime.StateChanged += s => states.Add(s);
            bool telegraphBeforeDamage = false;
            float start = Time.time;
            while (playerHealth.Current >= 6f && Time.time - start < 6f)
            {
                if (slime.TelegraphVisible) telegraphBeforeDamage = true;
                yield return null;
            }
            Assert.That(playerHealth.Current, Is.LessThan(6f), "the slime eventually lands a hit");
            Assert.That(states, Does.Contain(EnemyState.Alert));
            Assert.That(states.IndexOf(EnemyState.Alert), Is.LessThan(states.IndexOf(EnemyState.Attack)));
            Assert.That(telegraphBeforeDamage, Is.True);
        }

        [UnityTest]
        public IEnumerator WallsBlockSight()
        {
            Teleport(new Vector2(1.6f, 0f));
            var slime = Spawn(Slime, new Vector2(4.8f, 0f), c => c.wanderRadius = 0f);
            yield return Wait(1.5f);
            Assert.That(slime.Engaged, Is.False, "obstacle between them");
            Teleport(new Vector2(4.8f, 2.6f));
            yield return Wait(1.2f);
            Assert.That(slime.Engaged, Is.True, "clear line of sight");
        }

        [UnityTest]
        public IEnumerator NoAttackSlotsBeyondCapacity()
        {
            playerHealth.PushInvulnerability();
            Teleport(new Vector2(-5f, -2f));
            var positions = new[] { new Vector2(-7f, -2f), new Vector2(-3f, -2f), new Vector2(-5f, 0f), new Vector2(-5f, -4f) };
            var skeletons = positions.Select(p => Spawn(Skeleton, p, c => c.wanderRadius = 0f)).ToArray();
            int most = 0;
            float start = Time.time;
            while (Time.time - start < 6f)
            {
                int attacking = skeletons.Count(s => s != null && s.State == EnemyState.Attack &&
                    (s.AttackPhase == AttackStage.Windup || s.AttackPhase == AttackStage.Active));
                most = Mathf.Max(most, attacking);
                yield return new WaitForFixedUpdate();
            }
            playerHealth.PopInvulnerability();
            Assert.That(most, Is.GreaterThanOrEqualTo(1));
            Assert.That(most, Is.LessThanOrEqualTo(AttackSlots.Capacity));
        }

        [UnityTest]
        public IEnumerator SkeletonRisesOnceThenStaysDown()
        {
            var skeleton = Spawn(Skeleton, new Vector2(6f, -3f), c => { c.wanderRadius = 0f; c.sightRadius = .6f; c.pursuitRadius = .6f; });
            yield return null;
            skeleton.Health.Kill(player.gameObject);
            yield return null;
            Assert.That(skeleton.State, Is.EqualTo(EnemyState.Downed));
            float start = Time.time;
            while (skeleton.State == EnemyState.Downed && Time.time - start < 8f) yield return null;
            Assert.That(skeleton.IsAlive, Is.True);
            Assert.That(skeleton.Health.Current, Is.EqualTo(2f).Within(.01f));
            skeleton.Health.Kill(player.gameObject);
            yield return null;
            Assert.That(skeleton.State, Is.EqualTo(EnemyState.Dead));
            yield return Wait(1.8f);
            Assert.That(skeleton == null, Is.True, "corpse fades and is removed");
        }

        [UnityTest]
        public IEnumerator BossChangesPhaseWithInvulnerableRoarAndOpensBarrierOnDefeat()
        {
            playerHealth.PushInvulnerability();
            Teleport(new Vector2(-6f, -3f));
            var barrier = new GameObject("Barrier");
            spawned.Add(barrier);
            var boss = Spawn(Boss, new Vector2(4f, 1f));
            boss.SetDefeatTargets(barrier);
            int phase = 0;
            boss.PhaseChanged += p => phase = p;
            bool defeated = false;
            boss.Defeated += () => defeated = true;
            yield return null;
            boss.Health.ReceiveHit(Hit(9f, Vector2.right, 0f, Team.Player));
            yield return null;
            Assert.That(phase, Is.EqualTo(2));
            Assert.That(boss.State, Is.EqualTo(EnemyState.Phase));
            Assert.That(boss.Health.CanBeHit, Is.False, "roar is invulnerable");
            yield return Wait(boss.Config.phaseTransitionTime + .2f);
            Assert.That(boss.State, Is.Not.EqualTo(EnemyState.Phase));
            Assert.That(boss.Health.CanBeHit || boss.Health.IsInvulnerable, Is.True);
            boss.Health.Kill(player.gameObject);
            yield return null;
            Assert.That(defeated, Is.True);
            Assert.That(barrier.activeSelf, Is.False);
            playerHealth.PopInvulnerability();
        }

        [UnityTest]
        public IEnumerator LeashedEnemyGivesUpAndReturnsHome()
        {
            playerHealth.PushInvulnerability();
            Teleport(new Vector2(-2f, 3f));
            var slime = Spawn(Slime, new Vector2(-6f, 3f), c => { c.wanderRadius = 0f; c.leashRadius = 1.5f; c.reactionTime = .1f; });
            var states = new List<EnemyState>();
            slime.StateChanged += s => states.Add(s);
            yield return Wait(4f);
            playerHealth.PopInvulnerability();
            Assert.That(states, Does.Contain(EnemyState.Chase));
            Assert.That(states, Does.Contain(EnemyState.Return));
        }

        [UnityTest]
        public IEnumerator SwordNoiseDrawsIdleEnemiesBeyondSight()
        {
            var slime = Spawn(Slime, new Vector2(-5.4f, -3f), c => c.wanderRadius = 0f);
            yield return Wait(.4f);
            Assert.That(slime.Engaged, Is.False);
            yield return Tap(Key.Space);
            yield return new WaitForFixedUpdate();
            Assert.That(slime.State, Is.EqualTo(EnemyState.Search).Or.EqualTo(EnemyState.Chase).Or.EqualTo(EnemyState.Alert));
        }

        [UnityTest]
        public IEnumerator HitsKnockBackAndFlashEnemies()
        {
            var slime = Spawn(Slime, new Vector2(0f, -3.9f), c => { c.wanderRadius = 0f; c.sightRadius = .5f; c.pursuitRadius = .5f; c.maxHealth = 10f; });
            yield return Wait(.1f);
            float y = slime.transform.position.y;
            yield return Tap(Key.Space);
            yield return Wait(.3f);
            Assert.That(slime.Health.Current, Is.EqualTo(9f));
            Assert.That(slime.transform.position.y, Is.LessThan(y - .15f), "pushed away from the sword");
            Assert.That(slime.Engaged, Is.True, "being hit engages");
        }
    }
}
#endif
