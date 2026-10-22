using System.Text.Json;
using Woden.ControlsTesting;

internal static class SessionGateChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool value){checks++;if(!value)throw new Exception("Session check failed "+checks);}
        void Refuses(Action action){checks++;try{action();}catch(InvalidDataException){return;}throw new Exception("Unsafe session admission "+checks);}
        var birth=DateTimeOffset.Parse("2026-10-17T20:00:00Z");string nonce=new('a',32);
        var client=new SessionClient(42,birth.UtcTicks,99,"UnityWndClass",Path.GetFullPath("game.exe"));
        var arm=new RawRequest(1,nonce,"arm",null,Client:client);
        RawRequest Decode(RawRequest value,int previous=0)=>Protocol.Command(JsonSerializer.SerializeToUtf8Bytes(value,Protocol.Json),nonce,previous,birth.AddSeconds(1),birth,menuTrial:true);
        Check(Decode(arm)==arm);
        foreach(var bad in new[]{arm with{Nonce=new('b',32)},arm with{Sequence=2},arm with{Client=null},
            arm with{Client=client with{StartedUtcTicks=birth.UtcTicks+1}},arm with{Client=client with{Window=0}},
            arm with{Client=client with{Class="ConsoleWindowClass"}},arm with{Client=client with{ProcessId=0}},
            arm with{Raw="inject raw axis 0"},arm with{Arguments="anything"},
            arm with{Operation="status"}})Refuses(()=>Decode(bad));
        var gate=new SessionGate();
        Check(!gate.Armed && !gate.AdmitRow(true,true));
        Refuses(()=>gate.Arm(client,1,false,true));Refuses(()=>gate.Arm(client,1,true,false));
        Check(!gate.Armed);
        gate.Arm(client,1,true,true);Check(gate.Armed && gate.ArmSequence==1 && gate.AdmitRow(true,true));
        Refuses(()=>gate.Arm(client,2,true,true));
        Check(!gate.AdmitRow(false,true) && gate.Revoked && !gate.Armed);
        Refuses(()=>gate.Arm(client,2,true,true));Check(!gate.AdmitRow(true,true));
        gate=new SessionGate();gate.Revoke(); // abort wins over a pending command on disk
        Refuses(()=>gate.Arm(client,1,true,true));Check(!gate.AdmitRow(true,true));
        gate=new SessionGate();gate.Arm(client,1,true,true);
        Check(!gate.AdmitRow(true,false) && gate.Revoked);
        var stop=new RawRequest(2,nonce,"stop",null);
        Check(Decode(stop,1)==stop);Refuses(()=>Decode(stop));
        Console.WriteLine($"PASS: {checks} NEW session gate/protocol checks; no engine, native load, device or runtime.");
    }
}
