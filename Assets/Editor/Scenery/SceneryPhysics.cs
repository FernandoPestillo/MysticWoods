using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using static MysticWoods.EditorTools.RestructureUtil;
using Object = UnityEngine.Object;

namespace MysticWoods.EditorTools
{
    /// <summary>
    /// Single source of truth for the physics of every scenery prefab and of the level border.
    /// Rules:
    ///  - Obstacles live on the "Environment" layer, on a static Rigidbody2D, with a frictionless material
    ///    so characters slide along them instead of sticking.
    ///  - The solid shape covers only the ground footprint (trunk, rock base, fence line), never the canopy.
    ///    Organic footprints are horizontal capsules; built ones (fences, walls, arch pillars) are boxes.
    ///  - Trees and the arch also get a canopy trigger + TransparetObject so they fade over the player.
    ///    Triggers never block movement and HitQuery ignores them for line of sight.
    ///  - Low vegetation and floor pieces are walkable: no collider, no body.
    ///  - Gameplay pieces (gate, seals) keep their geometry; pickups and zone triggers are left untouched.
    /// Footprints are fractions of the sprite rect: x offset from centre, width, height, centre height from the bottom.
    /// Batch: Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.SceneryPhysics.RunBatch
    /// </summary>
    public static class SceneryPhysics
    {
        public const string LayerName = "Environment";
        public const int LayerIndex = 9;
        public const string MaterialPath = "Assets/Settings/Physics/Environment Obstacle.physicsMaterial2D";
        private const string Prefabs = "Assets/Art/Environment/Prefabs/";
        private const string PropsTexture = "Assets/Art/Environment/Textures/woodland-props.png";
        private const string SlicesPath = "Documentation/Scenery/sprite-slices.json";

        public enum Body { Walkable, Trunk, Solid, Fence, Arch, Barrier }

        public readonly struct Footprint
        {
            public readonly float X, W, H, Y;
            public Footprint(float x, float w, float h, float y) { X = x; W = w; H = h; Y = y; }
        }

        public sealed class Spec
        {
            public string Name; public Body Body; public Footprint Foot; public Footprint Canopy; public float PillarX;
        }

        private static Spec Tree(string n, float x, float w, float h, float y, float canopyFrom = .3f) =>
            new() { Name = n, Body = Body.Trunk, Foot = new(x, w, h, y), Canopy = new(0, .85f, .98f - canopyFrom, (.98f + canopyFrom) / 2) };
        private static Spec Solid(string n, float x, float w, float h, float y) => new() { Name = n, Body = Body.Solid, Foot = new(x, w, h, y) };
        private static Spec Fence(string n, float x, float w, float h, float y) => new() { Name = n, Body = Body.Fence, Foot = new(x, w, h, y) };
        private static Spec Walk(string n) => new() { Name = n, Body = Body.Walkable };
        private static Spec Barrier(string n) => new() { Name = n, Body = Body.Barrier };

        public static readonly Spec[] Specs =
        {
            // woodland-props (regenerated)
            Tree("OakA", -.02f, .30f, .09f, .08f), Tree("OakB", 0, .28f, .09f, .08f), Tree("OakC", .12f, .26f, .09f, .08f),
            Walk("FernBush"), Walk("FlowerBush"), Walk("MushroomBush"),
            Solid("MossRockA", 0, .62f, .20f, .14f), Solid("MossRockB", -.05f, .58f, .18f, .13f), Solid("MossRockC", 0, .68f, .26f, .17f),
            Solid("FallenLog", -.03f, .82f, .26f, .20f), Fence("FenceA", 0, .92f, .10f, .08f), Fence("FenceB", -.02f, .84f, .10f, .08f),
            // woodland-flora-2
            Tree("FirTree", 0, .20f, .07f, .07f), Tree("BirchTree", .02f, .14f, .06f, .07f), Tree("DeadTree", .05f, .34f, .08f, .08f),
            Tree("Sapling", 0, .12f, .05f, .06f, .25f),
            Solid("MossStump", .05f, .55f, .22f, .18f), Solid("CrystalRock", 0, .72f, .24f, .18f), Solid("RootBall", .05f, .70f, .22f, .17f),
            Walk("GlowShrooms"), Walk("BerryBush"), Walk("TallGrass"), Walk("Bramble"), Walk("Bellflowers"),
            // woodland-ruins
            new() { Name = "OpenArch", Body = Body.Arch, Foot = new(0, .20f, .10f, .10f), PillarX = .29f, Canopy = new(0, .90f, .83f, .535f) },
            Solid("BrokenPillar", 0, .45f, .12f, .14f), Fence("RuinWall", 0, .86f, .14f, .16f), Solid("AmberWaystone", 0, .32f, .12f, .17f),
            Walk("StoneMedallion"),
            // gameplay pieces: geometry is owned by their scripts/prefabs
            Barrier("BossGate"), Barrier("ArenaEntrySeal"), Barrier("FinalSeal"),
        };

