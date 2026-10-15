using System.Runtime.InteropServices;
using System.Text;
using WodenRallyEdge.Core;
using Dbce.Wheel.Shim;

namespace WodenRallyEdge;

/// <summary>
/// dbce_shim.dll (the shared input shim, experimental ABI 2, outrun2-decomp windows/shim_dll/dbce_shim.h): loaded like the
/// toolkit's WheelFfb.dll, by full path and GetProcAddress. The load checks the ABI version, the frame size and every
/// action name against <see cref="ShimActionMap.Names"/>, so a layout or contract mismatch refuses instead of misreading.
/// Every handle call is on the thread that opened it (Unity's main thread).
/// </summary>
internal static class ShimNative
{
    internal const int Abi = ShimActionMap.Abi, FrameSize = 296, ArgsSize = 128;
    internal const uint Latched = 1, Capture = 2;
    /// <summary>dbce_shim_open_args, fixed layout (128 bytes): every pointer is unmanaged memory owned by the caller for
    /// the duration of the call (the DLL copies what it keeps).</summary>
    [StructLayout(LayoutKind.Explicit, Size = ArgsSize)]
    internal struct OpenArgs
    {
        [FieldOffset(0)] public uint StructSize; [FieldOffset(4)] public uint Flags; [FieldOffset(8)] public int ProfileLength;
        [FieldOffset(12)] public byte FencesVerified; [FieldOffset(13)] public byte ForceAbsent;
        [FieldOffset(16)] public IntPtr Folder; [FieldOffset(24)] public IntPtr Nonce; [FieldOffset(32)] public IntPtr Profile;
        [FieldOffset(40)] public IntPtr Unsupported; [FieldOffset(48)] public IntPtr Hwnd; [FieldOffset(56)] public IntPtr SessionFile;
        [FieldOffset(64)] public IntPtr BatchFile; [FieldOffset(72)] public IntPtr CaptureFolder; [FieldOffset(80)] public IntPtr GameId;
        [FieldOffset(88)] public IntPtr GameChannels; [FieldOffset(96)] public IntPtr Context; [FieldOffset(104)] public IntPtr AdapterVersion;
        [FieldOffset(112)] public double Hz; [FieldOffset(120)] public double Seconds;
    }
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string path);
    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCall();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr NameCall(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenCall([MarshalAs(UnmanagedType.LPWStr)] string folder, [MarshalAs(UnmanagedType.LPStr)] string nonce, byte[] profile, int profileLength,
        [MarshalAs(UnmanagedType.LPStr)] string unsupported, IntPtr hwnd, out IntPtr handle, [Out] byte[] why, int whyLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TickCall(IntPtr handle, [In, Out] byte[] frame);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Open2Call(ref OpenArgs args, out IntPtr handle, [Out] byte[] why, int whyLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RecordCall(IntPtr handle, ulong frameSeq, [In] double[] values, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TextCall(IntPtr handle, [Out] byte[] text, int length, out int needed);

    private static OpenCall? _open; private static Open2Call? _open2; private static RecordCall? _record; private static TickCall? _tick;
    private static TextCall? _startReport, _devices, _profile, _stop;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SnapshotSizesCall(out int header,out int device,out int sample);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SnapshotCall(IntPtr handle,ulong sequence,
        [In,Out] byte[] header,[Out] byte[] devices,int deviceCapacity,[Out] byte[] samples,int sampleCapacity);
    private static SnapshotCall? _snapshot;
    internal static bool SnapshotAvailable => _snapshot!=null;
    internal static string SnapshotStatus {get;private set;}="Shared capture snapshot extension is unavailable; active input is unchanged.";
    internal static bool Loaded { get; private set; }

    /// <summary>Loads and checks the DLL in <paramref name="directory"/>; null when ready, otherwise why not.</summary>
    internal static string? Load(string directory)
    {
        if (Loaded) return null;
        string path = Path.GetFullPath(Path.Combine(directory, "dbce_shim.dll"));
        if (!File.Exists(path)) return "dbce_shim.dll is not beside the plugin";
        using (var file = File.OpenRead(path))
        {
            string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(file));
            if(!hash.Equals("9B8A036F664CF8F45CCCF82B98E5E4376F93215B60EAC50B6DB965BF498015D8",StringComparison.OrdinalIgnoreCase)&&
               !hash.Equals("4AF269C9F2FDBAE3CEF1A78E563C3877CB0AB820D4BE4C2579A1AD6EA25CECC5",StringComparison.OrdinalIgnoreCase))
                return "dbce_shim.dll differs from the reviewed ABI 2 dependency pins";
        }
        IntPtr module = LoadLibraryW(path);
        if (module == IntPtr.Zero) return $"dbce_shim.dll did not load (error {Marshal.GetLastWin32Error()})";
        T? Get<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(module, name);
            return p == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }
        var abi = Get<IntCall>("dbce_shim_abi_version"); var size = Get<IntCall>("dbce_shim_frame_size"); var name = Get<NameCall>("dbce_shim_action_name");
        var argsSize = Get<IntCall>("dbce_shim_open_args_size");
        _open = Get<OpenCall>("dbce_shim_open"); _open2 = Get<Open2Call>("dbce_shim_open2"); _record = Get<RecordCall>("dbce_shim_record"); _tick = Get<TickCall>("dbce_shim_tick");
        _startReport = Get<TextCall>("dbce_shim_start_report"); _devices = Get<TextCall>("dbce_shim_device_reports");
        _profile = Get<TextCall>("dbce_shim_profile_info"); _stop = Get<TextCall>("dbce_shim_stop");
        if (abi == null || size == null || argsSize == null || name == null || _open == null || _open2 == null || _record == null || _tick == null ||
            _startReport == null || _devices == null || _profile == null || _stop == null)
            return "dbce_shim.dll lacks an ABI 2 export";
        if (abi() != Abi || size() != FrameSize || argsSize() != ArgsSize || Marshal.SizeOf<OpenArgs>() != ArgsSize)
            return $"dbce_shim.dll ABI {abi()}, frame {size()}, arguments {argsSize()} bytes; this mod needs ABI {Abi}, {FrameSize}, {ArgsSize} bytes";
        for (int i = 0; i < ShimActionMap.Count; i++)
            if (Marshal.PtrToStringAnsi(name(i)) != ShimActionMap.Names[i]) return $"dbce_shim.dll action {i} is not '{ShimActionMap.Names[i]}'";
        if (name(ShimActionMap.Count) != IntPtr.Zero) return "dbce_shim.dll has more actions than this mod knows";
        var snapshotAbi=Get<IntCall>("dbce_shim_snapshot_abi_version");
        var snapshotSizes=Get<SnapshotSizesCall>("dbce_shim_snapshot_sizes");
        var snapshot=Get<SnapshotCall>("dbce_shim_snapshot");
        if(snapshotAbi!=null&&snapshotSizes!=null&&snapshot!=null)
        {
            try
            {
                if(snapshotSizes(out int header,out int device,out int sample)!=0)throw new InvalidDataException("Snapshot size query was refused.");
                ShimSnapshotAbi.Validate(snapshotAbi(),header,device,sample);
                _snapshot=snapshot;SnapshotStatus="Accepted ordinary input snapshots are available.";
            }
            catch(InvalidDataException ex){SnapshotStatus="Shared capture unavailable: "+ex.Message;}
        }
        Loaded = true;
        return null;
    }

    internal static int Open(string folder, string nonce, byte[] profile, string unsupported, IntPtr hwnd, out IntPtr handle, out string why)
    {
        var buffer = new byte[512];
        int r = _open!(folder, nonce, profile, profile.Length, unsupported, hwnd, out handle, buffer, buffer.Length);
        why = Text(buffer);
        return r;
    }

    // Developer trial only. The caller has independently armed the resident
    // no-force fence and progression/network mutes before admitting this path.
    internal static int OpenLatched(string folder, string nonce, byte[] profile, string unsupported, IntPtr hwnd,
        string sessionFile, string batchFile, out IntPtr handle, out string why, string adapterVersion = "woden-menu-trial@1")
    {
        var pointers = new List<IntPtr>();
        IntPtr String(string text) { var p=Marshal.StringToCoTaskMemUTF8(text); pointers.Add(p); return p; }
        // Both allocation families are released explicitly below.
        IntPtr data=Marshal.AllocHGlobal(profile.Length), folderPtr=Marshal.StringToHGlobalUni(folder),
            sessionPtr=Marshal.StringToHGlobalUni(sessionFile), batchPtr=Marshal.StringToHGlobalUni(batchFile);
        try
        {
            Marshal.Copy(profile,0,data,profile.Length);
            var args=new OpenArgs { StructSize=ArgsSize,Flags=Latched,ProfileLength=profile.Length,FencesVerified=1,ForceAbsent=1,
                Folder=folderPtr,Nonce=String(nonce),Profile=data,Unsupported=String(unsupported),Hwnd=hwnd,
                SessionFile=sessionPtr,BatchFile=batchPtr,AdapterVersion=String(adapterVersion) };
            var buffer=new byte[512]; int r=_open2!(ref args,out handle,buffer,buffer.Length);why=Text(buffer);return r;
        }
        finally { foreach(var p in pointers) Marshal.FreeCoTaskMem(p);Marshal.FreeHGlobal(data);Marshal.FreeHGlobal(folderPtr);Marshal.FreeHGlobal(sessionPtr);Marshal.FreeHGlobal(batchPtr); }
    }

    /// <summary>One frame into <paramref name="frame"/> (296 bytes, struct_size set here).</summary>
    internal static int Tick(IntPtr handle, byte[] frame)
    {
        BitConverter.GetBytes(FrameSize).CopyTo(frame, 0);
        return _tick!(handle, frame);
    }
    internal static string StartReport(IntPtr handle) => Read(_startReport!, handle);
    internal static string DeviceReports(IntPtr handle) => Read(_devices!, handle);
    internal static int Snapshot(IntPtr handle,ulong frameSequence,out ShimSnapshot? snapshot)
    {
        snapshot=null;
        if(_snapshot==null)return ShimSnapshotAbi.Refused;
        int result=ShimSnapshot.Take((seq,header,devices,deviceCapacity,samples,sampleCapacity)=>
            _snapshot(handle,seq,header,devices,deviceCapacity,samples,sampleCapacity),frameSequence,out snapshot);
        if(result==ShimSnapshotAbi.Ok&&snapshot?.FrameSeq!=frameSequence)throw new InvalidDataException("Snapshot does not match the accepted input frame.");
        return result;
    }
    /// <summary>The profile as Core's parser read it: "key=value" lines (ok, error, profile, revision, transmission, sha256).</summary>
    internal static string ProfileInfo(IntPtr handle) => Read(_profile!, handle);
    internal static int Stop(IntPtr handle, out string report)
    {
        var buffer = new byte[2048];
        int r = _stop!(handle, buffer, buffer.Length, out _);
        report = Text(buffer);
        return r;
    }
    // Calls are game-input-thread only, with no native callbacks. Reuse the buffer for the live identity report.
    private static byte[] _textBuffer = new byte[8192];
    private static string Read(TextCall call, IntPtr handle)
    {
        var buffer = _textBuffer;
        int r = call(handle, buffer, buffer.Length, out int needed);
        // Device identity reports include HID paths. Do not silently parse a truncated report as a smaller inventory.
        if (r == 0 && needed > buffer.Length)
        {
            if (needed > 128 * 1024) return "(unavailable: report exceeds 128 KiB)";
            buffer = _textBuffer = new byte[needed];
            r = call(handle, buffer, buffer.Length, out needed);
            if (needed > buffer.Length) return "(unavailable: report changed size)";
        }
        return r == 0 ? Text(buffer) : $"(unavailable: {r})";
    }
    private static string Text(byte[] b) { int n = Array.IndexOf(b, (byte)0); return Encoding.UTF8.GetString(b, 0, n < 0 ? b.Length : n); }

    /// <summary>A frame's fields, decoded from the fixed layout (dbce_shim_frame: seq 8, down 24, up 32, run 40, actions 48).</summary>
    internal static void Decode(byte[] frame, ShimAction[] actions, out ulong seq, out ulong down, out ulong up, out byte run)
    {
        seq = BitConverter.ToUInt64(frame, 8); down = BitConverter.ToUInt64(frame, 24); up = BitConverter.ToUInt64(frame, 32); run = frame[40];
        for (int i = 0; i < ShimActionMap.Count; i++)
        {
            int at = 48 + i * 8;
            actions[i] = new ShimAction(BitConverter.ToSingle(frame, at), (ShimStatus)frame[at + 4], frame[at + 5] != 0);
        }
    }
}
