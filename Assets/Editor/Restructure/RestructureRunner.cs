using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    /// <summary>
    /// Entry points for the graphics/combat/AI restructure. Every step is idempotent and can be
    /// re-run from the menu after tweaking a builder.
    /// Batch: Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.RestructureRunner.RunAll
    /// </summary>
    public static class RestructureRunner
    {
        [MenuItem("Mystic Woods/Restructure/1. Rendering (URP 2D)")]
        public static void Rendering() => RenderingSetup.Run();

        [MenuItem("Mystic Woods/Restructure/2. Effects, Player and Enemies")]
        public static void Content()
        {
            EffectsBuilder.Run();
            PlayerUpgrade.Run();
            EnemyUpgrade.Run();
        }

        [MenuItem("Mystic Woods/Restructure/3. Migrate Level")]
        public static void Level()
        {
            EffectsBuilder.ImportSprites();
            LevelMigration.Run();
        }

        [MenuItem("Mystic Woods/Restructure/Verify")]
        public static void VerifyMenu() => Debug.Log(Verify());

        public static void RunAll()
        {
            try
            {
                RenderingSetup.Run();
                EffectsBuilder.Run();
                PlayerUpgrade.Run();
                LevelMigration.Run(); // converts enemies after reading legacy scene data
                AssetDatabase.SaveAssets();
                Debug.Log(Verify());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void VerifyBatch()
        {
            string report = Verify();
            Debug.Log(report);
            EditorApplication.Exit(report.Contains("FAIL") ? 1 : 0);
        }

        /// <summary>Missing scripts, legacy components and error shaders across scenes and prefabs.</summary>
        public static string Verify()
        {
            var report = new StringBuilder("[Restructure] VERIFY\n");
            bool ok = true;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Animations", "Assets/Art", "Assets/Gameplay", "Assets/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int missing = prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing > 0) { ok = false; report.AppendLine($"FAIL missing scripts in {path}: {missing}"); }
            }
            foreach (string scenePath in new[] { "Assets/Scenes/TelaInicial.unity", "Assets/Scenes/FaseMysticWoods.unity", "Assets/Scenes/TelaFinal.unity",
                         "Assets/Gameplay/Player/Sandbox/PlayerSandbox.unity" })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                int missing = objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
                int errorShaders = objects.SelectMany(g => g.GetComponents<Renderer>()).SelectMany(r => r.sharedMaterials)
                    .Count(m => m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported));
                int builtin = objects.SelectMany(g => g.GetComponents<Renderer>()).Select(r => r.sharedMaterial)
                    .Count(m => m != null && m.name == "Sprites-Default");
                if (missing > 0 || errorShaders > 0) ok = false;
                report.AppendLine($"{(missing > 0 || errorShaders > 0 ? "FAIL" : "ok  ")} {scenePath}: objects={objects.Length} missingScripts={missing} errorShaders={errorShaders} builtinSprite={builtin}");
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Art", "Assets/Gameplay" }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader == null) continue;
                bool broken = ShaderUtil.ShaderHasError(shader) || !shader.isSupported;
                if (broken) ok = false;
                report.AppendLine($"{(broken ? "FAIL" : "ok  ")} shader {shader.name}");
            }
            report.AppendLine("pipeline=" + (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name : "Built-in"));
            report.AppendLine(ok ? "RESULT PASS" : "RESULT FAIL");
            return report.ToString();
        }
    }
}
