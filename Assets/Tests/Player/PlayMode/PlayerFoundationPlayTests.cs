#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using MysticWoods.Gameplay.Player;

namespace MysticWoods.Tests.Player
{
    public sealed class PlayerFoundationPlayTests
    {
        private PlayerMovement motor;
        private PlayerAnimationController visual;
        private PlayerInputReader input;
        private Rigidbody2D body;
        private Keyboard keyboard;
        private InputDevice[] disabled;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale=1;
            oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            disabled=InputSystem.devices.Where(x=>x is Keyboard && x.enabled).ToArray();foreach(var device in disabled)InputSystem.DisableDevice(device);
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            EditorSceneManager.LoadSceneInPlayMode("Assets/Gameplay/Player/Sandbox/PlayerSandbox.unity",new LoadSceneParameters(LoadSceneMode.Single));
            for(int i=0;i<5;i++)yield return null;
            motor=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();visual=UnityEngine.Object.FindAnyObjectByType<PlayerAnimationController>();body=motor.GetComponent<Rigidbody2D>();input=motor.GetComponent<PlayerInputReader>();
            input.SendMessage("OnApplicationFocus",true);Teleport(new Vector2(0,-3));yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale=1;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            foreach(var device in disabled)if(device.added)InputSystem.EnableDevice(device);
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;
            var sandbox=SceneManager.GetActiveScene();var cleanup=SceneManager.CreateScene("Player Test Cleanup");SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(sandbox);
        }
        private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        private static IEnumerator Wait(float seconds){yield return new WaitForSeconds(seconds);}
        private void Teleport(Vector2 position){motor.Stop();body.position=position;body.transform.position=position;Physics2D.SyncTransforms();}

