using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace Orbit;
internal interface ISecrets { string? Read(string key); void Write(string key,string value); void Delete(string key); }
internal sealed class Credentials : ISecrets
{
    const string Prefix="Orbit.Windows/v1/";
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct Entry { public uint Flags,Type; public string TargetName,Comment; public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten; public uint Size; public IntPtr Blob; public uint Persist,Count; public IntPtr Attributes; public string Alias,User; }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ReadNative(string name,uint type,uint flags,out IntPtr p);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool WriteNative(ref Entry entry,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool DeleteNative(string name,uint type,uint flags);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr p);
    public static byte[] Encode(string value) { var bytes=Encoding.UTF8.GetBytes(value); if(bytes.Length>2560)throw new ArgumentOutOfRangeException(nameof(value));return bytes; }
    public string? Read(string key) {
        if(!ReadNative(Prefix+key,1,0,out var p)) { if(Marshal.GetLastWin32Error()==1168)return null;throw new InvalidOperationException("credential-read"); }
        try { var e=Marshal.PtrToStructure<Entry>(p);var b=new byte[e.Size];Marshal.Copy(e.Blob,b,0,b.Length);return Encoding.UTF8.GetString(b); } finally {CredFree(p);}
    }
    public void Write(string key,string value) {
        var b=Encode(value);var p=Marshal.AllocHGlobal(b.Length);
        try {Marshal.Copy(b,0,p,b.Length);var e=new Entry {Type=1,TargetName=Prefix+key,Comment="Orbit",Size=(uint)b.Length,Blob=p,Persist=2,Alias="",User="Orbit"};if(!WriteNative(ref e,0))throw new InvalidOperationException("credential-write");}
        finally { for(int i=0;i<b.Length;i++)Marshal.WriteByte(p,i,0);Marshal.FreeHGlobal(p);Array.Clear(b); }
    }
    public void Delete(string key) {if(!DeleteNative(Prefix+key,1,0)&&Marshal.GetLastWin32Error()!=1168)throw new InvalidOperationException("credential-delete");}
}
internal sealed class SecretSession(ISecrets store)
{
    internal static readonly string[] Keys=["clientId","clientSecret","access","refresh","expiry"];
    public string? Get(string field) {var a=store.Read("active");return a==null?null:store.Read(a+"/"+field);}
    public Dictionary<string,string> Values() {var d=new Dictionary<string,string>();foreach(var key in Keys)if(Get(key) is string value)d[key]=value;return d;}
    public void Replace(Dictionary<string,string> values) {
        var old=store.Read("active");var next=Guid.NewGuid().ToString("N");var written=new List<string>();
        try {foreach(var (key,value) in values){store.Write(next+"/"+key,value);written.Add(next+"/"+key);}store.Write("active",next);}
        catch {foreach(var key in written)try{store.Delete(key);}catch{}throw;}
        if(old!=null)foreach(var key in Keys)try{store.Delete(old+"/"+key);}catch{}
    }
}
