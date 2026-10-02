$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$bin = Join-Path (Split-Path $PSScriptRoot) 'bin\Debug'
foreach ($file in Get-ChildItem $bin -Filter '*.dll') {
    try { [void][Reflection.Assembly]::LoadFrom($file.FullName) } catch [BadImageFormatException] { }
}
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'uYouWin.exe'))
$controls = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'Microsoft.Toolkit.Wpf.UI.Controls.dll'))
function Assert($condition, [string]$message) {
    if (!$condition) { throw ('FAIL: ' + $message) }
    Write-Output ('PASS: ' + $message)
}
$app = New-Object Windows.Application
$window = $null
$player = $null
try {
    $viewport = [Activator]::CreateInstance($assembly.GetType('uYouWin.Views.VideoViewport'))
    $element = [Activator]::CreateInstance($controls.GetType('Microsoft.Toolkit.Wpf.UI.Controls.MediaPlayerElement'))
    [void]$viewport.Children.Add($element)
    $window = New-Object Windows.Window
    $window.ShowInTaskbar = $false
    $window.ShowActivated = $false
    $window.Left = -2000
    $window.Top = -2000
    $window.Width = 800
    $window.Height = 500
    $window.Content = $viewport
    $window.Show()
    $window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    $window.UpdateLayout()
    $native = $element.GetUwpInternalObject()
    Assert ($null -ne $native) 'Real toolkit host exposes a native UWP MediaPlayerElement'
    $setPlayer = $native.GetType().GetMethod('SetMediaPlayer')
    $player = [Activator]::CreateInstance($setPlayer.GetParameters()[0].ParameterType)
    for ($i = 0; $i -lt 3; $i++) {
        [void]$setPlayer.Invoke($native, [object[]]@($player))
        [void]$setPlayer.Invoke($native, [object[]]@($null))
        [void]$setPlayer.Invoke($native, [object[]]@($null))
    }
    Assert $true 'Repeated native attach/detach accepts null without the toolkit wrapper exception'
    foreach ($size in @(@(640,480), @(1200,800), @(800,600))) {
        $window.Width = $size[0]
        $window.Height = $size[1]
        $window.UpdateLayout()
        $window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
        $window.UpdateLayout()
        Write-Output ('Host: ' + $element.ActualWidth + 'x' + $element.ActualHeight + '; Native: ' + $native.ActualWidth + 'x' + $native.ActualHeight)
        Assert ($native.ActualWidth -gt 0 -and $native.ActualHeight -gt 0) 'Native renderer has nonzero layout bounds'
        Assert ([Math]::Abs($native.ActualWidth - $element.ActualWidth) -lt 1 -and [Math]::Abs($native.ActualHeight - $element.ActualHeight) -lt 1) 'Actual native control fits its WPF host after resize'
        Assert ($native.Stretch.ToString() -eq 'Uniform') 'Actual native control uses Uniform scaling'
    }
    Write-Output 'Native surface checks passed; no media was opened and no application data was accessed.'
} catch {
    $exception = $_.Exception
    while ($exception) {
        Write-Output ($exception.GetType().FullName + ': ' + $exception.Message)
        $exception = $exception.InnerException
    }
    throw
} finally {
    if ($window) { $window.Close() }
    if ($player -is [IDisposable]) { $player.Dispose() }
    $app.Shutdown()
}
