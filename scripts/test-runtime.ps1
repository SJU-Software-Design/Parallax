param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$Player,
    [string]$WorkDirectory = (Join-Path $env:TEMP 'parallax-contact-runtime'),
    [string]$Compiler,
    [string]$UnityBindings,
    [string]$Python = "python",
    [string]$UnityPyPath,
    [string]$Suite = 'HeldContactTests.Run'
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path -LiteralPath $Project).Path
$Player = (Resolve-Path -LiteralPath $Player).Path
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
if ($WorkDirectory -eq $Player -or $WorkDirectory.StartsWith($Player + [IO.Path]::DirectorySeparatorChar)) {
    throw 'Runtime checks require a separate temporary player directory.'
}
if (-not $Compiler) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $Compiler = & $vswhere -latest -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
    }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Pass -Compiler with the Roslyn csc.exe path.' }
$runtime = [IO.Path]::GetFullPath((Join-Path $WorkDirectory 'player'))
if ($runtime -eq $Player -or $runtime.StartsWith($Player + [IO.Path]::DirectorySeparatorChar)) {
    throw 'Runtime checks must never mutate the supplied player.'
}
$copies = Join-Path $WorkDirectory 'sources'
New-Item -ItemType Directory -Path $runtime,$copies -Force | Out-Null
# Copy the original player only on first use. Never patch the supplied player.
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'UnityPlayer.dll'))) {
    Get-ChildItem -LiteralPath $Player -Force | Copy-Item -Destination $runtime -Recurse -Force
}
$data = Join-Path $runtime 'Parallax_Data'
$marker = Join-Path $runtime '.parallax-runtime-copy'
[IO.File]::WriteAllText($marker,$Player)
$prepare = Join-Path $PSScriptRoot 'runtime/prepare-test-player.py'
$prepareArgs = @($prepare,'--player',$runtime,'--project',$Project)
if ($UnityPyPath) { $prepareArgs += @('--unitypy-path',$UnityPyPath) }
& $Python @prepareArgs
if ($LASTEXITCODE -ne 0) { throw 'Preparing the isolated runtime test player failed.' }
$managed = Join-Path $data 'Managed'
if ($UnityBindings) { Get-ChildItem -LiteralPath $UnityBindings -Filter 'UnityEngine*.dll' | Copy-Item -Destination $managed -Force }
$arguments = @('/nologo','/target:library','/nostdlib+','/langversion:latest','/optimize+',
    '/define:UNITY_STANDALONE_WIN,PARALLAX_RUNTIME_TESTS',('/out:' + (Join-Path $managed 'Assembly-CSharp.dll')))
Get-ChildItem -LiteralPath $managed -Filter '*.dll' | Where-Object Name -NE 'Assembly-CSharp.dll' | ForEach-Object {
    $arguments += '/reference:' + $_.FullName
}
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $Project 'Assets\Parallax') -Recurse -Filter '*.cs' | Where-Object {
    $_.FullName -notmatch '[\\/]Editor[\\/]' -or $_.Name -match '(Tests|ControlFeelValidation|PhysicsPackageValidation)\.cs$'
}
foreach ($source in $sourceFiles) {
    # The shipped player's linker inlines these constants. Substitute only in
    # temporary compile copies, never in the Unity project.
    $code = [IO.File]::ReadAllText($source.FullName).Replace('Mathf.PI','3.14159274f').Replace('Mathf.Deg2Rad','0.0174532924f').Replace('Mathf.Rad2Deg','57.29578f').Replace('float.NaN','(0f/0f)').Replace('float.PositiveInfinity','(1f/0f)').Replace('float.NegativeInfinity','(-1f/0f)')
    $target = Join-Path $copies ($source.FullName.Substring($Project.Length + 1).Replace('\','_'))
    [IO.File]::WriteAllText($target,$code)
    $arguments += $target
}
$bootstrap = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene setup adapter only: test bodies and production code are unchanged.
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene }
    public enum NewSceneMode { Single }
    public static class EditorSceneManager
    {
        public static void NewScene(NewSceneSetup setup, NewSceneMode mode)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            Physics.SyncTransforms();
        }
    }
}
public sealed class RuntimeTestBootstrap : MonoBehaviour
{
    public static void Initialize() { Schedule(); }
    static bool scheduled;
    public static bool Schedule()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-parallax-runtime-tests") < 0) return false;
        if (scheduled) return false;
        if (!scheduled)
        {
            scheduled=true; var host=new GameObject("Parallax runtime checks");
            DontDestroyOnLoad(host); host.AddComponent<RuntimeTestBootstrap>();
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        }
        return true;
    }
    void Start()
    {
        var args=Environment.GetCommandLineArgs(); int at=Array.IndexOf(args,"-parallax-suite");
        var entries=(at>=0?args[at+1]:"HeldContactTests.Run").Split(',');
        var errors=new List<string>();
        foreach(var entry in entries)
        {
            try
            {
                int split=entry.LastIndexOf('.');
                typeof(RuntimeTestBootstrap).Assembly.GetType("ShadeLink.Editor."+entry.Substring(0,split),true)
                    .GetMethod(entry.Substring(split+1),BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
                Debug.Log("RUNTIME_SUITE_PASS "+entry);
            }
            catch(Exception e)
            {
                var error=e is TargetInvocationException?e.InnerException:e;
                errors.Add(entry+": "+error); Debug.LogError("RUNTIME_SUITE_FAIL "+errors[errors.Count-1]);
            }
        }
        Debug.Log("RUNTIME_TESTS_FINISHED failures="+errors.Count+" unity="+Application.unityVersion);
        Application.Quit(errors.Count==0?0:1);
    }
}
'@
$bootstrapFile = Join-Path $copies 'RuntimeTestBootstrap.cs'
[IO.File]::WriteAllText($bootstrapFile,$bootstrap)
$arguments += $bootstrapFile
& $Compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
$log = Join-Path $WorkDirectory 'runtime.log'
$playerArguments = @('-batchmode','-nographics','-parallax-runtime-tests','-parallax-suite',$Suite,'-logFile',('"'+$log+'"'))
$process = Start-Process -FilePath (Join-Path $runtime 'Parallax.exe') -ArgumentList $playerArguments -WorkingDirectory $WorkDirectory -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Runtime checks timed out. See $log" }
Get-Content -LiteralPath $log | Where-Object { $_ -match 'HELD_CONTACT_|RUNTIME_|_TESTS_OK|SUITE_|VALIDATION_' }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'RUNTIME_TESTS_FINISHED failures=0')) {
    throw "Runtime checks failed (exit $($process.ExitCode)). See $log"
}
Write-Output 'Native Unity player checks passed. This is not a Unity Editor rebuild.'
