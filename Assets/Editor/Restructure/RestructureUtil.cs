using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MysticWoods.EditorTools
{
    /// <summary>Small helpers shared by the restructure builders.</summary>
    internal static class RestructureUtil
    {
        public const string EffectsRoot = "Assets/Art/Effects/";
        public const string RenderingRoot = "Assets/Settings/Rendering/";
        public const string IdleSpriteMaterial = "Assets/Art/Characters/HighDetail/IdleSprite.mat";
        public const string TelegraphMaterial = "Assets/Art/Characters/HighDetail/AttackTelegraph.mat";
        public const string LitSpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        public const string UnlitSpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        public static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path));
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Imports a generated PNG as a single point-filtered sprite.</summary>
        public static Sprite ImportSprite(string path, float pixelsPerUnit, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new Exception("Missing texture " + path);
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) || importer.spritePivot != pivot;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spritePivot = pivot;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }

        public static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property) ?? throw new Exception($"{target.GetType().Name}.{property} not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property) ?? throw new Exception($"{target.GetType().Name}.{property} not found");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Component FindComponentByTypeName(GameObject root, string typeName) =>
            root.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == typeName);

        public static object ReadField(Component component, string field)
        {
            if (component == null) return null;
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p == null) return null;
            return p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceValue : null;
        }

        public static Object[] ReadArray(Component component, string field)
        {
            if (component == null) return new Object[0];
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p == null || !p.isArray) return new Object[0];
            var result = new Object[p.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = p.GetArrayElementAtIndex(i).objectReferenceValue;
            return result;
        }

        public static GameObject Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>GetComponent or AddComponent (the ?? operator does not see Unity's fake nulls).</summary>
        public static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        public static T Ensure<T>(Component owner) where T : Component => Ensure<T>(owner.gameObject);

        public static void Log(string message) => Debug.Log("[Restructure] " + message);
    }
}
