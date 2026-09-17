# Compile the current storage and bridge policy sources with PowerShell's bundled compiler.
# This does not build or replace the WPF application and never reads the user's Orbit data.
$ErrorActionPreference='Stop'
if([Environment]::Version.Major -lt 10){throw 'This source test needs PowerShell on .NET 10.'}
$sourceRoot=Join-Path $PSScriptRoot '../src/Orbit.Windows'
$parts=@('DdayStore.cs','DdaySelfTests.cs') | ForEach-Object {Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw}
$policy=Get-Content -LiteralPath (Join-Path $sourceRoot 'Policies.cs') -Raw
$parts += $policy.Substring(0,$policy.IndexOf('internal static class PathPolicy'))
$imports=($parts | ForEach-Object {[regex]::Matches($_,'(?m)^using [^\r\n]+;').Value} | Sort-Object -Unique) -join "`n"
$bodies=($parts | ForEach-Object {$_ -replace '(?m)^using [^\r\n]+;\r?\n','' -replace 'namespace Orbit;',''}) -join "`n"
$wrapper=@'
internal static class Settings { public static string DataRoot => throw new InvalidOperationException("real-store-disabled"); }
public static class DdaySourceProbe {
  public static int Run() {
    int count=0;
    void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);count++;Console.WriteLine("PASS "+name);}
    DdaySelfTests.Run(Check);
    Check(BridgePolicy.Validate("{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2028-02-29\",\"pinned\":false}",out _),"dday-policy-valid");
    foreach(var bad in new[]{"{}","{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2027-02-29\",\"pinned\":false}","{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2028-02-29\"}","{\"action\":\"deleteDday\",\"request\":\"1\",\"id\":\"../fixture\"}","{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2028-02-29\",\"pinned\":\"false\"}"})
      Check(!BridgePolicy.Validate(bad,out _),"dday-policy-invalid");
    return count;
  }
}
'@
if('Orbit.DdaySourceProbe' -as [type]){throw 'Use a fresh PowerShell process so current source is compiled again.'}
Add-Type -TypeDefinition ("#nullable enable`n"+$imports+"`nnamespace Orbit {`n"+$bodies+"`n"+$wrapper+"`n}")
$count=[Orbit.DdaySourceProbe]::Run()
Write-Output "RESULT dday-source passed=$count failed=0 (storage and policy only; WPF build not run)"