        [MenuItem("Mystic Woods/Scenery/Configure Physics")]
        public static void Run()
        {
            EnsureLayer();
            var material = EnsureMaterial();
            var scene = EditorSceneManager.OpenScene(LevelMigration.LevelPath, OpenSceneMode.Single);
            RefreshPropsAtlas(scene);
            foreach (var spec in Specs) ConfigurePrefab(spec, material);
            AssetDatabase.SaveAssets();
            ConfigureScene(scene, AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("Scenery physics configured.");
        }

        public static void RunBatch()
        {
            try { Run(); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------------------------------------------------------------- project settings

        private static void EnsureLayer()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            var slot = layers.GetArrayElementAtIndex(LayerIndex);
            if (slot.stringValue != LayerName)
            {
                if (!string.IsNullOrEmpty(slot.stringValue)) throw new Exception($"Layer {LayerIndex} is already '{slot.stringValue}'");
                slot.stringValue = LayerName;
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            // Environment collides with exactly what Default collides with (characters live on Default).
            for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(LayerIndex, i, Physics2D.GetIgnoreLayerCollision(0, i));
            Physics2D.IgnoreLayerCollision(LayerIndex, 0, false);
        }

        private static PhysicsMaterial2D EnsureMaterial()
        {
            var material = LoadOrCreate(MaterialPath, () => new PhysicsMaterial2D("Environment Obstacle"));
            material.friction = 0;
            material.bounciness = 0;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- regenerated props atlas

        [Serializable] private class SliceFile { public SliceAtlas[] atlases; }
        [Serializable] private class SliceAtlas { public string name; public SliceRect[] sprites; }
        [Serializable] private class SliceRect { public string name; public int x, y, w, h; }

        /// <summary>
        /// Points the existing woodland-props sprites (same names and spriteIDs, so every prefab keeps its
        /// reference) at the regenerated art, and rescales prefabs and scene instances so each prop keeps
        /// its previous height in the world.
        /// </summary>
        private static void RefreshPropsAtlas(Scene scene)
        {
            var slices = JsonUtility.FromJson<SliceFile>(File.ReadAllText(SlicesPath)).atlases.FirstOrDefault(a => a.name == "woodland-props");
            if (slices == null) return;
            var importer = (TextureImporter)AssetImporter.GetAtPath(PropsTexture);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            var ratio = new Dictionary<string, float>();
            foreach (var rect in rects)
            {
                var slice = slices.sprites.FirstOrDefault(s => s.name == rect.name) ?? throw new Exception("No new slice for " + rect.name);
                var next = new Rect(slice.x, slice.y, slice.w, slice.h);
                if (rect.rect == next) continue;
                ratio[rect.name] = rect.rect.height / slice.h;
                rect.rect = next;
                rect.alignment = SpriteAlignment.Custom;
                rect.pivot = new Vector2(.5f, Mathf.Min(.06f, 5f / slice.h)); // base of the contact shadow
            }
            if (ratio.Count == 0) return;
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();

            // Record world scales first: editing the prefab propagates to instances without overrides.
            var instances = new List<(Transform t, Vector3 scale, float r)>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (source != null && ratio.TryGetValue(source.name, out float r) && AssetDatabase.GetAssetPath(source).StartsWith(Prefabs))
                        instances.Add((t, t.localScale, r));
                }
            foreach (var (name, r) in ratio)
            {
                string path = Prefabs + name + ".prefab";
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.transform.localScale *= r;
                    var renderer = root.GetComponent<SpriteRenderer>();
                    renderer.size = renderer.sprite.bounds.size;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var (t, scale, r) in instances) t.localScale = scale * r;
            Log($"woodland-props: {ratio.Count} sprites re-cut, {instances.Count} scene instances rescaled.");
        }

        // ---------------------------------------------------------------- prefabs

        private static void ConfigurePrefab(Spec spec, PhysicsMaterial2D material)
        {
            string path = Prefabs + spec.Name + ".prefab";
            if (!File.Exists(path)) throw new Exception("Missing prefab " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (spec.Body == Body.Barrier) ConfigureBarrier(root, material);
                else ConfigureProp(root, spec, material);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureProp(GameObject root, Spec spec, PhysicsMaterial2D material)
        {
            var renderer = root.GetComponent<SpriteRenderer>() ?? throw new Exception(spec.Name + " has no SpriteRenderer on its root");
            foreach (var c in root.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
            var transparency = root.GetComponent<TransparetObject>();
            var body = root.GetComponent<Rigidbody2D>();
            if (spec.Body == Body.Walkable)
            {
                if (transparency != null) Object.DestroyImmediate(transparency);
                if (body != null) Object.DestroyImmediate(body);
                root.layer = 0;
                return;
            }

            root.layer = LayerIndex;
            if (body == null) body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            Bounds b = renderer.sprite.bounds; // local, pivot-relative
            Vector2 size = b.size;
            Vector2 Centre(Footprint f, float xShift = 0) => new(b.center.x + (f.X + xShift) * size.x, b.min.y + f.Y * size.y);

            switch (spec.Body)
            {
                case Body.Trunk:
                case Body.Solid:
                {
                    var capsule = root.AddComponent<CapsuleCollider2D>();
                    capsule.direction = CapsuleDirection2D.Horizontal;
                    capsule.size = new Vector2(spec.Foot.W * size.x, spec.Foot.H * size.y);
                    capsule.offset = Centre(spec.Foot);
                    capsule.sharedMaterial = material;
                    break;
                }
                case Body.Fence:
                {
                    var box = root.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(spec.Foot.W * size.x, spec.Foot.H * size.y);
                    box.offset = Centre(spec.Foot);
                    box.sharedMaterial = material;
                    break;
                }
                case Body.Arch:
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var pillar = root.AddComponent<BoxCollider2D>();
                        pillar.size = new Vector2(spec.Foot.W * size.x, spec.Foot.H * size.y);
                        pillar.offset = Centre(spec.Foot, side * spec.PillarX);
                        pillar.sharedMaterial = material;
                    }
                    break;
            }

            bool fades = spec.Body == Body.Trunk || spec.Body == Body.Arch;
            if (fades)
            {
                var canopy = root.AddComponent<BoxCollider2D>();
                canopy.isTrigger = true;
                canopy.size = new Vector2(spec.Canopy.W * size.x, spec.Canopy.H * size.y);
                canopy.offset = Centre(spec.Canopy);
                if (transparency == null) root.AddComponent<TransparetObject>();
            }
            else if (transparency != null) Object.DestroyImmediate(transparency);
        }

        /// <summary>Gate and seals: their scripts toggle them, so only layer, body and material are normalised.</summary>
        private static void ConfigureBarrier(GameObject root, PhysicsMaterial2D material)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider.isTrigger) continue;
                collider.gameObject.layer = LayerIndex;
                collider.sharedMaterial = material;
                var body = collider.GetComponent<Rigidbody2D>();
                if (body == null) body = collider.gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
            }
        }

        // ---------------------------------------------------------------- scene

        private static void ConfigureScene(Scene scene, PhysicsMaterial2D material)
        {
            var specs = Specs.ToDictionary(s => s.Name);
            int mirrored = 0, reverted = 0, removedAdded = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source == null || !specs.TryGetValue(source.name, out var spec) || spec.Body == Body.Barrier) continue;
                    // The prefab owns physics: drop colliders, bodies and fades added by hand on an instance
                    // (the ruins arch had its own copies, which doubled its colliders and fade script).
                    foreach (var added in PrefabUtility.GetAddedComponents(go))
                        if (added.instanceComponent is Collider2D || added.instanceComponent is Rigidbody2D || added.instanceComponent is TransparetObject)
                        {
                            added.Revert(InteractionMode.AutomatedAction);
                            removedAdded++;
                        }
                    var renderer = go.GetComponent<SpriteRenderer>();
                    var colliders = go.GetComponents<Collider2D>();
                    var originals = source.GetComponents<Collider2D>();
                    for (int i = 0; i < colliders.Length && i < originals.Length; i++)
                    {
                        // flipX mirrors the art but not the colliders: mirror their offsets on the instance.
                        if (renderer != null && renderer.flipX)
                        {
                            colliders[i].offset = new Vector2(-originals[i].offset.x, originals[i].offset.y);
                            mirrored++;
                        }
                        else if (colliders[i].offset != originals[i].offset)
                        {
                            PrefabUtility.RevertObjectOverride(colliders[i], InteractionMode.AutomatedAction);
                            reverted++;
                        }
                    }
                }

            // Map border: same layer and material as every other obstacle.
            var border = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include).FirstOrDefault(c => c.name == "Bloqueios");
            if (border != null)
            {
                border.gameObject.layer = LayerIndex;
                foreach (var c in border.GetComponents<Collider2D>()) c.sharedMaterial = material;
            }
            Log($"Scene: {removedAdded} hand-added physics components removed, {mirrored} collider offsets mirrored for flipped instances, " +
                $"{reverted} stale overrides reverted, border on '{LayerName}'.");
        }
    }
}
