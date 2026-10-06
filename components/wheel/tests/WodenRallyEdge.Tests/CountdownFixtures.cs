using System.Reflection;
using WodenRallyEdge.Core;

namespace UnityEngine
{
    internal static class Time { internal static float time; }
}
namespace WodenRallyEdge
{
    internal sealed partial class MainCar
    {
        internal enum CarStatus { WARMING, RACE, END, DESTROYED }
        internal CarStatus Status = CarStatus.RACE;
        internal bool Selected = true, Replay;
        internal RaceConditions? field_Private_RaceConditions_0;
    }
    internal sealed class RaceConditions
    {
        internal readonly List<MainCar> PlayerCarList = new();
        internal object GM = new();
    }
    internal static class Pause { internal static bool Paused; }
    // STD-018: the linked assist marks the race so its leaderboard upload is refused.
    internal static class LeaderboardGuard { internal static int Marks; internal static void MarkAssisted() => Marks++; }
    internal sealed class CountDown
    {
        internal bool Active = true;
        internal static bool InfiniteTime;
        internal float TimeLeft = 60, TimerControl;
        internal object? GM;
        // Emulates only the verified native decrement/expiry sequence from the
        // guarded CountDown.Update, not the mod's anchor adjustment formula.
        internal void Update()
        {
            if (!Active || InfiniteTime) return;
            TimeLeft -= UnityEngine.Time.time - TimerControl;
            TimerControl = UnityEngine.Time.time;
            if (TimeLeft < 0) Active = false;
        }
    }
    internal static class CountdownChecks
    {
        private static readonly MethodInfo Prefix = Method("Prefix"), Postfix = Method("Postfix"), Finalizer = Method("Finalizer");
        private static MethodInfo Method(string name) => typeof(CountdownTimerAssist).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        private static CountdownAdjustment? Begin(CountDown timer)
        {
            object?[] args = { timer, null }; Prefix.Invoke(null, args); return (CountdownAdjustment?)args[1];
        }
        private static void End(CountDown timer, CountdownAdjustment? adjustment)
        {
            Postfix.Invoke(null, new object?[] { timer, adjustment });
            Finalizer.Invoke(null, new object?[] { timer, null, adjustment });
        }
        private static CountDown Ready()
        {
            var race = new RaceConditions();
            Runtime.Local = new MainCar { field_Private_RaceConditions_0 = race }; race.PlayerCarList.Add(Runtime.Local);
            Runtime.Settings = new() { CountdownAssistEnabled = true, CountdownSpeed = 75 };
            Pause.Paused = false; CountDown.InfiniteTime = false; UnityEngine.Time.time = 1;
            return new() { GM = race.GM };
        }
        internal static void Run(Action<bool, string> check)
        {
            var timer = Ready();
            for (int second = 1; second <= 80; second++)
            {
                UnityEngine.Time.time = second; var state = Begin(timer); timer.Update(); End(timer, state);
            }
            check(timer.TimeLeft == 0 && timer.Active, "75% budget reaches zero after 80 driving seconds and native strict-negative expiry remains");
            timer.TimeLeft += 30; UnityEngine.Time.time = 81; var next = Begin(timer); timer.Update(); End(timer, next);
            check(timer.TimeLeft == 29.25f && timer.TimerControl == 81, "checkpoint bonus kept whole, next tick scaled, consumed native anchor retained");
            timer = Ready(); timer.TimeLeft = .8f;
            next = Begin(timer); timer.Update(); End(timer, next);
            check(timer.Active && Math.Abs(timer.TimeLeft - .05f) < .0001f, "scaled step reaches native expiry before it can incorrectly time out");
            UnityEngine.Time.time = 2; next = Begin(timer); timer.Update(); End(timer, next);
            check(!timer.Active && timer.TimeLeft < 0, "assisted countdown still expires normally");
            check(Begin(timer) == null, "expired timer is never revived");
            timer = Ready(); next = Begin(timer); End(timer, next);
            check(timer.TimerControl == 0, "native early return restores unconsumed anchor, including double cleanup");
            next = Begin(timer); var failure = new InvalidOperationException("native fixture failure");
            var returned = Finalizer.Invoke(null, new object?[] { timer, failure, next });
            check(ReferenceEquals(failure, returned) && timer.TimerControl == 0, "exception cleanup restores anchor and preserves exception");
            foreach (string blocked in new[] { "off", "normal", "no player", "not selected", "warmup", "finished", "replay", "missing race", "multiplayer", "wrong timer", "pause", "inactive", "infinite" })
            {
                timer = Ready();
                switch (blocked)
                {
                    case "off": Runtime.Settings.CountdownAssistEnabled = false; break;
                    case "normal": Runtime.Settings.CountdownSpeed = 100; break;
                    case "no player": Runtime.Local = null; break;
                    case "not selected": Runtime.Local!.Selected = false; break;
                    case "warmup": Runtime.Local!.Status = MainCar.CarStatus.WARMING; break;
                    case "finished": Runtime.Local!.Status = MainCar.CarStatus.END; break;
                    case "replay": Runtime.Local!.Replay = true; break;
                    case "missing race": Runtime.Local!.field_Private_RaceConditions_0 = null; break;
                    case "multiplayer": Runtime.Local!.field_Private_RaceConditions_0!.PlayerCarList.Add(new()); break;
                    case "wrong timer": timer.GM = new(); break;
                    case "pause": Pause.Paused = true; break;
                    case "inactive": timer.Active = false; break;
                    case "infinite": CountDown.InfiniteTime = true; break;
                }
                check(Begin(timer) == null && timer.TimerControl == 0 && timer.TimeLeft == 60, "timer left untouched: " + blocked);
            }
            timer = Ready(); UnityEngine.Time.time = 0;
            check(Begin(timer) == null, "frozen Unity clock does not consume time");
            UnityEngine.Time.time = 1; next = Begin(timer); timer.Update(); End(timer, next);
            Runtime.Settings.CountdownAssistEnabled = false; UnityEngine.Time.time = 2;
            next = Begin(timer); timer.Update(); End(timer, next);
            check(timer.TimeLeft == 58.25f, "turning assist off resumes full rate without resetting remaining budget");
            Runtime.Local = null;
        }
    }
}
