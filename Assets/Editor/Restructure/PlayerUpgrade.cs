using System.Collections.Generic;
using System.IO;
using System.Linq;
using MysticWoods.Gameplay.Combat;
using MysticWoods.Gameplay.Player;
using MysticWoods.Gameplay.Player.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    // The legacy global PlayerController would otherwise win name lookup.
    using PlayerController = MysticWoods.Gameplay.Player.PlayerController;

    /// <summary>
    /// Adds combat to the PlayerFoundation prefab in place (GUID preserved): attack and death
    /// clips from the existing atlas, health, sword combat, flash, after-images, light and shadow.
    /// </summary>
    internal static class PlayerUpgrade
    {
        private const string Atlas = "Assets/Art/Characters/Textures/player-hd.png";
        [System.Serializable] private sealed class AlignmentData { public Alignment[] frames; }
        [System.Serializable] private sealed class Alignment { public string name; public float x; public float y; }

        public static void Run()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s => s.name);
            var alignment = JsonUtility.FromJson<AlignmentData>(File.ReadAllText(PlayerFoundationBuilder.Root + "Editor/PlayerSpriteAlignment.json"))
                .frames.ToDictionary(f => f.name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerFoundationBuilder.ControllerPath);
            var machine = controller.layers[0].stateMachine;
            for (int d = 0; d < 4; d++)
            {
                var facing = (PlayerFacing)d;
                bool side = facing == PlayerFacing.Left || facing == PlayerFacing.Right;
                int first = side ? 40 : facing == PlayerFacing.Up ? 44 : 36;
                // Attack frames keep the body pivot where the idle pose had it: the sword may
                // extend below the feet, so the opaque-base rule used by Idle/Walk does not apply.
                var anchor = alignment[side ? "player_6" : facing == PlayerFacing.Up ? "player_12" : "player_0"];
                var clip = BuildClip("Attack_" + facing, sprites, Enumerable.Range(first, 4).Select(i => "player_" + i).ToArray(),
                    8f, new Vector2(anchor.x * (facing == PlayerFacing.Left ? -1 : 1), anchor.y), false);
                AddState(machine, "Attack_" + facing, clip, true, new Vector3(280 + d * 220, 380));
            }
            var down = alignment["player_0"];
            var death = BuildClip("Death", sprites, new[] { "player_48", "player_49", "player_50" }, 8f, new Vector2(down.x, down.y), false);
            AddState(machine, "Death", death, false, new Vector3(280, 520));
            EditorUtility.SetDirty(controller);
            UpgradePrefab();
            AssetDatabase.SaveAssets();
            Log("Player upgraded with combat states and components.");
        }

        private static AnimationClip BuildClip(string name, Dictionary<string, Sprite> sprites, string[] frames, float fps,
            Vector2 offset, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = fps };
            var keys = frames.Select((f, i) => new ObjectReferenceKeyframe { time = i / fps, value = sprites[f] }).ToList();
            // The last sprite key holds for one frame, so the clip lasts exactly frames / fps.
            float duration = frames.Length / fps;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Sprite", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Sprite", typeof(Transform), "m_LocalPosition.x"),
                new AnimationCurve(new Keyframe(0, offset.x, float.PositiveInfinity, float.PositiveInfinity), new Keyframe(duration - 1f / fps, offset.x, float.PositiveInfinity, float.PositiveInfinity)));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Sprite", typeof(Transform), "m_LocalPosition.y"),
                new AnimationCurve(new Keyframe(0, offset.y, float.PositiveInfinity, float.PositiveInfinity), new Keyframe(duration - 1f / fps, offset.y, float.PositiveInfinity, float.PositiveInfinity)));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.stopTime = duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string path = PlayerFoundationBuilder.Root + "Animation/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void AddState(AnimatorStateMachine machine, string name, Motion clip, bool rateControlled, Vector3 position)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name, position);
            state.motion = clip;
            state.writeDefaultValues = false;
            state.tag = name.StartsWith("Attack") ? "Attack" : name;
            state.speedParameterActive = rateControlled;
            if (rateControlled) state.speedParameter = PlayerAnimationController.PlaybackRateParameter;
        }

        private static void UpgradePrefab()
        {
            string path = PlayerFoundationBuilder.PrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.tag = "Player";
                var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerFoundationBuilder.ConfigPath);
                var health = Ensure<Health>(root);
                health.Configure(Team.Player, config.MaxHealth, 0f, config.HurtInvulnerability);
                var combat = Ensure<PlayerCombat>(root);
                Set(combat, "config", config);
                var visual = root.transform.Find("Visual");
                var sprite = visual.Find("Sprite").GetComponent<SpriteRenderer>();
                var flash = Ensure<SpriteFlash>(root);
                Set(flash, "target", sprite);
                var after = Ensure<AfterImage>(root);
                Set(after, "source", sprite);
                Set(after, "ghostMaterial", AssetDatabase.LoadAssetAtPath<Material>(RenderingSetup.GlowPath));
                var presentation = visual.GetComponent<PlayerAnimationController>();
                Set(presentation, "controller", root.GetComponent<PlayerController>());

                var lightGo = Child(root.transform, "Player Light");
                lightGo.transform.localPosition = new Vector3(0f, .55f, 0f);
                var light = EffectsBuilder.AddPointLight(lightGo, new Color(1f, .86f, .62f), 4.2f, .55f, .7f);
                light.shadowsEnabled = false;

                var shadowGo = Child(root.transform, "Shadow");
                shadowGo.transform.localPosition = new Vector3(0f, .03f, 0f);
                shadowGo.transform.localScale = new Vector3(.75f, .75f, 1f);
                var shadow = Ensure<SpriteRenderer>(shadowGo);
                shadow.sprite = EffectsBuilder.Shadow;
                shadow.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
                shadow.sortingLayerID = sprite.sortingLayerID;
                shadow.sortingOrder = sprite.sortingOrder - 2;
                shadow.color = new Color(1f, 1f, 1f, .55f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
