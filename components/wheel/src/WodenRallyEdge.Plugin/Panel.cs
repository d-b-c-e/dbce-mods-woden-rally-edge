using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Uses only the renderer primitives verified in this stripped Unity build.
internal static class Panel
{
    internal static bool Open { get; private set; }
    internal static string Message = "F6 settings / F8 stops FFB";
    private static readonly string[] Axes = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
    private static float _scale, _left, _top, _height, _scroll, _extent;
    private static int _drag = -1, _focus, _ordinal, _count, _devicePage, _menuMove, _menuStep;
    private static bool _menuConfirm;
    private static bool _body, _activate, _cursorVisible, _dirty, _editBumper, _cameraBindings, _lookBehind, _deviceDetails, _devicePicker;
    private static GUIStyle? _label, _title, _help;
    private static CursorLockMode _cursorLock;
    private static Pause? _ownedPause;
    private static double _nextHotkeyError;
    private static bool? _keyboardAvailable;
    private static int _settingsFrame = -1;
    private static bool _imguiSettingsHeld;
    internal static string HotkeyStatus { get; private set; } = "Keyboard not sampled";
    private static double _changedAt, _nextError, _liveMessageUntil;
    private static string _forza = "", _detail = "", _saveStatus = "Saved";
    private static float _rate;
    private static readonly HashSet<string> Expanded = new();
    private static readonly Color Accent = new(.95f, .73f, .3f, 1);
    private static bool Advanced => Runtime.Settings.UiView == "Advanced";
    private static bool ConnectionDirty => _forza != Runtime.Settings.ForzaPort.ToString() || _detail != Runtime.Settings.DetailPort.ToString() || (int)_rate != Runtime.Settings.DetailHz;
    private static bool Editing => Runtime.Wheel?.Capturing == true || ConnectionDirty || MenuOwnership.Closing;
    private static string ActionLabel(string a) => a switch { "Steer" => "Steering", "Handbrake" => "Handbrake (axis)", "Gear up" => "Shift up", "Gear down" => "Shift down", "Camera" => "Change camera", "Rear view" => "Look behind", "Respawn" => "Reset car", "Settings panel" => "Settings", "Panic stop" => "Stop FFB", _ => a };
    private static Rect R(float x, float y, float w, float h) => new(_left + x * _scale, _top + (y - (_body ? _scroll : 0)) * _scale, w * _scale, h * _scale);
    private static bool Inside(Rect r, Vector2 p) => p.x >= r.x && p.y >= r.y && p.x < r.x + r.width && p.y < r.y + r.height;
    private static bool Visible(Rect r) => !_body || r.y >= _top + 144 * _scale && r.y + r.height <= _top + (_height - 110) * _scale;
    private static void ReadConnection() { _forza = Runtime.Settings.ForzaPort.ToString(); _detail = Runtime.Settings.DetailPort.ToString(); _rate = Runtime.Settings.DetailHz; }
    private static void Navigate(string page, string? view = null)
    {
        if (Editing) { Message = SettingsPresentation.EditLock; return; }
        try { Runtime.Settings.SetPresentation(view ?? Runtime.Settings.UiView, page); _saveStatus = "Saved"; }
        catch (Exception ex) { _saveStatus = "Save failed"; Message = ex.Message; }
        _scroll = 0; _drag = -1; _devicePicker = false; _focus = 0; GUIUtility.keyboardControl = 0;
    }
    internal static void Update()
    {
        ObserveSettingsOpening();
        MenuOwnership.Tick();
        MenuNavigation.Update();
        if (!Runtime.Focused) { MenuOwnership.BeginCapture(); return; }
        Keyboard? kb = null;
        try { kb = Keyboard.current; }
        catch (Exception ex) { HotkeyFailure("Keyboard.current", ex); }
        if (_keyboardAvailable != (kb != null))
        {
            _keyboardAvailable = kb != null;
            string availability = kb == null ? "Keyboard.current unavailable; IMGUI F6 / wheel Settings remain available" : "Keyboard available; waiting for F6";
            if (_settingsFrame != Time.frameCount) HotkeyStatus = availability;
            Runtime.Log.LogInfo(availability);
        }
        // A panic read/output error must never prevent the settings escape route.
        try { if (kb != null && kb[Key.F8].wasPressedThisFrame || Runtime.Wheel?.Button("Panic stop") == true) Runtime.Force?.Panic(); }
        catch (Exception ex) { HotkeyFailure("F8 / Stop FFB", ex); }
        try
        {
            bool capturing = Runtime.Wheel?.Capturing == true;
            if (!capturing && !MenuOwnership.Closing && (kb != null && kb[Key.F6].wasPressedThisFrame || Runtime.Wheel?.Button("Settings panel") == true))
            { SettingsKey("InputSystem / bound Settings"); }
            if (Open && kb != null && kb[Key.Escape].wasPressedThisFrame) { if (capturing) Runtime.Wheel?.Cancel(); else if (ConnectionDirty) ReadConnection(); else Toggle(); }
            if (!Open && Runtime.Wheel?.Button("Pause") == true && Runtime.Local != null)
            {
                var pause = Runtime.Local.MyControls?.PauseScript;
                if (pause != null) { Runtime.Force?.Suspend("paused"); if (Pause.Paused) pause.UnsetPause(); else pause.SetPause(); }
            }
        }
        catch (Exception ex) { HotkeyFailure("Settings / Escape / Pause", ex); }
        if (Open)
        {
            Cursor.visible = true; UiNative.CursorLock(CursorLockMode.None);
            Runtime.Wheel?.UpdateCapture();
        }
        if (_dirty && Runtime.Clock.Elapsed.TotalSeconds - _changedAt > .7) Save();
    }
    private static void HotkeyFailure(string route, Exception error)
    {
        HotkeyStatus = route + ": " + error.Message; Message = "Hotkeys: " + HotkeyStatus;
        if (Runtime.Clock.Elapsed.TotalSeconds >= _nextHotkeyError)
        { Runtime.Log.LogWarning(Message); _nextHotkeyError = Runtime.Clock.Elapsed.TotalSeconds + 10; }
    }
    // Opening-only observation runs before direct startup shortcuts and before
    // our own menu dispatch. Closing still uses the existing release barrier.
    internal static bool ObserveSettingsOpening()
    {
        if (Open || !Runtime.Focused || Runtime.Wheel?.Capturing == true || MenuOwnership.Closing) return true;
        bool known = true, key = false, bound = false;
        try { key = Input.GetKeyDown(KeyCode.F6); }
        catch (Exception ex) { known = false; HotkeyFailure("Legacy F6 opening", ex); }
        try { var kb = Keyboard.current; key |= kb != null && kb[Key.F6].wasPressedThisFrame; }
        catch (Exception ex) { known = false; HotkeyFailure("InputSystem F6 opening", ex); }
        try { bound = Runtime.Wheel?.Button("Settings panel") == true; }
        catch (Exception ex) { known = false; HotkeyFailure("Bound Settings opening", ex); }
        if (key || bound)
        {
            // A delayed IMGUI key-down for this press must not close the panel.
            if (key) _imguiSettingsHeld = true;
            SettingsKey(key ? "Early F6 opening" : "Early bound Settings opening");
        }
        return known;
    }
    private static void SettingsKey(string source)
    {
        if (!Runtime.Focused || Runtime.Wheel?.Capturing == true || MenuOwnership.Closing || _settingsFrame == Time.frameCount) return;
        _settingsFrame = Time.frameCount; HotkeyStatus = "Settings input observed: " + source;
        Runtime.Log.LogInfo(HotkeyStatus); Toggle();
    }
    internal static void MenuAction(string action)
    {
        if (action == "Back") { if (Runtime.Wheel?.Capturing == true) Runtime.Wheel.Cancel(); else if (ConnectionDirty) ReadConnection(); else Close(true); return; }
        if (action == "Confirm") _menuConfirm = true;
        if (action == "Menu up") _menuMove--;
        if (action == "Menu down") _menuMove++;
        if (action == "Menu left") _menuStep = -1;
        if (action == "Menu right") _menuStep = 1;
    }
    internal static void Toggle()
    {
        if (Open) { if (Editing) { Message = SettingsPresentation.EditLock; return; } Close(true); return; }
        Open = true; Runtime.Force?.Suspend("Settings open");
        MenuOwnership.Begin();
        ReadConnection(); _scroll = 0;
        _cursorVisible = UiNative.CursorVisible; _cursorLock = Cursor.lockState;
        Cursor.visible = true; UiNative.CursorLock(CursorLockMode.None);
        try
        {
            var car = Runtime.Local;
            var pause = car?.MyControls?.PauseScript;
            if (car != null && car.Status == MainCar.CarStatus.RACE && !Pause.Paused && pause != null) { pause.SetPause(); _ownedPause = pause; }
        }
        catch (Exception ex) { Message = "Panel open; pause manually if needed: " + ex.Message; }
        Runtime.Log.LogInfo("F6 panel opened");
    }
    internal static void Close(bool resume)
    {
        if (resume && Open) { MenuOwnership.RequestClose(); return; }
        CompleteClose(resume);
    }
    internal static void CompleteClose(bool resume)
    {
        if (!Open) return;
        Open = false; _imguiSettingsHeld = false; Runtime.Wheel?.Cancel(); _drag = -1; Save();
        try
        {
            MenuOwnership.End();
            if (resume && _ownedPause != null && Pause.Paused && !_ownedPause.PhotomodeActive) _ownedPause.UnsetPause();
            Cursor.visible = _cursorVisible; UiNative.CursorLock(_cursorLock);
        }
        catch (Exception ex) { Runtime.Log.LogWarning("Panel restore: " + ex.Message); }
        _ownedPause = null;
        Runtime.Log.LogInfo("F6 panel closed");
    }
    internal static void ShowCameraMessage(string message)
    { Message = message; _liveMessageUntil = Runtime.Clock.Elapsed.TotalSeconds + 3; }
    internal static void SettingsChanged() => Dirty();
    private static void Dirty() { _dirty = true; _saveStatus = "Saving…"; _changedAt = Runtime.Clock.Elapsed.TotalSeconds; }
    private static void Save() { try { Runtime.Settings.Save(); _dirty = false; _saveStatus = "Saved"; } catch (Exception ex) { _saveStatus = "Save failed"; Message = "Could not save settings: " + ex.Message; _changedAt = Runtime.Clock.Elapsed.TotalSeconds; } }

