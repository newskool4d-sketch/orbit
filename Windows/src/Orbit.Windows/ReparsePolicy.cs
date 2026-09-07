using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Orbit;
internal static class ReparsePolicy
{
    [StructLayout(LayoutKind.Sequential)] struct TagInfo {public uint Attributes,Tag;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandleEx(SafeFileHandle file,int type,out TagInfo info,uint size);
    public static bool Cloud(uint tag)=>(tag&0xFFFF0FFFu)==0x9000001Au;
    public static bool Safe(string path)
    {
        try {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)return true;
            using var handle=CreateFileW(path,0x80,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
            return !handle.IsInvalid&&GetFileInformationByHandleEx(handle,9,out var info,8)&&Cloud(info.Tag);
        }catch{return false;}
    }
}
