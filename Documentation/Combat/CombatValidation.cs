using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CombatValidation
{
    public static readonly List<string> Report = new List<string>();
    public static readonly string PlayerPath = "Assets/Animations/Player/Player.prefab";
    public static readonly string[] EnemyPaths = {"Assets/Animations/Enemies/Slime/Slime.prefab", "Assets/Animations/Enemies/Skeleton/Esqueleto.prefab", "Assets/Animations/Enemies/Boss/SlimeBoss.prefab"};
    static bool finished;
    static double started;
    public static void Check(bool value, string message)
    {
        if(!value)throw new Exception(message);
        Report.Add("PASS "+message);
    }
    public static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Directory.GetCurrentDirectory(),"CombatValidationClone.marker")))
        {
            Debug.LogWarning("Run only in an isolated validation clone with CombatValidationClone.marker.");
            return;
        }
        try
        {
            started=EditorApplication.timeSinceStartup;
            Application.logMessageReceived+=(condition,stack,type)=>{
                if(type!=LogType.Error && type!=LogType.Exception)return;
                if(stack.Contains("UnityEditor.Search")){Report.Add("EDITOR NOTE: internal QuickSearch indexing exception");return;}
                Finish(new Exception(condition+"\n"+stack));
            };
            EditorApplication.update+=()=>{if(!finished && EditorApplication.timeSinceStartup-started>180)Finish(new Exception("Validation timeout"));};
            EditorSceneManager.OpenScene("Assets/Scenes/FaseMysticWoods.unity");
            var boss=UnityEngine.Object.FindFirstObjectByType<SlimeBoss>();
            Check(boss!=null && Mathf.Approximately(boss._moveSpeedSlime,1.7f),"scene boss uses dedicated controller at 1.7 u/s");
            Check(boss.GetComponent<SlimeController>()==null,"scene boss has no duplicate slime controller");
            Check(boss.BarreiraFinal!=null,"boss final barrier reference preserved");
            Check(UnityEngine.Object.FindObjectsByType<SlimeController>(FindObjectsSortMode.None).All(x=>x._moveSpeedSlime==2f),"all scene slimes configured at 2 u/s");
            Check(UnityEngine.Object.FindObjectsByType<SkeletonController>(FindObjectsSortMode.None).All(x=>x._moveSpeedSkeleton==2.4f),"all scene skeletons configured at 2.4 u/s");
            var composite=UnityEngine.Object.FindFirstObjectByType<CompositeCollider2D>();
            composite.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();composite.GenerateGeometry();
            int clear=0;for(int y=1;y<79;y++)for(int x=1;x<95;x++)if(!composite.OverlapPoint(new Vector2(x+.5f,y+.5f)))clear++;
            Check(clear==7332 && composite.OverlapPoint(new Vector2(.5f,13.5f)),"7332 grass cells clear; map boundary remains solid");
            Check(GameObject.Find("Solo").GetComponent<Collider2D>()==null,"grass ground remains non-solid");
            foreach(string path in EnemyPaths.Concat(new[]{PlayerPath}))
            {
                var prefab=Prefab(path);
                Check(prefab.GetComponent<CadencedMovement2D>()!=null,path+" smoothing component resolves");
                Check(prefab.GetComponent<Rigidbody2D>().interpolation==RigidbodyInterpolation2D.Interpolate,path+" interpolates physics");
                Check(prefab.GetComponent<IdleSpriteMotion>()!=null,path+" idle breathing preserved");
            }
            foreach(var guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Animations/Player/Player_Attack"}))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                Check(Mathf.Abs(clip.length-.5f)<.001f && !clip.isLooping,clip.name+" is a complete .50s nonlooping swing ("+clip.length+")");
                Check(!AnimationUtility.GetCurveBindings(clip).Any(x=>x.type==typeof(BoxCollider2D)),clip.name+" no longer controls sword colliders");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Combat validation driver").AddComponent<CombatValidationDriver>();
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.isPlaying=true;
        }
        catch(Exception ex){Finish(ex);}
    }
    public static void Finish(Exception error)
    {
        if(finished)return;finished=true;
        Report.Add(error==null?"RESULT PASS":"FAIL "+error);
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(),"combat-validation.txt"),Report);
        Debug.Log("COMBAT_VALIDATION "+(error==null?"PASS":"FAIL"));
        EditorApplication.Exit(error==null?0:1);
    }
}