    private static void Fill(Rect r, Color color)
    {
        if (!Visible(r) || Event.current.type != EventType.Repaint) return;
        var previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = previous;
    }
    private static void Label(float x, float y, float width, string text, bool help = false, float height = 28)
    { var r = R(x,y,width,height); if (Visible(r)) GUI.Label(r, new GUIContent(text), help ? _help! : _label!); }
    private static bool Button(float x, float y, float width, string text, bool active = false, bool enabled = true)
    {
        var r = R(x,y,width,34); if (!Visible(r)) return false;
        int id = enabled ? _ordinal++ : -1; var e = Event.current;
        bool hover = Inside(r,e.mousePosition), focused = enabled && id == _focus && GUIUtility.keyboardControl == 0;
        Fill(r, focused ? Accent : new Color(.19f,.24f,.30f,1));
        Fill(new Rect(r.x+2*_scale,r.y+2*_scale,r.width-4*_scale,r.height-4*_scale), !enabled ? new Color(.09f,.11f,.14f,1) : active ? new Color(.37f,.28f,.12f,1) : hover ? new Color(.23f,.28f,.34f,1) : new Color(.13f,.17f,.23f,1));
        Label(x+8,y+5,width-16,text,false,24);
        if (enabled && (e.type == EventType.MouseDown && e.button == 0 && hover || focused && _activate))
        { _focus = id; _activate = false; if(e.type != EventType.Repaint) e.Use(); GUIUtility.keyboardControl = 0; return true; }
        return false;
    }
    private static void Toggle(float x,float y,float width,string text,ref bool value)
    {
        Label(x,y+4,width-132,text+":");
        if (Button(x+width-128,y,60,"Off",!value) && value) { value=false; Dirty(); }
        if (Button(x+width-64,y,64,"On",value) && !value) { value=true; Dirty(); }
    }
    private static void Slider(float y,string label,ref float value,float min,float max,string format="F2")
    {
        Label(250,y,260,label); Label(852,y,90,value.ToString(format,CultureInfo.InvariantCulture));
        var r=R(520,y+3,310,26); if (!Visible(r)) return; int id=_ordinal++; var e=Event.current;
        bool focused=id==_focus && GUIUtility.keyboardControl==0;
        if (e.type==EventType.MouseDown && e.button==0 && Inside(r,e.mousePosition)) { _drag=(int)y; _focus=id; GUIUtility.keyboardControl=0; e.Use(); }
        if (_drag==(int)y && e.type is EventType.MouseDrag or EventType.MouseDown or EventType.Used)
        { value=min+Math.Clamp((e.mousePosition.x-r.x)/r.width,0,1)*(max-min); Dirty(); if(e.type!=EventType.Used)e.Use(); }
        if (_drag==(int)y && e.type==EventType.MouseUp) { _drag=-1; e.Use(); }
        if (focused && e.type==EventType.KeyDown && e.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
        { value=Math.Clamp(value+(e.keyCode==KeyCode.RightArrow?1:-1)*(max-min)/100,min,max); Dirty(); e.Use(); }
        if(focused && _menuStep!=0) { value=Math.Clamp(value+_menuStep*(max-min)/100,min,max);_menuStep=0;Dirty(); }
        Fill(r,focused?new Color(.3f,.3f,.3f,1):new Color(.08f,.1f,.15f,1));
        Fill(new Rect(r.x,r.y+r.height*.4f,r.width*Math.Clamp((value-min)/(max-min),0,1),r.height*.2f),Accent);
        float knob=r.x+r.width*Math.Clamp((value-min)/(max-min),0,1); Fill(new Rect(knob-3*_scale,r.y,6*_scale,r.height),Color.white);
    }
    private static void Bar(float x,float y,float width,float value,bool steering)
    {
        var r=R(x,y,width,12); Fill(r,new Color(.06f,.08f,.12f,1));
        float origin=steering?.5f:0, length=steering?Math.Clamp(value,-1,1)*.5f:Math.Clamp(value,0,1);
        Fill(new Rect(r.x+r.width*(origin+Math.Min(0,length)),r.y,r.width*Math.Abs(length),r.height),Accent);
    }
    private static string Value(float v,bool steering) => steering ? Math.Abs(v)<.02 ? "Centre" : v<0 ? $"Left {Math.Abs(v):P0}" : $"Right {v:P0}" : v.ToString("P0");
    private static void End(float y) => _extent=Math.Max(_extent,y);
    internal static void Draw()
    {
        // IMGUI is already this game's supported keyboard route for text and
        // focus. Accept its F6 event too, deduplicating the InputSystem edge.
        var hotkey = Event.current;
        if (hotkey.keyCode == KeyCode.F6)
        {
            if (hotkey.type == EventType.KeyUp) _imguiSettingsHeld = false;
            if (hotkey.type == EventType.KeyDown)
            {
                if (!_imguiSettingsHeld) { _imguiSettingsHeld = true; try { SettingsKey("IMGUI F6"); } catch (Exception ex) { HotkeyFailure("IMGUI F6", ex); } }
                hotkey.Use();
            }
        }
        if(!Open) { DrawCameraMessage(); return; } var previousColor=GUI.color; bool previousEnabled=GUI.enabled;
        try
        {
            GUI.color=Color.white; GUI.enabled=true; _body=false;
            _scale=Math.Min((Screen.width-32f)/980, (Screen.width>=3000?1.5f:1)*Runtime.Settings.UiScale/100);
            _height=Math.Min(740,(Screen.height-32f)/_scale); _left=(Screen.width-980*_scale)/2; _top=(Screen.height-_height*_scale)/2;
            _label??=new(); _title??=new(); _help??=new();
            UiNative.Style(_label,(int)(18*_scale),true); _label.normal.textColor=Color.white;
            UiNative.Style(_help,(int)(15*_scale),true); _help.normal.textColor=new Color(.70f,.77f,.85f,1);
            UiNative.Style(_title,(int)(25*_scale),false); _title.normal.textColor=Accent;
            var e=Event.current;
            bool navigationAllowed=Runtime.Wheel?.CaptureButton==null || Runtime.Wheel.SavePending;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Tab && navigationAllowed)
            { _focus=(_focus+(e.shift?-1:1)+Math.Max(1,_count))%Math.Max(1,_count); GUIUtility.keyboardControl=0; e.Use(); }
            if(_menuMove!=0) { _focus=(_focus+_menuMove+Math.Max(1,_count))%Math.Max(1,_count);_menuMove=0;GUIUtility.keyboardControl=0; }
            _activate=(_menuConfirm || e.type==EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.Space) && navigationAllowed;_menuConfirm=false;
            if(e.type==EventType.ScrollWheel && Inside(R(238,144,724,_height-254),e.mousePosition)) { _scroll=Math.Clamp(_scroll+e.delta.y*30,0,Math.Max(0,_extent-(_height-110))); _drag=-1; e.Use(); }
            if(e.type==EventType.KeyDown && e.keyCode is KeyCode.PageDown or KeyCode.PageUp)
            { _scroll=Math.Clamp(_scroll+(e.keyCode==KeyCode.PageDown?1:-1)*240,0,Math.Max(0,_extent-(_height-110))); _drag=-1; e.Use(); }
            _ordinal=0; _extent=144;
            Fill(R(0,0,980,_height),new Color(.035f,.05f,.075f,1)); Fill(R(0,100,220,_height-100),new Color(.06f,.08f,.12f,1));
            GUI.Label(R(24,18,540,42),new GUIContent("Woden / Wheel settings"),_title);
            Label(585,24,60,"View:");
            if(Button(648,18,124,"Simple",!Advanced,!Editing))Navigate(Runtime.Settings.UiPage,"Simple");
            if(Button(780,18,174,"Advanced",Advanced,!Editing))Navigate(Runtime.Settings.UiPage,"Advanced");
            Label(24,64,925,Editing?SettingsPresentation.EditLock:"F6 close · F8 stops FFB · Tab / Enter to navigate · "+_saveStatus,true);
            int row=0; foreach(string page in SettingsPresentation.Pages(Runtime.Settings.UiView))
                if(Button(16,112+row++*44,188,page,Runtime.Settings.UiPage==page,!Editing))Navigate(page);
            if(Button(16,_height-108,188,"Stop FFB")) { try { Runtime.Force?.Panic(); } catch(Exception ex) { Message="Save failed: "+ex.Message; } }
            if(Button(16,_height-64,188,"Close",false,!Editing))Close(true);
            if(MenuOwnership.Closing) { Label(250,170,680,Message,true,90); if(Button(250,280,240,"Keep settings open"))MenuOwnership.CancelClose(); return; }
            Label(250,112,680,Runtime.Settings.UiPage);
            _body=true;
            switch(Runtime.Settings.UiPage)
            { case "Controls":ControlsPage();break; case "FFB":ForcePage();break; case "Cameras":CameraPage();break; case "Driving":DifficultyPage();break; case "Telemetry":TelemetryPage();break; case "Help":HelpPage();break; default:SetupPage();break; }
            _body=false; _scroll=Math.Clamp(_scroll,0,Math.Max(0,_extent-(_height-110)));
            if(_extent>_height-110)
            {
                if(Button(250,_height-99,90,"Up",false,_scroll>0)) {_scroll=Math.Max(0,_scroll-240);_drag=-1;}
                if(Button(350,_height-99,100,"Down",false,_scroll<_extent-(_height-110))) {_scroll=Math.Min(_extent-(_height-110),_scroll+240);_drag=-1;}
                Label(474,_height-94,470,"Scroll or Page Up / Down for more",true);
            }
            Fill(R(238,_height-58,720,1),new Color(.25f,.3f,.36f,1));
            string status=Runtime.Wheel?.SaveError ?? (_saveStatus=="Save failed"?"Save failed — "+Message:Message);
            Label(250,_height-49,698,status,true,43);
            _count=_ordinal;_menuStep=0;
            if(e.type==EventType.MouseDown && Inside(R(0,0,980,_height),e.mousePosition))e.Use();
        }
        catch(Exception ex) { if(Runtime.Clock.Elapsed.TotalSeconds>_nextError) { Runtime.Log.LogError("F6 draw: "+ex); _nextError=Runtime.Clock.Elapsed.TotalSeconds+5; } }
        finally { _body=false; GUI.color=previousColor;GUI.enabled=previousEnabled; }
    }
    private static void DrawCameraMessage()
    {
        if (!Runtime.Focused || !MountedCamera.PlayerOwned || Runtime.Clock.Elapsed.TotalSeconds >= _liveMessageUntil) return;
        var old = GUI.color;
        try
        {
            float scale = Screen.width >= 3000 ? 1.5f : 1;
            _help ??= new(); UiNative.Style(_help, (int)(16 * scale), true); _help.normal.textColor = Color.white;
            var rect = new Rect(24 * scale, Screen.height - 90 * scale, Math.Min(Screen.width - 48 * scale, 1000 * scale), 64 * scale);
            if (Event.current.type == EventType.Repaint) { GUI.color = new Color(.035f,.05f,.075f,1); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = old; }
            GUI.Label(new Rect(rect.x+12*scale,rect.y+8*scale,rect.width-24*scale,rect.height-16*scale),new GUIContent(Message),_help);
        }
        catch (Exception ex) { if (Runtime.Clock.Elapsed.TotalSeconds > _nextError) { Runtime.Log.LogWarning("Camera adjustment hint: " + ex.Message); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 5; } }
        finally { GUI.color = old; }
    }
    private static void Refresh() { Runtime.Force?.Reconnect("Device refresh");Runtime.Devices?.Refresh(); }
    private static void SetupPage()
    {
        var cfg=Runtime.Settings; bool before=cfg.WheelEnabled;
        Toggle(250,152,390,"Wheel controls",ref cfg.WheelEnabled); if(before!=cfg.WheelEnabled)Runtime.Force?.Suspend("Wheel controls changed");
        if(Button(700,152,244,"Refresh devices"))Refresh();
        Label(250,202,690,"Connect your controls, check the bars, then drive.",true,36);
        string? next=null; int row=0;
        foreach(string name in new[]{"Steer","Throttle","Brake"})
        {
            float y=258+row++*64; var binding=Runtime.Wheel!.Bindings.Axis(name);
            bool available=Runtime.Devices?.TryAxis(binding,out _) == true; float value=0; if(available)Runtime.Devices!.TryAxis(binding,out value);
            Label(250,y,160,ActionLabel(name));Bar(420,y+8,350,value,name=="Steer");Label(782,y,162,available?Value(value,name=="Steer"):"Not available",true);
            if(next==null && (!available || binding?.Valid!=true))next=name;
        }
        Label(250,465,690,!cfg.WheelEnabled?"Next: turn Wheel controls On.":next!=null?"Next: bind or reconnect "+ActionLabel(next)+".":"Ready to drive. Close settings and check the car responds.",false,44);
        if(next!=null && Button(250,520,340,"Bind "+ActionLabel(next))) { Navigate("Controls");Runtime.Wheel!.BeginAxis(next); }
        else if(next==null && Button(250,520,340,"Optional controls and buttons"))Navigate("Controls");
        End(560);
    }
    private static string BindingLabel(string action)
    {
        var b=Runtime.Wheel!.Bindings; string text=b.Buttons.TryGetValue(action,out var button)?Runtime.Devices!.Describe(button.DeviceGuid)+" - "+Dbce.Wheel.Ffb.DigitalInput.Label(button.Button):"";
        if(b.CameraKeys.TryGetValue(action,out var key)&&key!="None")text+=(text.Length>0?" + ":"")+key;
        return text.Length==0?"Not bound":text;
    }
    private static void ClearBinding(string action)
    { Runtime.Wheel!.TryCommit(proposed => { proposed.Buttons.Remove(action); if(proposed.CameraKeys.ContainsKey(action))proposed.CameraKeys[action]="None"; }); }
    private static void BindingRow(float y,string action,string? label=null)
    {
        Label(250,y,210,label??ActionLabel(action));Label(464,y,478,BindingLabel(action),true,42);
        if(Button(250,y+44,100,"Bind"))Runtime.Wheel!.BeginButton(action);
        if(Button(362,y+44,100,"Clear"))ClearBinding(action);
    }
    private static bool CapturePage()
    {
        var input=Runtime.Wheel!; if(!input.Capturing)return false;
        string action=input.CaptureAxis??input.CaptureButton??"Pending binding change";
        Label(250,158,690,"Binding: "+ActionLabel(action));
        Label(250,202,690,input.SavePending?"Your previous binding remains active. Retry the saved proposal, or Cancel to discard it.":input.CaptureAxis=="Steer"?"Centre the wheel. Turn right first, then sweep fully both ways.":input.CaptureAxis!=null?"Release the control, then press or pull fully and release.":CameraTuning.Actions.Contains(action)?"Press a key or wheel button. F6/F8 are reserved; Escape cancels.":"Press one wheel button. Escape cancels. Native keyboard controls stay in the game menu.",true,48);
        Label(250,262,690,input.Status,true,48);
        if(input.Capture!=null)
        {
            var b=input.PreviewBinding;
            Label(250,304,690,b==null?"Waiting for the full range…":Runtime.Devices!.Describe(b.DeviceGuid)+" · "+Axes[b.Axis]+" axis",true,42);
            float v=0; bool ok=Runtime.Devices!.TryAxis(b,out v);Bar(250,354,500,ok?v:0,action=="Steer");Label(770,342,170,ok?Value(v,action=="Steer"):"--",true);
            Toggle(250,388,390,"Invert calibration",ref input.CaptureInvert);
            Slider(434,"Deadzone (%)",ref input.CaptureDeadzone,0,25,"F0");
            Label(250,476,690,b==null?input.Capture.Status:$"Endpoints {b.Calibration.Rest} → {b.Calibration.End}"+(b.Calibration.Centre is int c?$" · centre {c}":""),true,28);
            if(Button(250,518,248,"Save calibration",false,b!=null&&ok))input.FinishAxis();
            if(Button(514,518,150,"Cancel"))input.Cancel();
            Label(250,568,690,"Previous bindings stay active after Cancel or timeout. Handbrake axis and button remain independent.",true,46);End(622);
        }
        else
        {
            if(input.SavePending && Button(250,336,180,"Retry save"))input.RetrySave();
            if(Button(input.SavePending?442:250,336,180,"Cancel"))input.Cancel();End(385);
        }
        return true;
    }
    private static void ControlsPage()
    {
        if(CapturePage())return; var input=Runtime.Wheel!; float y=152;
        foreach(string name in new[]{"Steer","Throttle","Brake","Handbrake"})
        {
            var b=input.Bindings.Axis(name);Label(250,y,210,ActionLabel(name));
            Label(464,y,480,b==null?"Not bound":Runtime.Devices!.Describe(b.DeviceGuid)+" · "+Axes[b.Axis]+" axis",true,42);
            float v=0;bool ok=Runtime.Devices?.TryAxis(b,out v)==true;Bar(250,y+39,430,ok?v:0,name=="Steer");Label(700,y+29,240,ok?"Device input: "+Value(v,name=="Steer"):b==null?"Not bound":"Saved device unavailable",true);
            if(Button(250,y+60,100,"Bind"))input.BeginAxis(name);
            if(Button(362,y+60,142,"Calibrate",false,b!=null))input.BeginAxis(name,true);
            if(Button(516,y+60,100,"Clear",false,b!=null)) { input.TryCommit(proposed => proposed.SetAxis(name,null)); }
            if(b!=null)Label(638,y+64,304,$"Invert: {(b.Inverted?"On":"Off")} · Deadzone {b.Calibration.Deadzone:P0}",true);
            y+=104;
        }
        foreach(var group in new[]{("Driving buttons",new[]{"Gear up","Gear down","Handbrake","Camera","Rear view","Respawn"}),("Menu buttons",new[]{"Pause","Confirm","Back","Menu up","Menu down","Menu left","Menu right"}),("Mod buttons",new[]{"Settings panel","Panic stop"}),("Extra game buttons",new[]{"Lights","Horn","Records","Next song"}),("Shifter bindings",WheelInput.GateActions)})
        {
            if(Button(250,y,694,(Expanded.Contains(group.Item1)?"− ":"+ ")+group.Item1,Expanded.Contains(group.Item1))) { if(!Expanded.Add(group.Item1))Expanded.Remove(group.Item1); }
            y+=46;
            if(!Expanded.Contains(group.Item1))continue;
            foreach(string action in group.Item2){BindingRow(y,action,action=="Handbrake"?"Handbrake (button)":null);y+=94;}
            if(group.Item1=="Menu buttons"||group.Item1=="Shifter bindings") {Label(250,y,690,group.Item1=="Menu buttons"?"Up/down moves settings focus; left/right adjusts sliders. Confirm activates; Back closes. Native menus use their selected Unity UI item. "+MenuNavigation.Status:"H-pattern: hold a gate for that gear; the mod steps the game's Shift up/down until it matches. Out of gear cuts the drive (Woden has no neutral). R swaps the pedals, because Woden reverses on the brake. Needs the game's manual transmission. Clutch is not routed. Now: "+(Runtime.Wheel?.Shifter.Status??""),true,60);y+=70;}
        }
        Label(250,y,690,"Handbrake: progressive rear braking/grip; the game's engine cut remains digital. Button input requests full braking.",true,48);y+=64;
        if(Advanced)
        {
            Label(250,y,420,"Local player index: "+Runtime.Settings.Player);
            if(Button(714,y,100,"−")){Runtime.Settings.Player=Math.Max(0,Runtime.Settings.Player-1);Dirty();}
            if(Button(830,y,100,"+")){Runtime.Settings.Player=Math.Min(3,Runtime.Settings.Player+1);Dirty();}y+=52;
            Label(250,y,690,input.Status,true,46);y+=54;
        }
        End(y);
    }

