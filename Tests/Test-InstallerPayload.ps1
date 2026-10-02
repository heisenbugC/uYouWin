param([string]$MsiPath = "$PSScriptRoot\..\..\uYouWinInstaller\bin\Release\uYouWin Installer.msi")
$ErrorActionPreference = 'Stop'
function Assert($ok, [string]$message) { if (!$ok) { throw "FAIL: $message" }; "PASS: $message" }
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path $MsiPath).Path, 0)
function ReadRows([string]$sql, [int]$columns) {
    $view = $database.OpenView($sql)
    [void]$view.Execute()
    try {
        while ($record = $view.Fetch()) {
            $row = @(); for ($i=1; $i -le $columns; $i++) { $row += $record.StringData($i) }
            ,$row
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($record)
        }
    } finally { [void]$view.Close(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($view) }
}
try {
    $directories = @{}
    foreach ($row in (ReadRows 'SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`' 3)) {
        $directories[$row[0]] = @($row[1], ($row[2].Split(':')[0].Split('|')[-1]))
    }
    function RelativeDirectory([string]$id) {
        if ($id -eq 'INSTALLFOLDER') { return '' }
        $d = $directories[$id]
        if (!$d) { throw "Unknown directory: $id" }
        $parent = RelativeDirectory $d[0]
        if ($d[1] -eq '.') { return $parent }
        return $parent + $d[1] + '\'
    }
    $components = @{}
    foreach ($row in (ReadRows 'SELECT `Component`, `Directory_` FROM `Component`' 2)) { $components[$row[0]] = $row[1] }
    $files = @{}
    foreach ($row in (ReadRows 'SELECT `Component_`, `FileName`, `FileSize` FROM `File`' 3)) {
        $path = (RelativeDirectory $components[$row[0]]) + $row[1].Split('|')[-1]
        $files[$path] = [long]$row[2]
    }
    Assert ($directories['INSTALLFOLDER'][0] -eq 'ProgramFiles64Folder') 'MSI targets 64-bit Program Files'
    foreach ($name in @('uYouWin.exe','uYouWin.exe.config','yt-dlp.exe','deno.exe','Microsoft.Toolkit.Win32.UI.XamlHost.dll','Microsoft.Toolkit.Win32.UI.XamlHost.pri','Microsoft.Toolkit.Win32.UI.XamlHost.winmd','vcruntime140_app.dll','msvcp140_app.dll','vcruntime140_1_app.dll')) {
        Assert ($files.ContainsKey($name)) "Root runtime file is packaged: $name"
    }
    Assert (@($files.Keys | Where-Object { $_ -match '^(Data|Cache|Assets)\\' }).Count -eq 0) 'No developer data, cache or old dependency layout is packaged'
    Assert (@($files.Keys | Where-Object { $_ -match '\.(pdb|pfx|ttf)$' }).Count -eq 0) 'No debug symbols, signing keys or system fonts are shipped'
    Assert ($files.ContainsKey('en-GB\ModernWpf.resources.dll')) 'Localization subdirectories are preserved'
    $release = (Resolve-Path "$PSScriptRoot\..\bin\Release").Path
    $native = Get-ChildItem (Join-Path $release '_internal') -Recurse -File
    foreach ($file in $native) {
        $relative = $file.FullName.Substring($release.Length + 1)
        if (!$files.ContainsKey($relative) -or $files[$relative] -ne $file.Length) { throw "Missing or mismatched yt-dlp runtime: $relative" }
    }
    Assert ($native.Count -gt 0) "All $($native.Count) yt-dlp runtime files retain their structure"
    $payload = (Resolve-Path "$PSScriptRoot\..\..\uYouWinInstaller\obj\Release\Payload").Path
    $staged = @(Get-ChildItem $payload -Recurse -File)
    Assert ($files.Count -eq $staged.Count) 'MSI file count matches the staged Release payload'
    foreach ($file in $staged) {
        $relative = $file.FullName.Substring($payload.Length + 1)
        if (!$files.ContainsKey($relative) -or $files[$relative] -ne $file.Length) { throw "Payload mismatch: $relative" }
    }
    "All MSI payload checks passed ($($files.Count) files)."
} finally {
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($database)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
}
