param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\uYouWin.exe'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path $AssemblyPath).Path
$bin = Split-Path $AssemblyPath
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$temp = Join-Path ([IO.Path]::GetTempPath()) ('uYouWin-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
$pathType = $assembly.GetType('uYouWin.Services.Storage.DataPathProvider', $true)
$field = $pathType.GetField('_dataDirectory', [Reflection.BindingFlags]'Static,NonPublic')
$previous = $field.GetValue($null)
$data = Join-Path $temp 'Data'
[void][IO.Directory]::CreateDirectory($data)
$field.SetValue($null, $data)
function Assert($condition, [string]$message) {
    if (!$condition) { throw ('FAIL: ' + $message) }
    Write-Output ('PASS: ' + $message)
}
function Call-Static([string]$type, [string]$method, [object[]]$arguments) {
    $t = $assembly.GetType($type, $true)
    $m = $t.GetMethod($method, [Reflection.BindingFlags]'Public,NonPublic,Static')
    return $m.Invoke($null, $arguments)
}
function Import-CsvFixture([string]$method, [string]$csv) {
    $path = Join-Path $temp ($method + '.csv')
    [IO.File]::WriteAllText($path, $csv, [Text.Encoding]::UTF8)
    $task = $importer.GetType().GetMethod($method).Invoke($importer, [object[]]@([string]$path, [Threading.CancellationToken]::None))
    return ,$task.GetAwaiter().GetResult()
}
try {
    $normalizer = 'uYouWin.Services.Cache.QueryNormalizer'
    $a = Call-Static $normalizer 'Normalize' @("  HeLLo `t World  ")
    $b = Call-Static $normalizer 'Normalize' @('hello world')
    Assert ($a -eq $b) 'Search keys ignore case and repeated whitespace'
    $one = Call-Static $normalizer 'CacheKey' @($a, 'page-A')
    $two = Call-Static $normalizer 'CacheKey' @($a, 'page-B')
    Assert ($one -ne $two) 'Search pages have distinct cache keys'

    $cache = 'uYouWin.Services.Cache.VisitedUrlCache'
    $id = Call-Static $cache 'VideoIdFromUrl' @('https://youtu.be/aBcDeFgHiJk?t=12')
    Assert ($id -ceq 'aBcDeFgHiJk') 'Short URLs retain case-sensitive video IDs'
    $id = Call-Static $cache 'VideoIdFromUrl' @('https://www.youtube.com/watch?list=PL1&v=aBcDeFgHiJk&t=12')
    Assert ($id -ceq 'aBcDeFgHiJk') 'Watch URLs ignore unrelated query parameters'
    $id = Call-Static $cache 'VideoIdFromUrl' @('https://example.org/watch?v=aBcDeFgHiJk')
    Assert ($null -eq $id) 'Non-YouTube hosts are rejected'
    $videoType = $assembly.GetType('uYouWin.Models.Video', $true)
    foreach ($key in @('aBcDeFgHiJk', 'abcdefghijk')) {
        $video = [Activator]::CreateInstance($videoType)
        $video.Id = $key
        $video.Title = $key
        $video.YtUrl = 'https://www.youtube.com/watch?v=' + $key
        Call-Static $cache 'Save' @($video)
    }
    $first = Call-Static $cache 'TryGet' @('aBcDeFgHiJk')
    $second = Call-Static $cache 'TryGet' @('abcdefghijk')
    Assert ($first.Id -cne $second.Id) 'Visited metadata does not merge IDs that differ by case'

    $page = [Activator]::CreateInstance($assembly.GetType('uYouWin.Models.VideoSearchPage', $true))
    $page.NextPageToken = 'page-B'
    $page.Videos.Add($first)
    Call-Static 'uYouWin.Services.Cache.SearchResultCache' 'Save' @($one, $page)
    $cached = Call-Static 'uYouWin.Services.Cache.SearchResultCache' 'TryGet' @($one)
    Assert ($cached.Videos.Count -eq 1 -and $cached.NextPageToken -eq 'page-B') 'Search cache round-trips results and continuation token'

    $importer = [Activator]::CreateInstance($assembly.GetType('uYouWin.Services.Archive.GoogleTakeoutImporter', $true), $true)
    $subs = Import-CsvFixture 'ImportSubscriptionsAsync' @'
Channel Id,Channel Url,Channel Title
UCsample,https://www.youtube.com/channel/UCsample,"A, B channel"
'@
    Assert ($subs.Count -eq 1 -and $subs[0].ChannelName -eq 'A, B channel') 'Takeout subscriptions handle quoted commas'
    $history = Import-CsvFixture 'ImportHistoryAsync' @'
Video ID,Title,Time
 aBcDeFgHiJk,"First line
Second ""quoted"" line",2024-01-02T03:04:05Z
'@
    Assert ($history.Count -eq 1 -and $history[0].Title.Contains("`n") -and $history[0].Title.Contains('"quoted"')) 'Watch history supports multiline quoted fields'
    $playlists = Import-CsvFixture 'ImportPlaylistsAsync' @'
Playlist Id,Title,Description
PLtest,Imported playlist,Description

Video Id,Time Added
aBcDeFgHiJk,2024-01-02T03:04:05Z
abcdefghijk,2024-01-02T03:04:06Z
'@
    Assert ($playlists.Count -eq 1 -and $playlists[0].Videos.Count -eq 2 -and $playlists[0].Title -eq 'Imported playlist') 'Playlist preamble and video table are recognized'
    $searches = Import-CsvFixture 'ImportSearchHistoryAsync' @'
Query,Time
"hello, world",2024-01-02T03:04:05Z
'@
    Assert ($searches.Count -eq 1 -and $searches[0].Query -eq 'hello, world') 'Search-history CSV preserves search text'

    $library = [Activator]::CreateInstance($assembly.GetType('uYouWin.Services.Library.LibraryService', $true), $true)
    [void]$library.ImportSubscriptionsAsync($subs).GetAwaiter().GetResult()
    [void]$library.ImportSubscriptionsAsync($subs).GetAwaiter().GetResult()
    Assert ($library.GetSubscriptionsAsync().Result.Count -eq 1) 'Reimporting subscriptions does not duplicate them'
    [void]$library.ImportPlaylistsAsync($playlists).GetAwaiter().GetResult()
    Assert ($library.GetPlaylistsAsync().Result[0].Videos.Count -eq 2) 'Playlist order and videos persist'

    $rows = New-Object Text.StringBuilder
    [void]$rows.AppendLine('Video ID,Title,Time')
    for ($i = 0; $i -lt 600; $i++) {
        [void]$rows.AppendLine(('aBcDeFgHiJk,Video,' + ([DateTime]::new(2024,1,1)).AddSeconds($i).ToString('o')))
    }
    $many = Import-CsvFixture 'ImportHistoryAsync' $rows.ToString()
    [void]$library.ImportHistoryAsync($many).GetAwaiter().GetResult()
    [void]$library.ImportHistoryAsync($many).GetAwaiter().GetResult()
    Assert ($library.GetHistoryAsync().Result.Count -eq 600) 'History imports retain all records and deduplicate repeat imports'
    Assert ([IO.File]::Exists((Join-Path $data 'watch-history.json.bak'))) 'Atomic history writes retain a backup'
    $invalid = $false
    try { [void](Import-CsvFixture 'ImportHistoryAsync' "Video ID,Title`naBcDeFgHiJk,`"unterminated") }
    catch { $invalid = $true }
    Assert $invalid 'Malformed CSV is reported instead of silently imported'
    Write-Output 'All feature regression checks passed.'
}
finally {
    $field.SetValue($null, $previous)
    if ([IO.Directory]::Exists($temp)) { [IO.Directory]::Delete($temp, $true) }
}
