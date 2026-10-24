using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WodenRallyEdge;

// Source-linked synthetic checks only. Run separately; never poll owner devices.
internal static class CloseReleaseChecks
{
    internal static void Run(string directory, Action<bool,string> check)
    {
        Runtime.Settings = new(new ConfigFile(Path.Combine(directory,"close-release.cfg"),false));
        Runtime.Settings.FfbEnabled = false;
        Runtime.Settings.UiView = "Advanced";
        Runtime.Wheel = new(Path.Combine(directory,"close-release-bindings.json"));
        Runtime.Local = null; Runtime.Focused = true;
        Screen.width = 1280; Screen.height = 720;
        var events = new EventSystem(); EventSystem.current = events;
        var system = new GamePadSystem();
        var pad = new GamePadSystem.Game_Pad { PadActions = new[] { new GamePadSystem.Actions("fixture action",0,false) } };
        system.Game_Pads.Add(pad);
        void Stock(bool held)
        {
            pad.AnyKey = held;
            pad.PadActions![0] = pad.PadActions[0] with { value = held ? .73f : 0, Pressed = false };
            MenuOwnership.BeforeStock(system); MenuOwnership.AfterRead(pad); MenuOwnership.AfterStock(system);
        }
        void Draw(Event? input = null)
        { GUI.Commands.Clear(); Event.current = input ?? new(); Panel.Draw(); }
        void Pending()
        {
            Panel.Close(false); Keyboard.current = new(); Input.Held = false; Input.Keys.Clear(); Input.Down.Clear();
            Runtime.Devices!.Pressed.Clear(); Time.frameCount++;
            Panel.Toggle(); Panel.Close(true); Stock(true); MenuOwnership.Tick();
            check(Panel.Open && MenuOwnership.Closing && !events.enabled,"held input retains panel and stock ownership");
        }
        void PendingAfter(string route)
        {
            check(Panel.Open && MenuOwnership.Closing && !events.enabled,route+" leaves the guarded close pending");
            check(!Runtime.Settings.FfbEnabled,route+" does not enable forces");
        }
        void Canceled(string route)
        {
            check(Panel.Open && !MenuOwnership.Closing && !events.enabled,route+" cancels only pending close, without releasing stock ownership");
            check(!Runtime.Settings.FfbEnabled,route+" does not enable forces");
        }

        Pending();
        check(Panel.Message.Contains("PadActions[0]=0.73") && MenuOwnership.Status.Contains("PadActions[0]=0.73"),
            "pending close and periodic status identify the actual sampled field/value rather than guessing pedal identity");
        Draw();
        check(GUI.Commands.Any(c=>c.Text=="Closing…") && GUI.Commands.Any(c=>c.Text=="Keep settings open"),
            "pending close shows progress and a distinct cancellation action");
        check(!GUI.Commands.Any(c=>c.Text.Contains("Finish or cancel the current edit")),"pending close has its own accurate header");
        Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.Return}); PendingAfter("default Enter focus");

        Pending(); Keyboard.current![Key.Escape].wasPressedThisFrame=true; Panel.Update(); PendingAfter("InputSystem Escape");
        Pending(); Keyboard.current![Key.F6].wasPressedThisFrame=true; Panel.Update(); PendingAfter("InputSystem F6");
        Pending(); Keyboard.current=null; Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.Escape}); PendingAfter("IMGUI Escape without InputSystem keyboard");
        Pending(); Keyboard.current=null; Draw(new(){type=EventType.KeyDown,keyCode=KeyCode.F6}); PendingAfter("IMGUI F6 without InputSystem keyboard");
        Pending(); Panel.MenuAction("Back"); PendingAfter("panel Back action");
        Pending();
        var device=Guid.NewGuid(); Runtime.Wheel!.Bindings.Buttons["Back"]=new(device,35);
        Runtime.Devices!.Pressed.Add(new(device,35)); Panel.Update(); PendingAfter("bound Back while normal menu dispatch is suppressed");
        Runtime.Devices.Pressed.Clear();

        Pending(); Draw();
        var closing=GUI.Commands.First(c=>c.Text=="Closing…");
        Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(closing.Rect.x+10,closing.Rect.y+10)});
        PendingAfter("disabled header action");
        Draw();var cancel=GUI.Commands.First(c=>c.Text=="Keep settings open");
        Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(cancel.Rect.x+10,cancel.Rect.y+10)});
        Canceled("explicit Keep settings open action");
        // A view click itself must not create a close request or leave navigation locked.
        Draw(); var simple=GUI.Commands.First(c=>c.Text=="Simple");
        Draw(new(){type=EventType.MouseDown,button=0,mousePosition=new(simple.Rect.x+10,simple.Rect.y+10)});
        check(Runtime.Settings.UiView=="Simple" && !MenuOwnership.Closing,"Advanced to Simple does not request closing");

        Pending(); Runtime.Clock.Advance(.2); Stock(true); MenuOwnership.Tick();
        check(Panel.Open && MenuOwnership.Closing && !events.enabled,"held input never bypasses close protection");
        Stock(false); MenuOwnership.Tick(); Runtime.Clock.Advance(.05); Stock(false); MenuOwnership.Tick();
        check(Panel.Open && !events.enabled,"neutral input shorter than 100 ms does not hand off");
        Runtime.Clock.Advance(.06); Stock(false); MenuOwnership.Tick();
        check(!Panel.Open && events.enabled,"fresh neutral interval completes close and restores prior stock dispatch");
        check(!Runtime.Settings.FfbEnabled,"normal close does not change the saved force preference");
        check(Panel.Message==Panel.IdleMessage,"completed close clears the pending-close message");
        Time.frameCount++; Panel.Toggle(); Draw();
        check(Panel.Open && !MenuOwnership.Closing && !GUI.Commands.Any(c=>c.Text.Contains("Waiting to close")),
            "reopened panel shows no stale Waiting to close text");
        Panel.Close(false);
    }
}
