using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using WodenRallyEdge.Core;
using Dbce.Wheel.Ui;
using Dbce.Wheel.Input;

namespace WodenRallyEdge;

// Uses only the renderer primitives verified in this stripped Unity build.
internal static partial class Panel
{
    internal static bool Open { get; private set; }
    internal const string IdleMessage = "F6 settings / F8 stops FFB";
    internal static string Message = IdleMessage;
    private static readonly string[] Axes = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
    // Shared F6 frame (zori dd1dc175): title, view switch and Close on one row, page tabs below, fixed footer.
    // Pages keep their 250-based body coordinates; R() draws them BodyShift units left in the narrower panel.
    internal const float Width = 742;
    private const float BodyShift = 226;
    private static float _scale, _left, _top, _height, _scroll, _extent;
    private static int _drag = -1, _focus, _ordinal, _count, _devicePage, _menuMove, _menuStep;
    private static bool _menuConfirm;
    private static bool _body, _activate, _cursorVisible, _dirty, _editBumper, _cameraBindings, _lookBehind, _deviceDetails, _devicePicker;
    private static bool _sharedClosePrompt;
    private static bool _revealFocus;
    private static string? _sharedDeviceGuid;
    private static GUIStyle? _label, _title, _help, _link, _tab;
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
    private static Color ColorOf(F6Color token) => new(token.R,token.G,token.B,token.A);
    private static readonly Color Accent = ColorOf(F6Palette.Accent);
    private static bool Advanced => Runtime.Settings.UiView == "Advanced";
    private static bool ConnectionDirty => _forza != Runtime.Settings.ForzaPort.ToString() || _detail != Runtime.Settings.DetailPort.ToString() || (int)_rate != Runtime.Settings.DetailHz;
    private static bool Editing => Runtime.Wheel?.Capturing == true || SharedBindingHost.Current.CapturingAction!=null || _sharedClosePrompt || ConnectionDirty || MenuOwnership.Closing;
    private static string ActionLabel(string a) => a switch { "Steer" => "Steering", "Handbrake" => "Handbrake (axis)", "Gear up" => "Shift up", "Gear down" => "Shift down", "Camera" => "Change camera", "Rear view" => "Look behind", "Respawn" => "Reset car", "Settings panel" => "Settings", "Panic stop" => "Stop FFB", _ => a };
    private static Rect R(float x, float y, float w, float h) => _body
        ? new(_left + (x - BodyShift) * _scale, _top + (y - _scroll) * _scale, w * _scale, h * _scale)
        : new(_left + x * _scale, _top + y * _scale, w * _scale, h * _scale);
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
        WheelPrompts.Update();
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
            bool capturing = Runtime.Wheel?.Capturing == true || SharedBindingHost.Current.CapturingAction!=null;
            if (!capturing && (kb != null && kb[Key.F6].wasPressedThisFrame || Runtime.Wheel?.Button("Settings panel") == true))
            { SettingsKey("InputSystem / bound Settings"); }
            if (Open && kb != null && kb[Key.Escape].wasPressedThisFrame) { if(MenuOwnership.Closing)Close(true);else if (capturing) {Runtime.Wheel?.Cancel();SharedBindingHost.Current.CancelCapture();} else if(_sharedClosePrompt)_sharedClosePrompt=false;else if (ConnectionDirty) ReadConnection(); else Toggle(); }
            if (Open && MenuOwnership.Closing && Runtime.Wheel?.Button("Back") == true) Close(true);
            if (!Open && Runtime.Wheel?.Button("Pause") == true && Runtime.Local != null)
            {
                var pause = Runtime.Local.MyControls?.PauseScript;
                // In photo mode the native Start does nothing (B or Escape leaves it through UnsetPhotoMode); UnsetPause
                // there would resume the race with the photo camera, UI and post-process still applied.
                if (pause != null && !(Pause.Paused && pause.PhotomodeActive))
                { Runtime.Force?.Suspend("paused"); if (Pause.Paused) pause.UnsetPause(); else pause.SetPause(); }
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
        if (!Runtime.Focused || Runtime.Wheel?.Capturing == true || _settingsFrame == Time.frameCount) return;
        _settingsFrame = Time.frameCount; HotkeyStatus = "Settings input observed: " + source;
        if (MenuOwnership.Closing) return;
        Runtime.Log.LogInfo(HotkeyStatus); Toggle();
    }
    internal static void MenuAction(string action)
    {
        if (action == "Back" && MenuOwnership.Closing) return;
        if (action == "Back") { if (Runtime.Wheel?.Capturing == true || SharedBindingHost.Current.CapturingAction!=null) {Runtime.Wheel?.Cancel();SharedBindingHost.Current.CancelCapture();} else if(_sharedClosePrompt)_sharedClosePrompt=false;else if (ConnectionDirty) ReadConnection(); else Close(true); return; }
        if (action == "Confirm") _menuConfirm = true;
        if (action == "Menu up") _menuMove--;
        if (action == "Menu down") _menuMove++;
        if (action == "Menu left") _menuStep = -1;
        if (action == "Menu right") _menuStep = 1;
    }
    internal static void Toggle()
    {
        if (Open && MenuOwnership.Closing) return;
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
        if(resume&&Open&&SharedBindingHost.Current.HasDraft){if(!_sharedClosePrompt){_sharedClosePrompt=true;_focus=-1;}return;}
        if (resume && Open) { if(!MenuOwnership.Closing)_focus=-1;MenuOwnership.RequestClose(); return; }
        CompleteClose(resume);
    }
    private static void CancelPendingClose()
    {
        MenuOwnership.CancelClose(); _focus=0; _menuConfirm=false; _menuMove=_menuStep=0;
        GUIUtility.keyboardControl=0; Message="Close canceled. Settings remain open.";
    }
    internal static void CompleteClose(bool resume)
    {
        // Camera shortcuts can leave a pending save while F6 is already closed.
        if (!Open) { if (_dirty) Save(); return; }
        // The pending-close text is rewritten every tick while closing; it must not greet the next open as a false
        // "Waiting to close" footer (joint-01 frames 24-40). A Save failure below still replaces it.
        Message = IdleMessage;
        Open = false; _imguiSettingsHeld = false;_sharedClosePrompt=false;SharedBindingHost.Current.CancelCapture(); Runtime.Wheel?.Cancel(); _drag = -1; Save();
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
    internal static void ShowCameraMessage(string message, double seconds = 3)
    { Message = message; _liveMessageUntil = Runtime.Clock.Elapsed.TotalSeconds + seconds; }
    internal static void SettingsChanged() => Dirty();
    private static void Dirty() { _dirty = true; _saveStatus = "Saving…"; _changedAt = Runtime.Clock.Elapsed.TotalSeconds; }
    private static void Save() { try { Runtime.Settings.Save(); _dirty = false; _saveStatus = "Saved"; } catch (Exception ex) { _saveStatus = "Save failed"; Message = "Could not save settings: " + ex.Message; _changedAt = Runtime.Clock.Elapsed.TotalSeconds; } }
    // A preference that must reach disk now (FFB Off from F8 or the FFB page, or On): saved at once. A failure stays
    // pending, shows "Save failed" and is retried like any other change, instead of escaping to the caller and being lost.
    internal static void SaveSettingsNow() { _dirty = true; Save(); }

    private static void Fill(Rect r, Color color)
    {
        if (!Visible(r) || Event.current.type != EventType.Repaint) return;
        var previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = previous;
    }
    private static void Label(float x, float y, float width, string text, bool help = false, float height = 28)
    { var r = R(x,y,width,height); if (Visible(r)) GUI.Label(r, new GUIContent(text), help ? _help! : _label!); }
    private static void Reveal(Rect r)
    {
        if(!_body||!_revealFocus)return;
        float top=_top+144*_scale,bottom=_top+(_height-110)*_scale;
        if(r.y<top)_scroll-=(top-r.y)/_scale;
        else if(r.y+r.height>bottom)_scroll+=(r.y+r.height-bottom)/_scale;
        _scroll=Math.Max(0,_scroll);_drag=-1;
    }
    private static bool Button(float x, float y, float width, string text, bool active = false, bool enabled = true)
    {
        var r = R(x,y,width,F6Metrics.RowHeight);
        int id = enabled ? _ordinal++ : -1; var e = Event.current;
        if(enabled&&id==_focus){Reveal(r);r=R(x,y,width,F6Metrics.RowHeight);}
        if(!Visible(r))return false;
        bool hover = Inside(r,e.mousePosition), focused = enabled && id == _focus && GUIUtility.keyboardControl == 0;
        Fill(r, focused ? Accent : ColorOf(F6Palette.ButtonBorder));
        Fill(new Rect(r.x+2*_scale,r.y+2*_scale,r.width-4*_scale,r.height-4*_scale), ColorOf(!enabled?F6Palette.Window:active?F6Palette.Selected:hover?F6Palette.ButtonHover:F6Palette.Button));
        Label(x+8,y+5,width-16,text,false,24);
        if (enabled && (e.type == EventType.MouseDown && e.button == 0 && hover || focused && _activate))
        { _focus = id; _activate = false; if(e.type != EventType.Repaint) e.Use(); GUIUtility.keyboardControl = 0; return true; }
        return false;
    }
    // F6Widgets.Tab geometry: text, the accent line under the selected page, a focus wash.
    private static bool Tab(float x,float y,float width,string text,bool selected,bool enabled)
    {
        var r=R(x,y,width,F6Metrics.RowHeight+6);
        int id=enabled?_ordinal++:-1;var e=Event.current;
        bool hover=Inside(r,e.mousePosition),focused=enabled&&id==_focus&&GUIUtility.keyboardControl==0;
        if(focused||hover&&enabled)Fill(new Rect(r.x,r.y,r.width,r.height-F6Metrics.TabLine*_scale),ColorOf(F6Palette.TabFocus.WithAlpha(focused?.6f:.3f)));
        GUI.Label(R(x+F6Metrics.TabPadX,y+F6Metrics.TabPadY,width-2*F6Metrics.TabPadX,24),new GUIContent(text),selected?_label!:_tab!);
        if(selected)Fill(new Rect(r.x+6*_scale,r.yMax-F6Metrics.TabLine*_scale,r.width-12*_scale,F6Metrics.TabLine*_scale),Accent);
        if(enabled&&(e.type==EventType.MouseDown&&e.button==0&&hover||focused&&_activate))
        {_focus=id;_activate=false;if(e.type!=EventType.Repaint)e.Use();GUIUtility.keyboardControl=0;return true;}
        return false;
    }
    private static bool Link(float x,float y,float width,string text)
    {
        var r=R(x,y,width,F6Metrics.LinkHeight);
        int id=_ordinal++;var e=Event.current;
        if(id==_focus){Reveal(r);r=R(x,y,width,F6Metrics.LinkHeight);}
        if(!Visible(r))return false;
        bool hover=Inside(r,e.mousePosition),focused=id==_focus&&GUIUtility.keyboardControl==0;
        var color=focused||hover?ColorOf(F6Palette.Text):Accent;
        _link!.normal.textColor=color;
        GUI.Label(r,new GUIContent(text),_link);
        Fill(new Rect(r.x,r.yMax-3*_scale,r.width,F6Metrics.LinkUnderline*_scale),color);
        if(e.type==EventType.MouseDown&&e.button==0&&hover||focused&&_activate)
        {_focus=id;_activate=false;if(e.type!=EventType.Repaint)e.Use();GUIUtility.keyboardControl=0;return true;}
        return false;
    }
    private static void Toggle(float x,float y,float width,string text,ref bool value)
    {
        Label(x,y+4,width-2*F6Metrics.ButtonMinWidth-F6Metrics.RowGap-8,text+":");
        if (Button(x+width-2*F6Metrics.ButtonMinWidth-F6Metrics.RowGap,y,F6Metrics.ButtonMinWidth,"Off",!value) && value) { value=false; Dirty(); }
        if (Button(x+width-F6Metrics.ButtonMinWidth,y,F6Metrics.ButtonMinWidth,"On",value) && !value) { value=true; Dirty(); }
    }
    private static float ToggleRow(float y,string text,ref bool value)
    {int selected=value?1:0;float next=Choice(y,text+":",new[]{"Off","On"},ref selected);value=selected==1;return next;}
    private static float Choice(float y,string label,string[] choices,ref int selected,bool persist=true)
    {
        var widths=choices.Select(text=>Math.Max(F6Metrics.ButtonMinWidth,text.Length*10f+2*F6Metrics.ButtonPadX)).ToArray();
        var row=F6RowLayout.For(694,widths.Sum()+Math.Max(0,widths.Length-1)*F6Metrics.RowGap);
        Label(250,y,row.LabelWidth,label);
        float startX=row.Stacked?250:250+row.LabelWidth+F6Metrics.ColumnGap;
        float editorY=row.Stacked?y+32:y;
        var lines=F6Flow.Lines(widths,row.EditorWidth,F6Metrics.RowGap,out int count);
        float x=startX;int line=0;
        for(int i=0;i<choices.Length;i++)
        {
            if(lines[i]!=line){line=lines[i];x=startX;}
            float width=Math.Min(widths[i],row.EditorWidth);
            if(Button(x,editorY+line*(F6Metrics.RowHeight+F6Metrics.RowGap),width,choices[i],selected==i)&&selected!=i){selected=i;if(persist)Dirty();}
            x+=width+F6Metrics.RowGap;
        }
        return editorY+count*(F6Metrics.RowHeight+F6Metrics.RowGap)+6;
    }
    private static float Slider(float y,string label,ref float value,float min,float max,string format="F2",bool save=true)
    {
        void Changed() { if(save)Dirty(); }
        var row=F6RowLayout.For(694,F6NumberEditor.Needs());
        float editorX=row.Stacked?250:250+row.LabelWidth+F6Metrics.ColumnGap;
        float editorY=row.Stacked?y+32:y;
        var parts=F6NumberEditor.For(row.EditorWidth);
        Label(250,y,row.LabelWidth,label);
        Label(editorX+parts.ValueX,editorY+5,F6Metrics.ValueWidth,value.ToString(format,CultureInfo.InvariantCulture));
        float step=(max-min)/100;
        if(Button(editorX+parts.MinusX,editorY,F6Metrics.StepButton,"−")){_drag=-1;value=Math.Clamp(value-step,min,max);Changed();}
        if(Button(editorX+parts.PlusX,editorY,F6Metrics.StepButton,"+")){_drag=-1;value=Math.Clamp(value+step,min,max);Changed();}
        var r=R(editorX+parts.SliderX,editorY+3,parts.SliderWidth,28);
        float next=editorY+F6Metrics.RowHeight+F6Metrics.RowGap+6;
        int id=_ordinal++;var e=Event.current;
        if(id==_focus){Reveal(r);r=R(editorX+parts.SliderX,editorY+3,parts.SliderWidth,28);}
        if (!Visible(r)) return next;
        bool focused=id==_focus && GUIUtility.keyboardControl==0;
        if (e.type==EventType.MouseDown && e.button==0 && Inside(r,e.mousePosition)) { _drag=(int)y; _focus=id; GUIUtility.keyboardControl=0; e.Use(); }
        if (_drag==(int)y && e.type is EventType.MouseDrag or EventType.MouseDown or EventType.Used)
        { value=min+Math.Clamp((e.mousePosition.x-r.x)/r.width,0,1)*(max-min); Changed(); if(e.type!=EventType.Used)e.Use(); }
        if (_drag==(int)y && e.type==EventType.MouseUp) { _drag=-1; e.Use(); }
        if (focused && e.type==EventType.KeyDown && e.keyCode is KeyCode.LeftArrow or KeyCode.RightArrow)
        { value=Math.Clamp(value+(e.keyCode==KeyCode.RightArrow?1:-1)*(max-min)/100,min,max); Changed(); e.Use(); }
        if(focused && _menuStep!=0) { value=Math.Clamp(value+_menuStep*(max-min)/100,min,max);_menuStep=0;Changed(); }
        Fill(r,ColorOf(focused?F6Palette.ButtonHover:F6Palette.Track));
        Fill(new Rect(r.x,r.y+r.height*.4f,r.width*Math.Clamp((value-min)/(max-min),0,1),r.height*.2f),Accent);
        float knob=r.x+r.width*Math.Clamp((value-min)/(max-min),0,1); Fill(new Rect(knob-3*_scale,r.y,6*_scale,r.height),Color.white);
        return next;
    }
    private static void Bar(float x,float y,float width,float value,bool steering)
    {
        var r=R(x,y,width,12); Fill(r,new Color(.06f,.08f,.12f,1));
        float origin=steering?.5f:0, length=steering?Math.Clamp(value,-1,1)*.5f:Math.Clamp(value,0,1);
        Fill(new Rect(r.x+r.width*(origin+Math.Min(0,length)),r.y,r.width*Math.Abs(length),r.height),Accent);
    }
    private static string Value(float v,bool steering) => steering ? Math.Abs(v)<.02 ? "Center" : v<0 ? $"Left {Math.Abs(v):P0}" : $"Right {v:P0}" : v.ToString("P0");
    private static void End(float y) => _extent=Math.Max(_extent,y);
    internal static void Draw()
    {
        // IMGUI is already this game's supported keyboard route for text and
        // focus. Accept its F6 event too, deduplicating the InputSystem edge.
        var hotkey = Event.current;
        if (Open && MenuOwnership.Closing && hotkey.type == EventType.KeyDown && hotkey.keyCode == KeyCode.Escape)
        hotkey.Use();
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
            _scale=Math.Min((Screen.width-32f)/Width, (Screen.width>=3000?1.5f:1)*Runtime.Settings.UiScale/100);
            _height=Math.Min(740,(Screen.height-32f)/_scale); _left=(Screen.width-Width*_scale)/2; _top=(Screen.height-_height*_scale)/2;
            _label??=new(); _title??=new(); _help??=new(); _link??=new(); _tab??=new();
            UiNative.Style(_tab,(int)(F6Metrics.BodySize*_scale),false); _tab.normal.textColor=ColorOf(F6Palette.Secondary);
            UiNative.Style(_label,(int)(F6Metrics.BodySize*_scale),true); _label.normal.textColor=ColorOf(F6Palette.Text);
            UiNative.Style(_help,(int)(F6Metrics.HelpSize*_scale),true); _help.normal.textColor=ColorOf(F6Palette.Secondary);
            UiNative.Style(_link,(int)(F6Metrics.HelpSize*_scale),true); _link.normal.textColor=Accent;
            UiNative.Style(_title,(int)(F6Metrics.TitleSize*_scale),false); _title.normal.textColor=ColorOf(F6Palette.Text);
            var e=Event.current;
            _revealFocus=false;
            bool navigationAllowed=Runtime.Wheel?.CaptureButton==null || Runtime.Wheel.SavePending;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Tab && navigationAllowed)
            { _focus=(_focus+(e.shift?-1:1)+Math.Max(1,_count))%Math.Max(1,_count);_revealFocus=true; GUIUtility.keyboardControl=0; e.Use(); }
            if(_menuMove!=0) { _focus=(_focus+_menuMove+Math.Max(1,_count))%Math.Max(1,_count);_menuMove=0;_revealFocus=true;GUIUtility.keyboardControl=0; }
            _activate=(_menuConfirm || e.type==EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.Space) && navigationAllowed;_menuConfirm=false;
            if(e.type==EventType.ScrollWheel && Inside(R(0,144,Width,_height-254),e.mousePosition)) { _scroll=Math.Clamp(_scroll+e.delta.y*30,0,Math.Max(0,_extent-(_height-110))); _drag=-1; e.Use(); }
            if(e.type==EventType.KeyDown && e.keyCode is KeyCode.PageDown or KeyCode.PageUp)
            { _scroll=Math.Clamp(_scroll+(e.keyCode==KeyCode.PageDown?1:-1)*240,0,Math.Max(0,_extent-(_height-110))); _drag=-1; e.Use(); }
            _ordinal=0; _extent=144;
            Fill(R(0,0,Width,_height),ColorOf(F6Palette.Window));
            GUI.Label(R(24,18,330,42),new GUIContent("Woden / Wheel settings"),_title);
            // Drawn in the earlier sidebar order, so keyboard focus order is unchanged: views, pages, Stop FFB, Close.
            if(Button(364,18,84,"Simple",!Advanced,!Editing))Navigate(Runtime.Settings.UiPage,"Simple");
            if(Button(454,18,104,"Advanced",Advanced,!Editing))Navigate(Runtime.Settings.UiPage,"Advanced");
            Label(24,64,694,MenuOwnership.Closing?"Closing after controls are released · Keep settings open cancels":Editing?SettingsPresentation.EditLock:"F6 close · F8 stops FFB · Tab / Enter to navigate · "+_saveStatus,true);
            float tabX=22; foreach(string page in SettingsPresentation.Pages(Runtime.Settings.UiView))
            {
                // Give the short FFB tab enough room for its scaled text; Advanced still fits the row.
                float tabWidth=page.Length*10f+2*F6Metrics.TabPadX+(page=="FFB"?14:0);
                if(Tab(tabX,92,tabWidth,page,Runtime.Settings.UiPage==page,!Editing))Navigate(page);
                tabX+=tabWidth;
            }
            Fill(R(0,136,Width,1),ColorOf(F6Palette.Divider));
            if(Button(598,_height-99,120,"Stop FFB")) { try { Runtime.Force?.Panic(); } catch(Exception ex) { Message="Save failed: "+ex.Message; } }
            if(Button(574,18,144,MenuOwnership.Closing?"Closing…":"Close",false,!Editing))Close(true);
            if(_sharedClosePrompt)
            {
                if(_focus<0)_focus=_ordinal+1; // Keep editing is the first close-intent default.
                Label(24,170,694,BindingPresentation.Unsaved+". Discard them before closing, or keep editing.",true,72);
                if(Button(24,260,280,"Discard and close")){SharedBindingHost.Current.Discard();_sharedClosePrompt=false;Close(true);}
                if(Button(320,260,280,"Keep editing"))_sharedClosePrompt=false;
                _count=_ordinal;return;
            }
            if(MenuOwnership.Closing)
            {
                Label(24,170,680,Message,true,110);
                if(Button(24,300,280,"Keep settings open"))CancelPendingClose();
                _count=_ordinal;_focus=Math.Clamp(_focus,0,Math.Max(0,_count-1));_menuStep=0;
                return;
            }
            _body=true;
            switch(Runtime.Settings.UiPage)
            { case "Controls":ControlsPage();break; case "FFB":ForcePage();break; case "Cameras":CameraPage();break; case "Driving":DifficultyPage();break; case "Telemetry":TelemetryPage();break; case "Help":HelpPage();break; default:SetupPage();break; }
            _body=false; _scroll=Math.Clamp(_scroll,0,Math.Max(0,_extent-(_height-110)));
            if(_extent>_height-110)
            {
                if(Button(24,_height-99,90,"Up",false,_scroll>0)) {_scroll=Math.Max(0,_scroll-240);_drag=-1;}
                if(Button(124,_height-99,100,"Down",false,_scroll<_extent-(_height-110))) {_scroll=Math.Min(_extent-(_height-110),_scroll+240);_drag=-1;}
                Label(240,_height-94,340,"Scroll or Page Up / Down for more",true);
            }
            Fill(R(12,_height-58,Width-24,1),new Color(.25f,.3f,.36f,1));
            string status=Runtime.Wheel?.SaveError ?? (_saveStatus=="Save failed"?"Save failed — "+Message:Message);
            status=BindingFooterStatus(SharedBindingHost.Current,status);
            Label(24,_height-49,694,status,true,43);
            _count=_ordinal;_focus=Math.Clamp(_focus,0,Math.Max(0,_count-1));_menuStep=0;
            if(e.type==EventType.MouseDown && Inside(R(0,0,Width,_height),e.mousePosition))e.Use();
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
            bool threeDisplays = TripleView.Active;
            float viewportWidth = threeDisplays ? Screen.width / 3f : Screen.width;
            float width = Math.Min(viewportWidth - 48 * scale, 1000 * scale);
            float left = 24 * scale;
            if (threeDisplays)
            {
                // Use the recording HUD's center-display anchor, with this hint's existing size and padding.
                var layout = SessionStatusPresentation.Layout(Screen.width, Screen.height, true);
                left = layout.X + (layout.Width - width) / 2;
            }
            var rect = new Rect(left, Screen.height - 90 * scale, width, 64 * scale);
            if (Event.current.type == EventType.Repaint) { GUI.color = new Color(.035f,.05f,.075f,1); GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color = old; }
            GUI.Label(new Rect(rect.x+12*scale,rect.y+8*scale,rect.width-24*scale,rect.height-16*scale),new GUIContent(Message),_help);
        }
        catch (Exception ex) { if (Runtime.Clock.Elapsed.TotalSeconds > _nextError) { Runtime.Log.LogWarning("Camera adjustment hint: " + ex.Message); _nextError = Runtime.Clock.Elapsed.TotalSeconds + 5; } }
        finally { GUI.color = old; }
    }
    private static void Refresh() { Runtime.Force?.Reconnect("Device refresh");Runtime.Devices?.Refresh(); }
    private static void SetupPage()
    {
        if(ShimInput.Owns) { ShimPage(); return; }
        var cfg=Runtime.Settings; bool before=cfg.WheelEnabled;
        Toggle(250,152,390,"Wheel controls",ref cfg.WheelEnabled); if(before!=cfg.WheelEnabled)Runtime.Force?.Suspend("Wheel controls changed");
        if(Button(700,152,244,"Refresh devices"))Refresh();
        Label(250,202,690,Runtime.Settings.InputBackend=="Shim"?ShimInput.Status:"Connect your controls, check the bars, then drive.",true,36);
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
        var b=Runtime.Wheel!.Bindings; string text=b.Buttons.TryGetValue(action,out var button)?(Runtime.Devices?.Describe(button.DeviceGuid)??"Saved device unavailable")+" - "+Dbce.Wheel.Ffb.DigitalInput.Label(button.Button):"";
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
        Label(250,202,690,input.SavePending?"Your previous binding remains active. Retry the saved proposal, or Cancel to discard it.":input.EditingDeadzone?"Adjust the deadzone without recapturing. Values inside it become exactly zero; full travel stays full input.":input.CaptureAxis=="Steer"?"Center the wheel. Turn right first, then sweep fully both ways.":input.CaptureAxis!=null?"Release the control, then press or pull fully and release.":CameraTuning.Actions.Contains(action)?"Press a key or wheel button. F6/F8 are reserved; Escape cancels.":"Press one wheel button. Escape cancels. Native keyboard controls stay in the game menu.",true,48);
        Label(250,250,690,input.Status,true,32);
        if(input.Capture!=null || input.EditingDeadzone)
        {
            var b=input.PreviewBinding;
            Label(250,286,690,b==null?"Waiting for the full range…":(Runtime.Devices?.Describe(b.DeviceGuid)??"Saved device unavailable")+" · "+Axes[b.Axis]+" axis",true,28);
            float v=0; bool ok=Runtime.Devices?.TryAxis(b,out v)==true;Bar(250,330,500,ok?v:0,action=="Steer");Label(770,318,170,ok?Value(v,action=="Steer"):"--",true);
            if(!input.EditingDeadzone)Toggle(250,354,390,"Invert calibration",ref input.CaptureInvert);
            else Label(250,354,690,"Inversion: "+(b?.Inverted==true?"On":"Off")+" (unchanged)",true);
            float end;
            if(input.EditingDeadzone&&input.SavePending) { Label(250,396,690,"Deadzone "+input.CaptureDeadzone.ToString("0.##",CultureInfo.InvariantCulture)+"% · waiting for Retry save or Cancel",true);end=442; }
            else
            {
                float before=input.CaptureDeadzone;
                end=Slider(396,"Deadzone (%)",ref input.CaptureDeadzone,0,25,"0.##",save:false);
                if(input.CaptureDeadzone!=before)input.DeadzoneEdited();
            }
            Label(250,end,690,b==null?input.Capture?.Status??"Saved calibration unavailable":$"Endpoints {b.Calibration.Rest} → {b.Calibration.End}"+(b.Calibration.Centre is int c?$" · center {c}":""),true,28);
            if(Button(250,518,248,input.EditingDeadzone?(input.SavePending?"Retry save":"Save deadzone"):"Save calibration",false,b!=null&&(ok||input.EditingDeadzone)))
            { if(input.EditingDeadzone&&input.SavePending)input.RetrySave();else input.FinishAxis(); }
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
        if(ShimInput.Owns) { ShimPage(); return; }
        if(CapturePage())return; var input=Runtime.Wheel!; float y=152;
        if(Runtime.Settings.InputBackend=="Shim")
        {
            Label(250,y,690,ShimInput.Status,true,72);y+=84;
            Label(250,y,690,"Native bindings below remain active until Shim admission. Close the game to change dbce-controls.ini or the backend.",true,60);y+=72;
        }
        foreach(string name in new[]{"Steer","Throttle","Brake","Handbrake"})
        {
            var b=input.Bindings.Axis(name);Label(250,y,210,ActionLabel(name));
            Label(464,y,480,b==null?"Not bound":(Runtime.Devices?.Describe(b.DeviceGuid)??"Saved device unavailable")+" · "+Axes[b.Axis]+" axis",true,42);
            float v=0;bool ok=Runtime.Devices?.TryAxis(b,out v)==true;Bar(250,y+39,430,ok?v:0,name=="Steer");Label(700,y+29,240,ok?"Device input: "+Value(v,name=="Steer"):b==null?"Not bound":"Saved device unavailable",true);
            if(Button(250,y+60,100,"Bind"))input.BeginAxis(name);
            if(Button(362,y+60,142,"Calibrate",false,b!=null))input.BeginAxis(name,true);
            if(Button(516,y+60,100,"Clear",false,b!=null)) { input.TryCommit(proposed => proposed.SetAxis(name,null)); }
            if(Button(638,y+60,142,"Deadzone",false,b?.Valid==true)) { input.BeginDeadzone(name);_scroll=0;_focus=0; }
            if(b!=null)Label(790,y+64,152,(b.Calibration.Deadzone*100).ToString("0.##",CultureInfo.InvariantCulture)+$"% · Invert {(b.Inverted?"On":"Off")}",true);
            y+=104;
        }
        foreach(var group in new[]{("Driving buttons",new[]{"Gear up","Gear down","Handbrake","Camera","Rear view","Respawn"}),("Menu buttons",new[]{"Pause","Confirm","Back","Menu up","Menu down","Menu left","Menu right"}),("Mod buttons",new[]{"Settings panel","Panic stop"}),("Extra game buttons",new[]{"Lights","Horn","Records","Next song"}),("Shifter bindings",WheelInput.GateActions)})
        {
            if(Button(250,y,694,(Expanded.Contains(group.Item1)?"− ":"+ ")+group.Item1,Expanded.Contains(group.Item1))) { if(!Expanded.Add(group.Item1))Expanded.Remove(group.Item1); }
            y+=46;
            if(!Expanded.Contains(group.Item1))continue;
            foreach(string action in group.Item2){BindingRow(y,action,action=="Handbrake"?"Handbrake (button)":null);y+=94;}
            if(group.Item1=="Menu buttons"||group.Item1=="Shifter bindings") {Label(250,y,690,group.Item1=="Menu buttons"?"Up/down moves settings focus; left/right adjusts sliders. Confirm activates; Back closes. Arcade car choice takes one direction per press; transmission uses the native menu. "+MenuNavigation.Status:"H-pattern: hold a gate for that gear; the mod steps the game's Shift up/down until it matches. Out of gear cuts the drive (Woden has no neutral). R swaps the pedals, because Woden reverses on the brake. Needs the game's manual transmission. Clutch is not routed. Now: "+(Runtime.Wheel?.Shifter.Status??""),true,60);y+=70;}
        }
        Label(250,y,690,"Handbrake: progressive rear braking/grip; the game's engine cut remains digital. Button input requests full braking.",true,48);y+=64;
        y=GearboxRow(y);
        if(Advanced)
        {
            Label(250,y,420,"Local player index: "+Runtime.Settings.Player);
            if(Button(714,y,100,"−")){Runtime.Settings.Player=Math.Max(0,Runtime.Settings.Player-1);Dirty();}
            if(Button(830,y,100,"+")){Runtime.Settings.Player=Math.Min(3,Runtime.Settings.Player+1);Dirty();}y+=52;
            Label(250,y,690,input.Status,true,46);y+=54;
        }
        End(y);
    }

    private static void ShimPage()
    {
        if(Runtime.Settings.UiPage=="Controls"){SharedBindingPage();return;}
        var cfg=Runtime.Settings; bool before=cfg.WheelEnabled;
        Toggle(250,152,690,"Wheel controls",ref cfg.WheelEnabled);
        if(before!=cfg.WheelEnabled)Runtime.Force?.Suspend("Wheel controls changed");
        Label(250,202,690,ShimInput.Status,true,48);
        Label(250,254,690,"Controls use dbce-controls.ini from launch. Close the game to change the profile or backend. Standalone bindings are preserved.",true,64);
        float y=330;
        foreach(string id in new[]{"steer","throttle","brake","handbrake"})
        {
            var action=ShimInput.Action(id);
            Label(250,y,160,char.ToUpperInvariant(id[0])+id[1..]);
            Bar(420,y+8,310,action.Applied,id=="steer");
            Label(742,y,202,action.Status.ToString(),true);y+=54;
        }
        Label(250,y,690,"Wheel-only pilot: Unity gamepads must be disconnected before launch. Native keyboard controls remain available.",true,54);y+=64;
        Label(250,y,690,"Sequential shifts or H-pattern N/R/gears 1-6 require native Manual. Clutch, select and gears 7-8 are unavailable. Lights, Records and Next song retain native keyboard controls.",true,72);y+=84;
        Label(250,y,690,ShimInput.TransmissionStatus,true,64);y+=76;
        End(GearboxRow(y));
    }

    // Shared wording: a retained Save failure first, then an unsaved draft, then a saved revision for next start.
    internal static string BindingFooterStatus(SharedBindingHost host,string generalStatus)
    {
        string binding=BindingPresentation.Footer(host.HasDraft,host.SavedPendingRestart,host.SaveFailure);
        return binding.Length==0?generalStatus:string.IsNullOrEmpty(generalStatus)?binding:binding+" · "+generalStatus;
    }

    private static void SharedBindingPage()
    {
        var host=SharedBindingHost.Current;
        bool editable=host.Access==SharedBindingAccess.Editable;
        float y=152;
        // Keep the first binding/value in the initial 720p body. This stripped
        // host uses shared metrics with rectangles rather than GUILayout widgets.
        Label(250,y,690,"Shared controls: "+host.ActiveSource,true,42);y+=42+F6Metrics.RowGap;
        // In-game binding uses only devices this profile already opened; there is no Find devices here.
        // Three help-size lines; a taller row pushes the first binding out of the initial 720p body.
        Label(250,y,690,BindingPresentation.SetupGuidance(canFindDevices:false),true,54);y+=54+F6Metrics.RowGap;
        float accessHeight=editable?42:68;
        Label(250,y,690,host.AccessReason,true,accessHeight);y+=accessHeight+F6Metrics.RowGap;
        if(host.SavedPendingRestart){Label(250,y,690,BindingPresentation.SavedForNextStart+". Active controls are unchanged.",true,44);y+=52;}
        if(host.HasDraft){Label(250,y,690,BindingPresentation.Unsaved,true,28);y+=36;}
        if(host.SaveFailure.Length>0){Label(250,y,690,host.SaveFailure,true,90);y+=98;}
        if(Button(250,y,220,BindingPresentation.SaveBindings,false,editable&&host.HasDraft&&host.CapturingAction==null))host.Save(out _);
        if(Button(486,y,220,"Discard changes",false,host.HasDraft))host.Discard();y+=F6Metrics.RowHeight+F6Metrics.RowGap;
        var devices=host.Devices;
        int selectedDevice=-1;
        if(devices.Count>0)
        {
            _sharedDeviceGuid??=(devices.FirstOrDefault(d=>d.IsDefault)??devices[0]).InstanceGuid;
            for(int i=0;i<devices.Count;i++)if(devices[i].InstanceGuid==_sharedDeviceGuid)selectedDevice=i;
            int prior=selectedDevice;
            y=Choice(y,BindingPresentation.BindFrom,devices.Select(d=>d.Name.Length>36?d.Name[..36]+"…":d.Name).ToArray(),ref selectedDevice,false);
            if(prior!=selectedDevice&&selectedDevice>=0)_sharedDeviceGuid=devices[selectedDevice].InstanceGuid;
            if(selectedDevice<0){Label(250,y,690,"The selected device is unavailable. Choose one under Bind from; no substitute was selected.",true,48);y+=56;}
        }
        float captureHeight=editable&&host.CapturingAction==null?54:68;
        Label(250,y,690,host.CapturingAction==null?host.CaptureMessage:$"Binding {host.CapturingAction} · {host.CaptureSecondsLeft:0}s · {host.CaptureMessage}",true,captureHeight);y+=captureHeight+F6Metrics.RowGap;
        if(host.CapturingAction!=null)
        {if(Button(250,y,220,"Cancel capture"))host.CancelCapture();End(y+48);return;}
        foreach(var row in host.Rows)
        {
            Label(250,y,690,row.Label);y+=34;
            Label(250,y,690,"Active: "+row.Active.Text,true,42);y+=48;
            if(row.NextStart is { } saved){Label(250,y,690,"Next start: "+saved.Text,true,42);y+=48;}
            if(row.Draft is { } draft){Label(250,y,690,"Draft: "+draft.Text,true,42);y+=48;}
            if(row.Note.Length!=0){Label(250,y,690,row.Note,true,44);y+=50;}
            bool allowed=editable&&row.Note.Length==0;
            if(Button(250,y,120,"Bind",false,allowed&&selectedDevice>=0))host.BeginCapture(row.Action,devices[selectedDevice].InstanceGuid);
            if(Button(382,y,120,"Clear",false,allowed))host.Clear(row.Action);
            if(row.IsAxis&&Button(514,y,220,(row.DraftInverted??row.NextStartInverted??row.ActiveInverted)?"Use normal axis":"Invert axis",false,allowed))
                host.SetInverted(row.Action,!(row.DraftInverted??row.NextStartInverted??row.ActiveInverted));
            y+=54;
        }
        End(y);
    }

    // [Input] NativeGearbox (NativeGearboxPreference): the game's own Manual/Automatic choice, set at the next start.
    private static float GearboxRow(float y)
    {
        var cfg=Runtime.Settings;
        Label(250,y+6,170,"Gearbox at start");
        var options=NativeGearboxPreference.Options;
        for(int i=0;i<options.Length;i++)
            if(Button(i==0?426:i==1?548:670,y,i==2?150:110,options[i],cfg.NativeGearbox==options[i])&&cfg.NativeGearbox!=options[i]) { cfg.NativeGearbox=options[i]; Dirty(); }
        y+=48;
        Label(250,y,690,"Sets the game's own transmission choice once, on the first main menu after the next game start. Keep leaves it alone; the car-select menu can still change it. This start: "+NativeGearboxApply.Status,true,60);
        return y+70;
    }

    private static void ForcePage()
    {
        var cfg=Runtime.Settings;var force=Runtime.Force!;
        Label(250,156,160,"FFB:");
        if(Button(430,152,90,"Off",!cfg.FfbEnabled)) {try{force.SetEnabled(false);}catch(Exception ex){Message="Save failed: "+ex.Message;}}
        if(Button(532,152,90,"On",cfg.FfbEnabled)) {try{force.SetEnabled(true);}catch(Exception ex){Message="Save failed: "+ex.Message;}}
        var target=Runtime.Devices!.ResolveForceTarget(cfg.FfbFollowSteering,cfg.FfbGuid,Runtime.Wheel!.SteeringGuid,Runtime.Wheel.SteeringReason);
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
        // Steering strength and the crash kick are independent controls in Simple (STD-027).
        y=Slider(y,"Steering strength (%)",ref cfg.FfbStrength,0,100,"F0");
        Label(250,y,690,force.Status,true,42);y+=48;
        if(Button(250,y,330,"Default steering strength: 50%")){cfg.FfbStrength=50;Dirty();}y+=46;
        if(!Advanced && cfg.CustomFfb){if(Button(250,y,694,"Custom FFB tuning active. Review in Advanced"))Navigate("FFB","Advanced");y+=44;}
        // One force model for players (STD-026): no selector. A saved Classic choice is kept, and said so, until Grip is tuned for this game.
        Label(250,y,690,cfg.FfbModel=="Grip"?"Steering strength sets cornering resistance and return to center from the front tires' grip; the wheel lightens as the front slides. Now: "+force.GripStatus:"Earlier force model, kept from your saved settings until the current one is tuned for this game. Reset FFB tuning (Advanced) switches to the current model.",true,46);y+=52;
        y=ToggleRow(y,"Crash effects",ref cfg.CrashEnabled);Label(250,y,690,"A short push and rattle when you hit something. Now: "+force.CrashStatus,true,40);y+=46;
        if(cfg.CrashEnabled){y=Slider(y,"Crash strength (%)",ref cfg.CrashStrength,0,100,"F0");Label(250,y,690,"Force of the hardest hit; separate from Steering strength.",true,40);y+=48;}
        if(Advanced)
        {
            Label(250,y,690,"On is remembered; feedback starts during driving. F8 / Stop FFB saves Off.",true,46);y+=58;
            if(cfg.FfbModel=="Grip")
            {
                y=Slider(y,"Reference (x front load)",ref cfg.FfbLoadRatio,.2f,10,"F2");Label(250,y,690,"Full force at Steering strength 50 as a multiple of the mean driving front load. Higher is lighter; default 2.",true,40);y+=48;
                y=Slider(y,"Smoothing",ref cfg.FfbGripSmoothing,0,.95f,"F2");Label(250,y,690,"Per-update smoothing of the tire force; default 0.2.",true,40);y+=48;
                y=Slider(y,"Steering damping",ref cfg.FfbDamping,0,.5f);Label(250,y,690,"Resists quick wheel movement with its own gain; Steering strength does not scale it. Default 0.05.",true,40);y+=48;
            }
            else
            {
                // The earlier model's own settings, shown only while a saved Classic choice is active.
                y=Slider(y,"Peak output cap (%)",ref cfg.FfbPeak,0,50,"F0");Label(250,y,690,"Earlier model: maximum commanded force; default 25%.",true,40);y+=48;
                y=Slider(y,"Smoothing (ms)",ref cfg.FfbSmoothing,0,200,"F0");Label(250,y,690,"Earlier model: higher values soften rapid changes but delay feedback. Default 35 ms.",true,40);y+=48;
                y=Slider(y,"Steering damping",ref cfg.FfbDamping,0,.5f);Label(250,y,690,"Earlier model: resists wheel movement, scaled with strength; default 0.05.",true,40);y+=48;
                y=Slider(y,"Reference front load",ref cfg.FfbLoadReference,100,50000,"F0");Label(250,y,690,"Earlier model: higher values reduce the tire estimate. Unity force units; default 6000.",true,40);y+=48;
                y=Slider(y,"Slip response scale",ref cfg.FfbSlipScale,.02f,3);Label(250,y,690,"Earlier model: larger values soften the slip response. Game slip units; default 0.35.",true,40);y+=48;
            }
            y=ToggleRow(y,"Invert force direction",ref cfg.FfbInvert);
            if(Button(250,y,280,"Reset FFB tuning")){cfg.FfbModel="Grip";cfg.FfbLoadRatio=2;cfg.FfbGripSmoothing=.2f;cfg.FfbStrength=50;cfg.FfbPeak=25;cfg.FfbSmoothing=35;cfg.FfbDamping=.05f;cfg.FfbLoadReference=6000;cfg.FfbSlipScale=.35f;cfg.FfbInvert=false;Dirty();}y+=46;
            Label(250,y,690,$"Accepted output {force.Sent:P1}; calls {force.Attempts}; failures {force.Failures}. Reset preserves On/Off and device.",true,46);y+=52;
        }
        End(y);
    }
    private static void CameraPage()
    {
        if(CapturePage())return;var cfg=Runtime.Settings;float y=152;
        // One triple-screen selector, as in the other mods (STD-022): Off, Surround, Separate monitors.
        int layout=TripleLayoutChoice.Current(TripleView.Mode==TripleMode.Off,SpanWindow.Enabled);
        int priorLayout=layout;y=Choice(y,"Triple screens",TripleLayoutChoice.Labels,ref layout);
        if(layout!=priorLayout){var picked=TripleLayoutChoice.Apply(layout);TripleView.Mode=picked.Off?TripleMode.Off:TripleView.Mode==TripleMode.On?TripleMode.On:TripleMode.Auto;SpanWindow.Enabled=picked.Span;}
        Label(250,y,690,layout==0?"Single view. Window size changes at the next game start. Status: "+TripleView.Status:layout==1?"Three views on Surround. Window layout applies at the next game start. Status: "+TripleView.Status:"One borderless window across three side-by-side monitors, applied at the next game start. Span: "+SpanWindow.Status+"; views: "+TripleView.Status,true,46);y+=56;
        int fovMode=TripleView.MatchGameFov?1:0;y=Choice(y,"Triple FOV",new[]{"Physical rig","Adjustable FOV"},ref fovMode);TripleView.MatchGameFov=fovMode==1;
        Label(250,y,690,TripleView.MatchGameFov?"Bonnet and bumper use the active view lens, including Fit to car. All three views are recalculated together using a virtual eye distance; your measured rig is kept.":"Bonnet and bumper use the measured rig geometry. Their FOV keys and slider are inactive while triples are on; stock chase keeps its saved FOV behavior.",true,64);y+=72;
        Label(250,y,690,TripleView.CenterFovStatus,true,46);y+=54;
        y=ToggleRow(y,"Bonnet camera",ref cfg.Bonnet);y=ToggleRow(y,"Bumper camera",ref cfg.Bumper);
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
            if(!_editBumper){bool fit=cfg.CameraAutoFit;y=ToggleRow(y,"Fit to car",ref cfg.CameraAutoFit);if(fit&&!cfg.CameraAutoFit)cfg.SetCameraPose(false,MountedCamera.Pose(false));}
            var pose=MountedCamera.Pose(_editBumper);float side=pose.Side,height=pose.Height,forward=pose.Forward,pitch=pose.Pitch,fov=pose.Fov;
            y=Slider(y,"Side position (m)",ref side,-2,2);y=Slider(y,"Height (m)",ref height,.1f,3);y=Slider(y,"Forward position (m)",ref forward,-2,4);
            y=Slider(y,"Tilt down (degrees)",ref pitch,-30,30,"F1");
            if(TripleView.MountedFovLocked)
            {
                Label(250,y,690,$"Field of view: physical rig ({FovLockText.Angle(TripleView.CenterVerticalFov)}). Lens FOV {fov:F0}° applies only to single or adjustable views.",true,42);y+=48;
                // One explicit action where the slider would be: the same Triple FOV choice as above, never automatic.
                if(Button(250,y,330,"Use Adjustable FOV"))TripleView.MatchGameFov=true;y+=46;
            }
            else y=Slider(y,"Vertical field of view (degrees)",ref fov,30,110,"F0");
            var edited=new CameraPose(side,height,forward,pitch,fov);if(edited!=pose){MountedCamera.SavePose(_editBumper,edited);Dirty();}
            if(Button(250,y,260,"Reset this view")){cfg.ResetCamera(_editBumper);Dirty();}y+=46;
            Label(250,y,690,"Position moves the selected mount in meters; positive tilt looks down. FOV widens single and adjustable views; physical triples use rig geometry. Reset restores this mount's defaults.",true,58);y+=66;
            y=Slider(y,"Move step per press (m)",ref cfg.CameraMoveStep,.005f,.25f,"F3");
            y=Slider(y,"Tilt step per press (°)",ref cfg.CameraTiltStep,.1f,10,"F1");
            y=Slider(y,"FOV step per press (°)",ref cfg.CameraFovStep,.5f,10,"F1");
            if(Button(250,y,330,"Default steps: 0.02 m, 1°, 2°")){cfg.CameraMoveStep=CameraSteps.Default.Move;cfg.CameraTiltStep=CameraSteps.Default.Tilt;cfg.CameraFovStep=CameraSteps.Default.Fov;Dirty();}y+=46;
            Label(250,y,690,"Steps set how far each numpad press moves, tilts or zooms the active view. Smaller steps place the view more finely.",true,46);y+=54;
        }
        y=ToggleRow(y,"Frame rate display",ref cfg.ShowFrameRate);
        Label(250,y,690,"Frame rate, last 10 s: "+FrameRate.Summary,true,42);y+=48;
        Label(250,y,690,MountedCamera.Status,true,42);End(y+50);
    }
    private static void DifficultyPage()
    {
        var cfg=Runtime.Settings;Toggle(250,152,520,"Countdown assist",ref cfg.CountdownAssistEnabled);
        float y=Slider(218,"Countdown speed (%)",ref cfg.CountdownSpeed,25,100,"F0");
        Label(250,y,690,$"At {cfg.CountdownSpeed:F0}%, 60 timer seconds allow about {6000/cfg.CountdownSpeed:F0} driving seconds.",true,48);y+=56;
        if(Button(250,y,230,"Default speed: 75%")){cfg.CountdownSpeed=75;Dirty();}y+=48;
        Label(250,y,690,"Slows only the single-player time limit. Car physics, elapsed stage clocks and checkpoint bonuses keep their normal behavior. Default Off.",true,72);y+=80;
        Label(250,y,690,(cfg.CountdownAssistEnabled?CountdownTimerAssist.Status:"Off — normal countdown speed")+" · "+LeaderboardGuard.Status,true,46);End(y+56);
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
            TextPort(y,"Forza UDP port (0 = off)",ref _forza);y+=54;TextPort(y,"Detailed JSON port (0 = off)",ref _detail);y+=54;y=Slider(y,"Detailed stream rate (Hz)",ref _rate,1,60,"F0");_rate=(int)Math.Round(_rate);
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
            Label(250,y,690,"Recordings are prepared by the agent before launch ("+RecordingProfiles.OrdinaryLimit+" maximum). No recording starts from this menu. Changing connection/view keeps any capture running.",true,70);y+=80;
        }
        End(y);
    }
    private static void HelpPage()
    {
        Label(250,152,690,"Woden Rally Edge Wheel "+ReportVersion+" · development candidate",true,48);
        float y=ReportHelp(206);
        Label(250,y,690,"No controls? Turn Wheel controls On in Setup, then bind and calibrate the three required axes in Controls.",true,58);y+=74;
        Label(250,y,690,"No feedback? Check On and the selected FFB device. The FFB page explains inactive output. Close settings and drive. Stop FFB / F8 remembers Off.",true,68);y+=84;
        Label(250,y,690,"Camera keys? Open Cameras → Adjustment bindings. Keys affect the active Bonnet or Bumper; hold to repeat, release to stop.",true,58);y+=80;
        y=Slider(y,"UI scale (%)",ref Runtime.Settings.UiScale,85,150,"F0");
        if(Button(250,y,240,"Default scale: 100%")){Runtime.Settings.UiScale=100;Dirty();}
        if(Button(508,y,436,"Create support file (local)"))CreateSupport();
        y+=56;
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
