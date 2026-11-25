using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Enemies;
using MysticWoods.Gameplay.Player;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MysticWoods.Tests.Combat
{
    public sealed class RestructureAssetTests
    {
        private static readonly string[] EnemyPrefabs =
        {
            "Assets/Animations/Enemies/Slime/Slime.prefab",
            "Assets/Animations/Enemies/Skeleton/Esqueleto.prefab",
            "Assets/Animations/Enemies/Boss/SlimeBoss.prefab",
        };

        [Test]
        public void ProjectRendersWithUrp2DAndYAxisSorting()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null, "URP asset assigned");
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Rendering/MysticWoods-2DRenderer.asset");
            Assert.That(renderer, Is.Not.Null);
            var so = new SerializedObject(renderer);
            Assert.That(so.FindProperty("m_TransparencySortMode").intValue, Is.EqualTo((int)TransparencySortMode.CustomAxis));
            Assert.That(so.FindProperty("m_TransparencySortAxis").vector3Value, Is.EqualTo(Vector3.up));
            Assert.That(pipeline.supportsHDR, Is.True, "bloom needs HDR");
        }

        [TestCase("Mystic Woods/Idle Sprite")]
        [TestCase("Mystic Woods/Forest Ground")]
        [TestCase("Mystic Woods/Attack Telegraph")]
        [TestCase("Mystic Woods/Effect Sprite")]
        [TestCase("Mystic Woods/Player Sandbox Solid")]
        public void CustomShadersCompileForUrp(string name)
        {
            var shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void EnemiesShareOneBrainAndCarryNoLegacyScripts()
        {
            foreach (string path in EnemyPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent<EnemyBrain>().Config, Is.Not.Null, path);
                Assert.That(prefab.GetComponent<Health>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<EnemyMotor>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<EnemyPresentation>(), Is.Not.Null);
                Assert.That(prefab.transform.Find("AreaDetection"), Is.Null, "perception uses queries, not trigger areas");
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m == null), Is.False, "missing scripts");
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m.GetType().Assembly.GetName().Name == "Assembly-CSharp"), Is.False);
            }
        }

        [Test]
        public void EnemyConfigsAreConsistent()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(config.pursuitRadius, Is.GreaterThanOrEqualTo(config.sightRadius), config.name);
                Assert.That(config.attacks, Is.Not.Empty, config.name);
                foreach (var attack in config.attacks)
                {
                    Assert.That(attack.minRange, Is.LessThanOrEqualTo(attack.maxRange), config.name + "/" + attack.name);
                    Assert.That(attack.windup, Is.GreaterThanOrEqualTo(.4f), "every attack is readable: " + attack.name);
                }
                for (int i = 1; i < config.phaseThresholds.Length; i++)
                    Assert.That(config.phaseThresholds[i], Is.LessThan(config.phaseThresholds[i - 1]));
                if (config.attacks.Any(a => a.kind == EnemyAttackKind.Summon)) Assert.That(config.summonPrefab, Is.Not.Null);
                if (config.attacks.Any(a => a.kind == EnemyAttackKind.Volley)) Assert.That(config.projectilePrefab, Is.Not.Null);
            }
        }

        [Test]
        public void LevelIsWiredToTheNewCharacterAndAtmosphere()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FaseMysticWoods.unity", OpenSceneMode.Single);
            var players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            Assert.That(players.Length, Is.EqualTo(1));
            var player = players[0];
            Assert.That(player.CompareTag("Player"), Is.True);
            var vcam = Object.FindAnyObjectByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include);
            Assert.That(vcam.Follow, Is.EqualTo(player.transform));
            Assert.That(vcam.GetComponent<CinemachineShake>(), Is.Not.Null);
            var hud = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).FirstOrDefault(m => m.GetType().Name == "HeartsHud");
            Assert.That(hud, Is.Not.Null);
            Assert.That(new SerializedObject(hud).FindProperty("health").objectReferenceValue, Is.EqualTo(player.GetComponent<Health>()));
            Assert.That(new SerializedObject(hud).FindProperty("hearts").arraySize, Is.EqualTo(6));
            var boss = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include).Single(b => b.Config.phaseThresholds.Length > 0);
            Assert.That(new SerializedObject(boss).FindProperty("deactivateOnDefeat").arraySize, Is.GreaterThan(0), "boss opens the exit");
            Assert.That(Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include).Length, Is.EqualTo(13), "7 slimes, 5 skeletons, 1 boss");
            Assert.That(Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include).Count(l => l.lightType == Light2D.LightType.Global), Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<CombatVfx>(FindObjectsInactive.Include), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DamageVignette>(FindObjectsInactive.Include), Is.Not.Null);
            var camera = Object.FindAnyObjectByType<Camera>();
            Assert.That(camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing, Is.True);
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Any(m => m == null), Is.False);
        }
    }

    public sealed class HealthRuleTests
    {
        private GameObject go;
        private Health health;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Health");
            health = go.AddComponent<Health>();
            health.Configure(Team.Enemy, 4f, 2f, 0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static DamageInfo Hit(float amount, Team team = Team.Player, float poise = 1f) =>
            new DamageInfo(amount, Vector2.right, 0f, poise, null, team, Vector2.zero);

        [Test]
        public void SameTeamAndZeroDamageAreIgnored()
        {
            Assert.That(health.ReceiveHit(Hit(1f, Team.Enemy)), Is.EqualTo(HitResult.Ignored));
            Assert.That(health.ReceiveHit(Hit(0f)), Is.EqualTo(HitResult.Ignored));
            Assert.That(health.Current, Is.EqualTo(4f));
        }

        [Test]
        public void PoiseBreaksOnceThenRefills()
        {
            int staggers = 0;
            health.Staggered += _ => staggers++;
            health.ReceiveHit(Hit(1f));
            Assert.That(staggers, Is.Zero);
            health.ReceiveHit(Hit(1f));
            Assert.That(staggers, Is.EqualTo(1));
            Assert.That(health.Poise, Is.EqualTo(2f));
        }

        [Test]
        public void InvulnerabilityLocksStack()
        {
            health.PushInvulnerability();
            health.PushInvulnerability();
            health.PopInvulnerability();
            Assert.That(health.ReceiveHit(Hit(1f)), Is.EqualTo(HitResult.Ignored));
            health.PopInvulnerability();
            Assert.That(health.ReceiveHit(Hit(1f)), Is.EqualTo(HitResult.Damaged));
        }

        [Test]
        public void DeathIsReportedOnceAndRestoreRevives()
        {
            int deaths = 0;
            health.Died += _ => deaths++;
            Assert.That(health.ReceiveHit(Hit(10f)), Is.EqualTo(HitResult.Killed));
            Assert.That(health.ReceiveHit(Hit(1f)), Is.EqualTo(HitResult.Ignored));
            health.Kill();
            Assert.That(deaths, Is.EqualTo(1));
            health.Restore(.5f);
            Assert.That(health.Current, Is.EqualTo(2f));
            Assert.That(health.IsDead, Is.False);
        }
    }
}
