using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    /// <summary>
    /// Converts the three enemy prefabs in place (GUIDs kept, so every scene instance follows):
    /// legacy chase/melee scripts are replaced by the shared brain, motor, telegraph and presentation.
    /// </summary>
    internal static class EnemyUpgrade
    {
        public const string ConfigRoot = "Assets/Gameplay/Enemies/Configuration/";
        public const string SlimePrefab = "Assets/Gameplay/Enemies/Prefabs/Slime.prefab";
        public const string SkeletonPrefab = "Assets/Gameplay/Enemies/Prefabs/Esqueleto.prefab";
        public const string BossPrefab = "Assets/Gameplay/Enemies/Prefabs/SlimeBoss.prefab";
        private static readonly string[] Legacy = { "SlimeController", "SkeletonController", "SlimeBoss", "EnemyMeleeAttack", "IdleSpriteMotion", "CadencedMovement2D" };

        public static void Run()
        {
            EnsureFolder(ConfigRoot);
            var slime = Slime();
            var skeleton = Skeleton();
            var boss = Boss();
            AddReviveGate();
            Convert(SlimePrefab, slime, 1f, null);
            Convert(SkeletonPrefab, skeleton, 1.4f, null);
            Convert(BossPrefab, boss, 30f, new Color(.55f, 1f, .45f));
            AssetDatabase.SaveAssets();
            Log("Enemy prefabs converted.");
        }

        private static EnemyConfig Config(string name)
        {
            var config = LoadOrCreate(ConfigRoot + name + ".asset", () => ScriptableObject.CreateInstance<EnemyConfig>());
            EditorUtility.SetDirty(config);
            return config;
        }

        private static EnemyConfig Slime()
        {
            var c = Config("SlimeConfig");
            c.maxHealth = 3f; c.poise = 0f; c.staggerDuration = .4f; c.invulnerabilityAfterHit = .08f;
            c.wanderSpeed = .8f; c.chaseSpeed = 2.1f; c.acceleration = 16f; c.hopPeriod = .55f;
            c.wanderRadius = 2.2f; c.wanderPause = 1.6f;
            c.sightRadius = 4.5f; c.pursuitRadius = 7.5f; c.reactionTime = .5f; c.memory = 2.5f; c.alertAllies = 4f; c.leashRadius = 10f;
            c.engageDistance = 1.1f; c.strafeAmount = .5f; c.retreatOnHit = 0f; c.separation = .6f;
            c.revives = 0; c.hitFlash = new Color(1f, 1f, 1f, .95f); c.deathTint = new Color(.55f, 1f, .5f);
            c.attacks = new[]
            {
                new EnemyAttack { name = "Bote", kind = EnemyAttackKind.Melee, minRange = 0f, maxRange = .45f, windup = .5f, active = .16f,
                    recovery = .4f, cooldown = .65f, reach = .45f, arc = 100f, damage = 1f, knockback = 6f },
                new EnemyAttack { name = "Salto", kind = EnemyAttackKind.Lunge, weight = 1.2f, minRange = .8f, maxRange = 2.4f, windup = .65f,
                    active = .3f, recovery = .6f, cooldown = 2.6f, reach = .2f, speed = 6.5f, damage = 1f, knockback = 7f },
            };
            c.phaseThresholds = new float[0];
            return c;
        }

        private static EnemyConfig Skeleton()
        {
            var c = Config("SkeletonConfig");
            c.maxHealth = 4f; c.poise = 1.5f; c.staggerDuration = .5f; c.invulnerabilityAfterHit = .08f;
            c.wanderSpeed = .9f; c.chaseSpeed = 2.4f; c.acceleration = 14f; c.hopPeriod = 0f;
            c.wanderRadius = 2.5f; c.wanderPause = 2f;
            c.sightRadius = 5.5f; c.pursuitRadius = 9f; c.reactionTime = .4f; c.memory = 3.5f; c.alertAllies = 5f; c.leashRadius = 13f;
            c.engageDistance = 1.9f; c.strafeAmount = .8f; c.retreatOnHit = .35f; c.retreatTime = .45f; c.separation = .8f;
            c.revives = 1; c.reviveDelay = 2.5f; c.reviveHealth = .5f;
            c.hitFlash = new Color(1f, 1f, 1f, .95f); c.deathTint = new Color(.92f, .88f, .75f);
            c.attacks = new[]
            {
                new EnemyAttack { name = "Corte", kind = EnemyAttackKind.Melee, minRange = 0f, maxRange = .6f, windup = .6f, active = .18f,
                    recovery = .5f, cooldown = .75f, reach = .6f, arc = 110f, damage = 1f, knockback = 7f },
                new EnemyAttack { name = "Estocada", kind = EnemyAttackKind.Lunge, weight = .8f, minRange = 1f, maxRange = 2.6f, windup = .7f,
                    active = .22f, recovery = .55f, cooldown = 3f, reach = .25f, speed = 9f, damage = 1f, knockback = 8f },
            };
            c.phaseThresholds = new float[0];
            return c;
        }

        private static EnemyConfig Boss()
        {
            var c = Config("SlimeBossConfig");
            c.maxHealth = 24f; c.poise = 8f; c.staggerDuration = .6f; c.knockbackResistance = .85f; c.invulnerabilityAfterHit = .05f;
            c.wanderSpeed = .6f; c.chaseSpeed = 1.7f; c.acceleration = 10f; c.hopPeriod = .9f;
            c.wanderRadius = 0f; c.wanderPause = 2f;
            c.sightRadius = 9f; c.pursuitRadius = 22f; c.reactionTime = .8f; c.memory = 6f; c.alertAllies = 0f; c.leashRadius = 30f;
            c.engageDistance = 2f; c.strafeAmount = 0f; c.ignoresAttackSlots = true; c.separation = 1.4f;
            c.revives = 0; c.hitFlash = new Color(1f, 1f, 1f, .8f); c.deathTint = new Color(.55f, 1f, .45f);
            c.phaseThresholds = new[] { .66f, .33f };
            c.phaseTransitionTime = 1.4f; c.phaseSpeedUp = 1.18f; c.maxSummons = 3; c.phaseShockwave = 3.4f;
            c.summonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefab);
            c.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.OrbPath).GetComponent<EnemyProjectile>();
            c.attacks = new[]
            {
                new EnemyAttack { name = "Esmagar", kind = EnemyAttackKind.Melee, minRange = 0f, maxRange = .9f, windup = .8f, active = .22f,
                    recovery = .65f, cooldown = 1f, reach = .8f, arc = 120f, damage = 1f, knockback = 10f, heavy = true, superArmor = true },
                new EnemyAttack { name = "Salto Esmagador", kind = EnemyAttackKind.Slam, weight = 1.3f, minRange = 1.5f, maxRange = 7f,
                    windup = .75f, active = .55f, recovery = .9f, cooldown = 4.5f, reach = 1.9f, speed = 7f, damage = 1f, knockback = 11f,
                    heavy = true, superArmor = true },
                new EnemyAttack { name = "Cuspe Acido", kind = EnemyAttackKind.Volley, minPhase = 2, minRange = 2f, maxRange = 8f,
                    windup = .55f, active = .1f, recovery = .5f, cooldown = 3.2f, count = 5, spread = 60f, speed = 5.5f, reach = .22f,
                    damage = 1f, knockback = 5f, superArmor = true },
                new EnemyAttack { name = "Chamado", kind = EnemyAttackKind.Summon, minPhase = 2, weight = .8f, minRange = 0f, maxRange = 20f,
                    windup = .9f, active = .2f, recovery = .6f, cooldown = 11f, count = 2, superArmor = true },
            };
            return c;
        }

        /// <summary>The skeleton's death clip flowed straight into Revive; gate it so the final death sticks.</summary>
        private static void AddReviveGate()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Gameplay/Enemies/Animation/Skeleton/Skeleton.controller");
            if (controller == null) return;
            if (controller.parameters.All(p => p.name != "canRevive"))
            {
                controller.AddParameter("canRevive", AnimatorControllerParameterType.Bool);
                var parameters = controller.parameters;
                parameters.First(p => p.name == "canRevive").defaultBool = true;
                controller.parameters = parameters;
            }
            foreach (var state in controller.layers[0].stateMachine.states.Select(s => s.state))
            {
                if (state.name != "Skeleton_Death") continue;
                foreach (var transition in state.transitions)
                {
                    if (transition.destinationState == null || transition.destinationState.name != "Skeleton_Revive") continue;
                    if (transition.conditions.All(x => x.parameter != "canRevive"))
                        transition.AddCondition(AnimatorConditionMode.If, 0f, "canRevive");
                }
            }
            EditorUtility.SetDirty(controller);
        }

        private static void Convert(string path, EnemyConfig config, float mass, Color? glow)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (string name in Legacy)
                    foreach (var component in root.GetComponents<Component>().Where(c => c != null && c.GetType().Name == name).ToArray())
                        Object.DestroyImmediate(component, true);
                var detection = root.transform.Find("AreaDetection");
                if (detection != null) Object.DestroyImmediate(detection.gameObject, true);
                foreach (var missing in root.GetComponents<MonoBehaviour>().Where(m => m == null).ToArray()) Object.DestroyImmediate(missing, true);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                root.tag = "Enemy";
                var sprite = root.GetComponent<SpriteRenderer>();
                sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(IdleSpriteMaterial);
                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.linearDamping = 0f;
                body.mass = mass;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var health = Ensure<Health>(root);
                health.Configure(Team.Enemy, config.maxHealth, config.poise, config.invulnerabilityAfterHit);
                if (root.GetComponent<EnemyMotor>() == null) root.AddComponent<EnemyMotor>();
                var flash = Ensure<SpriteFlash>(root);
                Set(flash, "target", sprite);
                if (root.GetComponent<EnemyTelegraph>() == null) root.AddComponent<EnemyTelegraph>();
                var brain = Ensure<EnemyBrain>(root);
                brain.Configure(config, AssetDatabase.LoadAssetAtPath<Material>(TelegraphMaterial));
                EditorUtility.SetDirty(brain);
                var presentation = Ensure<EnemyPresentation>(root);

                // Children are authored in world units and divided by the root scale (the boss is scaled up).
                float scale = Mathf.Max(.01f, root.transform.localScale.x);
                var collider = root.GetComponent<Collider2D>();
                Bounds local = collider != null ? LocalBounds(collider, root.transform) : sprite.sprite.bounds;
                Bounds spriteBounds = sprite.sprite.bounds;

                var shadowGo = Child(root.transform, "Shadow");
                float width = Mathf.Max(local.size.x * scale * 1.15f, .5f);
                shadowGo.transform.localPosition = new Vector3(local.center.x, local.min.y + .02f / scale, 0f);
                shadowGo.transform.localScale = Vector3.one * (width / 1.5f) / scale; // sprite is 1.5 units wide
                var shadow = Ensure<SpriteRenderer>(shadowGo);
                shadow.sprite = EffectsBuilder.Shadow;
                shadow.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
                shadow.sortingLayerID = sprite.sortingLayerID;
                shadow.sortingOrder = sprite.sortingOrder - 2;
                shadow.color = new Color(1f, 1f, 1f, .45f);

                var iconGo = Child(root.transform, "Alert Icon");
                iconGo.transform.localPosition = new Vector3(spriteBounds.center.x, spriteBounds.max.y + .12f / scale, 0f);
                iconGo.transform.localScale = Vector3.one / scale;
                var icon = Ensure<SpriteRenderer>(iconGo);
                icon.sprite = EffectsBuilder.Alert;
                icon.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
                icon.sortingLayerName = "Foreground";
                icon.sortingOrder = 50;
                icon.enabled = false;
                presentation.Bind(sprite, shadow, icon, EffectsBuilder.Alert, EffectsBuilder.Search);
                EditorUtility.SetDirty(presentation);

                if (glow.HasValue)
                {
                    var lightGo = Child(root.transform, "Boss Glow");
                    lightGo.transform.localPosition = new Vector3(spriteBounds.center.x, spriteBounds.center.y, 0f);
                    lightGo.transform.localScale = Vector3.one / scale;
                    EffectsBuilder.AddPointLight(lightGo, glow.Value, 4.5f, .9f, .55f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Bounds LocalBounds(Collider2D collider, Transform root)
        {
            switch (collider)
            {
                case BoxCollider2D box: return new Bounds(box.offset, box.size);
                case CircleCollider2D circle: return new Bounds(circle.offset, Vector3.one * circle.radius * 2f);
                case CapsuleCollider2D capsule: return new Bounds(capsule.offset, capsule.size);
                default: return new Bounds(Vector3.zero, Vector3.one * .5f);
            }
        }
    }
}
