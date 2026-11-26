using System.Collections.Generic;
using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Enemies;
using MysticWoods.Gameplay.Player;
using MysticWoods.Gameplay.Player.Editor;
using MysticWoods.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    // The legacy global PlayerController would otherwise win name lookup.
    using PlayerController = MysticWoods.Gameplay.Player.PlayerController;

    /// <summary>
    /// Moves FaseMysticWoods onto the new player, enemies and URP 2D look. Legacy components are
    /// read through SerializedObject by type name, so this keeps compiling after they are deleted.
    /// </summary>
    internal static class LevelMigration
    {
        public const string LevelPath = "Assets/Scenes/FaseMysticWoods.unity";
        private const string BuiltinSprite = "Sprites-Default";
        private static readonly string[] EnvironmentPrefabs =
            { "Assets/Art/Environment/Prefabs" };

        public static void Run()
        {
            LightEnvironmentPrefabs();
            UpdateCommandsPanel();
            var scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);
            var all = AllObjects(scene);

            // 1. Read what the legacy objects knew before their scripts disappear.
            var legacyHeart = all.SelectMany(g => g.GetComponents<Component>()).FirstOrDefault(c => c != null && c.GetType().Name == "HeartSystem");
            var legacyBoss = all.SelectMany(g => g.GetComponents<Component>()).FirstOrDefault(c => c != null && c.GetType().Name == "SlimeBoss");
            GameObject bossObject = legacyBoss != null ? legacyBoss.gameObject : null;
            var barrier = ReadField(legacyBoss, "BarreiraFinal") as GameObject;
            var hearts = ReadArray(legacyHeart, "coracao").OfType<Image>().ToArray();
            var full = ReadField(legacyHeart, "cheio") as Sprite;
            var empty = ReadField(legacyHeart, "vazio") as Sprite;

            // 2. Prefab conversions propagate to every instance in the open scene.
            EnemyUpgrade.Run();
            StripLegacy(AllObjects(scene));
            if (bossObject == null) bossObject = all.FirstOrDefault(g => g != null && g.name.StartsWith("SlimeBoss"));

            GameObject player = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include)
                .Select(p => p.gameObject).FirstOrDefault();
            if (legacyHeart != null)
            {
                var legacy = legacyHeart.gameObject;
                var body = legacy.GetComponents<Collider2D>().FirstOrDefault(c => !c.isTrigger);
                Vector3 feet = body != null ? new Vector3(body.bounds.center.x, body.bounds.min.y + .04f, 0f) : legacy.transform.position;
                if (player == null)
                {
                    player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationBuilder.PrefabPath), scene);
                    player.name = "Player";
                    player.transform.SetParent(legacy.transform.parent, true);
                    player.transform.SetSiblingIndex(legacy.transform.GetSiblingIndex());
                }
                player.transform.position = feet;
                Remap(all, legacy, player);
                Log($"Legacy player replaced at {feet}. new='{player.name}' parent='{(player.transform.parent != null ? player.transform.parent.name : "<root>")}' legacy='{legacy.name}' instanceRoot='{PrefabUtility.GetOutermostPrefabInstanceRoot(player)?.name}'");
                Object.DestroyImmediate(legacy);
                all = AllObjects(scene);
            }
            if (player == null) player = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include).Select(p => p.gameObject).FirstOrDefault();
            if (player == null) throw new System.Exception("No player in " + LevelPath);
            var health = player.GetComponent<Health>();

            // 3. HUD and menus.
            HeartsHud hud = Object.FindAnyObjectByType<HeartsHud>(FindObjectsInactive.Include);
            if (hud == null && hearts.Length > 0)
            {
                hud = hearts[0].transform.parent.gameObject.AddComponent<HeartsHud>();
                hud.Bind(health, hearts, full, empty);
                EditorUtility.SetDirty(hud);
            }
            var deathClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerFoundationBuilder.Root + "Animation/Death.anim");
            const string menusPrefab = "Assets/UI/GameMenus/GameMenus.prefab";
            var menusRoot = PrefabUtility.LoadPrefabContents(menusPrefab);
            try
            {
                foreach (var controller in menusRoot.GetComponentsInChildren<GameMenuController>(true)) Set(controller, "deathClip", deathClip);
                PrefabUtility.SaveAsPrefabAsset(menusRoot, menusPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(menusRoot); }
            var menus = Object.FindAnyObjectByType<GameMenuController>(FindObjectsInactive.Include);
            if (menus != null) Set(menus, "deathClip", deathClip);

            // 4. Boss defeat opens the exit, boss bar on the HUD.
            EnemyBrain boss = bossObject != null ? bossObject.GetComponent<EnemyBrain>() : null;
            if (boss != null && barrier != null) SetArray(boss, "deactivateOnDefeat", new Object[] { barrier });
            if (boss != null && hud != null) BuildBossBar(hud.GetComponentInParent<Canvas>().rootCanvas, boss);

            // 5. Camera, atmosphere and combat feedback.
            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                EditorUtility.SetDirty(data);
            }
            foreach (var vcam in Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include))
                if (vcam.GetComponent<CinemachineShake>() == null) vcam.gameObject.AddComponent<CinemachineShake>();
            EnsurePrefabInstance(scene, EffectsBuilder.AtmospherePath);
            EnsurePrefabInstance(scene, EffectsBuilder.FeedbackPath);

            // 6. Lit materials for the world; painted light stays unlit.
            int converted = ConvertRenderers(all.SelectMany(g => g.GetComponents<Renderer>()));
            Log($"Scene renderers switched to URP 2D materials: {converted}.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log("Level migrated.");
        }

        private static readonly string[] LegacyTypes =
        {
            "SlimeController", "SkeletonController", "SlimeBoss", "HeartSystem", "PlayerController", "EnemyMeleeAttack",
            "SwordHitbox", "Sword_attack", "DetectionController", "IdleSpriteMotion", "CadencedMovement2D"
        };

        /// <summary>Removes legacy components still present as scene overrides (e.g. auto-added RequireComponents).</summary>
        private static void StripLegacy(List<GameObject> all)
        {
            int removed = 0;
            foreach (string typeName in LegacyTypes)
                foreach (var go in all)
                {
                    if (go == null) continue;
                    foreach (var component in go.GetComponents<Component>())
                    {
                        // Only global-namespace legacy types; the new PlayerController lives in a namespace.
                        if (component == null || component.GetType().FullName != typeName) continue;
                        if (typeName == "PlayerController" || typeName == "HeartSystem" || typeName == "SwordHitbox") continue; // removed with the legacy player
                        Object.DestroyImmediate(component, true);
                        removed++;
                    }
                }
            var roots = all.Where(g => g != null && PrefabUtility.IsOutermostPrefabInstanceRoot(g)).ToArray();
            PrefabUtility.RemoveUnusedOverrides(roots, InteractionMode.AutomatedAction);
            Log($"Legacy scene components removed: {removed}; overrides cleaned on {roots.Length} instances.");
        }

        private static List<GameObject> AllObjects(Scene scene)
        {
            var list = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
            return list;
        }

        /// <summary>Points every scene reference to the old player (object, transform, same-type component) at the new one.</summary>
        private static void Remap(List<GameObject> all, GameObject legacy, GameObject replacement)
        {
            var legacySet = new HashSet<GameObject>(legacy.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
            int count = 0;
            foreach (var go in all)
            {
                if (go == null || legacySet.Contains(go)) continue;
                foreach (var component in go.GetComponents<Component>())
                {
                    // Hierarchy links (m_Father/m_Children) must never be rewritten.
                    if (component == null || component is Transform) continue;
                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        var value = it.objectReferenceValue;
                        GameObject owner = value is GameObject g ? g : value is Component c ? c.gameObject : null;
                        if (owner == null || !legacySet.Contains(owner)) continue;
                        Object mapped = null;
                        if (owner == legacy)
                        {
                            if (value is GameObject) mapped = replacement;
                            else if (value is Transform) mapped = replacement.transform;
                            else if (value is Component comp) mapped = replacement.GetComponent(comp.GetType());
                        }
                        it.objectReferenceValue = mapped;
                        changed = true;
                        count++;
                        Log($"  ref {component.GetType().Name}.{it.propertyPath} on '{go.name}' -> {(mapped != null ? mapped.name : "null")}");
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Log($"Remapped {count} references.");
        }

        private static void EnsurePrefabInstance(Scene scene, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool exists = scene.GetRootGameObjects().Any(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab);
            if (!exists) PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        /// <summary>Painted light (sun patches, pickup halos) must not be darkened by Light2D nor doubled by bloom.</summary>
        private static bool IsPaintedLight(Renderer renderer)
        {
            string n = renderer.gameObject.name.ToLowerInvariant();
            return n.Contains("light") || n.Contains("glow") || n.Contains("halo") || n.Contains("firefl") || n.Contains("vaga") || n.Contains("sun");
        }

        /// <summary>Built-in sprite material → URP lit (or additive glow). Returns how many changed.</summary>
        public static int ConvertRenderers(IEnumerable<Renderer> renderers)
        {
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitSpriteMaterial);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
            int count = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !(renderer is SpriteRenderer || renderer is TilemapRenderer)) continue;
                var material = renderer.sharedMaterial;
                if (material != null && material.name != BuiltinSprite && material.shader.name != "Sprites/Default") continue;
                renderer.sharedMaterial = IsPaintedLight(renderer) ? unlit : lit;
                EditorUtility.SetDirty(renderer);
                count++;
            }
            return count;
        }

        /// <summary>Lit materials and light sources on the environment prefabs themselves.</summary>
        private static void LightEnvironmentPrefabs()
        {
            var lights = new Dictionary<string, (Color color, float radius, float intensity, float y)>
            {
                { "GoldKey", (new Color(1f, .8f, .35f), 2.6f, 1.1f, .3f) },
                { "AmberWaystone", (new Color(1f, .68f, .3f), 3.4f, .95f, .8f) },
                { "MushroomBush", (new Color(.45f, .95f, 1f), 1.7f, .7f, .3f) },
                { "LevelExit", (new Color(1f, .95f, .75f), 3.8f, 1.05f, .8f) },
                { "BossGate", (new Color(1f, .62f, .32f), 3.2f, .8f, 1f) },
                { "StoneMedallion", (new Color(.6f, .8f, 1f), 2f, .55f, .4f) },
            };
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", EnvironmentPrefabs))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = ConvertRenderers(root.GetComponentsInChildren<Renderer>(true));
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (lights.TryGetValue(name, out var spec))
                    {
                        var go = Child(root.transform, "Light 2D");
                        go.transform.localPosition = new Vector3(0f, spec.y, 0f);
                        EffectsBuilder.AddPointLight(go, spec.color, spec.radius, spec.intensity);
                        changed++;
                    }
                    if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void BuildBossBar(Canvas canvas, EnemyBrain boss)
        {
            if (canvas.GetComponentInChildren<BossHealthBar>(true) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/MainMenu/Menu Labels.asset") ??
                canvas.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null);
            var root = new GameObject("Boss Health", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -22f);
            rect.sizeDelta = new Vector2(380f, 40f);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;

            Image Bar(string name, Color color, float inset, bool filled)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                var r = (RectTransform)go.transform;
                r.SetParent(rect, false);
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(.5f, 0f);
                r.sizeDelta = new Vector2(-inset * 2f, 14f - inset * 2f);
                r.anchoredPosition = new Vector2(0f, inset);
                var image = go.GetComponent<Image>();
                image.sprite = EffectsBuilder.White;
                image.color = color;
                image.raycastTarget = false;
                if (filled)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Horizontal;
                    image.fillOrigin = 0;
                    image.fillAmount = 1f;
                }
                return image;
            }
            Bar("Frame", new Color(.09f, .06f, .04f, .92f), 0f, false);
            Bar("Back", new Color(.16f, .2f, .16f, 1f), 3f, false);
            var trail = Bar("Trail", new Color(1f, .86f, .55f, 1f), 3f, true);
            var fill = Bar("Fill", new Color(.42f, .88f, .36f, 1f), 3f, true);
            // Phase thresholds as notches.
            foreach (float threshold in boss.Config.phaseThresholds)
            {
                var notch = Bar("Phase " + threshold.ToString("0.00"), new Color(.09f, .06f, .04f, .9f), 0f, false);
                var r = notch.rectTransform;
                r.anchorMin = new Vector2(threshold, 0f);
                r.anchorMax = new Vector2(threshold, 0f);
                r.sizeDelta = new Vector2(2f, 14f);
            }
            var labelGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(.5f, 1f);
            labelRect.sizeDelta = new Vector2(0f, 24f);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = "REI SLIME";
            label.fontSize = 18f;
            label.characterSpacing = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, .93f, .78f);
            label.raycastTarget = false;
            var bar = root.AddComponent<BossHealthBar>();
            bar.Bind(boss, group, fill, trail, label);
            EditorUtility.SetDirty(bar);
        }

        /// <summary>Adds the Dodge row to the shared commands panel and updates the attack text.</summary>
        private static void UpdateCommandsPanel()
        {
            const string path = "Assets/UI/Shared/CommandsPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform Row(string name) => root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
                var run = Row("Run");
                var attack = Row("Attack");
                if (run == null || attack == null) return;
                foreach (var text in attack.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text.StartsWith("Atacar")) text.text = "Atacar: combo de três golpes";
                var dodge = Row("Dodge");
                if (dodge == null)
                {
                    dodge = Object.Instantiate(run.gameObject, run.parent).transform;
                    dodge.name = "Dodge";
                    dodge.SetSiblingIndex(attack.GetSiblingIndex() + 1);
                    foreach (var text in dodge.GetComponentsInChildren<TMP_Text>(true))
                        text.text = text.text == "Correr" ? "Esquivar (invulnerável)" : "K / CTRL / MOUSE DIR.";
                }
                // Five rows at 92 units instead of four at 100, keeping the panel size.
                void Y(string name, float y) { var r = Row(name) as RectTransform; if (r != null) r.anchoredPosition = new Vector2(r.anchoredPosition.x, y); }
                Y("Movement", 160f); Y("Run", 68f); Y("Attack", -24f); Y("Dodge", -116f); Y("Pause", -208f); Y("VoltarComandosButton", -300f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
