using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using MysticWoods.Environment;
using static MysticWoods.EditorTools.RestructureUtil;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace MysticWoods.EditorTools
{
    /// <summary>
    /// Imports the Codex-generated scenery atlases (ground decor, second flora set, grass/dirt edges,
    /// ruin flagstones), builds prefabs and tiles, and dresses FaseMysticWoods. Deterministic and
    /// idempotent: re-running removes what it placed before and places the same result again.
    /// Batch: Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.SceneryExpansion.RunBatch
    /// </summary>
    public static class SceneryExpansion
    {
        private const string Root = "Assets/Art/Environment/";
        private const string Textures = Root + "Textures/";
        private const string Prefabs = Root + "Prefabs/";
        private const string SlicesPath = "Documentation/Scenery/sprite-slices.json";
        private const string DecorTexture = Textures + "ground-decor.png";
        private const string FloraTexture = Textures + "woodland-flora-2.png";
        private const string EdgeTexture = Textures + "terrain-edges.png";
        private const string FloorTexture = Textures + "ruin-floor.png";
        private const string RuinFloorTilePath = Root + "Tiles/Ruin Floor.asset";
        private const string DetailsRoot = "Scenery Details";
        private const int Seed = 20261126;

        // Sorting on the Bottom layer: Solo 0, Transicoes 1, ruin floor 2, edges 3, flowers/decor 4.
        private const int RuinFloorOrder = 2, EdgeOrder = 3, DecorOrder = 4;

        private enum Kind { Tree, Sapling, Solid, Bush, Glow }

        private sealed class FloraSpec
        {
            public string Name, Source; public Kind Kind; public float Height;
            public FloraSpec(string name, string source, Kind kind, float height) { Name = name; Source = source; Kind = kind; Height = height; }
        }

        // Height in world units at prefab scale (oaks are ~6.2 u tall, bushes ~1.75 u).
        private static readonly FloraSpec[] Flora =
        {
            new("FirTree", "OakA", Kind.Tree, 6.6f),
            new("BirchTree", "OakA", Kind.Tree, 6.2f),
            new("DeadTree", "OakA", Kind.Tree, 5.6f),
            new("MossStump", "MossRockA", Kind.Solid, 1.35f),
            new("Sapling", "OakA", Kind.Sapling, 3.1f),
            new("GlowShrooms", "MushroomBush", Kind.Glow, 1.9f),
            new("BerryBush", "FernBush", Kind.Bush, 1.6f),
            new("TallGrass", "FernBush", Kind.Bush, 1.7f),
            new("Bramble", "FernBush", Kind.Bush, 1.55f),
            new("CrystalRock", "MossRockA", Kind.Solid, 1.6f),
            new("RootBall", "MossRockA", Kind.Solid, 1.7f),
            new("Bellflowers", "FernBush", Kind.Bush, 1.3f),
        };

        [MenuItem("Mystic Woods/Scenery/Build and Dress Level")]
        public static void Run()
        {
            var slices = LoadSlices();
            var decor = ImportAtlas(DecorTexture, slices["ground-decor"], 230f, centered: true);
            var flora = ImportAtlas(FloraTexture, slices["woodland-flora-2"], 100f, centered: false);
            var edges = ImportAtlas(EdgeTexture, slices["terrain-edges"], 290f, centered: true);
            BuildRuinFloorTile();
            var prefabs = Flora.ToDictionary(f => f.Name, f => BuildFloraPrefab(f, flora[f.Name]));
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(LevelMigration.LevelPath, OpenSceneMode.Single);
            // OpenScene unloads unreferenced assets, which leaves the new tile's managed wrapper dead.
            var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(RuinFloorTilePath);
            DressLevel(scene, prefabs, decor, edges, floorTile);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("Scenery expansion done.");
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------------------------------------------------------------- import

        [Serializable] private class SliceFile { public SliceAtlas[] atlases; }
        [Serializable] private class SliceAtlas { public string name; public Slice[] sprites; }
        [Serializable] private class Slice { public string name; public int x, y, w, h; }

        private static Dictionary<string, Slice[]> LoadSlices()
        {
            var file = JsonUtility.FromJson<SliceFile>(File.ReadAllText(SlicesPath));
            return file.atlases.ToDictionary(a => a.name, a => a.sprites);
        }

#pragma warning disable CS0618 // TextureImporter.spritesheet is the simplest stable way to write rects.
        private static Dictionary<string, Sprite> ImportAtlas(string path, Slice[] slices, float ppu, bool centered)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path) ?? throw new Exception("Missing " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);
            importer.spritesheet = slices.Select(s => new SpriteMetaData
            {
                name = s.name,
                rect = new Rect(s.x, s.y, s.w, s.h),
                alignment = (int)SpriteAlignment.Custom,
                // Props stand on their base; flat decals rotate around their centre.
                pivot = centered ? new Vector2(.5f, .5f) : new Vector2(.5f, Mathf.Min(.06f, 10f / s.h)),
            }).ToArray();
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            foreach (var s in slices)
                if (!sprites.ContainsKey(s.name)) throw new Exception($"Sprite {s.name} missing after import of {path}");
            return sprites;
        }
#pragma warning restore CS0618

        private static void BuildRuinFloorTile()
        {
            // Same import contract as forest-floor.png: one opaque sprite, 256 px cap, mirrored in world space by the shader.
            var importer = (TextureImporter)AssetImporter.GetAtPath(FloorTexture) ?? throw new Exception("Missing " + FloorTexture);
            var reference = (TextureImporter)AssetImporter.GetAtPath(Textures + "forest-floor.png");
            var settings = new TextureImporterSettings();
            reference.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = reference.spritePixelsPerUnit;
            // Slabs read at 8 world units per repeat; 512 px keeps the grass's ~64 texels per unit.
            importer.maxTextureSize = 512;
            importer.textureCompression = reference.textureCompression;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAllAssetsAtPath(FloorTexture).OfType<Sprite>().First();

            string materialPath = Root + "Materials/Ruin Floor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Forest Path.mat"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetFloat("_Transitions", 1);
            material.SetFloat("_TextureWorldSize", 8);
            material.SetFloat("_EdgeSoftness", .46f);
            material.SetColor("_Tint", Color.white);
            EditorUtility.SetDirty(material);
            // The dirt path keeps its original border width now that the shader exposes it.
            var pathMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Forest Path.mat");
            pathMaterial.SetFloat("_EdgeSoftness", .27f);
            EditorUtility.SetDirty(pathMaterial);

            var tile = LoadOrCreate(RuinFloorTilePath, ScriptableObject.CreateInstance<ForestTransitionTile>);
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
        }

        private static GameObject BuildFloraPrefab(FloraSpec spec, Sprite sprite)
        {
            string path = Prefabs + spec.Name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(Prefabs + spec.Source + ".prefab");
            try
            {
                root.name = spec.Name;
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.size = sprite.bounds.size;
                Vector2 size = sprite.bounds.size;
                float scale = spec.Height / size.y;
                root.transform.localScale = Vector3.one * scale;

                foreach (var box in root.GetComponents<BoxCollider2D>()) Object.DestroyImmediate(box);
                foreach (var other in root.GetComponents<Collider2D>()) Object.DestroyImmediate(other);
                var transparency = root.GetComponent<TransparetObject>();
                switch (spec.Kind)
                {
                    case Kind.Tree:
                    case Kind.Sapling:
                    {
                        float trunk = spec.Name == "FirTree" ? .2f : spec.Kind == Kind.Sapling ? .14f : .17f;
                        var feet = root.AddComponent<BoxCollider2D>();
                        feet.size = new Vector2(size.x * trunk, size.y * .09f);
                        feet.offset = new Vector2(0, size.y * .03f);
                        var canopy = root.AddComponent<BoxCollider2D>();
                        canopy.isTrigger = true;
                        canopy.size = new Vector2(size.x * .8f, size.y * .82f);
                        canopy.offset = new Vector2(0, size.y * .5f);
                        if (transparency == null) root.AddComponent<TransparetObject>();
                        break;
                    }
                    case Kind.Solid:
                    {
                        var box = root.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(size.x * .66f, size.y * .26f);
                        box.offset = new Vector2(0, size.y * .13f);
                        if (transparency != null) Object.DestroyImmediate(transparency);
                        break;
                    }
                    default:
                        if (transparency != null) Object.DestroyImmediate(transparency);
                        break;
                }
                if (spec.Kind == Kind.Glow)
                    foreach (var light in root.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true))
                    {
                        light.color = new Color(.35f, .95f, .9f);
                        light.transform.localPosition = new Vector3(0, size.y * .35f, 0);
                    }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---------------------------------------------------------------- level dressing

        private static void DressLevel(Scene scene, Dictionary<string, GameObject> prefabs, Dictionary<string, Sprite> decor,
            Dictionary<string, Sprite> edges, TileBase floorTile)
        {
            var forest = scene.GetRootGameObjects().First(g => g.name == "High Detail Forest").transform;
            var grid = forest.Find("Grid");
            var props = forest.Find("Woodland Props");
            var path = grid.Find("Transicoes").GetComponent<Tilemap>();
            var flowers = grid.Find("Detalhes").GetComponent<TilemapRenderer>();
            flowers.sortingOrder = DecorOrder;

            // Remove what a previous run placed.
            var old = forest.Find(DetailsRoot);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var placedFlora = prefabs.Values.Select(AssetDatabase.GetAssetPath).ToHashSet();
            foreach (Transform child in props.Cast<Transform>().ToArray())
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source != null && placedFlora.Contains(AssetDatabase.GetAssetPath(source)) && !child.name.EndsWith("(Tree)"))
                    Object.DestroyImmediate(child.gameObject);
            }

            var details = new GameObject(DetailsRoot).transform;
            details.SetParent(forest, false);
            details.SetSiblingIndex(grid.GetSiblingIndex() + 1);

            var map = new PathMap(path);
            var occupied = props.Cast<Transform>().Select(t => (Vector2)t.position).ToList();
            var keepClear = KeepClearPoints(scene);

            int floorCells = PaintRuinFloor(grid, map, floorTile);
            int swapped = VaryTrees(props, prefabs);
            int shrubs = PlaceUnderstory(props, prefabs, map, occupied, keepClear);
            int bands = PlaceEdges(Child(details, "Edges").transform, map, edges);
            int decals = ScatterDecor(Child(details, "Ground Decor").transform, map, decor, keepClear);
            Log($"Ruin floor cells={floorCells}, trees varied={swapped}, understory={shrubs}, edge bands={bands}, decals={decals}.");
        }

        private sealed class PathMap
        {
            public readonly bool[,] Path = new bool[96, 80];
            public PathMap(Tilemap tilemap)
            {
                for (int x = 0; x < 96; x++)
                    for (int y = 0; y < 80; y++)
                        Path[x, y] = tilemap.HasTile(new Vector3Int(x, y, 0));
            }
            public bool IsPath(int x, int y) => x >= 0 && y >= 0 && x < 96 && y < 80 && Path[x, y];
            /// <summary>Chebyshev distance (cells) to the nearest path cell, capped.</summary>
            public int DistanceToPath(int x, int y, int cap = 4)
            {
                for (int d = 0; d <= cap; d++)
                    for (int dx = -d; dx <= d; dx++)
                        for (int dy = -d; dy <= d; dy++)
                            if ((Mathf.Abs(dx) == d || Mathf.Abs(dy) == d) && IsPath(x + dx, y + dy)) return d;
                return cap + 1;
            }
            /// <summary>Distance (cells) from a path cell to the nearest grass cell, capped.</summary>
            public int DepthInPath(int x, int y, int cap = 4)
            {
                for (int d = 1; d <= cap; d++)
                    for (int dx = -d; dx <= d; dx++)
                        for (int dy = -d; dy <= d; dy++)
                            if ((Mathf.Abs(dx) == d || Mathf.Abs(dy) == d) && !IsPath(x + dx, y + dy)) return d;
                return cap + 1;
            }
        }

        /// <summary>Gameplay points the dressing must never crowd: spawns, key, gate, exit, waystones, player start.</summary>
        private static List<Vector2> KeepClearPoints(Scene scene)
        {
            var names = new[] { "Slime", "Esqueleto", "SlimeBoss", "Player", "GoldKey", "EntradaBoss", "AreaFinal" };
            var points = new List<Vector2>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    if (names.Any(p => n == p || n.StartsWith(p + " (")) || n.StartsWith("AmberWaystone") || n.StartsWith("BossGate") ||
                        n.StartsWith("LevelExit") || n.StartsWith("ArenaEntrance") || n.StartsWith("StoneMedallion") || n.StartsWith("Fence"))
                        points.Add(t.position);
                }
            return points;
        }

        private static float Noise(float x, float y, float scale, int salt) => Mathf.PerlinNoise(x * scale + salt * 17.13f, y * scale + salt * 7.71f);

        private static int PaintRuinFloor(Transform grid, PathMap map, TileBase tile)
        {
            var go = grid.Find("PisoRuinas")?.gameObject;
            if (go == null)
            {
                go = new GameObject("PisoRuinas", typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(grid, false);
                go.transform.SetSiblingIndex(grid.Find("Transicoes").GetSiblingIndex() + 1);
            }
            var tilemap = go.GetComponent<Tilemap>();
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ruin Floor.mat");
            renderer.sortingLayerName = "Bottom";
            renderer.sortingOrder = RuinFloorOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            tilemap.ClearAllTiles();

            int count = 0;
            var arenaCentre = new Vector2(75.5f, 66.5f);
            for (int x = 0; x < 96; x++)
                for (int y = 0; y < 80; y++)
                {
                    if (!map.IsPath(x, y)) continue;
                    float n = Noise(x, y, .23f, 1) * .35f;
                    // Arena: an eroded ellipse of old flagstones around the medallion.
                    var d = new Vector2((x + .5f - arenaCentre.x) / 14.5f, (y + .5f - arenaCentre.y) / 10.2f);
                    bool arena = d.magnitude < .64f + n;
                    // Approach: a broken causeway from the ruins through the arch into the arena.
                    bool causeway = x >= 61 && x <= 64 && y >= 51 && y <= 58 && Mathf.Abs(x + .5f - 63f) < 1.6f + n * 2f;
                    if (!arena && !causeway) continue;
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }
            // Rubble and moss along the stone border break up its cell outline.
            var rubbleRoot = grid.parent.Find(DetailsRoot);
            var rubble = AssetDatabase.LoadAllAssetsAtPath(DecorTexture).OfType<Sprite>().Where(s => s.name is "Pebbles" or "MossPatch" or "GrassTuftA" or "Roots").ToArray();
            var rng = new Random(Seed + 4);
            var parent = Child(rubbleRoot, "Ruin Rubble").transform;
            for (int x = 0; x < 96; x++)
                for (int y = 0; y < 80; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (!tilemap.HasTile(cell)) continue;
                    foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    {
                        if (tilemap.HasTile(cell + d) || rng.NextDouble() > .5) continue;
                        var pos = new Vector2(x + .5f + d.x * .48f, y + .5f + d.y * .48f) + new Vector2((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * .6f;
                        var sprite = rubble[rng.Next(rubble.Length)];
                        // Flat pieces may spin freely; the tuft has upright blades and stays upright.
                        float angle = sprite.name == "GrassTuftA" ? 0 : (float)(rng.NextDouble() * 360);
                        Spawn(parent, sprite, pos, angle, (float)(.75 + rng.NextDouble() * .3), rng.NextDouble() < .5, DecorOrder);
                    }
                }
            if (tilemap.GetUsedTilesCount() == 0 && count > 0) throw new Exception("Ruin floor tiles were not stored");
            return count;
        }

        /// <summary>Replaces part of the 365 oaks with firs, birches and dead trees, grouped into groves by low-frequency noise.</summary>
        private static int VaryTrees(Transform props, Dictionary<string, GameObject> prefabs)
        {
            int swapped = 0;
            foreach (Transform child in props.Cast<Transform>().ToArray())
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (source == null || !source.name.StartsWith("Oak")) continue;
                Vector2 p = child.position;
                // Rolls depend only on position, so a re-run never swaps the oaks that were kept.
                var rng = new Random(Seed ^ Mathf.RoundToInt(p.x * 100) * 73856093 ^ Mathf.RoundToInt(p.y * 100) * 19349663);
                float grove = Noise(p.x, p.y, .07f, 3);
                string replacement = null;
                double roll = rng.NextDouble();
                if (grove < .38f && roll < .72) replacement = "FirTree";
                else if (grove > .64f && roll < .62) replacement = "BirchTree";
                else if (roll < .045) replacement = "DeadTree";
                if (replacement == null) continue;

                float relative = child.localScale.x / source.transform.localScale.x;
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[replacement], props);
                tree.name = replacement + " (Tree)";
                tree.transform.SetSiblingIndex(child.GetSiblingIndex());
                tree.transform.position = child.position;
                tree.transform.localScale = prefabs[replacement].transform.localScale * relative;
                tree.GetComponent<SpriteRenderer>().flipX = child.GetComponent<SpriteRenderer>().flipX ^ (rng.NextDouble() < .5);
                Object.DestroyImmediate(child.gameObject);
                swapped++;
            }
            return swapped;
        }

        private static int PlaceUnderstory(Transform props, Dictionary<string, GameObject> prefabs, PathMap map, List<Vector2> occupied, List<Vector2> keepClear)
        {
            var rng = new Random(Seed + 1);
            string[] small = { "MossStump", "Sapling", "GlowShrooms", "BerryBush", "TallGrass", "Bramble", "CrystalRock", "RootBall", "Bellflowers" };
            double[] weight = { 1.2, .8, .9, 1.2, 1.4, 1, .6, .6, 1.3 };
            var candidates = new List<Vector2>();
            for (int x = 1; x < 95; x++)
                for (int y = 1; y < 79; y++)
                    if (!map.IsPath(x, y) && map.DistanceToPath(x, y) == 1) candidates.Add(new Vector2(x + .5f, y + .5f));
            Shuffle(candidates, rng);
            int placed = 0;
            foreach (var c in candidates)
            {
                if (placed >= 70) break;
                var p = c + new Vector2((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * .5f;
                if (occupied.Any(o => (o - p).sqrMagnitude < 2.2f * 2.2f) || keepClear.Any(k => (k - p).sqrMagnitude < 3f * 3f)) continue;
                string name = Pick(small, weight, rng);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name], props);
                go.name = name;
                go.transform.position = p;
                go.transform.localScale = prefabs[name].transform.localScale * (float)(.88 + rng.NextDouble() * .24);
                go.GetComponent<SpriteRenderer>().flipX = rng.NextDouble() < .5;
                occupied.Add(p);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// Lays grass bands along the path/grass border to hide the cell steps. Each border cell gets one band
        /// turned toward the averaged direction of the nearby grass, so a staircase of cells reads as a
        /// slanted organic edge instead of being traced step by step.
        /// </summary>
        private static int PlaceEdges(Transform parent, PathMap map, Dictionary<string, Sprite> edges)
        {
            var rng = new Random(Seed + 2);
            var bands = edges.Keys.Where(k => k.StartsWith("Edge")).OrderBy(k => k).Select(k => edges[k]).ToArray();
            var corners = new[] { edges["CornerR"], edges["CornerL"] };
            var sides = new[] { Vector2Int.up, Vector2Int.left, Vector2Int.down, Vector2Int.right };
            int count = 0;
            for (int x = 0; x < 96; x++)
                for (int y = 0; y < 80; y++)
                {
                    if (!map.IsPath(x, y)) continue;
                    var open = sides.Where(d => !map.IsPath(x + d.x, y + d.y)).ToArray();
                    if (open.Length == 0) continue;
                    var centre = new Vector2(x + .5f, y + .5f);

                    // Smoothed outward normal from the grass in a 5x5 neighbourhood.
                    Vector2 normal = Vector2.zero;
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            if ((dx != 0 || dy != 0) && !map.IsPath(x + dx, y + dy))
                                normal += new Vector2(dx, dy) / (dx * dx + dy * dy);
                    if (normal.sqrMagnitude < .04f || open.Length >= 3)
                    {
                        // Narrow necks and dead ends: fall back to one band per open side.
                        foreach (var d in open) count += Band(parent, bands, rng, centre, d, .44f);
                        continue;
                    }
                    normal.Normalize();
                    count += Band(parent, bands, rng, centre, normal, open.Length == 2 ? .36f : .42f);
                    // Two open sides that are not opposite: a convex dirt corner, round it with a clump.
                    if (open.Length == 2 && open[0] != -open[1] && rng.NextDouble() < .55)
                    {
                        // CornerR has its grass toward the top-left (-45° from up), CornerL toward the top-right.
                        int which = rng.Next(2);
                        float angle = Angle(normal) + (which == 0 ? -45f : 45f);
                        count += Spawn(parent, corners[which], centre + normal * .3f, angle, (float)(.8 + rng.NextDouble() * .2), false, EdgeOrder);
                    }
                }
            return count;
        }

        /// <summary>The band art has its grass side up; turn it so that side faces <paramref name="outward"/>.</summary>
        private static float Angle(Vector2 outward) => Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f;

        private static int Band(Transform parent, Sprite[] bands, Random rng, Vector2 centre, Vector2 outward, float reach)
        {
            outward = outward.normalized;
            Vector2 along = new Vector2(outward.y, -outward.x) * (float)(rng.NextDouble() - .5f) * .45f;
            Vector2 pos = centre + outward * (reach + (float)(rng.NextDouble() - .5) * .12f) + along;
            float angle = Angle(outward) + (float)(rng.NextDouble() - .5) * 10f;
            return Spawn(parent, bands[rng.Next(bands.Length)], pos, angle, (float)(.9 + rng.NextDouble() * .25), rng.NextDouble() < .5, EdgeOrder);
        }

        private static int ScatterDecor(Transform parent, PathMap map, Dictionary<string, Sprite> decor, List<Vector2> keepClear)
        {
            var rng = new Random(Seed + 3);
            string[] onPath = { "Pebbles", "LeavesGold", "LeavesGreen", "Branch", "Puddle", "GrassTuftA", "GrassTuftB", "MossPatch" };
            double[] pathWeight = { 1.4, 1.2, 1, .7, .35, .8, .8, .6 };
            string[] onGrass = { "GrassTuftA", "GrassTuftB", "GrassTuftC", "Clover", "FlowersIvory", "FlowersGold", "FlowersBlue", "FairyRing", "MossPatch", "FernSprouts", "Roots", "LeavesGold" };
            double[] grassWeight = { 1.2, 1.2, 1.2, .9, .9, .8, .8, .25, .7, 1, .4, .6 };
            int count = 0;
            for (int x = 0; x < 96; x++)
                for (int y = 0; y < 80; y++)
                {
                    bool path = map.IsPath(x, y);
                    if (!path && map.DistanceToPath(x, y) > 2) continue; // hidden under canopies anyway
                    double chance = path ? (map.DepthInPath(x, y) <= 1 ? .3 : .13) : .42;
                    if (rng.NextDouble() > chance) continue;
                    var pos = new Vector2(x + (float)rng.NextDouble(), y + (float)rng.NextDouble());
                    if (keepClear.Any(k => (k - pos).sqrMagnitude < 1.2f)) continue;
                    string name = path ? Pick(onPath, pathWeight, rng) : Pick(onGrass, grassWeight, rng);
                    if (name == "Puddle" && map.DepthInPath(x, y) < 2) name = "Pebbles";
                    count += Spawn(parent, decor[name], pos, 0, (float)(.8 + rng.NextDouble() * .35), rng.NextDouble() < .5, DecorOrder);
                }
            return count;
        }

        private static int Spawn(Transform parent, Sprite sprite, Vector2 pos, float angle, float scale, bool flip, int order)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, angle));
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.flipX = flip;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitSpriteMaterial);
            r.sortingLayerName = "Bottom";
            r.sortingOrder = order;
            go.isStatic = true;
            return 1;
        }

        private static string Pick(string[] names, double[] weights, Random rng)
        {
            double roll = rng.NextDouble() * weights.Sum();
            for (int i = 0; i < names.Length; i++) { roll -= weights[i]; if (roll <= 0) return names[i]; }
            return names[^1];
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        }
    }
}
