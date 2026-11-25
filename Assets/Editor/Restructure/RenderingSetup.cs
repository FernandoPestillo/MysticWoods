using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static MysticWoods.EditorTools.RestructureUtil;

namespace MysticWoods.EditorTools
{
    /// <summary>
    /// URP 2D pipeline: pipeline asset, 2D renderer (Y-axis sorting, HDR for bloom), the forest
    /// post-processing profile and the damage pulse profile, plus effect materials.
    /// </summary>
    internal static class RenderingSetup
    {
        public const string PipelinePath = RenderingRoot + "MysticWoods-URP.asset";
        public const string RendererPath = RenderingRoot + "MysticWoods-2DRenderer.asset";
        public const string ForestProfilePath = RenderingRoot + "ForestPost.asset";
        public const string DamageProfilePath = RenderingRoot + "DamagePulse.asset";
        public const string AdditivePath = EffectsRoot + "Materials/Effect Additive.mat";
        public const string AlphaPath = EffectsRoot + "Materials/Effect Alpha.mat";
        public const string GlowPath = EffectsRoot + "Materials/Glow Additive.mat";

        public static UniversalRenderPipelineAsset Pipeline() => AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

        public static void Run()
        {
            EnsureFolder(RenderingRoot);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                // The public menu wraps an internal helper that also loads default shaders/post data.
                var menus = Type.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus, Unity.RenderPipelines.Universal.Editor", true);
                var rendererType = Type.GetType("UnityEngine.Rendering.Universal.RendererType, Unity.RenderPipelines.Universal.Runtime", true);
                var create = menus.GetMethod("CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic);
                renderer = (Renderer2DData)create.Invoke(null, new object[] { RendererPath, Enum.Parse(rendererType, "_2DRenderer"), false, "Renderer" });
            }
            var so = new SerializedObject(renderer);
            so.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
            // Lights may exceed 1; keep it modest so pixel art does not wash out.
            so.FindProperty("m_HDREmulationScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);

            var pipeline = Pipeline();
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null; // every level uses the default above
            }

            BuildForestProfile();
            BuildDamageProfile();
            BuildEffectMaterials();
            AssetDatabase.SaveAssets();
            Log("URP 2D pipeline assigned: " + PipelinePath);
        }

        private static VolumeProfile Profile(string path)
        {
            var profile = LoadOrCreate(path, () => ScriptableObject.CreateInstance<VolumeProfile>());
            return profile;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        private static void BuildForestProfile()
        {
            var profile = Profile(ForestProfilePath);
            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(.92f);
            bloom.intensity.Override(.65f);
            bloom.scatter.Override(.62f);
            bloom.tint.Override(new Color(1f, .93f, .78f));
            bloom.highQualityFiltering.Override(false);
            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(.26f);
            vignette.smoothness.Override(.42f);
            vignette.color.Override(new Color(.02f, .07f, .07f));
            var color = Component<ColorAdjustments>(profile);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            color.postExposure.Override(.05f);
            var lgg = Component<LiftGammaGain>(profile);
            // Teal shadows, warm highlights: the emerald/bronze palette of the title screen.
            lgg.lift.Override(new Vector4(.97f, 1f, 1.03f, -.02f));
            lgg.gain.Override(new Vector4(1.03f, 1f, .96f, 0f));
            var tonemap = Component<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.None);
            EditorUtility.SetDirty(profile);
        }

        private static void BuildDamageProfile()
        {
            var profile = Profile(DamageProfilePath);
            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(.42f);
            vignette.smoothness.Override(.55f);
            vignette.color.Override(new Color(.55f, .02f, .03f));
            var aberration = Component<ChromaticAberration>(profile);
            aberration.intensity.Override(.22f);
            var color = Component<ColorAdjustments>(profile);
            color.saturation.Override(-25f);
            EditorUtility.SetDirty(profile);
        }

        private static void BuildEffectMaterials()
        {
            EnsureFolder(EffectsRoot + "Materials");
            var shader = Shader.Find("Mystic Woods/Effect Sprite") ?? throw new Exception("Effect shader missing");
            var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(EffectsRoot + "Textures/particle-dot.png");
            Material Make(string path, float src, float dst, float intensity, Texture texture)
            {
                var material = LoadOrCreate(path, () => new Material(shader));
                material.shader = shader;
                material.SetFloat("_SrcBlend", src);
                material.SetFloat("_DstBlend", dst);
                material.SetFloat("_Intensity", intensity);
                if (texture != null) material.SetTexture("_MainTex", texture);
                EditorUtility.SetDirty(material);
                return material;
            }
            Make(AdditivePath, (float)BlendMode.One, (float)BlendMode.One, 1.6f, dot);
            Make(AlphaPath, (float)BlendMode.SrcAlpha, (float)BlendMode.OneMinusSrcAlpha, 1f, dot);
            Make(GlowPath, (float)BlendMode.One, (float)BlendMode.One, 1.25f, null);

            // Fireflies were drawn with a legacy particle shader; keep their texture, gain bloom.
            var fireflies = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/HighDetail/Materials/Fireflies.mat");
            if (fireflies != null && fireflies.shader != shader)
            {
                var texture = fireflies.HasProperty("_MainTex") ? fireflies.GetTexture("_MainTex") : null;
                fireflies.shader = shader;
                if (texture != null) fireflies.SetTexture("_MainTex", texture);
                fireflies.SetFloat("_SrcBlend", (float)BlendMode.One);
                fireflies.SetFloat("_DstBlend", (float)BlendMode.One);
                fireflies.SetFloat("_Intensity", 2.2f);
                EditorUtility.SetDirty(fireflies);
            }
        }
    }
}
