# Launch-scoped game state. Keep this adapter's paths explicit; never restore arbitrary paths from a tape.
# reg.exe export was observed silently omitting Unity PlayerPrefs values.
# Preserve exact registry bytes; the native helper can access only this game's key.
function Initialize-SessionRegistry {
    if ('WodenSessionRegistry' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class WodenSessionRegistry {
    const string Path = @"Software\ViJuDa\Super Woden Rally Edge";
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegOpenKeyEx(IntPtr key,string path,int options,int access,out IntPtr result);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegQueryValueEx(IntPtr key,string name,IntPtr reserved,out uint type,byte[] data,ref uint size);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegSetValueEx(IntPtr key,string name,int reserved,uint type,byte[] data,int size);
    [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
    public sealed class Value { public string name; public uint type; public string data; }
    static IntPtr Open(int access) { IntPtr key; int error=RegOpenKeyEx(new IntPtr(unchecked((int)0x80000001)),Path,0,access,out key); if(error!=0)throw new Win32Exception(error);return key; }
    public static Value Read(string name) {
        IntPtr key=Open(1);
        try { uint size=0,type;int error=RegQueryValueEx(key,name,IntPtr.Zero,out type,null,ref size);if(error!=0)throw new Win32Exception(error);
            if(size>16777216)throw new InvalidOperationException("Oversize game preference");
            byte[] bytes=new byte[size];error=RegQueryValueEx(key,name,IntPtr.Zero,out type,bytes,ref size);if(error!=0)throw new Win32Exception(error);
            return new Value { name=name,type=type,data=Convert.ToBase64String(bytes,0,(int)size) };
        } finally { RegCloseKey(key); }
    }
    public static void Write(string name,uint type,string data) {
        if(name==null || name.IndexOf('\0')>=0)throw new ArgumentException("Invalid preference name");
        byte[] bytes=Convert.FromBase64String(data);if(bytes.Length>16777216)throw new InvalidOperationException("Oversize game preference");
        IntPtr key=Open(2);try { int error=RegSetValueEx(key,name,0,type,bytes,bytes.Length);if(error!=0)throw new Win32Exception(error); } finally { RegCloseKey(key); }
    }
}
'@
}
function Get-SessionRegistryValue([string]$Name) { Initialize-SessionRegistry; [WodenSessionRegistry]::Read($Name) }
function Set-SessionRegistryValue($Value) { Initialize-SessionRegistry; [WodenSessionRegistry]::Write($Value.name, $Value.type, $Value.data) }

