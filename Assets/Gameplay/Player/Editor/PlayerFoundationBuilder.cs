using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using MysticWoods.Gameplay.Player.Sandbox;

namespace MysticWoods.Gameplay.Player.Editor
{
    public static class PlayerFoundationBuilder
    {
        public const string Root = "Assets/Gameplay/Player/";
        public const string PrefabPath = Root + "Prefabs/PlayerFoundation.prefab";
        public const string ScenePath = Root + "Sandbox/PlayerSandbox.unity";
        public const string ControllerPath = Root + "Animation/PlayerLocomotion.controller";
        public const string ConfigPath = Root + "Configuration/DefaultPlayerConfig.asset";
        public const string InputPath = Root + "Configuration/PlayerControls.inputactions";
        [Serializable] private sealed class AlignmentData { public Alignment[] frames; }
        [Serializable] private sealed class Alignment { public string name; public float x; public float y; }
        private static Dictionary<string, Sprite> sprites;
        private static Dictionary<string, Alignment> alignment;

        public static void Run()
        {
            try { Create(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [MenuItem("Mystic Woods/Player/Open Sandbox")]
        public static void OpenSandbox()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }
        public static void Create()
        {
            sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/Textures/player-hd.png")
                .OfType<Sprite>().ToDictionary(s => s.name);
            alignment = JsonUtility.FromJson<AlignmentData>(File.ReadAllText(Root + "Editor/PlayerSpriteAlignment.json"))
                .frames.ToDictionary(f => f.name);
            var config = AssetDatabase.LoadAssetAtPath<PlayerConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<PlayerConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            var input = BuildInput();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = BuildAnimation();
            if (!File.Exists(PrefabPath)) BuildPlayer(config, input, controller);
            if (!File.Exists(ScenePath)) BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_FOUNDATION_ASSETS_READY");
        }
        private static InputActionAsset BuildInput()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (asset != null) return asset;
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player"); actions.AddActionMap(map);
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            var run = map.AddAction("Run", InputActionType.Button); run.AddBinding("<Keyboard>/leftShift");run.AddBinding("<Keyboard>/rightShift");
            File.WriteAllText(InputPath, actions.ToJson()); UnityEngine.Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(InputPath); return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        }
        private static AnimatorController BuildAnimation()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(PlayerAnimationController.PlaybackRateParameter, AnimatorControllerParameterType.Float);
            var parameters = controller.parameters;parameters[0].defaultFloat=1f;controller.parameters=parameters;
            var machine = controller.layers[0].stateMachine;
            for (int mode = 0; mode < 2; mode++) for (int direction = 0; direction < 4; direction++)
            {
                var facing = (PlayerFacing)direction;
                string name = (mode == 0 ? "Idle_" : "Walk_") + facing;
                var clip = BuildClip(name, mode == 1, facing);
                var state = machine.AddState(name, new Vector3(280 + direction * 220, 100 + mode * 140));
                state.motion = clip;state.writeDefaultValues = false;state.tag = mode == 0 ? "Idle" : "Walk";
                if (mode == 1) { state.speedParameter = PlayerAnimationController.PlaybackRateParameter;state.speedParameterActive = true; }
                if (mode == 0 && direction == 0) machine.defaultState = state;
            }
            return controller;
        }
        private static AnimationClip BuildClip(string name, bool walking, PlayerFacing facing)
        {
            bool side = facing == PlayerFacing.Left || facing == PlayerFacing.Right;
            int first = walking ? (side ? 24 : facing == PlayerFacing.Up ? 30 : 18) : (side ? 6 : facing == PlayerFacing.Up ? 12 : 0);
            int count = walking ? 6 : 1;
            float fps = walking ? 8 : 10;
            float duration = walking ? count / fps : 1.8f;
            var clip = new AnimationClip { name = name, frameRate = fps };
            var frames = new List<ObjectReferenceKeyframe>();var x = new AnimationCurve();var y = new AnimationCurve();
            for (int i = 0; i <= count; i++)
            {
                int frame = first + Mathf.Min(i, count - 1);string key = "player_" + frame;
                float time = i == count ? duration : i / fps;
                if (i < count || !walking) frames.Add(new ObjectReferenceKeyframe { time = i == count ? duration - 1f / fps : time, value = sprites[key] });
                var offset = alignment[key];
                x.AddKey(new Keyframe(time, offset.x * (facing == PlayerFacing.Left ? -1 : 1), float.PositiveInfinity, float.PositiveInfinity));
                y.AddKey(new Keyframe(time, offset.y, float.PositiveInfinity, float.PositiveInfinity));
            }
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Sprite", typeof(SpriteRenderer), "m_Sprite"), frames.ToArray());
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Sprite", typeof(Transform), "m_LocalPosition.x"), x);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Sprite", typeof(Transform), "m_LocalPosition.y"), y);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime = true;settings.stopTime = duration;AnimationUtility.SetAnimationClipSettings(clip, settings);
            string path = Root + "Animation/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);return existing;
            }
            AssetDatabase.CreateAsset(clip, path);return clip;
        }
        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);so.FindProperty(field).objectReferenceValue = value;so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void BuildPlayer(PlayerConfig config, InputActionAsset input, RuntimeAnimatorController controller)
        {
            var root = new GameObject("Player Foundation");
            var body = root.AddComponent<Rigidbody2D>();body.gravityScale = 0;body.linearDamping = 0;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var collider = root.AddComponent<CapsuleCollider2D>();collider.direction = CapsuleDirection2D.Horizontal;collider.size = new Vector2(.55f,.30f);collider.offset = new Vector2(0,.15f);
            var reader = root.AddComponent<PlayerInputReader>();Reference(reader,"inputActions",input);
            var motor = root.AddComponent<PlayerMovement>();Reference(motor,"config",config);root.AddComponent<PlayerController>();
            var visual = new GameObject("Visual");visual.transform.SetParent(root.transform,false);
            var animator = visual.AddComponent<Animator>();animator.runtimeAnimatorController = controller;animator.applyRootMotion = false;animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var sprite = new GameObject("Sprite");sprite.transform.SetParent(visual.transform,false);
            sprite.transform.localPosition = new Vector3(alignment["player_0"].x,alignment["player_0"].y,0);
            var renderer = sprite.AddComponent<SpriteRenderer>();renderer.sprite = sprites["player_0"];renderer.spriteSortPoint = SpriteSortPoint.Pivot;renderer.sortingOrder = 10;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Materials/IdleSprite.mat");
            var presentation = visual.AddComponent<PlayerAnimationController>();Reference(presentation,"movement",motor);Reference(presentation,"sprite",renderer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);UnityEngine.Object.DestroyImmediate(root);
        }
        private static Material Material(string name, Color color)
        {
            string path=Root+"Sandbox/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            var shader=Shader.Find("Mystic Woods/Player Sandbox Solid");if(shader==null)throw new Exception("Sandbox solid shader not available");
            material=new Material(shader){name=name};material.SetColor("_Color",color);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static GameObject Quad(string name,Vector2 position,Vector2 size,Material material,float z,bool obstacle=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position=new Vector3(position.x,position.y,z);go.transform.localScale=new Vector3(size.x,size.y,1);go.GetComponent<Renderer>().sharedMaterial=material;
            if(obstacle)go.AddComponent<BoxCollider2D>();return go;
        }
        private static void BuildScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ground=Material("Sandbox Floor",new Color(.055f,.12f,.105f));var grid=Material("Sandbox Grid",new Color(.085f,.17f,.145f));var wall=Material("Sandbox Obstacles",new Color(.27f,.36f,.31f));
            Quad("Simple Floor",Vector2.zero,new Vector2(21,12),ground,2);
            var gridRoot=new GameObject("Unit Grid (visual only)");
            for(int i=-10;i<=10;i++)Quad("Grid X "+i,new Vector2(i,0),new Vector2(.012f,12),grid,1).transform.SetParent(gridRoot.transform);
            for(int i=-6;i<=6;i++)Quad("Grid Y "+i,new Vector2(0,i),new Vector2(21,.012f),grid,1).transform.SetParent(gridRoot.transform);
            var walls=new GameObject("Collision Fixtures");
            foreach(var go in new[]{Quad("North Wall",new Vector2(0,5),new Vector2(18,.3f),wall,0,true),Quad("South Wall",new Vector2(0,-5),new Vector2(18,.3f),wall,0,true),Quad("West Wall",new Vector2(-9,0),new Vector2(.3f,10),wall,0,true),Quad("East Wall",new Vector2(9,0),new Vector2(.3f,10),wall,0,true),Quad("Vertical Obstacle",new Vector2(3,0),new Vector2(1,3),wall,0,true),Quad("Horizontal Obstacle",new Vector2(-3,2),new Vector2(3,1),wall,0,true)})go.transform.SetParent(walls.transform);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var camera=new GameObject("Sandbox Camera",typeof(Camera));camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);
            var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=6.25f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.045f,.04f);cam.transparencySortMode=TransparencySortMode.CustomAxis;cam.transparencySortAxis=Vector3.up;
            var diagnostics=new GameObject("Sandbox Diagnostics").AddComponent<PlayerSandboxDiagnostics>();Reference(diagnostics,"movement",player.GetComponent<PlayerMovement>());Reference(diagnostics,"presentation",player.GetComponentInChildren<PlayerAnimationController>());
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
    }
}
