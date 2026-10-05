namespace UnityEngine
{
    public static class Application
    {
        internal static int QuitCalls;
        public static void Quit() => QuitCalls++;
    }
}

namespace WodenRallyEdge
{
    internal static class StageCaptureContext { internal static void ObserveScene() { } }

    internal static class StageLifecycleChecks
    {
        internal static void Run(string root, Action<bool, string> check)
        {
            string directory = Path.Combine(root, "stage-lifecycle");
            Directory.CreateDirectory(directory);
            string valid = "id=" + Guid.NewGuid().ToString("N") + "\nexpiresUtc=" + DateTimeOffset.UtcNow.AddMinutes(1).ToString("O") + "\naction=record\nautoExit=true\n";
            void Setup(string request)
            {
                File.WriteAllText(Path.Combine(directory, "request.txt"), request);
                UnityEngine.Application.QuitCalls = 0;
                StageRunLifecycle.Initialize(directory);
            }
            Setup(valid);
            StageRunLifecycle.Tick(false, "idle", null, 0);
            check(!StageRunLifecycle.Closing, "does not exit before the valid request is acknowledged");
            StageRunLifecycle.Tick(true, "recording", null, 10);
            StageRunLifecycle.Tick(false, "recorded", null, 20);
            check(StageRunLifecycle.Closing && UnityEngine.Application.QuitCalls == 0, "recording gets a visible post-save exit delay");
            StageRunLifecycle.Tick(false, "recorded", null, 27.99);
            check(UnityEngine.Application.QuitCalls == 0, "keeps eight-second exit delay");
            StageRunLifecycle.Tick(false, "recorded", null, 28);
            StageRunLifecycle.Tick(false, "recorded", null, 40);
            check(UnityEngine.Application.QuitCalls == 1, "normal exit requested exactly once");
            foreach (string bad in new[] {
                valid.Replace("autoExit=true\n", ""), valid.Replace("autoExit=true", "autoExit=false"),
                valid.Replace("action=record", "action=stop"), valid.Replace("id=", "id=not-a-guid"),
                valid + "autoExit=true\n", valid.Replace(DateTimeOffset.UtcNow.Year.ToString(), "2000") })
            {
                Setup(bad);
                StageRunLifecycle.Tick(true, "recording", null, 1);
                StageRunLifecycle.Tick(false, "recorded", null, 2);
                StageRunLifecycle.Tick(false, "recorded", null, 50);
                check(UnityEngine.Application.QuitCalls == 0 && !StageRunLifecycle.Closing, "ordinary/malformed/expired requests cannot quit the owner game");
            }
            Setup(valid.Replace("action=record", "action=replay"));
            StageRunLifecycle.Tick(false, "idle", "bad recording seal", 1);
            StageRunLifecycle.Tick(false, "idle", "bad recording seal", 9);
            check(UnityEngine.Application.QuitCalls == 1, "rejected supervised request closes normally too");
            Setup(valid);
            StageRunLifecycle.Tick(true, "armed", null, 1);
            StageRunLifecycle.Tick(false, "cancelled before stage start", null, 2);
            StageRunLifecycle.Tick(false, "cancelled before stage start", null, 10);
            check(UnityEngine.Application.QuitCalls == 1, "Stop while armed still releases the supervised launch");
        }
    }
}
