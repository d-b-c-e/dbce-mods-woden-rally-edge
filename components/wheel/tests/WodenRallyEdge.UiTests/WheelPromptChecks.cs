using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using WodenRallyEdge;
using WodenRallyEdge.Core;

// Arcade header prompt (ArcadeHeaderText -> MenuCameraScript.Infotext) through the real hook source.
// The fixture header and Infotext model the decoded native writer; no game, font or rendering.
internal static class WheelPromptChecks
{
    internal static void Run(Action<bool,string> check,Guid id)
    {
        var wheel=Runtime.Wheel!;var hub=Runtime.Devices!;
        var saved=wheel.Bindings.Buttons.ToDictionary(x=>x.Key,x=>x.Value);var events=EventSystem.current;
        string L(int button)=>"「"+Dbce.Wheel.Ffb.DigitalInput.Label(button)+"」";
        string pad="■①Confirm - ②Back",keys="■「Enter」Confirm - 「Esc」Back",wheelText="■"+L(10)+"Confirm - "+L(11)+"Back";
        void Reset() {
            WheelPrompts.Source.Reset();WheelPrompts.Scoped=false;GamePadSystem.LastInput=0;Runtime.Local=null;Pause.Paused=false;
            MenuCameraScript.InfoTexts.Clear();MenuCameraScript.TypeWriterInput=null;MenuCameraScript.FailInfotext=false;
            foreach(var d in hub.Devices)d.Ok=true;hub.Pressed.Clear();Runtime.Focused=true;
            wheel.Bindings.Buttons.Clear();wheel.Bindings.Buttons["Confirm"]=new(id,10);wheel.Bindings.Buttons["Back"]=new(id,11);wheel.Bindings.Buttons["Pause"]=new(id,16);
        }
        string Refresh() {
            ArcadeHeaderPromptHook.Prefix();
            try { new ArcadeHeaderText().OnEnable(); } finally { ArcadeHeaderPromptHook.Finalizer(); }
            return MenuCameraScript.InfoTexts[^1];
        }
        void Wheel() { var lease=new CustomMenuLease(new MenuControls(),"Confirm");lease.Restore(); }
        void Native(Action<GamePadSystem.Game_Pad> press) { var p=new GamePadSystem.Game_Pad();press(p);WheelPrompts.AfterPadRead(p); }

        Reset();check(Refresh()==pad,"no wheel menu press keeps the stock pad prompt");
        Reset();GamePadSystem.LastInput=1;check(Refresh()==keys,"no wheel menu press keeps the stock keyboard prompt");
        Reset();Wheel();check(Refresh()==wheelText&&MenuCameraScript.TypeWriterInput==wheelText&&MenuCameraScript.InfoTexts.Count==1,
            "wheel-delivered Confirm shows bound wheel buttons on the Text and its TypeWriter through one stock write");
        Reset();GamePadSystem.LastInput=1;Wheel();check(Refresh()==wheelText,"wheel last after keyboard replaces the keyboard keys too");
        Reset();Wheel();Native(_=>{});check(Refresh()==wheelText,"neutral stock pad reads do not take the prompt back");
        Reset();Wheel();Native(p=>p.A=true);check(Refresh()==pad,"a later native pad press restores stock pad wording at the next refresh");
        Reset();GamePadSystem.LastInput=1;Wheel();Native(p=>p.AnyKey=true);check(Refresh()==keys,"a later native keyboard/aggregate press restores stock keyboard wording");
        Reset();Wheel();check(Refresh()==wheelText,"wheel prompt shown");Native(p=>p.B=true);check(Refresh()==pad&&MenuCameraScript.InfoTexts[0]==wheelText,
            "the next stock refresh replaces the wheel text; nothing global was changed");
        Reset();Wheel();Native(p=>p.LS=new(0,1));Wheel();check(Refresh()==wheelText,"a newer wheel press wins over an older native stick move");
        Reset();wheel.Bindings.Buttons.Remove("Back");Wheel();check(Refresh()=="■"+L(10)+"Confirm - ②Back","unbound action keeps its stock glyph");
        Reset();Wheel();hub.Devices[0].Ok=false;check(Refresh()==pad,"bound device not reading keeps stock glyphs");
        Reset();Wheel();Runtime.Local=new();Native(p=>p.A=true);check(Refresh()==wheelText,"pad reads while driving are not scanned");
        Pause.Paused=true;Native(p=>p.A=true);check(Refresh()==pad,"pad reads in the pause menu are scanned");
        Reset();Wheel();MenuCameraScript.Infotext("①Other",false,false,0);check(MenuCameraScript.InfoTexts[^1]=="■①Other","other Infotext writers stay stock");
        Reset();Wheel();MenuCameraScript.FailInfotext=true;bool threw=false;try{Refresh();}catch(InvalidOperationException){threw=true;}
        MenuCameraScript.FailInfotext=false;check(threw&&!WheelPrompts.Scoped&&Refresh()==wheelText,"native failure clears the scope and the re-entry guard");

        var owns=typeof(ShimInput).GetProperty(nameof(ShimInput.Owns),BindingFlags.Static|BindingFlags.NonPublic)!;
        Reset();Wheel();owns.SetValue(null,true);try{check(Refresh()==pad,"shared-profile (Shim) input keeps stock glyphs");}finally{owns.SetValue(null,false);}

        Reset();var startup=new StartupButtonLease(new MenuControls());startup.Restore();check(WheelPrompts.Source.WheelLast,"bound startup Start counts as a wheel menu press");
        Reset();EventSystem.current=new(){currentSelectedGameObject=new()};MenuNavigation.Update();hub.Pressed.Add(wheel.Bindings.Buttons["Confirm"]);
        long delivered=MenuNavigation.Delivered;MenuNavigation.Update();
        check(MenuNavigation.Delivered==delivered+1&&WheelPrompts.Source.WheelLast,"delivered wheel menu navigation counts as a wheel menu press");

        Reset();wheel.Bindings.Buttons.Clear();foreach(var x in saved)wheel.Bindings.Buttons[x.Key]=x.Value;
        WheelPrompts.Source.Reset();EventSystem.current=events;hub.Pressed.Clear();
    }
}
