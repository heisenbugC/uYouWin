$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$bin = Join-Path (Split-Path $PSScriptRoot) 'bin\Debug'
foreach ($file in Get-ChildItem $bin -Filter '*.dll') {
    try { [void][Reflection.Assembly]::LoadFrom($file.FullName) } catch [BadImageFormatException] { }
}
$codes = @{}
foreach ($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
    $op = $field.GetValue($null)
    $codes[([int]$op.Value -band 65535)] = $op
}
function Show-Method($method) {
    Write-Output ($method.DeclaringType.FullName + ' :: ' + $method.Name)
    $body = $method.GetMethodBody()
    if ($null -eq $body) { return }
    $bytes = $body.GetILAsByteArray()
    for ($i = 0; $i -lt $bytes.Length;) {
        $offset = $i
        $value = [int]$bytes[$i++]
        if ($value -eq 254) { $value = 65280 + $bytes[$i++] }
        $op = $codes[$value]
        $operand = ''
        switch ($op.OperandType.ToString()) {
            'InlineNone' { }
            {$_ -in 'InlineMethod','InlineField','InlineType','InlineTok'} {
                $token = [BitConverter]::ToInt32($bytes, $i); $i += 4
                try { $operand = $method.Module.ResolveMember($token).ToString() } catch { $operand = 'token ' + $token }
            }
            'InlineString' { $operand = $method.Module.ResolveString([BitConverter]::ToInt32($bytes, $i)); $i += 4 }
            'InlineSwitch' { $count = [BitConverter]::ToInt32($bytes, $i); $i += 4 + 4 * $count }
            {$_ -in 'InlineI8','InlineR'} { $i += 8 }
            {$_ -in 'InlineI','InlineBrTarget','InlineSig','ShortInlineR'} { $operand = [BitConverter]::ToInt32($bytes, $i); $i += 4 }
            'InlineVar' { $operand = [BitConverter]::ToUInt16($bytes, $i); $i += 2 }
            default { $operand = $bytes[$i++] }
        }
        Write-Output ('{0:X4}: {1} {2}' -f $offset, $op.Name, $operand)
    }
}
$controls = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'Microsoft.Toolkit.Wpf.UI.Controls.dll'))
$type = $controls.GetType('Microsoft.Toolkit.Wpf.UI.Controls.MediaPlayerElement', $true)
Show-Method ($type.GetMethod('SetMediaPlayer'))
Show-Method ($type.GetMethod('set_Stretch'))
foreach ($constructor in $type.GetConstructors()) { Show-Method $constructor }
$base = $type.BaseType
foreach ($name in @('SetContent','MeasureOverride','ArrangeOverride','GetUwpInternalObject','set_ChildInternal')) {
    $method = $base.GetMethod($name, [Reflection.BindingFlags]'Instance,Public,NonPublic')
    if ($method) { Show-Method $method }
}
