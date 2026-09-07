using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
namespace Orbit;
internal static class AppLinks
{
    [ComImport,Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] class ActivationManager {}
    [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivationManager {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string app,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint pid);
        [PreserveSig] int ActivateForFile(IntPtr app,IntPtr item,IntPtr verb,out uint pid);
        [PreserveSig] int ActivateForProtocol(IntPtr app,IntPtr item,out uint pid);
    }
    public static bool Registered(string provider) {
        if(provider is not ("codex" or "claude"))return false;
        using var key=Registry.ClassesRoot.OpenSubKey(provider);
        return key?.GetValue("URL Protocol")!=null;
    }
    public static int Open(string provider)
    {
        if(!Registered(provider))throw new InvalidOperationException("application-unavailable");
        var appId=provider=="codex"?"OpenAI.Codex_2p2nqsd0c76g0!App":"Claude_pzs8sxrjxfjjc!Claude";
        var manager=(IActivationManager)new ActivationManager();
        try {
            int hr=manager.ActivateApplication(appId,"",0,out var pid);
            if(hr<0)Marshal.ThrowExceptionForHR(hr);
            return (int)pid;
        }finally{Marshal.ReleaseComObject(manager);}
    }
    public static int Probe()
    {
        bool pass=true;
        foreach(var provider in new[]{"codex","claude"}) {
            try{var pid=Open(provider);bool alive=!Process.GetProcessById(pid).HasExited;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{provider,mode="appHome",dispatched=true,processAlive=alive,exactSessionVerified=false}));pass&=alive;}
            catch{pass=false;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{provider,dispatched=false}));}
        }
        return pass?0:1;
    }
}
