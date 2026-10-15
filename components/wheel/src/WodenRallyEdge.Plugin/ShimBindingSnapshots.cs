using System.Text.Json;
using Dbce.Wheel.Input;
using Dbce.Wheel.Shim;

namespace WodenRallyEdge;

// Transport and Woden admission policy only. The shared feed owns row conversion,
// explicit removals and frame deduplication; the ordinary handle owns every read.
internal static class ShimBindingSnapshots
{
    internal static void AfterAcceptedTick(IntPtr handle,ulong sequence,double tickStarted)
    {
        var host=SharedBindingHost.Current;
        ObserveFrame(host,sequence,()=>
        {
            if(!ShimNative.SnapshotAvailable)return (ShimSnapshotAbi.Refused,(ShimSnapshot?)null);
            int result=ShimNative.Snapshot(handle,sequence,out var snapshot);
            return (result,snapshot);
        },()=>ShimNative.DeviceReports(handle),tickStarted,()=>Runtime.Clock.Elapsed.TotalSeconds);
    }

    // Also used by consumer fixtures: no native loader, second reader or tick.
    internal static void ObserveFrame(SharedBindingHost host,ulong sequence,
        Func<(int Result,ShimSnapshot? Snapshot)> take,Func<string> reports,double tickStarted,Func<double> clock)
    {
        host.NoteCaptureFrame(); // Before Take, eligibility refresh or any refused/error path.
        if(!host.WantsSnapshot)return;
        try
        {
            var result=take();
            if(result.Result!=ShimSnapshotAbi.Ok||result.Snapshot==null)
            {host.ReaderUnavailable($"Shared capture snapshot was refused ({result.Result}); active input is unchanged.");return;}
            if(result.Snapshot.FrameSeq!=sequence)throw new InvalidDataException("Snapshot does not match the accepted input frame.");
            string identities=reports(); // Existing Core cache getter, no provider read.
            Admit(host,result.Snapshot,identities,tickStarted,clock());
        }
        catch(Exception ex) when(ex is InvalidDataException or JsonException or ArgumentException or OverflowException or KeyNotFoundException or FormatException or InvalidOperationException)
        {host.ReaderUnavailable("Shared capture snapshot unavailable: "+ex.Message);}
    }

    // Injected local times make the conservative source-age check executable in
    // fixtures. Age at Core's boundary plus the ENTIRE tick-to-copy interval is
    // an upper bound; no absolute-clock conversion or refreshed cached sample.
    internal static void Admit(SharedBindingHost host,ShimSnapshot snapshot,string reports,double tickStarted,double copiedAt)
    {
        if(!host.WantsSnapshot)return;
        double delivery=copiedAt-tickStarted;
        if(!double.IsFinite(delivery)||delivery<0||snapshot.FreshUs<=0)
        {host.ReaderUnavailable("Capture clock interval was invalid; prior bindings were kept.");return;}
        using var document=JsonDocument.Parse(reports,new JsonDocumentOptions{MaxDepth=8});
        if(document.RootElement.ValueKind!=JsonValueKind.Array||document.RootElement.GetArrayLength()>64)
            throw new InvalidDataException("Device report table is not bounded.");
        var names=new Dictionary<(uint Index,uint Epoch,Guid Instance,Guid Product),(string Name,string IdentityKey)>();
        foreach(var report in document.RootElement.EnumerateArray())
        {
            if(!Guid.TryParse(report.GetProperty("instance").GetString(),out var instance)||
                !Guid.TryParse(report.GetProperty("product").GetString(),out var product))
                throw new InvalidDataException("Device report identity is invalid.");
            uint index=report.GetProperty("index").GetUInt32(),epoch=report.GetProperty("epoch").GetUInt32();
            string name=report.GetProperty("name").GetString()??instance.ToString("D");
            string path=report.GetProperty("interfacePath").GetString()??"";
            if(!names.TryAdd((index,epoch,instance,product),(name,path)))throw new InvalidDataException("Repeated device report identity.");
        }
        var items=host.SnapshotFeed.Read(snapshot,row=>
        {
            if(!names.TryGetValue((row.Index,row.Epoch,row.Instance,row.Product),out var id)||row.HasPath&&id.IdentityKey.Length==0)return null;
            return (id.Name,id.IdentityKey.Length!=0?id.IdentityKey:"row:"+row.Index);
        });
        if(items==null)return;
        foreach(var item in items)
        {
            if(item.IsRemoval){host.Remove(item.Instance,item.Reason!);continue;}
            var row=snapshot.Devices.Single(d=>d.Instance==item.Instance);
            var read=snapshot.Samples.Single(s=>s.Index==row.Index);
            double age=((double)snapshot.BoundaryUs-read.ReadStartUs)/1_000_000.0+delivery;
            double fresh=Math.Min(snapshot.FreshUs,100_000)/1_000_000.0;
            if(read.ReadStartUs<0||read.ReadEndUs<read.ReadStartUs||snapshot.BoundaryUs<read.ReadEndUs||age<0||age>fresh)
            {host.Remove(item.Instance,ShimSnapshotFeed.ReadStale);continue;}
            host.Observe(new AcceptedBindingObservation(item.Instance,item.Product,item.Name,item.IdentityKey,item.Generation,item.ReadSequence,
                copiedAt-age,new BindingCaptureSnapshot(item.Axes,item.Buttons,item.Hats),item.Minimum,item.Maximum));
        }
    }
}
