$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
function Assert($condition, [string]$message) {
    if (!$condition) { throw ('FAIL: ' + $message) }
    Write-Output ('PASS: ' + $message)
}
[xml]$video = [IO.File]::ReadAllText((Join-Path $root 'Views\VideoPage.xaml'))
[xml]$overlay = [IO.File]::ReadAllText((Join-Path $root 'Views\FullScreenOverlayWindow.xaml'))
[xml]$subscriptions = [IO.File]::ReadAllText((Join-Path $root 'Views\SubsPage.xaml'))
$xamlNs = 'http://schemas.microsoft.com/winfx/2006/xaml'
$player = $video.SelectSingleNode('//*[local-name()="MediaPlayerElement"]')
Assert ($player.ParentNode.LocalName -eq 'VideoViewport') 'Native video receives finite viewport layout'
Assert (!$player.HasAttribute('Width') -and !$player.HasAttribute('Height')) 'No ActualWidth feedback bindings control the native host'
Assert ($player.GetAttribute('Stretch') -eq 'Uniform') 'Video scaling preserves the whole frame'
$code = [IO.File]::ReadAllText((Join-Path $root 'Views\VideoPage.xaml.cs'))
Assert (!$code.Contains('nativePlayer.Width') -and !$code.Contains('RasterizationScale')) 'Toolkit retains ownership of native DPI layout'
Assert (!$code.Contains('VideoPlayerElement.SetMediaPlayer(null)') -and $code.Contains('nativeSurface?.SetMediaPlayer(null)') -and
        $code.Contains('_surfaceOwner?.DetachVideoSurface()')) 'Detachment uses the null-safe native API rather than the toolkit wrapper'
$viewportCode = [IO.File]::ReadAllText((Join-Path $root 'Views\VideoViewport.cs'))
Assert ($viewportCode.Contains('SetNativeBounds(native, size)') -and $viewportCode.Contains('SetNativeBounds(presenter, size)') -and
        $viewportCode.Contains('presenter.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform')) 'Both native element and presenter are bounded with uniform scaling'
foreach ($icon in $overlay.SelectNodes('//*[local-name()="FontIcon"]')) {
    Assert ($icon.GetAttribute('FontSize') -eq '16') 'Player Segoe glyph size is 16 pixels'
}

$buttons = @($overlay.SelectNodes('//*[local-name()="Button"]'))
Assert ($buttons.Count -eq 10) 'Both playback modes share the complete control set'
foreach ($button in $buttons) {
    if ($null -ne $button.SelectSingleNode('./*[local-name()="Image"]')) {
        Assert ($button.GetAttribute('Style') -eq '{StaticResource VideoIconButtonStyle}') ($button.GetAttribute('Click') + ' uses borderless custom-icon styling')
    }
    $expected = if ($button.GetAttribute('Click') -eq 'PlayPause_Click') { '45' } else { '41' }
    Assert ($button.GetAttribute('Width') -eq $expected -and $button.GetAttribute('Height') -eq $expected) ($button.GetAttribute('Click') + ' has size increased by five')
}
$expectedMargins = @{
    PlayPause_Click = '9,0,0,0'; SeekForward_Click = '9,0,0,0'; Next_Click = '13,0,0,0';
    Subtitle_Click = '5,0,0,0'; Volume_Click = '5,0,0,0'; Info_Click = '5,0,0,0';
    Playlist_Click = '13,0,0,0'; FullScreen_Click = '9,0,0,0'
}
foreach ($button in $buttons) {
    $click = $button.GetAttribute('Click')
    if ($expectedMargins.ContainsKey($click)) {
        Assert ($button.GetAttribute('Margin') -eq $expectedMargins[$click]) ($click + ' has gap increased by five')
    }
}
$seekBack = $overlay.SelectSingleNode('//*[@Click="SeekBack_Click"]')
Assert ($seekBack.ParentNode.GetAttribute('Margin') -eq '9,0,0,0') 'Previous/seek button group gap increased by five'
$ring = $overlay.SelectSingleNode('//*[local-name()="ProgressRing"]')
Assert ($null -ne $ring -and $ring.GetAttribute('IsActive') -eq 'False') 'ModernWpf loading ring exists and starts inactive'
$overlayCode = [IO.File]::ReadAllText((Join-Path $root 'Views\FullscreenOverlayWindow.xaml.cs'))
Assert ($overlayCode.Contains('playback.IsOpening') -and $overlayCode.Contains('MediaPlaybackState.Opening') -and
        $overlayCode.Contains('MediaPlaybackState.Buffering') -and $overlayCode.Contains('LoadingRing.IsActive = loading && IsVisible')) 'Loading animation follows resolve/open/buffer state and window visibility'