        [UnityTest]
        public IEnumerator SandboxBootsWithoutLegacySystemsAndIdleDoesNotRestart()
        {
            Assert.That(visual.Sprite.sprite,Is.Not.Null);Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Idle));
            Assert.That(SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MonoBehaviour>()).Any(x=>x.GetType().Assembly.GetName().Name=="Assembly-CSharp"),Is.False);
            int changes=visual.StateChanges;yield return Wait(.5f);
            Assert.That(visual.StateChanges,Is.EqualTo(changes));Assert.That(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.GreaterThan(.15f));
        }
        [UnityTest]
        public IEnumerator SandboxRendersWithoutErrorMaterials()
        {
            yield return null;
            var camera=Camera.main;var target=new RenderTexture(64,64,16);var texture=new Texture2D(64,64);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,64,64),0,0);texture.Apply();
                int pink=texture.GetPixels32().Count(p=>p.r>245 && p.g<10 && p.b>245);
                Assert.That(pink,Is.Zero,"sandbox floor and obstacles must use a compatible shader");
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
                UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);
            }
        }
        [UnityTest]
        public IEnumerator WalkingAcceleratesAndBrakesAtConfiguredCadence()
        {
            Keys(Key.D);yield return Wait(.06f);Assert.That(body.linearVelocity.x,Is.InRange(.1f,3.49f));
            yield return Wait(.12f);Assert.That(body.linearVelocity.x,Is.EqualTo(3.5f).Within(.02f));
            float start=body.position.x;float time=Time.time;yield return Wait(.3f);
            Assert.That(body.position.x-start,Is.EqualTo((Time.time-time)*3.5f).Within(.12f));
            Keys();yield return Wait(.12f);Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01f));Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Idle));
        }
        [UnityTest]
        public IEnumerator DiagonalAndShiftSpeedsAreNormalizedAndAffectOnlyLocomotionRate()
        {
            Keys(Key.W,Key.D);yield return Wait(.2f);Assert.That(body.linearVelocity.magnitude,Is.EqualTo(3.5f).Within(.02f));
            Assert.That(body.linearVelocity.x,Is.EqualTo(body.linearVelocity.y).Within(.001f));
            Keys(Key.W,Key.D,Key.LeftShift);yield return Wait(.2f);Assert.That(body.linearVelocity.magnitude,Is.EqualTo(5.25f).Within(.02f));
            Assert.That(visual.Animator.GetFloat(PlayerAnimationController.PlaybackRateParameter),Is.EqualTo(1.5f).Within(.01f));
            Assert.That(visual.Animator.speed,Is.EqualTo(1f));
        }
        [UnityTest]
        public IEnumerator ArrowKeysAndRightShiftUseTheSameActions()
        {
            Keys(Key.RightArrow,Key.RightShift);yield return Wait(.2f);Assert.That(body.linearVelocity.x,Is.EqualTo(5.25f).Within(.02f));
            Assert.That(visual.Facing,Is.EqualTo(PlayerFacing.Right));
        }
        [UnityTest]
        public IEnumerator WalkAdvancesWithoutRestartAndDirectionKeepsItsCycle()
        {
            Keys(Key.W);yield return Wait(.25f);Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Walk));
            int changes=visual.StateChanges;float phase=visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            var seen=new System.Collections.Generic.HashSet<Sprite>();float until=Time.time+.2f;
            while(Time.time<until){Assert.That(visual.Sprite.sprite,Is.Not.Null);seen.Add(visual.Sprite.sprite);yield return null;}
            Assert.That(seen.Count,Is.GreaterThan(1));Assert.That(visual.StateChanges,Is.EqualTo(changes));
            Assert.That(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.GreaterThan(phase+.15f));
            float before=Mathf.Repeat(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);
            Keys(Key.D);yield return Wait(.13f);
            Assert.That(visual.Facing,Is.EqualTo(PlayerFacing.Right));Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Walk));
            float after=Mathf.Repeat(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);
            Assert.That(Mathf.Repeat(after-before+1,1),Is.LessThan(.3f),"direction must continue the existing step phase");
            Assert.That(visual.StateChanges,Is.EqualTo(changes+1));
        }
        [UnityTest]
        public IEnumerator OppositeAndVerticalDirectionsNeverLeaveAStaleHorizontalFlip()
        {
            Keys(Key.A);yield return Wait(.2f);Assert.That(visual.Sprite.flipX,Is.True);
            Keys(Key.D);yield return Wait(.3f);Assert.That(visual.Sprite.flipX,Is.False);Assert.That(visual.Facing,Is.EqualTo(PlayerFacing.Right));
            Keys(Key.W);yield return Wait(.2f);Assert.That(visual.Sprite.flipX,Is.False);Assert.That(visual.Facing,Is.EqualTo(PlayerFacing.Up));
            Keys();yield return Wait(.15f);Assert.That(visual.Facing,Is.EqualTo(PlayerFacing.Up));
        }
        [UnityTest]
        public IEnumerator SolidObstacleStopsPhysicsAndPresentation()
        {
            Teleport(Vector2.zero);Keys(Key.D);yield return Wait(1);
            Assert.That(body.position.x,Is.InRange(2.1f,2.25f));
            Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01f));Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Idle));
            Keys(Key.W,Key.D);yield return Wait(.3f);Assert.That(body.position.y,Is.GreaterThan(.2f),"character can slide along obstacle");
        }
        [UnityTest]
        public IEnumerator PauseFreezesMotionAndAnimationThenResumes()
        {
            Keys(Key.D);yield return Wait(.2f);Time.timeScale=0;
            Vector2 position=body.position;float phase=visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(body.position,Is.EqualTo(position));Assert.That(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(phase).Within(.001f));
            Time.timeScale=1;yield return Wait(.15f);Assert.That(body.position.x,Is.GreaterThan(position.x+.2f));
        }
        [UnityTest]
        public IEnumerator FocusLossAndDisableClearMovementWithoutStickingOnReturn()
        {
            Keys(Key.D);yield return Wait(.2f);input.SendMessage("OnApplicationFocus",false);yield return Wait(.15f);
            Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01f));input.SendMessage("OnApplicationFocus",true);yield return Wait(.2f);
            Assert.That(body.linearVelocity.x,Is.GreaterThan(3.4f));
            motor.gameObject.SetActive(false);Assert.That(body.linearVelocity.magnitude,Is.LessThan(.01f));
            Keys();yield return null;motor.gameObject.SetActive(true);input.SendMessage("OnApplicationFocus",true);yield return Wait(.15f);
            Assert.That(visual.State,Is.EqualTo(PlayerMotionState.Idle));Assert.That(visual.Sprite.sprite,Is.Not.Null);
        }
    }
}
#endif
