param([string]$AssemblyPath = "$PSScriptRoot\..\bin\Release\uYouWin.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$AssemblyPath = (Resolve-Path $AssemblyPath).Path
[void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path $AssemblyPath) 'Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$flags = [Reflection.BindingFlags]'Public,NonPublic,Static'
$paths = $assembly.GetType('uYouWin.Services.Storage.DataPathProvider', $true)
$field = $paths.GetField('_dataDirectory', $flags)
$old = $field.GetValue($null)
$temp = Join-Path ([IO.Path]::GetTempPath()) ('uYouWin-cache-' + [Guid]::NewGuid().ToString('N'))
$data = Join-Path $temp 'Data'
[void][IO.Directory]::CreateDirectory($data)
$field.SetValue($null, $data)
$disk = $assembly.GetType('uYouWin.Services.Cache.DiskCache', $true)
$disk.GetField('_nextCleanupUtc', $flags).SetValue($null, [DateTime]::MaxValue)
$videoType = $assembly.GetType('uYouWin.Models.Video', $true)
$save = $disk.GetMethod('Save', $flags).MakeGenericMethod($videoType)
$get = $disk.GetMethod('TryGet', $flags).MakeGenericMethod($videoType)
function Assert($ok, [string]$message) { if (!$ok) { throw "FAIL: $message" }; "PASS: $message" }
function CachePath([string]$key) { $disk.GetMethod('GetPath', $flags).Invoke($null, @('test', $key)) }
function SaveVideo([string]$key) {
    $v = [Activator]::CreateInstance($videoType); $v.Id = $key; $v.Title = $key
    [void]$save.Invoke($null, @('test', $key, $v))
}
function GetVideo([string]$key) { $get.Invoke($null, @('test', $key)) }
Add-Type -ReferencedAssemblies System.Net.Http -TypeDefinition @'
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class DiskCacheHandler : HttpMessageHandler
{
    public int Calls;
    public bool Fail;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.Forbidden : HttpStatusCode.OK)
        { Content = new StringContent(Fail ? "{\"error\":{}}" : "{\"items\":[],\"nextPageToken\":\"NEXT\"}") });
    }
}
'@
$handler = New-Object DiskCacheHandler
$http = [Net.Http.HttpClient]::new($handler)
function NewClient([string]$url, [string]$key, [bool]$enabled = $true) {
    $settings = [Activator]::CreateInstance($assembly.GetType('uYouWin.Models.YouTubeApiSettings'))
    $settings.BaseUrl = $url; $settings.ApiKey = $key
    $assembly.GetType('uYouWin.Services.YouTube.YouTubeApiClient').GetConstructors()[0].Invoke(@($http, $settings, $enabled))
}
function Request($client, [string]$endpoint, [string]$query) {
    $task = $client.GetAsync($endpoint, $query, [Threading.CancellationToken]::None)
    [void]$task.GetAwaiter().GetResult()
}
try {
    Assert ($paths.GetProperty('CacheDirectory', $flags).GetValue($null) -eq (Join-Path $temp 'Cache')) 'Data and Cache are separate sibling directories'
    SaveVideo 'aBcDeFgHiJk'; SaveVideo 'abcdefghijk'
    Assert ((GetVideo 'aBcDeFgHiJk').Id -cne (GetVideo 'abcdefghijk').Id) 'Hashed cache paths preserve ID case'
    Assert ((Get-ChildItem (Join-Path $temp 'Cache') -Recurse -Filter *.json).Count -eq 2) 'Each value has an independent disk entry'
    $path = CachePath 'aBcDeFgHiJk'
    $json = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    $json.SavedAtUtc = [DateTime]::UtcNow.AddDays(-29).ToString('o')
    [IO.File]::WriteAllText($path, ($json | ConvertTo-Json -Depth 20))
    $before = [IO.File]::ReadAllText($path)
    Assert ($null -ne (GetVideo 'aBcDeFgHiJk')) '29-day-old cache entries remain valid'
    Assert ([IO.File]::ReadAllText($path) -eq $before) 'Reads do not renew the TTL'
    $json.SavedAtUtc = [DateTime]::UtcNow.AddDays(-30).AddSeconds(-1).ToString('o')
    [IO.File]::WriteAllText($path, ($json | ConvertTo-Json -Depth 20))
    Assert ($null -eq (GetVideo 'aBcDeFgHiJk') -and !(Test-Path $path)) '30-day-old entries expire and are deleted on access'
    SaveVideo 'aBcDeFgHiJk'
    [IO.File]::WriteAllText((CachePath 'abcdefghijk'), '{broken')
    Assert ($null -ne (GetVideo 'aBcDeFgHiJk')) 'Reading one entry does not deserialize unrelated files'
    Assert ($null -eq (GetVideo 'abcdefghijk')) 'Corrupt cache entries are treated as misses'
    SaveVideo 'abcdefghijk'
    Assert ($null -ne (GetVideo 'abcdefghijk')) 'Atomic writes repair corrupt entries'
    [IO.File]::SetLastWriteTimeUtc((CachePath 'abcdefghijk'), [DateTime]::UtcNow.AddDays(-31))
    $history = Join-Path $data 'watch-history.json'
    [IO.File]::WriteAllText($history, '[]')
    [IO.File]::SetLastWriteTimeUtc($history, [DateTime]::UtcNow.AddDays(-100))
    [void]$disk.GetMethod('RemoveExpired', $flags).Invoke($null, @())
    Assert (!(Test-Path (CachePath 'abcdefghijk')) -and (Test-Path $history)) 'Cleanup removes old cache without expiring user data'
    Assert (@(Get-ChildItem (Join-Path $temp 'Cache') -Recurse -Filter *.tmp).Count -eq 0) 'Writes leave no temporary files'

    $client = NewClient 'https://one.invalid/v3/' 'secret-A'
    foreach ($endpoint in @('videos','search','channels','playlistItems','commentThreads')) {
        Request $client $endpoint 'part=snippet'
        Request $client $endpoint 'part=snippet'
    }
    Assert ($handler.Calls -eq 5) 'Successful video/search/channel/playlist/comment responses avoid repeated API calls'
    $second = NewClient 'https://one.invalid/v3/' 'secret-A'
    Request $second 'videos' 'part=snippet'
    Assert ($handler.Calls -eq 5) 'A new client reuses persisted disk responses'
    Request $second 'videos' 'part=snippet&pageToken=NEXT'
    Request (NewClient 'https://two.invalid/v3/' 'secret-A') 'videos' 'part=snippet'
    Request (NewClient 'https://one.invalid/v3/' 'secret-B') 'videos' 'part=snippet'
    Assert ($handler.Calls -eq 8) 'Continuation tokens, providers and credentials isolate API entries'
    Request (NewClient 'https://one.invalid/v3/' 'secret-A' $false) 'videos' 'part=snippet'
    Assert ($handler.Calls -eq 9) 'Connection testing bypasses cached responses'
    $handler.Fail = $true
    1..2 | ForEach-Object { try { Request $client 'videos' 'id=failure' } catch { } }
    Assert ($handler.Calls -eq 11) 'Failed API requests are never cached'
    $cts = New-Object Threading.CancellationTokenSource
    $cts.Cancel(); $cancelled = $false
    try { [void]$client.GetAsync('videos', 'part=snippet', $cts.Token).GetAwaiter().GetResult() } catch { $cancelled = $true }
    $cts.Dispose()
    Assert ($cancelled -and $handler.Calls -eq 11) 'Cancellation is respected even on a cache hit'
    $secrets = Get-ChildItem (Join-Path $temp 'Cache') -Recurse -File | Select-String 'secret-A|secret-B'
    Assert (!$secrets) 'API credentials are absent from cache payloads'
    $root = Split-Path $PSScriptRoot
    foreach ($name in @('HomePage','ChannelPage','HistoryPage','PlaylistPage','VideoPage','FullScreenOverlayWindow')) {
        $xaml = [IO.File]::ReadAllText((Join-Path $root "Views\$name.xaml"))
        Assert ($xaml.Contains('<local:AddToPlaylistButton')) "$name has a per-entry playlist action"
    }
    foreach ($name in @('HistoryPage','PlaylistPage')) {
        [xml]$xaml = [IO.File]::ReadAllText((Join-Path $root "Views\$name.xaml"))
        $heading = $xaml.SelectSingleNode('//*[local-name()="TextBlock" and @FontSize="28"]')
        Assert ($heading.GetAttribute('Grid.Row') -eq '0' -and $heading.GetAttribute('Visibility') -eq 'Visible') "$name has a dedicated visible title row"
    }
    'All disk-cache and entry UI checks passed.'
}
finally {
    $http.Dispose()
    $field.SetValue($null, $old)
    [IO.Directory]::Delete($temp, $true)
}
