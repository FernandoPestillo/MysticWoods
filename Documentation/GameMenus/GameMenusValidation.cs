using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using MysticWoods.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class GameMenusValidation
{
    public static readonly List<string> Report=new List<string>();
    static bool finished;
    static double started;
    static bool hadPrefs;
    static readonly string[] prefKeys={MainMenuPreferences.VolumeKey,MainMenuPreferences.WidthKey,MainMenuPreferences.HeightKey,MainMenuPreferences.ModeKey};
    static readonly Dictionary<string,string> prefs=new Dictionary<string,string>();
    public static void Check(bool condition,string message){if(!condition)throw new Exception(message);Report.Add("PASS "+message);}
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Directory.GetCurrentDirectory(),"GameMenusValidationClone.marker")))
        {
            Debug.LogWarning("Run only in an isolated validation clone with GameMenusValidationClone.marker.");
            return;
        }
        try
        {
            foreach(var key in prefKeys)if(PlayerPrefs.HasKey(key))prefs[key]=key==MainMenuPreferences.VolumeKey?PlayerPrefs.GetFloat(key).ToString("R",System.Globalization.CultureInfo.InvariantCulture):PlayerPrefs.GetInt(key).ToString();
            hadPrefs=true;started=EditorApplication.timeSinceStartup;
            Application.logMessageReceived+=(condition,stack,type)=>{
                if(type!=LogType.Error && type!=LogType.Exception)return;
                if(stack.Contains("UnityEditor.Search")){Report.Add("EDITOR NOTE: internal QuickSearch indexing exception");return;}
                Finish(new Exception(condition+"\n"+stack));
            };
            EditorApplication.update+=()=>{if(!finished && EditorApplication.timeSinceStartup-started>240)Finish(new Exception("menu validation timeout"));};
            foreach(string scene in new[]{"FaseMysticWoods","TelaFinal","TelaInicial"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,scene+": exactly one EventSystem");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Game menu validation driver").AddComponent<GameMenusValidationDriver>();
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.isPlaying=true;
        }
        catch(Exception ex){Finish(ex);}
    }
    public static void Finish(Exception error)
    {
        if(finished)return;finished=true;
        if(hadPrefs)
        {
            foreach(var key in prefKeys)
            {
                if(!prefs.ContainsKey(key)){PlayerPrefs.DeleteKey(key);continue;}
                if(key==MainMenuPreferences.VolumeKey)PlayerPrefs.SetFloat(key,float.Parse(prefs[key],System.Globalization.CultureInfo.InvariantCulture));else PlayerPrefs.SetInt(key,int.Parse(prefs[key]));
            }
            PlayerPrefs.Save();
        }
        Report.Add(error==null?"RESULT PASS":"FAIL "+error);
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(),"game-menus-validation.txt"),Report);
        Debug.Log("GAME_MENUS_VALIDATION "+(error==null?"PASS":"FAIL"));EditorApplication.Exit(error==null?0:1);
    }
}

