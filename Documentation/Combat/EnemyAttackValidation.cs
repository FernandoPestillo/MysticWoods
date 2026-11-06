using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class EnemyAttackValidation
{
    public static readonly List<string> Report=new List<string>();
    public static readonly string[] Paths={"Assets/Animations/Enemies/Slime/Slime.prefab","Assets/Animations/Enemies/Skeleton/Esqueleto.prefab","Assets/Animations/Enemies/Boss/SlimeBoss.prefab"};
    static bool finished;
    static double start;
    public static void Check(bool value,string message){if(!value)throw new Exception(message);Report.Add("PASS "+message);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Directory.GetCurrentDirectory(),"CombatValidationClone.marker")))
        {
            Debug.LogWarning("Run only in an isolated validation clone with CombatValidationClone.marker.");
            return;
        }
        try
        {
            start=EditorApplication.timeSinceStartup;
            Application.logMessageReceived+=(condition,stack,type)=>{
                if(type!=LogType.Error && type!=LogType.Exception)return;
                if(stack.Contains("UnityEditor.Search")){Report.Add("EDITOR NOTE: internal QuickSearch indexing exception");return;}
                Finish(new Exception(condition+"\n"+stack));
            };
            EditorApplication.update+=()=>{if(!finished && EditorApplication.timeSinceStartup-start>180)Finish(new Exception("Test timeout"));};
            EditorSceneManager.OpenScene("Assets/Scenes/FaseMysticWoods.unity");
            var boss=UnityEngine.Object.FindFirstObjectByType<SlimeBoss>();
            Check(boss.knockbackForce==12 && boss.GetComponent<EnemyMeleeAttack>()!=null,"scene boss inherits telegraphed melee and impulse 12");
            Check(UnityEngine.Object.FindObjectsByType<SlimeController>(FindObjectsSortMode.None).All(x=>x.knockbackForce==8 && x.GetComponent<EnemyMeleeAttack>()!=null),"scene slimes use impulse 8 and melee preparation");
            Check(UnityEngine.Object.FindObjectsByType<SkeletonController>(FindObjectsSortMode.None).All(x=>x.knockbackForce==8 && x.GetComponent<EnemyMeleeAttack>()!=null),"scene skeletons use impulse 8 and melee preparation");
            Check(boss.BarreiraFinal!=null,"boss barrier reference retained");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Enemy attack tests").AddComponent<EnemyAttackDriver>();
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.isPlaying=true;
        }
        catch(Exception error){Finish(error);}
    }
    public static void Finish(Exception error)
    {
        if(finished)return;finished=true;Report.Add(error==null?"RESULT PASS":"FAIL "+error);
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(),"enemy-attack-validation.txt"),Report);
        Debug.Log("ENEMY_ATTACK_VALIDATION "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
    }
}

