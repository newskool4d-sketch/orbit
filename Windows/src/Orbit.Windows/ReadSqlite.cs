using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
namespace Orbit;
internal sealed class ReadSqlite : IDisposable
{
    internal static class Native
    {
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_libversion();
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string filename,out IntPtr db,int flags,IntPtr vfs);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr db);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_busy_timeout(IntPtr db,int timeout);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int bytes,out IntPtr statement,IntPtr tail);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_step(IntPtr stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_stmt_readonly(IntPtr stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_name(IntPtr stmt,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(IntPtr stmt,int column);
        [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(IntPtr stmt,int index,[MarshalAs(UnmanagedType.LPUTF8Str)]string text,int length,IntPtr destructor);
    }
    readonly IntPtr db;
    public static string Version=>Marshal.PtrToStringUTF8(Native.sqlite3_libversion())??"unknown";
    public ReadSqlite(string path)
    {
        if(Version!="3.51.1")throw new IOException("sqlite-version-unverified");
        if(!File.Exists(path))throw new IOException("database-missing");
        if(File.Exists(path+"-wal")&&!File.Exists(path+"-shm"))throw new IOException("wal-sidecar-unavailable");
        string uri=new Uri(Path.GetFullPath(path)).AbsoluteUri+"?mode=ro&readonly_shm=1";
        int rc=Native.sqlite3_open_v2(uri,out db,1|0x40|0x10000,IntPtr.Zero);
        if(rc!=0){Native.sqlite3_close_v2(db);throw new IOException("database-open");}
        Native.sqlite3_busy_timeout(db,1000);
    }
    public List<Dictionary<string,string>> Query(string sql,params string[] values)
    {
        if(Native.sqlite3_prepare_v2(db,sql,-1,out var stmt,IntPtr.Zero)!=0)throw new IOException("database-schema");
        try
        {
            if(Native.sqlite3_stmt_readonly(stmt)==0)throw new InvalidOperationException("write-statement-rejected");
            for(int i=0;i<values.Length;i++)if(Native.sqlite3_bind_text(stmt,i+1,values[i],-1,new IntPtr(-1))!=0)throw new IOException("database-bind");
            var result=new List<Dictionary<string,string>>();
            for(int row=0;row<1000;row++)
            {
                int rc=Native.sqlite3_step(stmt);if(rc==101)return result;if(rc!=100)throw new IOException("database-read");
                var record=new Dictionary<string,string>();
                for(int col=0;col<Native.sqlite3_column_count(stmt);col++)record[Marshal.PtrToStringUTF8(Native.sqlite3_column_name(stmt,col))!]=Marshal.PtrToStringUTF8(Native.sqlite3_column_text(stmt,col))??"";
                result.Add(record);
            }
            throw new IOException("database-row-limit");
        }
        finally{Native.sqlite3_finalize(stmt);}
    }
    public void Dispose()=>Native.sqlite3_close_v2(db);
}
internal static class SqliteProbe
{
    [StructLayout(LayoutKind.Sequential)] struct Io {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [DllImport("kernel32.dll")] static extern bool GetProcessIoCounters(IntPtr handle,out Io io);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,IntPtr callback,IntPtr argument,IntPtr error);
    public static int Child(string path)
    {
        GetProcessIoCounters(Process.GetCurrentProcess().Handle,out var before);
        bool ok=false;int rows=0;
        try{using var db=new ReadSqlite(path);rows=int.Parse(db.Query("SELECT count(*) AS n FROM fixture")[0]["n"]);ok=rows==1;}catch{}
        GetProcessIoCounters(Process.GetCurrentProcess().Handle,out var after);
        Console.WriteLine(JsonSerializer.Serialize(new{ok,rows,writeOperations=after.WriteOperations-before.WriteOperations,writeBytes=after.WriteBytes-before.WriteBytes,version=ReadSqlite.Version}));
        return 0;
    }
    static string Hash(string path) { using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)); }
    public static async Task<int> Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"Orbit-sqlite-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string dbPath=Path.Combine(root,"fixture.sqlite");IntPtr writer=IntPtr.Zero;
        try
        {
            if(ReadSqlite.Native.sqlite3_open_v2(dbPath,out writer,2|4,IntPtr.Zero)!=0)throw new IOException();
            if(sqlite3_exec(writer,"PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE fixture(id INTEGER); INSERT INTO fixture VALUES(1);",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)!=0)throw new IOException();
            var before=Directory.GetFiles(root).ToDictionary(p=>p,Hash);
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
            start.ArgumentList.Add("--sqlite-reader");start.ArgumentList.Add(dbPath);
            using var child=Process.Start(start)!;var output=await child.StandardOutput.ReadToEndAsync();await child.WaitForExitAsync();
            using var doc=JsonDocument.Parse(output);var data=doc.RootElement;
            bool unchanged=Directory.GetFiles(root).Length==before.Count&&before.All(pair=>Hash(pair.Key)==pair.Value);
            bool pass=data.GetProperty("ok").GetBoolean()&&data.GetProperty("writeOperations").GetUInt64()==0&&data.GetProperty("writeBytes").GetUInt64()==0&&unchanged;
            Console.WriteLine(JsonSerializer.Serialize(new{test="sqlite-wal-readonly",pass,unchanged,reader=data}));
            return pass?0:1;
        }
        catch(Exception error) {Console.WriteLine(JsonSerializer.Serialize(new {test="sqlite-wal-readonly",pass=false,errorType=error.GetType().Name,errorCode=error.HResult}));return 1;}
        finally {if(writer!=IntPtr.Zero)ReadSqlite.Native.sqlite3_close_v2(writer);foreach(var p in Directory.GetFiles(root))File.Delete(p);Directory.Delete(root);}
    }
}