$style = $video.SelectSingleNode('//*[local-name()="Style" and @TargetType="TextBlock"]/*[local-name()="Setter" and @Property="Foreground"]')
Assert ($style.GetAttribute('Value') -eq '{DynamicResource SystemControlForegroundBaseHighBrush}') 'Video text responds to runtime theme changes'
$title = $subscriptions.SelectSingleNode('//*[local-name()="TextBlock" and @Text="Subscriptions"]')
Assert ($title.GetAttribute('Foreground') -eq '{DynamicResource SystemControlForegroundBaseHighBrush}') 'Subscriptions heading has theme contrast'
$channel = $subscriptions.SelectSingleNode('//*[local-name()="TextBlock" and @Text="{Binding ChannelName}"]')
Assert ($null -ne $channel -and $channel.GetAttribute('Foreground') -eq '{DynamicResource SystemControlForegroundBaseHighBrush}') 'Subscriptions explicitly display channel names with theme contrast'

# Exercise the compiled viewport panel using a harmless stand-in for the native host.
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin\Debug\uYouWin.exe'))
[xml]$layoutDocument = $video.CloneNode($true)
$layout = $layoutDocument.SelectSingleNode('//*[local-name()="Grid" and @*[local-name()="Name"]="PageLayout"]')
$layout.SetAttribute('xmlns:local', 'clr-namespace:uYouWin.Views;assembly=uYouWin')
$layout.RemoveAttribute('Background')
foreach ($node in @($layout.SelectNodes('./*[local-name()="ScrollViewer"]'))) { [void]$layout.RemoveChild($node) }
$viewport = $layout.SelectSingleNode('./*[local-name()="Grid"]')
foreach ($attribute in @('MouseMove','MouseLeave','MouseLeftButtonDown','SizeChanged')) { $viewport.RemoveAttribute($attribute) }
foreach ($node in @($viewport.SelectNodes('./*[local-name()="Popup"]'))) { [void]$viewport.RemoveChild($node) }
$native = $viewport.SelectSingleNode('.//*[local-name()="MediaPlayerElement"]')
$standIn = $layoutDocument.CreateElement('Border', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
[void]$standIn.SetAttribute('Name', $xamlNs, 'VideoPlayerElement')
[void]$native.ParentNode.ReplaceChild($standIn, $native)
$markup = $layout.OuterXml.Replace('clr-namespace:uYouWin.Views"', 'clr-namespace:uYouWin.Views;assembly=uYouWin"')
$grid = [Windows.Markup.XamlReader]::Parse($markup)
foreach ($size in @(@(640,480), @(1280,720), @(800,600), @(3840,2160))) {
    $grid.Measure([Windows.Size]::new($size[0], $size[1]))
    $grid.Arrange([Windows.Rect]::new(0,0,$size[0],$size[1]))
    $grid.UpdateLayout()
    $frame = $grid.FindName('RootGrid')
    $playerHost = $grid.FindName('VideoPlayerElement')
    Assert ([Math]::Abs($frame.ActualHeight - $size[1] * 2 / 3) -lt 1) ('Non-fullscreen frame stays in its allocated row at ' + ($size -join 'x'))
    Assert ([Math]::Abs($playerHost.ActualHeight - $frame.ActualHeight) -lt 1 -and
            [Math]::Abs($playerHost.ActualWidth - $frame.ActualWidth) -lt 1) 'Host dimensions match the measured frame'
}
Write-Output 'All presentation regression checks passed. Native video rendering still requires an interactive playback test.'