public class EnemyAttackDriver : MonoBehaviour
{
    PlayerController player;
    HeartSystem heart;
    Rigidbody2D playerBody;
    Collider2D playerCollider;
    Keyboard keyboard;
    GameObject enemy;
    Collider2D enemyCollider;
    EnemyMeleeAttack attack;
    IDamageable enemyHealth;
    DetectionController detector;
    Camera camera;
    int kind;
    void Check(bool value,string message)=>EnemyAttackValidation.Check(value,message);
    float Setting(string name)=>new SerializedObject(attack).FindProperty(name).floatValue;
    float Impulse=>kind==2?12:8;
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var device in InputSystem.devices.ToArray())if(device is Keyboard || device is Mouse)InputSystem.DisableDevice(device);
        keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        var actor=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Animations/Player/Player.prefab"));
        player=actor.GetComponent<PlayerController>();heart=actor.GetComponent<HeartSystem>();playerBody=actor.GetComponent<Rigidbody2D>();playerCollider=actor.GetComponent<Collider2D>();
        heart.coracao=Array.Empty<UnityEngine.UI.Image>();heart.MenuRestart=new GameObject("Death menu");heart.MenuRestart.SetActive(false);
        camera=new GameObject("Enemy attack camera").AddComponent<Camera>();camera.transform.position=new Vector3(.5f,.1f,-10);camera.orthographic=true;camera.orthographicSize=2;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.07f,.055f,1);
        yield return null;
        for(kind=0;kind<3;kind++)
        {
            yield return ContactAndCadence();
            yield return Dodge(false);yield return Dodge(true);
            yield return InterruptAndDeath();
        }
        yield return PauseAndDisable();
        yield return BoundedRecoil();
        Check(!ShaderUtil.ShaderHasError(Shader.Find("Mystic Woods/Idle Sprite")),"character preparation shader compiles on Metal");
        Check(!ShaderUtil.ShaderHasError(Shader.Find("Mystic Woods/Attack Telegraph")),"telegraph shader compiles on Metal");
        EnemyAttackValidation.Finish(null);
    }
    IEnumerator WaitFor(Func<bool> predicate,float timeout=4f)
    {
        float started=Time.realtimeSinceStartup;
        while(!predicate())
        {
            if(Time.realtimeSinceStartup-started>timeout)throw new Exception("Wait timeout kind="+kind+" phase="+(attack!=null?attack.Phase.ToString():"none")+" hp="+heart.Health+" detected="+(detector!=null?detector.detectedObjs.Count:-1)+" enemyHP="+(enemyHealth!=null?enemyHealth.Health:0));
            yield return null;
        }
    }
    IEnumerator Delay(float seconds){yield return new WaitForSeconds(seconds);}
    IEnumerator Frames(int count=2){for(int i=0;i<count;i++)yield return null;}
    void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    void TeleportPlayer(Vector2 position)
    {
        playerBody.position=position;playerBody.linearVelocity=Vector2.zero;player.GetComponent<CadencedMovement2D>().Stop();Physics2D.SyncTransforms();
    }
    IEnumerator Spawn()
    {
        if(enemy!=null)DestroyImmediate(enemy);
        Keys();player.gameObject.SetActive(false);
        heart.vida=6;heart._targetable=true;heart.gettingKnockBack=false;
        player.gameObject.SetActive(true);heart.Targetable=true;heart.MenuRestart.SetActive(false);
        player.GetComponent<Animator>().SetBool("isAlive",true);player.GetComponent<Animator>().Play("Blend Tree Idle");
        TeleportPlayer(new Vector2(10,10));
        enemy=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAttackValidation.Paths[kind]));
        enemy.transform.position=Vector3.zero;
        attack=enemy.GetComponent<EnemyMeleeAttack>();enemyHealth=enemy.GetComponent<IDamageable>();enemyCollider=enemy.GetComponent<Collider2D>();
        detector=enemy.GetComponentInChildren<DetectionController>();detector.enabled=false;
        yield return Frames();Physics2D.SyncTransforms();
        Vector2 origin=enemyCollider.bounds.center;
        float distance=enemyCollider.bounds.extents.x+playerCollider.bounds.extents.x+Setting("reach")*.5f;
        TeleportPlayer(origin+Vector2.right*distance-new Vector2(0,.1f));
        detector.detectedObjs.Add(playerCollider);
        camera.transform.position=new Vector3(origin.x+distance*.4f,origin.y,-10);camera.orthographicSize=kind==2?2.8f:1.7f;
    }
    void DisableController()
    {
        var script=enemy.GetComponents<MonoBehaviour>().Single(x=>x is SlimeController || x is SkeletonController || x is SlimeBoss);script.enabled=false;
    }
    void EnableController()
    {
        var script=enemy.GetComponents<MonoBehaviour>().Single(x=>x is SlimeController || x is SkeletonController || x is SlimeBoss);script.enabled=true;
    }
    IEnumerator ContactAndCadence()
    {
        yield return Spawn();DisableController();
        Vector2 origin=enemyCollider.bounds.center;
        float contact=enemyCollider.bounds.extents.x+playerCollider.bounds.extents.x-.01f;
        TeleportPlayer(origin+Vector2.right*contact-new Vector2(0,.1f));yield return Delay(.12f);
        Check(heart.Health==6,"kind "+kind+": body contact alone does not damage");
        EnableController();yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);
        float began=Time.fixedTime;float hp=heart.Health;Vector2 start=playerBody.position;Vector2 enemyStart=enemy.GetComponent<Rigidbody2D>().position;
        Check(attack.TelegraphVisible,"kind "+kind+": warning sector appears at windup start");
        yield return Delay(attack.WindupDuration*.45f);
        Check(heart.Health==hp,"kind "+kind+": preparation is harmless");
        Check(enemy.GetComponent<CadencedMovement2D>().Velocity==Vector2.zero,"kind "+kind+": pursuit stops during preparation");
        Check(enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsTag("Attack"),"kind "+kind+": dedicated attack animation state");
        var block=new MaterialPropertyBlock();enemy.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
        Check(block.GetColor("_AttackTint").a>.1f,"kind "+kind+": visible preparation tint");
        Check(enemy.GetComponentsInChildren<Collider2D>().Length==2,"kind "+kind+": warning creates no physical collider");
        Capture("enemy-"+kind+"-windup.png");
        yield return WaitFor(()=>heart.Health<hp);
        float firstHit=Time.fixedTime;
        Check(firstHit-began>=attack.WindupDuration-.04f,"kind "+kind+": first damage delayed by windup, seconds="+(firstHit-began).ToString("F3"));
        Check(heart.Health==hp-1,"kind "+kind+": one damage per strike");
        Capture("enemy-"+kind+"-strike.png");
        yield return Delay(.26f);
        float displacement=Vector2.Distance(playerBody.position,start);
        Check(displacement<.35f,"kind "+kind+": short actual recoil="+displacement.ToString("F3")+" units");
        Check(!heart.gettingKnockBack,"kind "+kind+": player control recovers quickly");
        Check(heart.Health==hp-1,"kind "+kind+": active window does not repeat damage");
        yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Ready);
        Check(!attack.TelegraphVisible,"kind "+kind+": warning closes during recovery/cooldown");
        yield return Delay(attack.CooldownDuration*.5f);
        Check(heart.Health==hp-1 && attack.Phase==EnemyMeleeAttack.AttackPhase.Ready,"kind "+kind+": cooldown prevents immediate new attack");
        // Restore the same melee distance after recoil to measure the next complete cycle.
        float distance=enemyCollider.bounds.extents.x+playerCollider.bounds.extents.x+Setting("reach")*.4f;
        TeleportPlayer((Vector2)enemyCollider.bounds.center+Vector2.right*distance-new Vector2(0,.1f));
        yield return WaitFor(()=>heart.Health<hp-1);
        float interval=Time.fixedTime-firstHit;
        float minimum=Setting("activeWindow")+Setting("recovery")+Setting("cooldown")+attack.WindupDuration;
        Check(interval>=minimum-.05f,"kind "+kind+": measured strike interval="+interval.ToString("F3")+" seconds");
        DestroyImmediate(enemy);yield return Frames();
    }
    IEnumerator Dodge(bool behind)
    {
        yield return Spawn();yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);
        Vector2 locked=attack.AttackDirection;float hp=heart.Health;
        yield return Delay(.12f);
        Vector2 center=enemyCollider.bounds.center;
        Vector2 escape=behind?-locked*(enemyCollider.bounds.extents.magnitude+playerCollider.bounds.extents.magnitude+.25f):locked*(attack.DamageRadius+playerCollider.bounds.extents.magnitude+2f);
        TeleportPlayer(center+escape-new Vector2(0,.1f));
        yield return Delay(attack.WindupDuration*.5f);
        Check(Vector2.Dot(locked,attack.AttackDirection)>.999f,"kind "+kind+": attack direction stays locked while player dodges "+behind);
        yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Recovery);
        Check(heart.Health==hp,"kind "+kind+": "+(behind?"dodging behind":"leaving warned area")+" avoids damage");
        DestroyImmediate(enemy);yield return Frames();
    }
    IEnumerator InterruptAndDeath()
    {
        yield return Spawn();yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);yield return Delay(.12f);
        float hp=heart.Health;enemyHealth.OnHit(1);
        Check(!attack.IsBusy && !attack.TelegraphVisible,"kind "+kind+": player hit interrupts preparation and warning");
        yield return Delay(attack.WindupDuration);
        Check(heart.Health==hp,"kind "+kind+": interrupted attack has no delayed damage");
        yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);
        enemyHealth.OnHit(1000);
        Check(!attack.IsBusy && !attack.TelegraphVisible && !enemyHealth.Targetable,"kind "+kind+": death cancels every attack phase");
        yield return Delay(.16f);Check(heart.Health==hp,"kind "+kind+": dead enemy does not deal damage");
        if(kind==1)
        {
            enemy.GetComponent<SkeletonController>().Revive();
            if(!detector.detectedObjs.Contains(playerCollider))detector.detectedObjs.Add(playerCollider);
            yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);
            Check(heart.Health==hp && attack.TelegraphVisible,"skeleton revival requires a fresh visible preparation");
        }
        DestroyImmediate(enemy);yield return Frames();
    }
    IEnumerator PauseAndDisable()
    {
        kind=0;yield return Spawn();yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);yield return Delay(.15f);
        float hp=heart.Health;float progress=enemy.GetComponent<Animator>().GetFloat("MeleeProgress");
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
        Check(heart.Health==hp && attack.Phase==EnemyMeleeAttack.AttackPhase.Windup,"pause freezes windup without damage");
        float paused=enemy.GetComponent<Animator>().GetFloat("MeleeProgress");yield return new WaitForSecondsRealtime(.12f);
        Check(Mathf.Abs(enemy.GetComponent<Animator>().GetFloat("MeleeProgress")-paused)<.0001f,"pause freezes visible preparation progress");
        Time.timeScale=1;yield return Delay(.1f);Check(heart.Health==hp,"resume preserves remaining reaction time");
        enemy.SetActive(false);Check(!attack.IsBusy && !attack.TelegraphVisible,"disabling enemy removes attack warning");
        enemy.SetActive(true);detector.detectedObjs.Add(playerCollider);yield return WaitFor(()=>attack.Phase==EnemyMeleeAttack.AttackPhase.Windup);
        yield return Delay(.2f);Check(heart.Health==hp,"reactivated enemy cannot resume a stale strike");
        DestroyImmediate(enemy);yield return Frames();
    }
    IEnumerator BoundedRecoil()
    {
        player.gameObject.SetActive(false);heart.vida=6;heart._targetable=true;player.gameObject.SetActive(true);heart.Targetable=true;
        player.GetComponent<Animator>().SetBool("isAlive",true);player.GetComponent<Animator>().Play("Blend Tree Idle");
        TeleportPlayer(Vector2.zero);Keys();yield return Frames();
        heart.OnHit(1,Vector2.right*1000);heart.OnHit(1,Vector2.left*1000);
        Check(heart.Health==5 && heart.IsInvulnerable,"simultaneous hits do not stack damage or opposite impulses");
        yield return Delay(.23f);
        Check(playerBody.position.x>0 && playerBody.position.magnitude<.27f,"excessive input impulse is capped; displacement="+playerBody.position.magnitude.ToString("F3"));
        Check(!heart.gettingKnockBack,"knockback finishes after .18 seconds");
        yield return Delay(.15f);Check(!heart.IsInvulnerable,"damage grace period expires");
        heart.OnHit(1,Vector2.left*8);Check(heart.Health==4,"next deliberate strike can damage after grace period");
        Keys(Key.D);yield return Delay(.4f);Check(player.GetComponent<CadencedMovement2D>().Velocity.x>3,"walking recovers after receiving enemy hit");Keys();
    }
    void Capture(string name)
    {
        var rt=new RenderTexture(900,600,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(900,600,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,900,600),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),name),image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=previous;
        DestroyImmediate(image);DestroyImmediate(rt);
    }
}
