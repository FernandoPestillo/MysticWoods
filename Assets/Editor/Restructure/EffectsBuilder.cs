using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    /// <summary>Editable prefabs for combat VFX, forest atmosphere (lights + volumes) and the slime orb.</summary>
    internal static class EffectsBuilder
    {
        public const string PrefabRoot = EffectsRoot + "Prefabs/";
        public const string FeedbackPath = PrefabRoot + "CombatFeedback.prefab";
        public const string AtmospherePath = PrefabRoot + "ForestAtmosphere.prefab";
        public const string OrbPath = PrefabRoot + "SlimeOrb.prefab";

        public static Sprite Shadow, Alert, Search, Dot, Ring, Orb, White;

        public static void ImportSprites()
        {
            string t = EffectsRoot + "Textures/";
            Shadow = ImportSprite(t + "blob-shadow.png", 32, new Vector2(.5f, .5f));
            Alert = ImportSprite(t + "alert-icon.png", 32, new Vector2(.5f, 0f));
            Search = ImportSprite(t + "search-icon.png", 32, new Vector2(.5f, 0f));
            Dot = ImportSprite(t + "particle-dot.png", 32, new Vector2(.5f, .5f));
            Ring = ImportSprite(t + "shockwave-ring.png", 32, new Vector2(.5f, .5f));
            Orb = ImportSprite(t + "slime-orb.png", 40, new Vector2(.5f, .5f));
            White = ImportSprite(t + "ui-white.png", 4, new Vector2(.5f, .5f));
        }

        public static void Run()
        {
            EnsureFolder(PrefabRoot);
            ImportSprites();
            BuildFeedback();
            BuildAtmosphere();
            BuildOrb();
            AssetDatabase.SaveAssets();
        }

        public static Light2D AddPointLight(GameObject go, Color color, float radius, float intensity, float falloff = .6f)
        {
            var light = Ensure<Light2D>(go);
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * .12f;
            light.falloffIntensity = falloff;
            light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();
            return light;
        }

        private static void BuildFeedback()
        {
            var root = new GameObject("CombatFeedback");
            var vfx = root.AddComponent<CombatVfx>();
            var additive = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.AdditivePath);
            var alpha = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.AlphaPath);
            var ringMat = new Material(additive) { name = "Shockwave Ring" };
            ringMat.SetTexture("_MainTex", Ring.texture);
            ringMat = SaveMaterial(ringMat, EffectsRoot + "Materials/Shockwave Ring.mat");

            var spark = System(root, "Hit Sparks", additive, .16f, .3f, 8f, .12f, .22f, 96, 4.5f, false);
            var dust = System(root, "Dust", alpha, .45f, .7f, .9f, .18f, .32f, 64, 1.2f, true, .25f);
            var death = System(root, "Death Burst", additive, .45f, .8f, 3.2f, .07f, .16f, 96, 2.2f, true, .25f);
            var splash = System(root, "Slime Splash", alpha, .3f, .5f, 3.4f, .08f, .15f, 96, 3f, true, .18f);
            var wave = System(root, "Shockwave", ringMat, .32f, .32f, 0f, 4.5f, 4.5f, 8, 0f, false);
            var size = wave.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .08f, 1f, 1f));
            var dustSize = dust.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.5f));
            var deathMain = death.main;
            deathMain.gravityModifier = -.08f;

            Set(vfx, "hitSpark", spark);
            Set(vfx, "dust", dust);
            Set(vfx, "deathBurst", death);
            Set(vfx, "slimeSplash", splash);
            Set(vfx, "shockwave", wave);
            PrefabUtility.SaveAsPrefabAsset(root, FeedbackPath);
            Object.DestroyImmediate(root);
        }

        private static Material SaveMaterial(Material material, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) { AssetDatabase.CreateAsset(material, path); return material; }
            existing.CopyPropertiesFromMaterial(material);
            Object.DestroyImmediate(material);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static ParticleSystem System(GameObject parent, string name, Material material, float minLife, float maxLife,
            float speed, float minSize, float maxSize, int max, float drag, bool shape, float radius = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = Color.white;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var sh = ps.shape;
            sh.enabled = shape;
            if (shape)
            {
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius;
                sh.rotation = Vector3.zero;
            }
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.85f, .5f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;
            if (drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Foreground";
            renderer.sortingOrder = 40;
            return ps;
        }

        private static void BuildAtmosphere()
        {
            var root = new GameObject("ForestAtmosphere");
            var global = new GameObject("Global Light 2D");
            global.transform.SetParent(root.transform, false);
            var light = global.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            // Dappled canopy light: slightly cool so warm point lights read as warm.
            light.color = new Color(.80f, .90f, .86f);
            light.intensity = .86f;
            light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();

            var post = new GameObject("Forest Post Processing");
            post.transform.SetParent(root.transform, false);
            var volume = post.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingSetup.ForestProfilePath);

            var damage = new GameObject("Damage Pulse");
            damage.transform.SetParent(root.transform, false);
            var damageVolume = damage.AddComponent<Volume>();
            damageVolume.isGlobal = true;
            damageVolume.priority = 10;
            damageVolume.weight = 0f;
            damageVolume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RenderingSetup.DamageProfilePath);
            damage.AddComponent<DamageVignette>();
            PrefabUtility.SaveAsPrefabAsset(root, AtmospherePath);
            Object.DestroyImmediate(root);
        }

        private static void BuildOrb()
        {
            var root = new GameObject("SlimeOrb");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = Orb;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.GlowPath);
            renderer.color = new Color(.75f, 1f, .7f, 1f);
            renderer.sortingOrder = 12;
            root.AddComponent<EnemyProjectile>();
            AddPointLight(root, new Color(.5f, 1f, .45f), 1.3f, .9f);
            PrefabUtility.SaveAsPrefabAsset(root, OrbPath);
            Object.DestroyImmediate(root);
        }
    }
}
