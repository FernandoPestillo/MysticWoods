using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.Animations;
using MysticWoods.Gameplay.Player;
using MysticWoods.Gameplay.Player.Editor;

namespace MysticWoods.Tests.Player
{
    public sealed class PlayerFoundationAssetTests
    {
        [Test]
        public void RuntimeAssemblyHasNoLegacyOrUIReference()
        {
            string[] names = typeof(PlayerMovement).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
            Assert.That(names, Does.Not.Contain("Assembly-CSharp"));
            Assert.That(names.Any(x => x.Contains("TextMeshPro") || x.Contains("Unity.ugui")), Is.False);
        }
        [Test]
        public void PrefabContainsOnlyFoundationBehavioursAndFootPhysics()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            foreach(var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                Assert.That(behaviour.GetType().Assembly, Is.EqualTo(typeof(PlayerMovement).Assembly));
            Assert.That(prefab.GetComponentsInChildren<Collider2D>(true).Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponent<CapsuleCollider2D>().size, Is.EqualTo(new Vector2(.55f,.30f)));
            Assert.That(prefab.GetComponent<Rigidbody2D>().gravityScale, Is.Zero);
            Assert.That(prefab.GetComponent<Rigidbody2D>().interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
            Assert.That(prefab.GetComponent<Rigidbody2D>().collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous));
        }
        [Test]
        public void InputContainsOnlyMovementAndRunWithBothKeyboardLayouts()
        {
            var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerFoundationBuilder.InputPath);
            Assert.That(input.actionMaps.Single().actions.Select(x=>x.name), Is.EquivalentTo(new[]{"Move","Run"}));
            var paths=input.FindAction("Player/Move").bindings.Select(x=>x.path).ToArray();
            Assert.That(paths, Does.Contain("<Keyboard>/w"));Assert.That(paths,Does.Contain("<Keyboard>/upArrow"));
            Assert.That(input.FindAction("Player/Run").bindings.Count, Is.EqualTo(2));
        }
        [Test]
        public void ConfigGuardsInvalidValuesWithoutChangingAsset()
        {
            var config=ScriptableObject.CreateInstance<PlayerConfig>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"walkSpeed\":-1,\"runSpeed\":-2,\"accelerationTime\":0,\"brakingTime\":0}",config);
                Assert.That(config.WalkSpeed,Is.GreaterThan(0));Assert.That(config.RunSpeed,Is.GreaterThanOrEqualTo(config.WalkSpeed));
                Assert.That(config.AccelerationTime,Is.GreaterThan(0));Assert.That(config.BrakingTime,Is.GreaterThan(0));
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        [TestCase(1,0,PlayerFacing.Down,PlayerFacing.Right)]
        [TestCase(-1,0,PlayerFacing.Down,PlayerFacing.Left)]
        [TestCase(0,1,PlayerFacing.Right,PlayerFacing.Up)]
        [TestCase(0,-1,PlayerFacing.Left,PlayerFacing.Down)]
        [TestCase(0,0,PlayerFacing.Left,PlayerFacing.Left)]
        [TestCase(.02f,0,PlayerFacing.Down,PlayerFacing.Right)]
        [TestCase(-.02f,0,PlayerFacing.Up,PlayerFacing.Left)]
        [TestCase(1,1,PlayerFacing.Down,PlayerFacing.Up)]
        [TestCase(1,1,PlayerFacing.Left,PlayerFacing.Right)]
        [TestCase(-1,-1,PlayerFacing.Up,PlayerFacing.Down)]
        [TestCase(-1,-1,PlayerFacing.Right,PlayerFacing.Left)]
        public void DirectionRemainsCardinalAndFollowsMotion(float x,float y,PlayerFacing previous,PlayerFacing expected)
            => Assert.That(PlayerDirection.Resolve(new Vector2(x,y),previous,.03f),Is.EqualTo(expected));

        [Test]
        public void AnimatorContainsEightRealStatesAndOnlyPresentationParameter()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerFoundationBuilder.ControllerPath);
            Assert.That(controller.parameters.Select(x=>x.name),Is.EqualTo(new[]{PlayerAnimationController.PlaybackRateParameter}));
            var states=controller.layers[0].stateMachine.states.Select(x=>x.state).ToArray();
            Assert.That(states.Length,Is.EqualTo(8));Assert.That(states.All(x=>x.motion!=null && !x.writeDefaultValues && x.transitions.Length==0),Is.True);
            Assert.That(controller.layers[0].stateMachine.defaultState.name,Is.EqualTo("Idle_Down"));
            foreach(var state in states)
            {
                var clip=(AnimationClip)state.motion;
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.True);
                foreach(var b in AnimationUtility.GetCurveBindings(clip))
                {
                    Assert.That(b.path,Is.EqualTo("Sprite"));Assert.That(b.propertyName,Does.StartWith("m_LocalPosition."));
                }
                Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Single().path,Is.EqualTo("Sprite"));
                Assert.That(clip.events,Is.Empty);
                Assert.That(clip.length,Is.EqualTo(state.name.StartsWith("Walk")?.75f:1.8f).Within(.001f));
            }
        }
        [Test]
        public void EveryFrameAlignsItsOpaqueFootprintWithoutTouchingPhysics()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationBuilder.PrefabPath);
            var player=UnityEngine.Object.Instantiate(prefab);var renderer=player.GetComponentInChildren<SpriteRenderer>();
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes("Assets/Art/Characters/HighDetail/player-hd.png"));var pixels=texture.GetPixels32();
            try
            {
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerFoundationBuilder.ControllerPath);
                var visual=player.transform.Find("Visual").gameObject;var originalPosition=player.transform.position;
                foreach(var clip in controller.animationClips)
                {
                    renderer.flipX=clip.name.EndsWith("Left");
                    var keys=AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
                    foreach(var key in keys)
                    {
                        clip.SampleAnimation(visual,key.time);var sprite=renderer.sprite;
                        int minX=int.MaxValue,maxX=-1,minY=int.MaxValue;Rect r=sprite.rect;
                        for(int y=(int)r.yMin;y<(int)r.yMax;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)
                            if(pixels[y*texture.width+x].a>32){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);}
                        var foot=new Vector2(((minX+maxX+1)*.5f-r.xMin-sprite.pivot.x)/sprite.pixelsPerUnit,(minY-r.yMin-sprite.pivot.y)/sprite.pixelsPerUnit);
                        if(renderer.flipX)foot.x=-foot.x;
                        foot+=(Vector2)renderer.transform.localPosition;
                        Assert.That(foot.magnitude,Is.LessThan(.0005f),clip.name+" @ "+key.time+" feet="+foot);
                        Assert.That(player.transform.position,Is.EqualTo(originalPosition));
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
