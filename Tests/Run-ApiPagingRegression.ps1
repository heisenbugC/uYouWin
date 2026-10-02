$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Net.Http
$root = Split-Path $PSScriptRoot
$bin = Join-Path $root 'bin\Release'
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'Newtonsoft.Json.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'uYouWin.exe'))
Add-Type -ReferencedAssemblies System.Net.Http -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class PagingTestHandler : HttpMessageHandler
{
    public readonly Queue<string> Responses = new Queue<string>();
    public readonly List<string> Requests = new List<string>();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri.ToString());
        if (Responses.Count == 0) throw new InvalidOperationException("Unexpected API request");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Responses.Dequeue()) });
    }
}
'@
function Assert($condition, [string]$message) {
    if (!$condition) { throw ('FAIL: ' + $message) }
    Write-Output ('PASS: ' + $message)
}
function New-Instance([string]$name) { return [Activator]::CreateInstance($assembly.GetType($name, $true), $true) }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('uYouWin-api-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
$dataField = $assembly.GetType('uYouWin.Services.Storage.DataPathProvider').GetField('_dataDirectory', [Reflection.BindingFlags]'NonPublic,Static')
$data = Join-Path $temp 'Data'
[void][IO.Directory]::CreateDirectory($data)
$dataField.SetValue($null, $data)
$handler = New-Object PagingTestHandler
$http = [Net.Http.HttpClient]::new($handler)
try {
    $settings = New-Instance 'uYouWin.Models.YouTubeApiSettings'
    $settings.ApiKey = 'test-only'
    $settings.BaseUrl = 'https://example.invalid/youtube/v3/'
    $clientType = $assembly.GetType('uYouWin.Services.YouTube.YouTubeApiClient')
    $client = $clientType.GetConstructors()[0].Invoke([object[]]@($http, $settings, $true))
    $serviceType = $assembly.GetType('uYouWin.Services.YouTube.YouTubeApiService')
    $service = $serviceType.GetConstructors()[0].Invoke([object[]]@($client))
    $appType = $assembly.GetType('uYouWin.App')
    $appType.GetProperty('YouTubeApiService', [Reflection.BindingFlags]'NonPublic,Static').SetValue($null, $service)
    $appType.GetProperty('LibraryService', [Reflection.BindingFlags]'NonPublic,Static').SetValue($null, (New-Instance 'uYouWin.Services.Library.LibraryService'))
    [IO.File]::WriteAllText((Join-Path $data 'app-preferences.json'), '{"RecommendationRegion":"GB"}')
    $handler.Responses.Enqueue('{"nextPageToken":"POPULAR-2","items":[{"id":"aBcDeFgHiJk","snippet":{"title":"Popular one"},"contentDetails":{"duration":"PT2M"}}]}')
    $handler.Responses.Enqueue('{"items":[{"id":"aBcDeFgHiJk","snippet":{"title":"Popular one"}},{"id":"abcdefghijk","snippet":{"title":"Popular two"}}]}')
    $homeViewModel = New-Instance 'uYouWin.ViewModels.HomePageViewModel'
    $homeViewModel.LoadRecommendationsAsync().GetAwaiter().GetResult()
    Assert ($homeViewModel.Videos.Count -eq 1 -and $homeViewModel.HasMoreResults) 'Recommendations preserve the next-page token'
    $homeViewModel.LoadMoreAsync().GetAwaiter().GetResult()
    Assert ($homeViewModel.Videos.Count -eq 2 -and !$homeViewModel.HasMoreResults) 'Recommendation pages append without duplicates and stop at the end'
    Assert ($handler.Requests[1].Contains('/videos?') -and $handler.Requests[1].Contains('chart=mostPopular') -and $handler.Requests[1].Contains('pageToken=POPULAR-2')) 'Recommendation load-more uses videos.list rather than search'
    Assert ($handler.Requests[0].Contains('regionCode=GB') -and $handler.Requests[1].Contains('regionCode=GB')) 'Recommendation paging preserves the region'
    Assert ($homeViewModel.HeaderText -eq 'Recommendations') 'Recommendation paging retains its heading'
    $count = $handler.Requests.Count
    $homeViewModel.LoadMoreAsync().GetAwaiter().GetResult()
    Assert ($handler.Requests.Count -eq $count) 'Exhausted recommendations do not request another page'

    $handler.Responses.Enqueue('{"nextPageToken":"SEARCH-2","items":[{"id":{"videoId":"12345678901"},"snippet":{"title":"Search one"}}]}')
    $handler.Responses.Enqueue('{"items":[{"id":"12345678901","contentDetails":{"duration":"PT1M"}}]}')
    $handler.Responses.Enqueue('{"items":[{"id":{"videoId":"12345678902"},"snippet":{"title":"Search two"}}]}')
    $handler.Responses.Enqueue('{"items":[{"id":"12345678902","contentDetails":{"duration":"PT1M"}}]}')
    $homeViewModel.SearchAsync('sample query').GetAwaiter().GetResult()
    $homeViewModel.LoadMoreAsync().GetAwaiter().GetResult()
    Assert ($homeViewModel.Videos.Count -eq 2 -and $homeViewModel.HeaderText -eq 'Search results' -and !$homeViewModel.HasMoreResults) 'Search still replaces recommendations and pages correctly'
    Assert ($handler.Requests[4].Contains('/search?') -and $handler.Requests[4].Contains('pageToken=SEARCH-2')) 'Search continuation uses the search endpoint'

    $handler.Responses.Enqueue('{"items":[{"snippet":{"title":"Example channel","description":"About this channel","customUrl":"@example","country":"GB","publishedAt":"2020-01-02T00:00:00Z"},"statistics":{"subscriberCount":"12300","videoCount":"40","viewCount":"456000","hiddenSubscriberCount":false},"contentDetails":{"relatedPlaylists":{"uploads":"UUexample"}}}]}')
    $token = [Threading.CancellationToken]::None
    $channel = $service.GetChannelAsync('UCexample', $token).GetAwaiter().GetResult()
    Assert ($channel.SubscriberCount -eq 12300 -and $channel.VideoCount -eq 40 -and $channel.ViewCount -eq 456000) 'Channel statistics parse public API counts'
    Assert ($channel.Country -eq 'GB' -and $channel.CustomUrl -eq '@example' -and $channel.PublishedAt.Year -eq 2020) 'Channel About metadata is available'
    Assert ($channel.UploadsPlaylistId -eq 'UUexample') 'Channel uploads playlist is retained'
    $handler.Responses.Enqueue('{"nextPageToken":"UPLOADS-2","items":[{"snippet":{"title":"Upload","resourceId":{"videoId":"aBcDeFgHiJk"},"videoOwnerChannelId":"UCexample","videoOwnerChannelTitle":"Example channel"}}]}')
    $handler.Responses.Enqueue('{"items":[{"id":"aBcDeFgHiJk","contentDetails":{"duration":"PT3M"}}]}')
    $page = $service.GetChannelVideosPageAsync('UCexample', $null, $token).GetAwaiter().GetResult()
    Assert ($page.Videos.Count -eq 1 -and $page.NextPageToken -eq 'UPLOADS-2') 'Channel uploads return a continuation token'
    Assert ($handler.Requests[$handler.Requests.Count - 2].Contains('playlistId=UUexample')) 'Channel videos come from the uploads playlist'
    $handler.Responses.Enqueue('{"items":[]}')
    $page = $service.GetChannelVideosPageAsync('UCexample', 'UPLOADS-2', $token).GetAwaiter().GetResult()
    Assert ($page.Videos.Count -eq 0 -and !$page.NextPageToken) 'Channel upload pagination handles an empty final page'
    Assert ($handler.Requests[$handler.Requests.Count - 1].Contains('pageToken=UPLOADS-2')) 'Channel continuation token is forwarded'
    Assert ($handler.Responses.Count -eq 0) 'All expected mock responses were consumed'
    Write-Output 'All API pagination checks passed.'
} finally {
    $http.Dispose()
    $dataField.SetValue($null, $null)
    [IO.Directory]::Delete($temp, $true)
}
