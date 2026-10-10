using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class CustomMenuChecks
{
    internal static void Run(Action<bool,string> check,Guid id)
    {
        var wheel=Runtime.Wheel!;var hub=Runtime.Devices!;
        var saved=wheel.Bindings.Buttons.ToDictionary(x=>x.Key,x=>x.Value);
        void Reset() {
            Panel.Close(false); CustomWheelMenu.Reset(); hub.Pressed.Clear(); hub.OnPoll=null;
            foreach(var d in hub.Devices)d.Ok=true;
            wheel.Bindings.Buttons.Clear();int index=10;
            foreach(var a in MenuNavigation.Actions.Concat(new[]{"Pause"}))wheel.Bindings.Buttons[a]=new(id,index++);
            Runtime.Local=null; Runtime.Focused=true; Input.Keys.Clear();Input.Down.Clear();Input.Held=false;Input.FailRead=false;
            Keyboard.current=new();Time.frameCount++;MenuCameraScript.Exiting=false;
            GameMaster.Demo=false;GameMaster.NrOfPlayers=1;
        }
        void Car(ArcadeCarSelect car) {
            Time.frameCount++;
            bool run=ArcadeWheelMenuHook.Prefix(car,out var state);
            try { if(run)car.FixedUpdate(); ArcadeWheelMenuHook.Postfix(state); }
            finally { ArcadeWheelMenuHook.Finalizer(state); }
        }
        void Stage(StagePresentation stage) {
            Time.frameCount++;
            bool run=PresentationWheelMenuHook.Prefix(stage,out var state);
            try {if(run)stage.Update();PresentationWheelMenuHook.Postfix(state);}
            finally {PresentationWheelMenuHook.Finalizer(state);}
        }
        void Neutral(ArcadeCarSelect car) {hub.Pressed.Clear();Car(car);Runtime.Clock.Advance(.11);Car(car);}
        void Down(string action){hub.Pressed.Clear();hub.Pressed.Add(wheel.Bindings.Buttons[action]);}
        Reset();var ready=new ArcadeCarSelect();Down("Confirm");Car(ready);
        check(ready.Selections==0,"custom menu requires fresh neutral after entry, held Confirm cannot carry in");
        Neutral(ready);Down("Confirm");Car(ready);
        check(ready.Selections==1&&!ready.field_Private_Boolean_0&&!ready.field_Private_MenuControls_0!.ButtonA,"car-select native transition runs and borrowed A restored");
        EventSystem.current=new(){currentSelectedGameObject=new()};long delivered=MenuNavigation.Delivered;MenuNavigation.Update();
        check(MenuNavigation.Delivered==delivered,"held car-select Confirm does not submit new transmission UI");
        hub.Pressed.Clear();MenuNavigation.Update();Down("Confirm");MenuNavigation.Update();
        check(MenuNavigation.Delivered==delivered+1,"released and repressed Confirm returns to ordinary transmission UI");

        foreach(string action in MenuNavigation.Actions) {
            Reset();var car=new ArcadeCarSelect();Neutral(car);Down(action);Car(car);
            check(car.Selections+car.Backs+car.Directions==1,"native custom menu consumes one "+action);
            check(!car.field_Private_MenuControls_0!.ButtonA&&!car.field_Private_MenuControls_0.ButtonB&&
                !car.field_Private_MenuControls_0.DPadLeft&&!car.field_Private_MenuControls_0.DPadRight&&
                !car.field_Private_MenuControls_0.DPadUp&&!car.field_Private_MenuControls_0.DpadDown,"temporary custom menu fields restored for "+action);
            Car(car);Car(car);check(car.Selections+car.Backs+car.Directions==1,"held "+action+" cannot repeat at native frame rate");
        }
        foreach(string failure in new[]{"focus","device","nativeHeld","panel","localCar","demo","multiplayer","otherPlayer","unready","debounce","exiting","capture","ambiguous","missingControls"}) {
            Reset();var car=new ArcadeCarSelect();Neutral(car);
            switch(failure) {
                case "focus":Runtime.Focused=false;break;case "device":hub.Devices[0].Ok=false;break;
                case "nativeHeld":car.field_Private_MenuControls_0!.LSLeft=true;break;
                case "panel":Panel.Toggle();break;case "localCar":Runtime.Local=new();break;
                case "demo":GameMaster.Demo=true;break;case "multiplayer":GameMaster.NrOfPlayers=2;break;
                case "otherPlayer":car.PlayerIndex=1;break;case "unready":car.field_Private_Boolean_0=false;break;
                case "debounce":car.field_Private_Boolean_1=true;break;case "exiting":MenuCameraScript.Exiting=true;break;
                case "capture":Panel.Toggle();wheel.BeginButton("Confirm");break;
                case "ambiguous":hub.Pressed.Add(wheel.Bindings.Buttons["Back"]);break;
                case "missingControls":car.field_Private_MenuControls_0=null;break;
            }
            hub.Pressed.Add(wheel.Bindings.Buttons["Confirm"]);Time.frameCount++;
            ArcadeWheelMenuHook.Prefix(car,out var lease);
            check(lease==null,"custom wheel field refused for "+failure);
            ArcadeWheelMenuHook.Postfix(lease);ArcadeWheelMenuHook.Finalizer(lease);
            if(wheel.Capturing)wheel.Cancel();
        }
        Reset();var delayed=new ArcadeCarSelect();Neutral(delayed);delayed.field_Private_Boolean_1=true;Down("Confirm");Car(delayed);Car(delayed);
        check(delayed.Selections==0,"press during native debounce cannot activate after debounce clears while held");
        Neutral(delayed);Down("Confirm");Car(delayed);check(delayed.Selections==1,"fresh post-debounce Confirm is accepted");

        Reset();var throwing=new ArcadeCarSelect();Neutral(throwing);throwing.ThrowOriginal=true;Down("Confirm");
        bool threw=false;try{Car(throwing);}catch(InvalidOperationException){threw=true;}
        check(threw&&throwing.Selections==1&&!throwing.field_Private_MenuControls_0!.ButtonA,"original exception and transition preserved while finalizer restores input");
        Reset();var native=new MenuControls{ButtonA=true};new CustomMenuLease(native,"Confirm").Restore();
        check(native.ButtonA,"field lease preserves pre-existing native true value");
        native=new();var pending=new CustomMenuLease(native,"Confirm");native.FailRestore=true;pending.Restore();
        var blocked=new ArcadeCarSelect();Time.frameCount++;
        check(!ArcadeWheelMenuHook.Prefix(blocked,out _)&&native.ButtonA,"unacknowledged restore blocks further original menu callbacks");
        native.FailRestore=false;check(CustomMenuLease.Recover()&&!native.ButtonA,"retry restores exact old field before native menu resumes");

        Reset();var stage=new StagePresentation();Stage(stage);Runtime.Clock.Advance(.11);Stage(stage);Down("Confirm");Stage(stage);
        check(stage.Starts==1&&!stage.MyControls!.ButtonA,"presentation Confirm uses native start callback and restores A");
        Stage(stage);check(stage.Starts==1,"held presentation Confirm does not start twice");
        Reset();var results=new StagePresentation{field_Private_Boolean_1=true};Stage(results);Runtime.Clock.Advance(.11);Stage(results);Down("Confirm");Stage(results);
        check(results.Results==0,"result/leaderboard screen has no synthetic Confirm route");
        Reset();wheel.Bindings.Buttons.Clear();foreach(var b in saved)wheel.Bindings.Buttons[b.Key]=b.Value;
    }
}