    private static void ForcePage()
    {
        var cfg=Runtime.Settings;var force=Runtime.Force!;
        Label(250,156,160,"FFB:");
        if(Button(430,152,90,"Off",!cfg.FfbEnabled)) {try{force.SetEnabled(false);}catch(Exception ex){Message="Save failed: "+ex.Message;}}
        if(Button(532,152,90,"On",cfg.FfbEnabled)) {try{force.SetEnabled(true);}catch(Exception ex){Message="Save failed: "+ex.Message;}}
        var target=Runtime.Devices!.ResolveForceTarget(cfg.FfbFollowSteering,cfg.FfbGuid,Runtime.Wheel!.Bindings.Steer?.DeviceGuid);
        Label(250,208,690,"FFB device:",true);
        string chosen=cfg.FfbFollowSteering?"Use steering wheel":Guid.TryParse(cfg.FfbGuid,out var id)?Runtime.Devices.Describe(id):"Choose a wheel";
        if(chosen.Length>65)chosen=chosen[..62]+"…";
        if(Button(250,240,694,chosen+"  v",_devicePicker))_devicePicker=!_devicePicker;
        float y=292;
        if(_devicePicker)
        {
            if(Button(250,y,694,"Use steering wheel",cfg.FfbFollowSteering)){force.Reconnect("Output selection changed");cfg.FfbFollowSteering=true;_devicePicker=false;Dirty();}y+=44;
            var candidates=Runtime.Devices.Devices.Where(d=>d.Info.ForceFeedback).ToArray();
            foreach(var d in candidates)
            {
                string guid=d.Info.InstanceGuid!.Value.ToString();bool duplicate=candidates.Count(c=>c.Info.Name==d.Info.Name)>1;
                string friendly=d.Info.Name.Length>55?d.Info.Name[..52]+"…":d.Info.Name;
                string name=friendly+(duplicate?" · "+guid[..8]:"");
                if(Button(250,y,694,name,!cfg.FfbFollowSteering&&Guid.TryParse(cfg.FfbGuid,out var selectedGuid)&&selectedGuid==d.Info.InstanceGuid)){force.Reconnect("Output selection changed");cfg.FfbFollowSteering=false;cfg.FfbGuid=guid;_devicePicker=false;Dirty();}y+=44;
            }
            if(!cfg.FfbFollowSteering && !candidates.Any(d=>Guid.TryParse(cfg.FfbGuid,out var selectedGuid)&&d.Info.InstanceGuid==selectedGuid)){Label(250,y,690,"Saved selection retained: "+chosen,true,42);y+=48;}
        }
        Label(250,y,690,target.Ready?"Selected: "+target.Reason:target.Reason,true,44);y+=54;
        if(Button(250,y,252,"Refresh / retry"))Refresh();y+=56;
        Slider(y,"Strength (%)",ref cfg.FfbStrength,0,100,"F0");y+=44;
        Label(250,y,690,force.Status,true,42);y+=48;
        if(Button(250,y,228,"Default strength: 50%")){cfg.FfbStrength=50;Dirty();}y+=46;
        if(!Advanced && cfg.CustomFfb){if(Button(250,y,694,"Custom FFB tuning active. Review in Advanced"))Navigate("FFB","Advanced");y+=44;}
        Label(250,y+4,200,"Force model:");if(Button(460,y,150,"Grip",cfg.FfbModel=="Grip")&&cfg.FfbModel!="Grip"){cfg.FfbModel="Grip";Dirty();}if(Button(622,y,150,"Classic",cfg.FfbModel=="Classic")&&cfg.FfbModel!="Classic"){cfg.FfbModel="Classic";Dirty();}y+=44;
        Label(250,y,690,cfg.FfbModel=="Grip"?"Grip: the front tyres' estimated force, lightening as they slide, as in art of rally (art's strength scale; calibration pending). Now: "+force.GripStatus:"Classic: the earlier slip estimate, capped at the peak below.",true,46);y+=52;
        if(Advanced)
        {
            Label(250,y,690,"Strength sets overall force. On is remembered; feedback starts during driving. F8 / Stop FFB saves Off.",true,46);y+=58;
            Toggle(250,y,450,"Crash kick",ref cfg.CrashEnabled);y+=40;Label(250,y,690,"A short push and rattle when you hit something (art of rally's crash cue). Now: "+force.CrashStatus,true,40);y+=46;
            if(cfg.CrashEnabled){Slider(y,"Crash strength (%)",ref cfg.CrashStrength,0,100,"F0");y+=44;}
            if(cfg.FfbModel=="Grip")
            {
                Slider(y,"Grip: reference (x front load)",ref cfg.FfbLoadRatio,.2f,10,"F2");y+=36;Label(250,y,690,"Full scale as a multiple of the mean driving front load. Higher is lighter; default 2.",true,40);y+=48;
                Slider(y,"Grip: smoothing",ref cfg.FfbGripSmoothing,0,.95f,"F2");y+=36;Label(250,y,690,"Per-update smoothing; default 0.2 as in art of rally.",true,40);y+=48;
            }
            Slider(y,"Peak output cap (%)",ref cfg.FfbPeak,0,50,"F0");y+=36;Label(250,y,690,"Classic only: maximum commanded force; default 25%.",true,40);y+=48;
            Slider(y,"Smoothing (ms)",ref cfg.FfbSmoothing,0,200,"F0");y+=36;Label(250,y,690,"Higher values soften rapid changes but delay feedback. Default 35 ms.",true,40);y+=48;
            Slider(y,"Steering damping",ref cfg.FfbDamping,0,.5f);y+=36;Label(250,y,690,"Resists wheel movement; dimensionless gain, default 0.05.",true,40);y+=48;
            Slider(y,"Reference front load",ref cfg.FfbLoadReference,100,50000,"F0");y+=36;Label(250,y,690,"Higher values reduce the tyre estimate. Unity force units; default 6000.",true,40);y+=48;
            Slider(y,"Slip response scale",ref cfg.FfbSlipScale,.02f,3);y+=36;Label(250,y,690,"Larger values soften the slip response. Game slip units; default 0.35.",true,40);y+=48;
            Toggle(250,y,450,"Invert FFB",ref cfg.FfbInvert);y+=46;
            if(Button(250,y,280,"Reset FFB tuning")){cfg.FfbModel="Grip";cfg.FfbLoadRatio=2;cfg.FfbGripSmoothing=.2f;cfg.FfbStrength=50;cfg.FfbPeak=25;cfg.FfbSmoothing=35;cfg.FfbDamping=.05f;cfg.FfbLoadReference=6000;cfg.FfbSlipScale=.35f;cfg.FfbInvert=false;Dirty();}y+=46;
            Label(250,y,690,$"Accepted output {force.Sent:P1}; calls {force.Attempts}; failures {force.Failures}. Reset preserves On/Off and device.",true,46);y+=52;
        }
        End(y);
    }
    private static void CameraPage()
    {
        if(CapturePage())return;var cfg=Runtime.Settings;float y=152;
        Toggle(250,y,450,"Bonnet",ref cfg.Bonnet);y+=44;Toggle(250,y,450,"Bumper",ref cfg.Bumper);y+=56;
        BindingRow(y,"Camera");y+=100;
        Label(250,y,690,"Cycle to Bonnet or Bumper to adjust the active view. Open Adjustment bindings for your saved keys or to rebind them.",true,46);y+=60;
        if(Button(250,y,694,(_lookBehind?"− ":"+ ")+"Look behind binding",_lookBehind))_lookBehind=!_lookBehind;y+=44;
        if(_lookBehind){BindingRow(y,"Rear view");y+=96;}
        if(Button(250,y,694,(_cameraBindings?"− ":"+ ")+"Adjustment bindings",_cameraBindings))_cameraBindings=!_cameraBindings;y+=44;
        if(_cameraBindings)
        {
            for(int i=2;i<CameraTuning.Actions.Length;i++){BindingRow(y,CameraTuning.Actions[i],CameraTuning.Labels[i]);y+=96;}
            if(Button(250,y,330,"Restore numpad defaults"))
            {
                var conflict=CameraTuning.AdjustmentDefaultsConflict(Runtime.Wheel!.Bindings);
                if(conflict!=null)Message="A numpad default is assigned to "+conflict+". Rebind it first.";
                else{Runtime.Wheel.TryCommit(CameraTuning.RestoreAdjustmentKeys);}
            }
            y+=46;Label(250,y,690,"Restores adjustment shortcuts only. Saved camera positions and Change camera stay unchanged.",true,46);y+=58;
        }
        bool custom=!cfg.CameraAutoFit || (cfg.GetCameraPose(true) with {Forward=CameraPose.Bumper.Forward})!=CameraPose.Bumper || cfg.BumperAhead!=CameraPose.BumperAheadDefault;
        if(!Advanced&&custom){if(Button(250,y,694,"Custom camera positioning active. Review in Advanced"))Navigate("Cameras","Advanced");y+=48;}
        if(Advanced)
        {
            if(Button(250,y,164,"Bonnet pose",!_editBumper))_editBumper=false;if(Button(426,y,164,"Bumper pose",_editBumper))_editBumper=true;y+=48;
            if(!_editBumper){bool fit=cfg.CameraAutoFit;Toggle(250,y,450,"Fit to car",ref cfg.CameraAutoFit);if(fit&&!cfg.CameraAutoFit)cfg.SetCameraPose(false,MountedCamera.Pose(false));y+=46;}
            var pose=MountedCamera.Pose(_editBumper);float side=pose.Side,height=pose.Height,forward=pose.Forward,pitch=pose.Pitch,fov=pose.Fov;
            Slider(y,"Side position (m)",ref side,-2,2);y+=44;Slider(y,"Height (m)",ref height,.1f,3);y+=44;Slider(y,"Forward position (m)",ref forward,-2,4);y+=44;
            Slider(y,"Tilt down (degrees)",ref pitch,-30,30,"F1");y+=44;Slider(y,"Field of view (degrees)",ref fov,30,110,"F0");y+=44;
            var edited=new CameraPose(side,height,forward,pitch,fov);if(edited!=pose){MountedCamera.SavePose(_editBumper,edited);Dirty();}
            if(Button(250,y,260,"Reset this view")){cfg.ResetCamera(_editBumper);Dirty();}y+=46;
            Label(250,y,690,"Position moves the selected mount in metres; positive tilt looks down. FOV widens the image. Reset restores this mount's defaults.",true,58);y+=66;
            Slider(y,"Move step per press (m)",ref cfg.CameraMoveStep,.005f,.25f,"F3");y+=44;
            Slider(y,"Tilt step per press (°)",ref cfg.CameraTiltStep,.1f,10,"F1");y+=44;
            Slider(y,"FOV step per press (°)",ref cfg.CameraFovStep,.5f,10,"F1");y+=44;
            if(Button(250,y,330,"Default steps: 0.02 m, 1°, 2°")){cfg.CameraMoveStep=CameraSteps.Default.Move;cfg.CameraTiltStep=CameraSteps.Default.Tilt;cfg.CameraFovStep=CameraSteps.Default.Fov;Dirty();}y+=46;
            Label(250,y,690,"Steps set how far each numpad press moves, tilts or zooms the active view. Smaller steps place the view more finely.",true,46);y+=54;
        }
        Toggle(250,y,450,"Show frame rate on screen",ref cfg.ShowFrameRate);y+=44;
        Label(250,y,690,"Frame rate, last 10 s: "+FrameRate.Summary,true,42);y+=48;
        Label(250,y,690,MountedCamera.Status,true,42);End(y+50);
    }
    private static void DifficultyPage()
    {
        var cfg=Runtime.Settings;Toggle(250,152,520,"Countdown assist",ref cfg.CountdownAssistEnabled);
        Slider(218,"Countdown speed (%)",ref cfg.CountdownSpeed,25,100,"F0");
        Label(250,270,690,$"At {cfg.CountdownSpeed:F0}%, 60 timer seconds allow about {6000/cfg.CountdownSpeed:F0} driving seconds.",true,48);
        if(Button(250,338,230,"Default speed: 75%")){cfg.CountdownSpeed=75;Dirty();}
        Label(250,402,690,"Slows only the single-player time limit. Car physics, elapsed stage clocks and checkpoint bonuses keep their normal behavior. Default Off.",true,72);
        Label(250,498,690,(cfg.CountdownAssistEnabled?CountdownTimerAssist.Status:"Off — normal countdown speed")+" · "+LeaderboardGuard.Status,true,46);End(554);
    }
    private static void TextPort(float y,string label,ref string text)
    { Label(250,y,370,label);var r=R(670,y-4,220,38);if(Visible(r))text=GUI.TextArea(r,text,5); }
    private static void TelemetryPage()
    {
        var cfg=Runtime.Settings;bool enabled=cfg.TelemetryEnabled;Toggle(250,152,450,"Telemetry",ref enabled);
        if(enabled!=cfg.TelemetryEnabled)
        { try{Runtime.Output!.ConfigureNetwork(new(cfg.ForzaPort,cfg.DetailPort,cfg.DetailHz,Enabled:enabled));cfg.TelemetryEnabled=enabled;Dirty();}catch(Exception ex){Message="Connection unchanged: "+ex.Message;} }
        var output=Runtime.Output;var active=output?.ActiveOptions;
        Label(250,210,690,output==null||output.Stopped?"Unavailable — output worker stopped":!cfg.TelemetryEnabled||cfg.ForzaPort==0&&cfg.DetailPort==0?"Off":output.LastError!=null?"Unavailable — "+output.LastError:output.Sending?"Sending UDP (receiver acknowledgement unavailable)":"Waiting for samples",true,48);
        Label(250,272,690,$"Forza Horizon 5 format → 127.0.0.1:{active?.ForzaPort ?? cfg.ForzaPort}\nDetailed JSON → 127.0.0.1:{active?.DetailPort ?? cfg.DetailPort} (0 = off)",false,58);
        Label(250,344,690,"Receiver setup: select Forza Horizon 5 UDP and match the Forza port above. Default 8000; detailed JSON uses 8001. Receiver integration still needs live verification.",true,62);
        float y=422;
        if(!Advanced){if(Button(250,y,410,"Connection settings (Advanced)"))Navigate("Telemetry","Advanced");y+=54;}
        if(output?.RecordingActive==true){Label(250,y,690,"Recording active · dropped samples "+output.RecordingDrops);y+=38;if(Button(250,y,248,"Stop recording"))output.StopRecording();y+=48;}
        else if(output!=null&&output.RecordingStatus!="Disabled"){Label(250,y,690,"Recording: "+output.RecordingStatus+(output.RecordingError!=null?" — "+output.RecordingError:""),true,44);y+=52;}
        if(Advanced)
        {
            TextPort(y,"Forza UDP port (0 = off)",ref _forza);y+=54;TextPort(y,"Detailed JSON port (0 = off)",ref _detail);y+=54;Slider(y,"Detailed stream rate (Hz)",ref _rate,1,60,"F0");_rate=(int)Math.Round(_rate);y+=50;
            if(Button(250,y,240,"Apply connection",false,ConnectionDirty))
            {
                if(int.TryParse(_forza,out int fp)&&int.TryParse(_detail,out int dp))
                {
                    try{output!.ConfigureNetwork(new(fp,dp,(int)_rate,Enabled:cfg.TelemetryEnabled));cfg.ForzaPort=fp;cfg.DetailPort=dp;cfg.DetailHz=(int)_rate;ReadConnection();Dirty();Message="Connection applied; recording retained.";}
                    catch(Exception ex){Message="Connection unchanged: "+ex.Message;}
                }else Message="Use whole-number ports 0..65535; active ports must differ.";
            }
            if(Button(510,y,148,"Cancel",false,ConnectionDirty))ReadConnection();y+=48;
            if(Button(250,y,356,"Restore connection defaults")){_forza="8000";_detail="8001";_rate=20;}y+=48;
            Label(250,y,690,$"Forza packets {output?.ForzaPackets}; overwritten ticks {output?.OverwrittenTicks}; send errors {output?.SendErrors}. Loopback only.",true,48);y+=60;
            Label(250,y,690,"Recordings are prepared by the agent before launch (20 minutes / 64 MiB maximum). No recording starts from this menu. Changing connection/view keeps any capture running.",true,70);y+=80;
        }
        End(y);
    }
    private static void HelpPage()
    {
        Label(250,152,690,"Woden Rally Edge Wheel "+Plugin.Version+" · development candidate");
        Label(250,206,690,"No controls? Turn Wheel controls On in Setup, then bind and calibrate the three required axes in Controls.",true,58);
        Label(250,280,690,"No feedback? Check On and the selected FFB device. The FFB page explains inactive output. Close settings and drive. Stop FFB / F8 remembers Off.",true,68);
        Label(250,364,690,"Camera keys? Open Cameras → Adjustment bindings. Keys affect the active Bonnet or Bumper; hold to repeat, release to stop.",true,58);
        Slider(444,"UI scale (%)",ref Runtime.Settings.UiScale,85,150,"F0");
        if(Button(250,492,240,"Default scale: 100%")){Runtime.Settings.UiScale=100;Dirty();}
        if(Button(508,492,436,"Create support file (local)"))CreateSupport();
        float y=552;
        if(!Advanced){if(Button(250,y,320,"Details (Advanced)"))Navigate("Help","Advanced");y+=48;}
        else
        {
            if(Button(250,y,694,(_deviceDetails?"− ":"+ ")+"Device details",_deviceDetails))_deviceDetails=!_deviceDetails;y+=48;
            if(_deviceDetails)
            {
                if(Button(250,y,280,"Refresh devices"))Refresh();y+=50;var devices=Runtime.Devices!.Devices;
                Label(250,y,690,Runtime.Devices.Status,true,44);y+=54;
                if(devices.Count>0)
                {
                    _devicePage%=devices.Count;var d=devices[_devicePage];Label(250,y,690,d.Info.Name+(d.Ok?" — reading":" — unavailable"));y+=40;
                    Label(250,y,690,d.Info.InstanceGuid+ $" · FFB {d.Info.ForceFeedback}",true,44);y+=50;
                    for(int i=0;i<8;i++){Label(250,y,690,Axes[i]+": "+d.Axes[i],true);y+=34;}
                    if(Button(250,y,260,"Next device"))_devicePage=(_devicePage+1)%devices.Count;y+=48;
                }
            }
        }
        End(y);
    }
    private static void CreateSupport()
    {
        try
        {
            string dir=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"WodenSupport");System.IO.Directory.CreateDirectory(dir);
            string path=System.IO.Path.Combine(dir,"support-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".txt");
            var cfg=Runtime.Settings;
            System.IO.File.WriteAllText(path,$"Woden {Plugin.Version}\nView: {cfg.UiView}\nFFB: {Runtime.Force?.Status}\nForce calls: {Runtime.Force?.Attempts}; failures: {Runtime.Force?.Failures}\nWheel: {Runtime.Wheel?.Status}\nCamera: {MountedCamera.Status}\nTelemetry error: {Runtime.Output?.LastError}\nRecording: {Runtime.Output?.RecordingStatus}\nDevices:\n"+string.Join("\n",Runtime.Devices!.Devices.Select(d=>$"{d.Info.Name} {d.Info.InstanceGuid} reading={d.Ok} FFB={d.Info.ForceFeedback}")));
            Message="Support file saved locally: "+path;
        }
        catch(Exception ex){Message="Support file failed: "+ex.Message;}
    }
}
