using WodenRallyEdge.Core;
namespace WodenRallyEdge;

internal sealed class BlockingForceDevice : IForceDevice
{
    internal readonly ManualResetEventSlim Writing = new(), Continue = new();
    internal readonly List<string> Calls = new();
    internal bool FailWrite, FailClose;
    internal int InCall, Overlaps;
    public bool CanOpen => true;
    public string? Error => "fixture failure";
    void Enter(string name) { if (Interlocked.Increment(ref InCall) != 1) Interlocked.Increment(ref Overlaps); Calls.Add(name); }
    void Leave() => Interlocked.Decrement(ref InCall);
    public bool Open(Guid guid) { Enter("open"); Leave(); return true; }
    public bool Write(float force)
    {
        Enter("write-start"); Writing.Set();
        if (!Continue.Wait(5000)) throw new TimeoutException("fixture writer not released");
        Calls.Add("write-end"); Leave(); return !FailWrite;
    }
    public void ZeroAndStop() { Enter("zero"); Leave(); }
    public void Panic() { Enter("panic"); Leave(); }
    public void Close() { Enter("close"); Leave(); if (FailClose) throw new InvalidOperationException("fixture close failed"); }
}
internal static class LifecycleChecks
{
    internal static void Run(Action<bool,string> check, Func<double,TelemetrySample> contact)
    {
        var gate = new RuntimeActivityGate();gate.BeginRuntime();
        var entered = new ManualResetEventSlim(); var resume = new ManualResetEventSlim();
        var callback = Task.Run(()=>{using var lease=gate.TryEnter();entered.Set();resume.Wait(5000);});
        check(entered.Wait(2000), "actual runtime gate has an entered callback");
        var drain = Task.Run(()=>gate.StopAndDrain());
        check(SpinWait.SpinUntil(()=>gate.Stopped,2000) && !drain.Wait(30), "runtime drain blocks while callback owns native-use window");
        resume.Set();check(Task.WaitAll(new[]{callback,drain},5000), "runtime callback drained");
        check(gate.TryEnter()==null, "late callback rejected after drain");
        gate.BeginRuntime();using(var lease=gate.TryEnter()){check(lease!=null,"explicit runtime restart opens callback gate");}
        gate.StopAndDrain();gate.StopAndDrain();check(gate.Stopped,"runtime stop idempotent");
        gate.BeginRuntime();bool cleaned=false;
        using(var outer=gate.TryEnter()){
            using(var nested=gate.TryEnter()){gate.StopAndDrain(()=>cleaned=true);check(!cleaned,"nested quit defers cleanup");}
            check(!cleaned,"cleanup waits for outer callback exit");
        }
        check(cleaned && gate.Stopped,"deferred cleanup runs after outer callback releases gate");
        foreach (bool failWrite in new[] { false, true })
        {
            ForceControllerChecks.Create();
            var device = new BlockingForceDevice { FailWrite=failWrite };
            var controller = new ForceController(device); controller.Prepare();
            var writer = Task.Run(()=>controller.Tick(contact(1)));
            check(device.Writing.Wait(2000), "actual controller producer entered fake write");
            var stop = Task.Run(controller.Shutdown);
            check(SpinWait.SpinUntil(()=>controller.StopRequested,2000), "stop published before waiting for producer");
            check(!stop.Wait(30) && !device.Calls.Contains("close"), "shutdown drains entered producer before close");
            var late = Task.Run(()=>{controller.Prepare();controller.Tick(contact(1.02));controller.SetEnabled(true);});
            device.Continue.Set();
            check(Task.WaitAll(new[] {writer,stop,late},5000), "producer and cleanup complete");
            check(device.Overlaps==0 && device.Calls.Count(x=>x=="open")==1 && device.Calls.Count(x=>x=="write-start")==1,
                "late producers neither overlap native calls nor reopen stopped runtime");
            check(device.Calls.IndexOf("write-end") < device.Calls.LastIndexOf("zero") &&
                device.Calls.LastIndexOf("zero") < device.Calls.LastIndexOf("close"), "drain then zero then close ordering");
            int calls=device.Calls.Count; controller.Shutdown(); controller.Prepare();
            check(device.Calls.Count==calls && controller.Sent==0, "repeated stop and prepare are terminal and idempotent");
            device.FailWrite=false; controller.BeginRuntime(); controller.Prepare(); controller.Tick(contact(2));
            check(device.Calls.Count(x=>x=="open")==2 && !controller.StopRequested, "explicit runtime start permits controlled restart");
            device.FailClose=true; controller.Shutdown();
            bool refused=false; try {controller.BeginRuntime();} catch(InvalidOperationException){refused=true;}
            check(refused && controller.StopRequested && controller.Status.Contains("Shutdown failed"), "failed cleanup prevents restart");
            device.FailClose=false; controller.Shutdown();controller.BeginRuntime();
            check(!controller.Connected && !controller.StopRequested, "explicit cleanup retry resolves failed close");
        }
    }
}
