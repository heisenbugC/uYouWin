$ErrorActionPreference = 'Stop'
$clock = [Diagnostics.Stopwatch]::StartNew()
$root = Split-Path $PSScriptRoot
$temp = Join-Path ([IO.Path]::GetTempPath()) ('uYouWin-native-probe-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
$process = $null
function Wait-ProbeProcess($child) {
    $remaining = [Math]::Max(0, 30000 - [int]$clock.ElapsedMilliseconds)
    if (!$child.WaitForExit($remaining)) {
        $child.Kill()
        throw 'TIMEOUT: Native regression action was terminated at the 30-second limit.'
    }
    $child.WaitForExit()
}
try {
    Get-ChildItem (Join-Path $root 'bin\Debug') -File |
        Where-Object { $_.Extension -in '.dll', '.winmd', '.pri' } |
        Copy-Item -Destination $temp
    $automation = [System.Management.Automation.PowerShell].Assembly.Location
    Copy-Item $automation $temp
    $exe = Join-Path $temp 'NativeSurfaceProbeHost.exe'
    $out = Join-Path $temp 'out.txt'
    $err = Join-Path $temp 'err.txt'
    $arguments = '/nologo /target:exe /platform:x64 /out:"' + $exe + '" /reference:"' + $automation +
        '" /win32manifest:"' + (Join-Path $PSScriptRoot 'NativeSurfaceProbe.manifest') + '" "' +
        (Join-Path $PSScriptRoot 'NativeSurfaceProbeHost.cs.txt') + '"'
    $compileOut = Join-Path $temp 'compiler-out.txt'
    $compileErr = Join-Path $temp 'compiler-err.txt'
    $process = Start-Process "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -ArgumentList $arguments -PassThru -RedirectStandardOutput $compileOut -RedirectStandardError $compileErr
    Wait-ProbeProcess $process
    Get-Content $compileOut
    Get-Content $compileErr
    if (![IO.File]::Exists($exe)) { throw 'Native probe compilation failed.' }
    $process.Dispose()
    $process = Start-Process $exe -ArgumentList ('"' + (Join-Path $PSScriptRoot 'Run-NativeSurfaceRegression.ps1') + '"') -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    Wait-ProbeProcess $process
    Get-Content $out
    Get-Content $err
    if ([IO.File]::ReadAllText($out) -notmatch 'Native surface checks passed') {
        throw ('Native probe failed. Exit=' + $process.ExitCode + '; stdout=' + [IO.File]::ReadAllText($out) + '; stderr=' + [IO.File]::ReadAllText($err))
    }
}
finally {
    if ($process) {
        if (!$process.HasExited) { $process.Kill() }
        $process.Dispose()
    }
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