public class CombatTestTarget : MonoBehaviour, IDamageable
{
    public float Health {get;set;}=1000;
    public bool Targetable {get;set;}=true;
    public int Hits;
    public Vector2 LastForce;
    public void OnHit(float amount,Vector2 force){if(!Targetable)return;Health-=amount;Hits++;LastForce=force;}
    public void OnHit(float amount)=>OnHit(amount,Vector2.zero);
    public void OnObjectDestroyed()=>Destroy(gameObject);
}

public class CombatValidationDriver : MonoBehaviour
{
    PlayerController player;
    CadencedMovement2D motion;
    Rigidbody2D body;
    Keyboard keyboard;
    Mouse mouse;
    HeartSystem heart;
    Camera previewCamera;
    readonly List<GameObject> temporary=new List<GameObject>();
    static readonly System.Reflection.FieldInfo Elapsed=typeof(PlayerController).GetField("attackElapsed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
    float AttackTime => (float)Elapsed.GetValue(player);
    void Check(bool v,string m)=>CombatValidation.Check(v,m);

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var device in InputSystem.devices.ToArray())if(device is Keyboard || device is Mouse)InputSystem.DisableDevice(device);
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
        var actor=Instantiate(CombatValidation.Prefab(CombatValidation.PlayerPath));
        player=actor.GetComponent<PlayerController>();motion=actor.GetComponent<CadencedMovement2D>();body=actor.GetComponent<Rigidbody2D>();
        heart=actor.GetComponent<HeartSystem>();heart.enabled=false;heart.MenuRestart=new GameObject("Mock death menu");heart.MenuRestart.SetActive(false);
        actor.transform.position=Vector3.zero;body.position=Vector2.zero;heart.vida=6;heart.Targetable=true;
        actor.GetComponent<Animator>().SetBool("isAlive",true);
        previewCamera=new GameObject("Combat Preview").AddComponent<Camera>();
        previewCamera.transform.position=new Vector3(0,.3f,-10);previewCamera.orthographic=true;previewCamera.orthographicSize=1.6f;
        previewCamera.backgroundColor=new Color(.035f,.07f,.055f,1);previewCamera.clearFlags=CameraClearFlags.SolidColor;
        yield return null;
        yield return Movement();
        yield return EnemyMovement();
        yield return Coverage();
        yield return Inputs();
        yield return Lifecycle();
        yield return Preview();
        yield return Progression();
        CombatValidation.Finish(null);
    }

    void Keys(bool click=false,params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,click));
    }
    IEnumerator Frames(int count=2){for(int i=0;i<count;i++)yield return null;}
    IEnumerator Seconds(float seconds){yield return new WaitForSeconds(seconds);}
    IEnumerator Tap(bool click=false,params Key[] movement)
    {
        bool startsNew=!player.IsAttacking;
        Keys(click,click?movement:movement.Concat(new[]{Key.Space}).ToArray());
        yield return Frames();Keys(false,movement);yield return Frames();
        if(startsNew)yield return WaitFor(()=>player.IsAttacking);
    }
    IEnumerator WaitFor(Func<bool> predicate,float timeout=1.5f)
    {
        float start=Time.realtimeSinceStartup;
        while(!predicate())
        {
            if(Time.realtimeSinceStartup-start>timeout)throw new Exception("Wait timeout at attack="+player.IsAttacking+" elapsed="+AttackTime);
            yield return null;
        }
    }
    IEnumerator ResetPlayer()
    {
        Keys();player.CancelCombat();body.position=Vector2.zero;body.linearVelocity=Vector2.zero;motion.Stop();
        yield return Frames();yield return new WaitForFixedUpdate();
    }
    IEnumerator Face(Key key)
    {
        Keys(false,key);yield return Frames();yield return new WaitForFixedUpdate();
        Keys();yield return Seconds(.12f);body.position=Vector2.zero;body.linearVelocity=Vector2.zero;motion.Stop();yield return Frames();
    }
    CombatTestTarget Target(Vector2 position,bool multiple=false,bool trigger=false)
    {
        var go=new GameObject("Hit target");temporary.Add(go);go.transform.position=position;
        var t=go.AddComponent<CombatTestTarget>();var child=new GameObject("Child body");child.transform.SetParent(go.transform,false);
        var c=child.AddComponent<BoxCollider2D>();c.size=Vector2.one*.08f;c.isTrigger=trigger;
        if(multiple){var second=go.AddComponent<CircleCollider2D>();second.radius=.06f;}
        foreach(var collider in go.GetComponentsInChildren<Collider2D>())Physics2D.IgnoreCollision(player.GetComponent<Collider2D>(),collider);
        Physics2D.SyncTransforms();return t;
    }
    void ClearTargets(){foreach(var go in temporary)if(go!=null)DestroyImmediate(go);temporary.Clear();}

    IEnumerator Movement()
    {
        yield return ResetPlayer();
        Keys(false,Key.D);yield return Frames(1);yield return Seconds(.065f);
        Check(motion.Velocity.x>0f && motion.Velocity.x<3.5f,"player accelerates instead of starting at full speed, velocity="+motion.Velocity+" key="+keyboard.dKey.isPressed+" active="+keyboard.enabled);
        yield return Seconds(.15f);
        Check(Mathf.Abs(motion.Velocity.magnitude-3.5f)<.02f,"walking reaches 3.5 u/s");
        Vector2 start=body.position;float begin=Time.fixedTime;yield return Seconds(.3f);
        float speed=(body.position-start).magnitude/(Time.fixedTime-begin);
        Check(Mathf.Abs(speed-3.5f)<.1f,"measured walking speed="+speed.ToString("F3"));
        Keys(false,Key.D,Key.W);yield return Seconds(.25f);
        Check(Mathf.Abs(motion.Velocity.magnitude-3.5f)<.02f,"diagonal walking is normalized");
        Keys(false,Key.D,Key.LeftShift);yield return Seconds(.25f);
        Check(Mathf.Abs(motion.Velocity.magnitude-5.25f)<.02f,"running reaches 5.25 u/s");
        start=body.position;begin=Time.fixedTime;yield return Seconds(.2f);
        speed=(body.position-start).magnitude/(Time.fixedTime-begin);
        Check(Mathf.Abs(speed-5.25f)<.1f,"measured running speed="+speed.ToString("F3"));
        Keys();yield return Seconds(.035f);
        Check(motion.Velocity.magnitude>0f && motion.Velocity.magnitude<5.25f,"player brakes gradually");
        yield return Seconds(.06f);Check(motion.Velocity==Vector2.zero,"running player comes to rest within approximately .08s braking");
        yield return ResetPlayer();
    }

    IEnumerator EnemyMovement()
    {
        float[] speeds={2f,2.4f,1.7f};
        for(int i=0;i<3;i++)
        {
            var go=Instantiate(CombatValidation.Prefab(CombatValidation.EnemyPaths[i]));
            go.transform.position=new Vector3(40+i*20,0,0);
            var rb=go.GetComponent<Rigidbody2D>();var m=go.GetComponent<CadencedMovement2D>();
            var detector=go.GetComponentInChildren<DetectionController>();detector.enabled=false;
            detector.detectedObjs.Add(player.GetComponent<Collider2D>());
            yield return Seconds(.08f);
            Check(m.Velocity.magnitude>0 && m.Velocity.magnitude<speeds[i],go.name+" accelerates smoothly");
            yield return Seconds(.25f);
            Check(Mathf.Abs(m.Velocity.magnitude-speeds[i])<.02f,go.name+" cruising speed="+m.Velocity.magnitude);
            Vector2 start=rb.position;float time=Time.fixedTime;yield return Seconds(.2f);
            float measured=(rb.position-start).magnitude/(Time.fixedTime-time);
            Check(Mathf.Abs(measured-speeds[i])<.12f,go.name+" measured physical speed="+measured.ToString("F3"));
            var target=go.GetComponent<IDamageable>();float hp=target.Health;
            Vector2 hitPosition=rb.position;target.OnHit(1,Vector2.right*600);yield return Seconds(.06f);
            Check(rb.position.x>hitPosition.x,go.name+" physics knockback displaces away from attack");
            Check(target.Health==hp-1 && m.Velocity==Vector2.zero,go.name+" damage preserves knockback control");
            detector.detectedObjs.Clear();yield return Seconds(.2f);
            Check(m.Velocity==Vector2.zero,go.name+" stops when target leaves");
            target.OnHit(1000);Vector2 dead=rb.position;yield return Seconds(.15f);
            Check(!target.Targetable && rb.position==dead,go.name+" does not pursue after death");
            if(i==1){go.GetComponent<SkeletonController>().Revive();Check(target.Targetable && target.Health==4,"skeleton revival preserved");}
            DestroyImmediate(go);
        }
    }

    IEnumerator Coverage()
    {
        Vector2[] directions={Vector2.right,Vector2.left,Vector2.up,Vector2.down};Key[] keys={Key.D,Key.A,Key.W,Key.S};
        for(int i=0;i<4;i++)
        {
            yield return ResetPlayer();yield return Face(keys[i]);
            Vector2 center=new Vector2(0,.1f);
            var close=Target(center+directions[i]*.2f,true);
            var edge=Target(center+directions[i]*1.28f);
            var outside=Target(center+directions[i]*1.5f);
            var behind=Target(center-directions[i]*.3f);
            var sensor=Target(center+directions[i]*.5f,false,true);
            var untargetable=Target(center+directions[i]*.6f);untargetable.Targetable=false;
            float hp=heart.Health;
            yield return Tap();yield return WaitFor(()=>player.IsAttacking);
            Check(close.Hits==0,"no damage during preparation "+directions[i]);
            yield return WaitFor(()=>AttackTime>=.16f);
            Check(close.Hits==1 && edge.Hits==1,"close and edge hits with already-overlapping targets "+directions[i]);
            Check(outside.Hits==0 && behind.Hits==0 && sensor.Hits==0 && untargetable.Hits==0,"range, back, triggers and Targetable filters "+directions[i]);
            Check(heart.Health==hp,"no self damage "+directions[i]);
            Check(close.LastForce.magnitude>599f && Vector2.Dot(close.LastForce,directions[i])>0,"600 force from player origin "+directions[i]);
            yield return WaitFor(()=>!player.IsAttacking);
            Check(close.Hits==1 && edge.Hits==1,"one hit per target across multiple colliders "+directions[i]);
            yield return Seconds(.08f);Check(close.Hits==1,"no damage after recovery "+directions[i]);
            ClearTargets();
        }
        yield return ResetPlayer();yield return Face(Key.D);
        var late=Target(new Vector2(2,.1f));
        yield return Tap();yield return WaitFor(()=>AttackTime>=.16f);
        late.transform.position=new Vector2(.8f,.1f);Physics2D.SyncTransforms();
        yield return WaitFor(()=>!player.IsAttacking);Check(late.Hits==1,"enemy entering during active window is detected");ClearTargets();
        foreach(var box in player.GetComponentsInChildren<SwordHitbox>())
            Check(box.swordCollider.isTrigger && !box.swordCollider.enabled,"sword has no solid physics and closes after attack");
    }

    IEnumerator Inputs()
    {
        yield return ResetPlayer();yield return Face(Key.D);
        var target=Target(new Vector2(.85f,.1f),true);
        yield return Tap();yield return WaitFor(()=>player.IsAttacking);yield return WaitFor(()=>AttackTime>=.36f);
        yield return Tap(true);yield return WaitFor(()=>target.Hits==2);yield return WaitFor(()=>!player.IsAttacking);
        Check(target.Hits==2,"late mouse press queues exactly one complete next swing");
        int before=target.Hits;
        Keys(false,Key.Space);yield return Seconds(1.1f);Keys();yield return Frames();
        Check(target.Hits==before+1 && !player.IsAttacking,"holding Space produces one swing");
        before=target.Hits;Keys(true,Key.Space);yield return Frames();Keys();yield return WaitFor(()=>player.IsAttacking);yield return WaitFor(()=>!player.IsAttacking);
        Check(target.Hits==before+1,"simultaneous keyboard and mouse produce one swing, delta="+(target.Hits-before));
        before=target.Hits;yield return Tap();yield return Tap(true);yield return WaitFor(()=>!player.IsAttacking);yield return Seconds(.15f);
        Check(target.Hits==before+1,"early repeated clicks do not build an unlimited queue");
        Time.timeScale=2f;
        for(int mode=0;mode<2;mode++)
        {
            before=target.Hits;
            for(int i=0;i<30;i++)
            {
                yield return Tap(mode==1);yield return WaitFor(()=>!player.IsAttacking);
                Check(!player.GetComponentsInChildren<SwordHitbox>().Any(x=>x.IsOpen),"hitboxes close after repeated "+(mode==0?"keyboard":"mouse")+" swing "+(i+1));
            }
            Check(target.Hits==before+30,"30 complete "+(mode==0?"keyboard":"mouse")+" attacks without lockups or duplicate hits");
        }
        Time.timeScale=1f;ClearTargets();
        yield return ResetPlayer();Keys(false,Key.D,Key.LeftShift);yield return Seconds(.2f);
        yield return Tap(false,Key.D,Key.LeftShift);yield return WaitFor(()=>AttackTime>=.1f);
        Check(motion.Velocity.magnitude<=1.226f,"attack caps running movement at 35 percent walking speed");
        Vector2 facing=player.Facing;Keys(false,Key.W,Key.LeftShift);yield return Frames();yield return new WaitForFixedUpdate();
        Check(player.Facing==facing,"attack facing remains fixed while moving in another direction");
        yield return WaitFor(()=>!player.IsAttacking);yield return Seconds(.2f);
        Check(player.Facing==Vector2.up && Mathf.Abs(motion.Velocity.magnitude-5.25f)<.02f,"facing and running recover automatically after attack");
        Keys(false,Key.W,Key.D);yield return Seconds(.1f);Check(player.Facing==Vector2.up,"diagonal tie retains previous cardinal facing");
        yield return ResetPlayer();
    }

    IEnumerator Lifecycle()
    {
        yield return Face(Key.D);var target=Target(new Vector2(.8f,.1f));
        yield return Tap();yield return WaitFor(()=>AttackTime>=.36f);yield return Tap();
        Time.timeScale=0;yield return Frames(3);float elapsed=AttackTime;
        Keys(true,Key.Space);yield return new WaitForSecondsRealtime(.1f);Keys();yield return Frames();
        Check(AttackTime==elapsed,"pause freezes current attack");
        Time.timeScale=1;yield return WaitFor(()=>!player.IsAttacking);yield return Seconds(.6f);
        Check(target.Hits==1,"pause discards buffered and paused commands");
        ClearTargets();yield return ResetPlayer();
        yield return Tap();yield return WaitFor(()=>player.IsAttacking);heart.OnHit(1,Vector2.left*2f);
        yield return WaitFor(()=>!player.IsAttacking);yield return Seconds(.25f);
        Check(!heart.gettingKnockBack,"knockback completes without locking attack or movement");
        Keys(false,Key.D);yield return Seconds(.2f);Check(motion.IsMoving,"movement recovers after knockback");
        yield return ResetPlayer();yield return Tap();yield return WaitFor(()=>AttackTime>=.12f);
        heart.Health=0;Check(!player.IsAttacking && !player.GetComponentsInChildren<SwordHitbox>().Any(x=>x.IsOpen),"death immediately closes sword and clears cycle");
        yield return Frames();Check(player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Player_Die"),"death state has priority");
        heart.vida=6;heart.Targetable=true;heart.MenuRestart.SetActive(false);heart.gettingKnockBack=false;
        player.GetComponent<Animator>().SetBool("isAlive",true);player.GetComponent<Animator>().Play("Blend Tree Idle");
        yield return Frames();yield return Tap();yield return WaitFor(()=>AttackTime>=.12f);
        player.enabled=false;Check(!player.GetComponentsInChildren<SwordHitbox>().Any(x=>x.IsOpen),"disabling player closes hitboxes");
        player.enabled=true;yield return Tap();yield return WaitFor(()=>!player.IsAttacking);Check(true,"player can attack again after re-enable");
    }

    IEnumerator Preview()
    {
        Key[] keys={Key.S,Key.W,Key.D,Key.A};string[] names={"down","up","right","left"};
        for(int i=0;i<4;i++)
        {
            yield return ResetPlayer();yield return Face(keys[i]);yield return Tap();yield return WaitFor(()=>AttackTime>=.18f);
            var animator=player.GetComponent<Animator>();
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Blend Tree Attack"),"directional attack state "+names[i]);
            Check(player.GetComponent<SpriteRenderer>().flipX==(i==3),"directional attack flip "+names[i]);
            Capture("combat-"+names[i]+".png");yield return WaitFor(()=>!player.IsAttacking);
        }
        yield return Seconds(.25f);Check(player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsTag("Idle"),"returns to previous Idle tag");
        var block=new MaterialPropertyBlock();player.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
        Check(block.GetVector("_IdleStretch").y>0f,"idle breathing still renders");
    }

    void Capture(string name)
    {
        var rt=new RenderTexture(640,640,24);previewCamera.targetTexture=rt;previewCamera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(640,640,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),name),tex.EncodeToPNG());
        previewCamera.targetTexture=null;RenderTexture.active=previous;DestroyImmediate(tex);DestroyImmediate(rt);
    }

    IEnumerator Progression()
    {
        Keys();
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/FaseMysticWoods.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return Frames(4);
        var realPlayer=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(realPlayer!=null && realPlayer._playerHeartSystem.Health==6,"level restart starts player with six hearts");
        var boss=UnityEngine.Object.FindFirstObjectByType<SlimeBoss>();var barrier=boss.BarreiraFinal;
        Check(barrier!=null,"boss final barrier reference resolves in Play Mode");
        var gate=UnityEngine.Object.FindFirstObjectByType<PortaBoss>();
        Check(gate!=null && gate.GetComponent<Collider2D>()!=null,"boss gate physical obstacle preserved");
        var playerBody=realPlayer.GetComponent<Rigidbody2D>();
        var key=UnityEngine.Object.FindFirstObjectByType<KeyController>();
        Check(key!=null,"gold key exists");
        playerBody.position=key.GetComponent<Collider2D>().bounds.center;
        yield return Seconds(.12f);
        Check(gate.KeysManager.chavesOuro==1,"actual gold-key trigger still collects key");
        playerBody.position=gate.GetComponent<Collider2D>().bounds.center;
        yield return Seconds(.12f);
        Check(!gate.gameObject.activeSelf && gate.KeysManager.chavesOuro==0,"physical contact opens boss gate and consumes key");
        var entry=UnityEngine.Object.FindFirstObjectByType<EntradaBoss>();
        playerBody.position=entry.GetComponent<Collider2D>().bounds.center;
        yield return Seconds(.12f);
        Check(barrier.activeSelf && !entry.gameObject.activeSelf,"arena entry activates final barrier");
        boss.OnHit(1000);
        Check(!boss.Targetable && !barrier.activeSelf,"defeating scene boss still releases final barrier");
    }
}
