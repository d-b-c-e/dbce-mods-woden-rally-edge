using UnityEngine;
using UnityEngine.EventSystems;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Use Unity's existing selected UI and event handlers. No synthetic OS input,
// virtual gamepad, guessed game-action indices, or driving-state mutation.
internal static class MenuNavigation
{
    internal static readonly string[] Actions = { "Confirm", "Back", "Menu up", "Menu down", "Menu left", "Menu right" };
    private static readonly Dictionary<string, ShortcutRepeat> Repeats = Actions.ToDictionary(a => a, _ => new ShortcutRepeat());
    internal static string Status { get; private set; } = "Bind menu buttons below; keyboard and controller still work.";
    internal static long Delivered { get; private set; }
    internal static void Update()
    {
        if (Runtime.Wheel == null) return;
        bool allowed = Runtime.Focused && (Runtime.Wheel.CaptureButton == null || Runtime.Wheel.SavePending);
        bool menu = Panel.Open || Runtime.Local == null || Pause.Paused || Runtime.Local.Status is MainCar.CarStatus.END or MainCar.CarStatus.DESTROYED;
        foreach (string action in Actions)
        {
            bool held = Runtime.Wheel.Button(action, false);
            if (!Repeats[action].Tick(held, allowed && menu, Runtime.Clock.Elapsed.TotalSeconds, action.StartsWith("Menu "))) continue;
            if (Panel.Open) { Panel.MenuAction(action); continue; }
            try
            {
                var events = EventSystem.current;
                if (events == null || !events.enabled || !events.isFocused)
                { Status = "This screen has no active Unity menu. Use its keyboard/controller controls."; continue; }
                var selected = events.currentSelectedGameObject;
                if (selected == null && events.firstSelectedGameObject != null)
                { events.SetSelectedGameObject(events.firstSelectedGameObject); selected = events.currentSelectedGameObject; }
                if (selected == null || !selected.activeInHierarchy)
                { Status = "Select a menu item with mouse/keyboard, then use the bound menu buttons."; continue; }
                GameObject handled;
                if (action == "Confirm") handled = ExecuteEvents.ExecuteHierarchy<ISubmitHandler>(selected, new BaseEventData(events), ExecuteEvents.submitHandler);
                else if (action == "Back") handled = ExecuteEvents.ExecuteHierarchy<ICancelHandler>(selected, new BaseEventData(events), ExecuteEvents.cancelHandler);
                else
                {
                    var direction = action switch { "Menu up" => MoveDirection.Up, "Menu down" => MoveDirection.Down, "Menu left" => MoveDirection.Left, _ => MoveDirection.Right };
                    var vector = direction switch { MoveDirection.Up => new Vector2(0,1), MoveDirection.Down => new Vector2(0,-1), MoveDirection.Left => new Vector2(-1,0), _ => new Vector2(1,0) };
                    handled = ExecuteEvents.ExecuteHierarchy<IMoveHandler>(selected, new AxisEventData(events) { moveDir = direction, moveVector = vector }, ExecuteEvents.moveHandler);
                }
                if (handled != null) { Delivered++; Status = "Menu input delivered: " + action; }
                else Status = "This menu item has no " + action + " handler. Use the game's keyboard/controller for this screen.";
            }
            catch (Exception ex) { Status = "Menu input unavailable: " + ex.Message; }
        }
    }
}
