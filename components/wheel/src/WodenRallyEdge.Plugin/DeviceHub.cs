using Dbce.Wheel.Ffb;
using Dbce.Wheel.Input;
using System.Runtime.InteropServices;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

internal sealed class DeviceHub : IDisposable
{
    internal sealed class Device
    {
        internal WheelFfbNative.DeviceInfo Info;
        internal int Slot;
        internal int[] Axes = new int[8];
        internal byte[] Buttons = new byte[DigitalInput.ButtonCount], Previous = new byte[DigitalInput.ButtonCount];
        internal byte[] Physical = new byte[128], ProfileButtons = new byte[160], PreviousProfile = new byte[160];
        internal int[] Pov = new int[4];
        internal bool Ok;
    }
    internal readonly List<Device> Devices = new();
    internal string Status { get; private set; } = "Not opened";
    private readonly DigitalInputEdges _presses = new();
    private readonly DigitalInputEdges _profilePresses = new();
    private readonly PovSnapshotReader? _rawReader;
    [DllImport("kernel32", CharSet=CharSet.Unicode, ExactSpelling=true)] private static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32", CharSet=CharSet.Ansi, ExactSpelling=true)] private static extern IntPtr GetProcAddress(IntPtr module,string name);
    // Testable coherent-reader dependency; no module or device operation.
    internal DeviceHub(PovSnapshotReader reader) { _rawReader=reader; }
    internal DeviceHub(string directory)
    {
        if (!WheelFfbNative.Load(directory)) { Status = WheelFfbNative.LastError; return; }
        // The wrapper pins this exact module for process lifetime; never load a
        // second native DLL or use an export belonging to another slot owner.
        var module=GetModuleHandleW(WheelFfbNative.LoadedFrom);
        var export=module==IntPtr.Zero?IntPtr.Zero:GetProcAddress(module,"ReadDeviceStateWithPov");
        if(export!=IntPtr.Zero) _rawReader=PovSnapshotReader.FromExport(export);
        Refresh();
    }
    internal void CloseReaders() { WheelFfbNative.CloseRead(); Devices.Clear(); ClearPresses(); }
    internal void Refresh()
    {
        CloseReaders();
        foreach (var info in WheelFfbNative.ListAllDevices())
        {
            if (!info.InstanceGuid.HasValue || DevicePreference.IsVirtualDevice(info.Name)) continue;
            int slot = WheelFfbNative.OpenRead(info.Index);
            if (slot < 0) continue;
            Devices.Add(new() { Info = info, Slot = slot });
        }
        Status = Devices.Count == 0 ? "No physical devices could be opened. Connect devices, then Refresh." : $"{Devices.Count} physical devices opened for input" + (WheelFfbNative.SupportsPov ? "; HAT directions available" : "; native HAT capability unavailable; matched input update required");
        Poll();
        foreach (var d in Devices) { Array.Copy(d.Buttons, d.Previous, d.Buttons.Length); Array.Copy(d.ProfileButtons,d.PreviousProfile,160); }
        ClearPresses();
    }
    internal void Poll()
    {
        foreach (var d in Devices)
        {
            Array.Copy(d.Buttons, d.Previous, d.Buttons.Length);
            Array.Copy(d.ProfileButtons,d.PreviousProfile,160);
            if(_rawReader is not null)
            {
                d.Ok=_rawReader.Read(d.Slot,d.Axes,d.Physical,d.Pov);
                if(d.Ok) HatButtons.Project(d.Physical,d.Pov,d.Buttons,d.ProfileButtons);
            }
            else { d.Ok=WheelFfbNative.Read(d.Slot,d.Axes,d.Buttons); Array.Clear(d.ProfileButtons,0,160); }
            if (!d.Ok) { Array.Clear(d.Buttons, 0, d.Buttons.Length); Array.Clear(d.Previous, 0, d.Previous.Length); Array.Clear(d.Axes, 0, 8); Array.Clear(d.ProfileButtons,0,160); Array.Clear(d.PreviousProfile,0,160); _presses.Disconnect(d.Info.InstanceGuid!.Value); _profilePresses.Disconnect(d.Info.InstanceGuid.Value); continue; }
            _presses.Observe(d.Info.InstanceGuid!.Value, d.Buttons, d.Previous);
            _profilePresses.Observe(d.Info.InstanceGuid!.Value,d.ProfileButtons,d.PreviousProfile);
        }
    }
    private static string Key(Guid guid, int button) => guid.ToString("D") + ":" + button;
    internal bool TryAxis(AxisBinding? binding, out float value)
    {
        value = 0;
        if (binding?.Valid != true) return false;
        var matches = Devices.Where(x => x.Info.InstanceGuid == binding.DeviceGuid).ToArray();
        var d = matches.Length == 1 ? matches[0] : null;
        if (d?.Ok != true) return false;
        value = (float)binding.Normalize(d.Axes[binding.Axis]); return true;
    }
    internal bool Button(ButtonBinding? binding, bool edge, string action = "default")
    {
        if (binding?.Valid != true) return false;
        var matches = Devices.Where(x => x.Info.InstanceGuid == binding.DeviceGuid).ToArray();
        var d = matches.Length == 1 ? matches[0] : null;
        if (d?.Ok != true) return false;
        if(binding.HatNeighbours)
            return _rawReader is not null && (edge?_profilePresses.Consume(binding.DeviceGuid,binding.Button,action):d.ProfileButtons[binding.Button]!=0);
        return edge ? _presses.Consume(binding.DeviceGuid, binding.Button, action) : d.Buttons[binding.Button] != 0;
    }
    internal void ClearPresses() { _presses.Clear(); _profilePresses.Clear(); }
    internal Dictionary<(Guid, int), int> AxesSnapshot()
    {
        var result = new Dictionary<(Guid, int), int>();
        foreach (var d in Devices.Where(x => x.Ok && x.Info.Axes > 0))
            for (int i = 0; i < 8; i++) result[(d.Info.InstanceGuid!.Value, i)] = d.Axes[i];
        return result;
    }
    internal IEnumerable<ButtonBinding> PressedButtons() => Devices.Where(x => x.Ok).SelectMany(d => Enumerable.Range(0, Math.Min(128, d.Info.Buttons)).Concat(Enumerable.Range(128, 32))
        .Where(i => d.Buttons[i] != 0 && d.Previous[i] == 0).Select(i => new ButtonBinding(d.Info.InstanceGuid!.Value, i)));
    internal string Describe(Guid guid) => Devices.FirstOrDefault(x => x.Info.InstanceGuid == guid)?.Info.Name ?? "Disconnected " + guid.ToString("D")[..8];
    internal ForceTarget ResolveForceTarget(bool follow, string guid, Guid? steering) => ForceSelection.Resolve(follow, guid, steering,
        Devices.Where(d => d.Info.InstanceGuid.HasValue).Select(d => new ForceCandidate(d.Info.InstanceGuid!.Value, d.Info.Name, d.Info.ForceFeedback, DevicePreference.IsVirtualDevice(d.Info.Name))));
    internal bool IsReading(Guid guid) => Devices.Any(x => x.Info.InstanceGuid == guid && x.Ok);
    public void Dispose() => CloseReaders();
}
