using Dbce.Wheel.Ffb;
using Dbce.Wheel.Input;
using WodenRallyEdge;
using WodenRallyEdge.Core;

int checks=0; void Check(bool ok,string why) { checks++; if(!ok) throw new Exception(why); }
Check(string.IsNullOrEmpty(WheelFfbNative.LoadedFrom),"native library must not be loaded");
var guid=Guid.NewGuid(); int hat=0, angle=-1, reads=0, failure=0;
var reader=new PovSnapshotReader((slot,axes,axisCount,buttons,buttonCount,pov,povCount)=>
{
    reads++; Check(slot==42 && axisCount==8 && buttonCount==128 && povCount==4,"one coherent snapshot");
    if(failure==2) throw new IOException("synthetic read failure");
    Array.Fill(axes,32768); axes[2]=1234; Array.Clear(buttons); buttons[31]=1;
    Array.Fill(pov,-1); pov[hat]=angle; return failure==1?0:1;
});
var hub=new DeviceHub(reader);
hub.Devices.Add(new(){Slot=42,Info=new(){InstanceGuid=guid,Name="Synthetic wheel",Axes=8,Buttons=128}});
for(hat=0;hat<4;hat++) for(int direction=0;direction<8;direction++)
{
    int binding=128+hat*8+direction;
    var profile=new ButtonBinding(guid,binding){HatNeighbours=true};
    var legacy=new ButtonBinding(guid,binding);
    foreach(int offset in new[]{-4501,-4500,-2250,-1,0,1,2250,4500,4501,9000,18000})
    {
        angle=-1; hub.Poll();
        angle=(direction*4500+offset+36000)%36000; int before=reads; hub.Poll();
        Check(reads==before+1,"no second read");
        bool expected=Math.Abs(offset)<=4500;
        Check(hub.Button(profile,false)==expected,"contract held at "+offset);
        Check(hub.Button(profile,true,"camera")==expected,"fresh profile edge");
        Check(!hub.Button(profile,true,"camera"),"same action consumes edge once");
        Check(hub.Button(profile,true,"other")==expected,"independent assigned action");
        Check(hub.Button(legacy,false)==(((angle+2250)/4500)%8==direction),"legacy nearest eight-way unchanged");
        Check(!hub.Button(profile with{DeviceGuid=Guid.NewGuid()},false),"wrong device refused");
        hub.Poll(); Check(!hub.Button(profile,true,"camera"),"held input does not retrigger");
    }
}
hat=0; angle=-1; hub.Poll(); angle=0; hub.Poll();
var up=new ButtonBinding(guid,128){HatNeighbours=true};
Check(hub.Button(up,true,"nav"),"north edge"); angle=4500; hub.Poll();
Check(hub.Button(up,false) && !hub.Button(up,true,"nav"),"cardinal-to-diagonal remains held without retrigger");
Check(hub.Button(new(guid,31),false),"physical boolean button unchanged");
var throttle=new AxisBinding(guid,2,new(1234,65535));
Check(hub.TryAxis(throttle,out float t) && t==0,"same coherent calibrated pedal rest");
foreach(int f in new[]{1,2})
{
    failure=f; hub.Poll();
    Check(!hub.Button(up,false) && !hub.Button(up,true,"nav") && !hub.Button(new(guid,31),false),"failed read releases all routes");
    Check(!hub.TryAxis(throttle,out t),"failed read is unavailable, never a pedal sample");
    failure=0; angle=-1; hub.Poll(); angle=0; hub.Poll(); Check(hub.Button(up,true,"nav"),"fresh press after failure");
}
hub.ClearPresses(); Check(!hub.Button(up,true,"nav"),"ownership clear discards pending profile press");
Check(!new ButtonBinding(guid,31){HatNeighbours=true}.Valid,"profile flag on physical button refused");
Check(!new ButtonBinding(guid,160){HatNeighbours=true}.Valid,"out-of-range hat refused");
Check(string.IsNullOrEmpty(WheelFfbNative.LoadedFrom),"test never loads native or opens any hardware");
Console.WriteLine($"PASS {checks} production DeviceHub input checks; fake coherent reader, no native library, game or device.");