public class GameMenusValidationDriver : MonoBehaviour
{
    Keyboard keyboard;Mouse mouse;
    Camera captureCamera;
    GameMenuController menus;
    PlayerController player;
    HeartSystem heart;
    readonly Vector2Int[] sizes={new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080),new Vector2Int(1920,1200)};
    void Check(bool c,string m)=>GameMenusValidation.Check(c,m);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).First(x=>x.name==name);
    void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    IEnumerator Frames(int count=3){for(int i=0;i<count;i++)yield return null;}
    IEnumerator Delay(float seconds){yield return new WaitForSecondsRealtime(seconds);}
    IEnumerator WaitFor(Func<bool> condition,float seconds=3)
    {
        float t=Time.realtimeSinceStartup;while(!condition()){if(Time.realtimeSinceStartup-t>seconds)throw new Exception("Wait timeout, state="+(menus!=null?menus.State.ToString():SceneManager.GetActiveScene().name));yield return null;}
    }
    IEnumerator Tap(Key key){Keys(key);yield return Frames();Keys();yield return Frames();}
    IEnumerator Load(string scene)
    {
        Keys();Time.timeScale=1;SceneManager.LoadScene(scene);yield return Frames(5);
        captureCamera=Camera.main;
        if(scene=="FaseMysticWoods")
        {
            menus=UnityEngine.Object.FindFirstObjectByType<GameMenuController>();player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();heart=player.GetComponent<HeartSystem>();
        }
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        foreach(var device in InputSystem.devices.ToArray())if(device is Keyboard || device is Mouse)InputSystem.DisableDevice(device);
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
        yield return Load("TelaInicial");yield return Delay(.3f);
        yield return MainMenuChecks();
        yield return Load("FaseMysticWoods");
        yield return PauseChecks();
        yield return SettingsChecks();
        yield return CombatPauseChecks();
        yield return DeathChecks();
        yield return VictoryChecks();
        GameMenusValidation.Finish(null);
    }
    IEnumerator MainMenuChecks()
    {
        var main=UnityEngine.Object.FindFirstObjectByType<MenuPrincipalManager>();
        GameMenusValidation.Report.Add("DIAGNOSTIC main="+(main!=null)+" event="+(EventSystem.current!=null)+" selected="+(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null?EventSystem.current.currentSelectedGameObject.name:"null")+" mainPanel="+(main!=null?Field<GameObject>(main,"painelMenuInicial").activeInHierarchy:false));
        yield return WaitFor(()=>EventSystem.current!=null && EventSystem.current.currentSelectedGameObject!=null);
        Check(EventSystem.current.currentSelectedGameObject.name=="JogarButton","main menu initial focus preserved");
        Check(main.GetComponentInChildren<MenuCommandsController>(true)!=null,"main menu uses shared commands component");
        CaptureAll("main-menu");
        main.AbrirComandos();yield return Delay(.25f);
        var commands=main.GetComponentInChildren<MenuCommandsController>(true);
        Check(commands.gameObject.activeInHierarchy,"main menu shared commands open");
        Check(commands.GetComponentsInChildren<TMP_Text>().Any(x=>x.text.Contains("um golpe por toque")),"commands explain one attack per press");
        CaptureAll("main-commands");yield return Tap(Key.Escape);
        Check(EventSystem.current.currentSelectedGameObject.name=="ComandosButton","main commands restore opener focus");
        main.AbrirConfiguracoes();yield return Delay(.25f);
        var settings=main.GetComponentInChildren<MenuSettingsController>(true);Check(settings.gameObject.activeInHierarchy,"main menu shared settings open");CaptureAll("main-settings");
        yield return Tap(Key.Escape);Check(EventSystem.current.currentSelectedGameObject.name=="ConfiguracoesButton","main settings restore opener focus");
        Check(Time.timeScale==1,"main menu does not retain paused timescale");
    }
    IEnumerator PauseChecks()
    {
        Check(menus!=null && menus.State==GameMenuController.MenuState.Playing,"level starts in Playing state");
        Check(heart.Health==6,"new level starts with six hearts");
        Check(UnityEngine.Object.FindObjectsByType<GameMenuController>(FindObjectsSortMode.None).Length==1 && !UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(x=>x.name=="MENU - Pause" || x.name=="MENU - Restart"),"one gameplay menu prefab, no legacy duplicate canvas");
        yield return Tap(Key.Escape);yield return Delay(.25f);
        Check(menus.State==GameMenuController.MenuState.Paused && Time.timeScale==0,"Escape pauses world");
        Check(EventSystem.current.currentSelectedGameObject.name=="RetomarButton","pause focuses Retomar");
        var buttons=menus.PausePanel.GetComponentsInChildren<Button>();
        Check(buttons.Select(x=>x.GetComponentInChildren<TMP_Text>().text).SequenceEqual(new[]{"RETOMAR","CONFIGURAÇÕES","COMANDOS","MENU PRINCIPAL"}),"pause action order matches requested layout");
        var rb=player.GetComponent<Rigidbody2D>();Vector2 pos=rb.position;float time=Time.time;yield return Delay(.12f);Check(rb.position==pos && Time.time==time,"pause freezes physics and scaled timers");
        CaptureAll("pause");
        yield return Tap(Key.DownArrow);Check(EventSystem.current.currentSelectedGameObject.name=="ConfiguracoesButton","keyboard navigation reaches settings");
        yield return Tap(Key.DownArrow);yield return Tap(Key.Enter);yield return Delay(.25f);
        Check(menus.State==GameMenuController.MenuState.Commands && Time.timeScale==0,"keyboard opens commands without unpausing");
        CaptureAll("pause-commands");yield return Tap(Key.Escape);
        Check(menus.State==GameMenuController.MenuState.Paused && EventSystem.current.currentSelectedGameObject.name=="ComandosButton","Escape closes commands and restores pause focus");
        yield return Click(Find(menus.PausePanel.transform,"RetomarButton").GetComponent<Button>());
        Check(menus.State==GameMenuController.MenuState.Playing && Time.timeScale==1,"mouse Retomar resumes game");
        yield return Frames();Check(!player.IsAttacking,"UI resume click does not become a player attack");
        for(int i=0;i<3;i++){yield return Tap(Key.Escape);yield return Tap(Key.Escape);}
        Check(menus.State==GameMenuController.MenuState.Playing,"repeated Escape toggles remain consistent");
    }
    IEnumerator SettingsChecks()
    {
        menus.Pause();yield return Frames();yield return Click(Find(menus.PausePanel.transform,"ConfiguracoesButton").GetComponent<Button>());yield return Delay(.25f);
        Check(menus.State==GameMenuController.MenuState.Settings && Time.timeScale==0,"mouse opens settings while paused");
        var settings=menus.Settings;var slider=settings.InitialSelection;float original=MainMenuPreferences.Current.Volume;
        slider.value=.31f;yield return Frames();Check(Mathf.Abs(AudioListener.volume-.31f)<.01f,"paused volume preview works");CaptureAll("pause-settings");
        settings.Cancel();yield return Frames();Check(Mathf.Abs(AudioListener.volume-original)<.01f && menus.State==GameMenuController.MenuState.Paused,"cancel reverts preview and returns to pause");
        Check(EventSystem.current.currentSelectedGameObject.name=="ConfiguracoesButton","settings restore opener focus");
        menus.OpenSettings();yield return Frames();settings.InitialSelection.value=.57f;settings.Apply();yield return Frames();
        Check(Mathf.Abs(MainMenuPreferences.Current.Volume-.57f)<.01f && menus.State==GameMenuController.MenuState.Paused,"apply persists volume and keeps world paused");
        menus.OpenSettings();yield return Frames();Check(Mathf.Abs(settings.InitialSelection.value-.57f)<.01f,"settings reopen with committed value");
        var resolution=Field<TMP_Dropdown>(settings,"resolutionDropdown");resolution.Show();yield return Frames();
        Check(resolution.IsExpanded,"resolution dropdown opens");yield return Tap(Key.Escape);yield return Delay(.2f);
        Check(!resolution.IsExpanded && menus.State==GameMenuController.MenuState.Settings,"Escape closes dropdown before settings, expanded="+resolution.IsExpanded+" state="+menus.State);
        yield return Tap(Key.Escape);Check(menus.State==GameMenuController.MenuState.Paused,"next Escape closes settings");
        menus.OpenSettings();yield return Frames();
        var mode=Field<TMP_Dropdown>(settings,"windowModeDropdown");mode.value=mode.value==0?1:0;settings.Apply();yield return Frames();
        Check(settings.IsConfirmingVideo && Time.timeScale==0,"video confirmation opens while gameplay paused");CaptureAll("pause-video-confirmation");
        yield return Tap(Key.Escape);Check(!settings.IsConfirmingVideo && menus.State==GameMenuController.MenuState.Settings,"Escape reverts video and returns to form");
        mode.value=mode.value==0?1:0;settings.Apply();yield return Frames();double deadline=Time.realtimeSinceStartupAsDouble;yield return Delay(15.2f);
        Check(!settings.IsConfirmingVideo && Time.realtimeSinceStartupAsDouble-deadline>=15 && Time.timeScale==0,"video times out after fifteen real seconds with world paused");
        mode.value=mode.value==0?1:0;settings.Apply();yield return Frames();settings.KeepVideo();yield return Frames();
        Check(menus.State==GameMenuController.MenuState.Paused,"Keep video commits and returns to pause");
        menus.Resume();yield return Frames();
    }
    IEnumerator CombatPauseChecks()
    {
        // Move an existing slime into a clear space and let its natural detector start a windup.
        var slime=UnityEngine.Object.FindFirstObjectByType<SlimeController>();var enemy=slime.GetComponent<EnemyMeleeAttack>();
        var rb=slime.GetComponent<Rigidbody2D>();rb.position=new Vector2(19.5f,13.5f);slime.transform.position=rb.position;
        var prb=player.GetComponent<Rigidbody2D>();prb.position=new Vector2(20.6f,13.28f);player.transform.position=prb.position;Physics2D.SyncTransforms();
        yield return WaitFor(()=>enemy.Phase==EnemyMeleeAttack.AttackPhase.Windup);menus.Pause();yield return Frames();
        var phase=enemy.Phase;float hp=heart.Health;yield return Delay(.2f);Check(enemy.Phase==phase && heart.Health==hp,"pause freezes enemy preparation without damage");menus.Resume();
        // Remove the nearby attacker so testing the player's strike is deterministic.
        slime.enabled=false;enemy.Cancel();enemy.enabled=false;
        Keys(Key.Space);yield return Frames();Keys();yield return WaitFor(()=>player.IsAttacking);
        menus.Pause();yield return Frames();float elapsed=Field<float>(player,"attackElapsed");yield return Delay(.15f);
        Check(player.IsAttacking && Field<float>(player,"attackElapsed")==elapsed,"pause preserves an in-progress player attack");
        menus.Resume();yield return WaitFor(()=>!player.IsAttacking);Check(!player.IsAttacking,"player attack finishes normally after resume");
        // Holding a click across resume cannot request another swing until released.
        menus.Pause();InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,true));yield return Frames();menus.Resume();yield return Delay(.15f);
        Check(!player.IsAttacking,"held menu click is suppressed after resume");InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames();yield return Tap(Key.Space);Check(player.IsAttacking,"fresh attack works after release");yield return WaitFor(()=>!player.IsAttacking);
    }
    IEnumerator DeathChecks()
    {
        menus.Pause();menus.OpenSettings();yield return Frames();float confirmed=MainMenuPreferences.Current.Volume;menus.Settings.InitialSelection.value=.12f;
        int notifications=0;heart.Died+=()=>notifications++;double began=Time.realtimeSinceStartupAsDouble;heart.Health=0;
        Check(menus.State==GameMenuController.MenuState.Dying && Time.timeScale==1,"death closes pause and lets animation play");
        Check(!player.IsAttacking && !heart.Targetable,"death immediately disables combat and physics target");
        Check(Mathf.Abs(AudioListener.volume-confirmed)<.01f,"death cancels unconfirmed settings preview");
        heart.Health=0;Check(notifications==1,"death event fires once per positive-to-zero transition");
        yield return Tap(Key.Escape);Check(menus.State==GameMenuController.MenuState.Dying,"Escape cannot pause while dying");
        yield return WaitFor(()=>menus.State==GameMenuController.MenuState.GameOver);
        Check(Time.realtimeSinceStartupAsDouble-began>=.375,"Game Over waits for complete death clip");
        Check(Time.timeScale==0 && menus.GameOverPanel.activeInHierarchy,"Game Over freezes world");yield return Delay(.25f);
        Check(EventSystem.current.currentSelectedGameObject.name=="TentarNovamenteButton","Game Over selects retry");
        Check(menus.GameOverPanel.GetComponentsInChildren<Button>().Select(x=>x.GetComponentInChildren<TMP_Text>().text).SequenceEqual(new[]{"TENTAR NOVAMENTE","MENU PRINCIPAL"}),"Game Over only has retry and main menu");
        CaptureAll("game-over");yield return Tap(Key.Escape);Check(menus.State==GameMenuController.MenuState.GameOver,"Escape does not dismiss Game Over");
        Find(menus.GameOverPanel.transform,"TentarNovamenteButton").GetComponent<Button>().onClick.Invoke();yield return Frames(6);
        menus=UnityEngine.Object.FindFirstObjectByType<GameMenuController>();player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();heart=player.GetComponent<HeartSystem>();captureCamera=Camera.main;
        Check(menus.State==GameMenuController.MenuState.Playing && Time.timeScale==1 && heart.Health==6,"retry starts a fresh level with six hearts");
        Check(Mathf.Abs(AudioListener.volume-.57f)<.01f,"retry preserves confirmed preferences");
        menus.Pause();Find(menus.PausePanel.transform,"MenuPrincipalButton").GetComponent<Button>().onClick.Invoke();yield return Frames(6);
        Check(SceneManager.GetActiveScene().name=="TelaInicial" && Time.timeScale==1,"pause main-menu action restores normal time");
        var main=UnityEngine.Object.FindFirstObjectByType<MenuPrincipalManager>();main.Jogar();yield return Frames(6);
        menus=UnityEngine.Object.FindFirstObjectByType<GameMenuController>();player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();heart=player.GetComponent<HeartSystem>();captureCamera=Camera.main;
        heart.Health=0;yield return WaitFor(()=>menus.State==GameMenuController.MenuState.GameOver);menus.ReturnToMainMenu();yield return Frames(6);
        Check(SceneManager.GetActiveScene().name=="TelaInicial" && Time.timeScale==1,"Game Over main-menu route works");
    }
    IEnumerator VictoryChecks()
    {
        yield return Load("FaseMysticWoods");
        var boss=UnityEngine.Object.FindFirstObjectByType<SlimeBoss>();boss.OnHit(1000);Check(!boss.BarreiraFinal.activeSelf,"boss defeat still opens final barrier");
        var exit=UnityEngine.Object.FindFirstObjectByType<AreaFinal>();var rb=player.GetComponent<Rigidbody2D>();rb.position=exit.GetComponent<Collider2D>().bounds.center;Physics2D.SyncTransforms();
        yield return WaitFor(()=>SceneManager.GetActiveScene().name=="TelaFinal");yield return Delay(.3f);captureCamera=Camera.main;
        var final=UnityEngine.Object.FindFirstObjectByType<MenuFinalManager>();
        Check(EventSystem.current.currentSelectedGameObject.name=="JogarNovamenteButton","victory selects replay");
        Check(final.GetComponentsInChildren<Button>().Select(x=>x.GetComponentInChildren<TMP_Text>().text).SequenceEqual(new[]{"JOGAR NOVAMENTE","MENU PRINCIPAL"}),"victory has replay and main menu only");
        Check(final.GetComponentsInChildren<TMP_Text>().Any(x=>x.text=="Você concluiu Mystic Woods.\nObrigado por jogar!"),"victory displays agreed thank-you message");
        CaptureAll("victory");yield return Tap(Key.Escape);Check(SceneManager.GetActiveScene().name=="TelaFinal","Escape does not dismiss victory");
        final.JogarNovamente();yield return Frames(6);Check(SceneManager.GetActiveScene().name=="FaseMysticWoods" && Time.timeScale==1 && UnityEngine.Object.FindFirstObjectByType<HeartSystem>().Health==6,"victory replay resets level and hearts");
        yield return Load("TelaFinal");UnityEngine.Object.FindFirstObjectByType<MenuFinalManager>().VoltarMenu();yield return Frames(6);
        Check(SceneManager.GetActiveScene().name=="TelaInicial" && Time.timeScale==1,"victory main-menu route works");
    }
    IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var point=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Frames();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left,true));yield return Frames();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Frames();
    }
    void CaptureAll(string name)
    {
        foreach(var size in sizes)Capture(name+"-"+size.x+"x"+size.y+".png",size);
    }
    void Capture(string name,Vector2Int size)
    {
        if(captureCamera==null)captureCamera=Camera.main;
        var rt=new RenderTexture(size.x,size.y,24);var previousTarget=captureCamera.targetTexture;captureCamera.targetTexture=rt;
        var roots=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x=>x.isRootCanvas).ToArray();
        var states=roots.Select(x=>(canvas:x,mode:x.renderMode,camera:x.worldCamera,distance:x.planeDistance,scale:x.scaleFactor,layer:x.sortingLayerID,order:x.sortingOrder,overrideSort:x.overrideSorting,scaler:x.GetComponent<CanvasScaler>(),enabled:x.GetComponent<CanvasScaler>()!=null && x.GetComponent<CanvasScaler>().enabled)).ToArray();
        foreach(var state in states)
        {
            if(state.scaler!=null)state.scaler.enabled=false;
            state.canvas.renderMode=RenderMode.ScreenSpaceCamera;state.canvas.worldCamera=captureCamera;state.canvas.planeDistance=1;state.canvas.overrideSorting=true;state.canvas.sortingLayerID=SortingLayer.layers.Last().id;state.canvas.sortingOrder=30000+state.order;
            var reference=state.scaler!=null?state.scaler.referenceResolution:new Vector2(1920,1080);float match=state.scaler!=null?state.scaler.matchWidthOrHeight:.5f;
            state.canvas.scaleFactor=Mathf.Pow(size.x/reference.x,1-match)*Mathf.Pow(size.y/reference.y,match);
        }
        Canvas.ForceUpdateCanvases();
        foreach(var canvas in roots)LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        var texts=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        foreach(var text in texts)
        {
            if(!text.transform.IsChildOf(roots.OrderByDescending(x=>x.sortingOrder).First().transform))continue;
            text.ForceMeshUpdate();Check(!text.isTextOverflowing,name+": text fits "+text.gameObject.name);
            var visible=System.Text.RegularExpressions.Regex.Replace(text.text,"<[^>]+>","");visible=new string(visible.Where(c=>!char.IsControl(c)).ToArray());
            Check(text.font.HasCharacters(visible,out List<char> missing),name+": font covers "+text.gameObject.name);
        }
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if(!button.transform.IsChildOf(roots.OrderByDescending(x=>x.sortingOrder).First().transform))continue;
            var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
            var points=corners.Select(x=>RectTransformUtility.WorldToScreenPoint(captureCamera,x)).ToArray();
            Check(points.All(x=>x.x>=-1 && x.x<=size.x+1 && x.y>=-1 && x.y<=size.y+1),name+": button on screen "+button.name);
        }
        captureCamera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),name),image.EncodeToPNG());
        RenderTexture.active=old;
        foreach(var state in states)
        {
            state.canvas.renderMode=state.mode;state.canvas.worldCamera=state.camera;state.canvas.planeDistance=state.distance;state.canvas.scaleFactor=state.scale;state.canvas.sortingLayerID=state.layer;state.canvas.sortingOrder=state.order;state.canvas.overrideSorting=state.overrideSort;
            if(state.scaler!=null)state.scaler.enabled=state.enabled;
        }
        captureCamera.targetTexture=previousTarget;Canvas.ForceUpdateCanvases();DestroyImmediate(image);DestroyImmediate(rt);
    }
}
