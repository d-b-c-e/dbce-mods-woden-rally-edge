using UnityEngine;
using UnityEngine.InputSystem;
using WodenRallyEdge;
using WodenRallyEdge.Core;

internal static class StartupMenuChecks
{
    internal static void Run(Action<bool,string> check, Action stockFrame, Guid device)
    {
        var hub = Runtime.Devices!;
        void Reset()
        {
            Panel.Close(false); Runtime.Focused = true;
            Keyboard.current = new(); Input.FailRead = false; Input.Held = false; Input.Keys.Clear(); Input.Down.Clear();
            hub.Pressed.Clear(); hub.OnPoll = null; Time.frameCount++;
            Event.current = new() { type = EventType.KeyUp, keyCode = KeyCode.F6 }; Panel.Draw();
        }
        void Daily(DailyMessage message) { if (DailyMessageInputHook.Prefix()) message.Update(); }
        void Title(TitleScreenScript title) { if (TitleScreenInputHook.Prefix()) title.FixedUpdate(); }
        void Release()
        {
            Input.Keys.Clear(); Input.Down.Clear(); Keyboard.current!.Clear(); hub.Pressed.Clear();
            stockFrame(); MenuOwnership.Tick(); Runtime.Clock.Advance(.11); stockFrame(); MenuOwnership.Tick();
        }

        Reset();
        var unready = new DailyMessage { Ready = false }; Input.Keys.Add(KeyCode.Space); Daily(unready);
        check(unready.Loads == 0 && !Panel.Open, "unready native message retains its input eligibility");
        var ready = new DailyMessage(); Daily(ready);
        check(ready.Loads == 1 && !Panel.Open, "ordinary legacy anyKey still advances ready message with panel closed");
        Reset();
        var timed = new TitleScreenScript();
        for (int i = 0; i < 4; i++) { Time.frameCount++; Title(timed); }
        check(timed.DemoLoads == 1 && timed.StartLoads == 0, "native timed attract runs normally with settings closed");
        var escaped = new TitleScreenScript(); Input.Keys.Add(KeyCode.Return); Input.Keys.Add(KeyCode.Escape); Title(escaped);
        check(escaped.StartLoads == 0, "native Return plus Escape exclusion is preserved");

        foreach (bool consumerFirst in new[] { true, false })
        {
            Reset();
            var message = new DailyMessage { Confirm = true }; var title = new TitleScreenScript { Start = true };
            Input.Keys.Add(KeyCode.F6); Input.Down.Add(KeyCode.F6);
            int polls = hub.Polls;
            if (!consumerFirst) { InputPolling.OncePerFrame(); Panel.Update(); }
            Daily(message); Title(title);
            if (consumerFirst) { InputPolling.OncePerFrame(); Panel.Update(); }
            check(Panel.Open && !MenuOwnership.Closing && message.Loads == 0 && title.StartLoads == 0,
                "legacy F6 opening blocks direct/cached startup shortcuts with " + (consumerFirst ? "consumer first" : "Lifecycle first"));
            check(hub.Polls == polls + 1, "startup and Lifecycle share one real poll per frame");
            Input.Down.Clear(); Time.frameCount++;
            Event.current = new() { type = EventType.KeyDown, keyCode = KeyCode.F6 }; Panel.Draw();
            check(Panel.Open && !MenuOwnership.Closing, "delayed IMGUI delivery of early F6 cannot close the panel");
            Input.Keys.Clear(); Input.Keys.Add(KeyCode.Return); Input.Keys.Add(KeyCode.Space);
            for (int i = 0; i < 6; i++) { Time.frameCount++; Daily(message); Title(title); }
            check(message.Loads == 0 && title.StartLoads == 0 && title.DemoLoads == 0 && title.FrameCount == 0,
                "open panel blocks direct Enter/anyKey and cached Confirm, without advancing startup timers");
            Panel.Close(true); stockFrame(); MenuOwnership.Tick(); Runtime.Clock.Advance(.2); stockFrame(); MenuOwnership.Tick();
            check(Panel.Open && MenuOwnership.Closing, "legacy held Enter/Space delays close even with an available neutral InputSystem keyboard");
            Release(); check(!Panel.Open, "fresh release completes close after direct legacy keys");
            Input.Keys.Add(KeyCode.Return); Time.frameCount++; Daily(message); Title(title);
            check(message.Loads == 1 && title.StartLoads == 1, "fresh stock input resumes after ownership release");
        }

        Reset();
        Runtime.Wheel!.Bindings.Buttons["Settings panel"] = new(device, 22);
        hub.OnPoll = () => hub.Pressed.Add(new(device, 22));
        var boundMessage = new DailyMessage { Start = true }; int beforeBound = hub.Polls;
        Daily(boundMessage); InputPolling.OncePerFrame(); Panel.Update();
        check(Panel.Open && !MenuOwnership.Closing && boundMessage.Loads == 0 && hub.Polls == beforeBound + 1,
            "bound Settings arriving in the early device poll owns input before the native shortcut");
        hub.OnPoll = null; hub.Pressed.Clear();
        Reset();
        Keyboard.current![Key.F6].wasPressedThisFrame = true;
        var modern = new DailyMessage(); Daily(modern); Panel.Update();
        check(Panel.Open && !MenuOwnership.Closing && modern.Loads == 0, "InputSystem early opening and normal update deduplicate");
        Reset();
        Runtime.Focused = false; Input.Down.Add(KeyCode.F6); Title(new());
        check(!Panel.Open, "unfocused startup callback does not open settings");
        Reset();
        Input.FailRead = true;
        check(!DailyMessageInputHook.Prefix() && !Panel.Open, "unknown opening input holds direct consumer instead of treating failure as no key");
        Input.FailRead = false;
        check(DailyMessageInputHook.Prefix(), "healthy opening read restores ordinary native dispatch");
        Reset();
        hub.OnPoll = () => throw new InvalidOperationException("fixture early poll failure");
        int failedPolls = hub.Polls;
        check(!DailyMessageInputHook.Prefix() && !TitleScreenInputHook.Prefix() && hub.Polls == failedPolls + 1,
            "failed shared poll remains unknown for every consumer in that frame without repeated reads");
        hub.OnPoll = null; Time.frameCount++;
        check(TitleScreenInputHook.Prefix(), "fresh healthy frame recovers from failed early poll");
        check(Runtime.Log.Infos.Any(s => s.Contains("Startup input guard active: DailyMessage.Update")) &&
            Runtime.Log.Infos.Any(s => s.Contains("Startup input guard active: TitleScreenScript.FixedUpdate")),
            "actual consumer guards produce bounded runtime diagnostics");
        Reset();
    }
}
