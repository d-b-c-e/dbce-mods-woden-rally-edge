using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Woden.ControlsTesting;

// Same bounded whole-file exchange used by the reviewed Art Core trial. Core's
// read/delete-shared reader sees either generation, including when held open.
internal static class TrialBatchWriter
{
    [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32",SetLastError=true)]
    static extern bool SetFileInformationByHandle(SafeFileHandle file,int infoClass,byte[] info,int size);
    internal static void Replace(string path,string text)
    {
        string full=Path.GetFullPath(path),temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,text,new UTF8Encoding(false));
        try
        {
            using var handle=CreateFileW(temp,0x10000|0x100000,7,IntPtr.Zero,3,0,IntPtr.Zero);
            if(handle.IsInvalid)throw new IOException("Batch temp open failed: "+Marshal.GetLastWin32Error());
            byte[] name=Encoding.Unicode.GetBytes(full),info=new byte[20+name.Length+2];
            BitConverter.GetBytes(3).CopyTo(info,0); // REPLACE_IF_EXISTS | POSIX_SEMANTICS
            BitConverter.GetBytes(name.Length).CopyTo(info,16);name.CopyTo(info,20);
            if(!SetFileInformationByHandle(handle,22,info,info.Length))throw new IOException("Batch atomic rename failed: "+Marshal.GetLastWin32Error());
        }
        catch { try{File.Delete(temp);}catch{}throw; }
    }
}
