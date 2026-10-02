$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$bin = Join-Path (Split-Path $PSScriptRoot) 'bin\Debug'
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'ModernWpf.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'ModernWpf.Controls.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'uYouWin.exe'))
[Windows.Application]::ResourceAssembly = $assembly
$app = [Activator]::CreateInstance($assembly.GetType('uYouWin.App'))
$app.InitializeComponent()
try {
    foreach ($theme in @('Light','Dark')) {
        [ModernWpf.ThemeManager]::Current.ApplicationTheme = [Enum]::Parse([ModernWpf.ApplicationTheme], $theme)
        $page = [Activator]::CreateInstance($assembly.GetType('uYouWin.Views.SubsPage'))
        $page.Measure([Windows.Size]::new(900,600))
        $page.Arrange([Windows.Rect]::new(0,0,900,600))
        $page.UpdateLayout()
        $title = $page.FindName('SubscriptionsTitle')
        Write-Output ('Theme: ' + $theme + '; title: ' + $title.Text + '; size: ' + $title.ActualWidth + 'x' + $title.ActualHeight)
        foreach ($key in @('SystemControlForegroundBaseHighBrush','SystemControlPageBackgroundChromeLowBrush','SystemControlBackgroundAltHighBrush','SystemControlBackgroundChromeMediumLowBrush')) {
            $resource = $page.TryFindResource($key)
            Write-Output ($key + ' = ' + $(if ($null -eq $resource) {'MISSING'} else {$resource.ToString()}))
        }
        Write-Output ('Title foreground: ' + $title.Foreground + '; Page background: ' + $page.Background)
        if ($title.ActualHeight -le 0 -or $title.Visibility -ne 'Visible' -or $title.Foreground.ToString() -eq $page.Background.ToString()) {
            throw 'Subscription heading has no visible layout or contrast.'
        }
        if ([Windows.Controls.Grid]::GetRow($title) -ne 0) { throw 'Subscription heading must occupy its own top row.' }
    }
    $overlay = [Activator]::CreateInstance($assembly.GetType('uYouWin.Views.FullScreenOverlayWindow'))
    $overlayRoot = $overlay.FindName('Root')
    $overlayRoot.Measure([Windows.Size]::new(900,500))
    $overlayRoot.Arrange([Windows.Rect]::new(0,0,900,500))
    $overlayRoot.UpdateLayout()
    $loading = $overlay.FindName('LoadingIndicator')
    if ($loading.Visibility -ne 'Collapsed') { throw 'Loading panel must start collapsed.' }
    if ($overlay.Background.Color.A -ne 0 -or $overlayRoot.Background.Color.A -gt 1) { throw 'Overlay must not have an opaque frame-sized background.' }
    $loading.Visibility = [Windows.Visibility]::Visible
    $overlayRoot.UpdateLayout()
    if ($loading.ActualWidth -ge 450 -or $loading.ActualHeight -ge 250) { throw 'Loading panel unexpectedly fills the video frame.' }
    Write-Output 'PASS: Compiled overlay has transparent frame background and bounded loading panel.'
    $overlay.Close()
} finally { $app.Shutdown() }
