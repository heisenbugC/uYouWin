# uYouWin  
A more Windows-native alternative of Freetube.  
  
* Dependencies:

  * .net Framework 4.8
  * Segoe MDL2 Assets Font
  * Windows 10 1903 or later (x64 for the installer)
  * Visual C++ x64 runtime (included by the bootstrapper)

## User storage

Settings, playlists, subscriptions and history are stored in `%LOCALAPPDATA%\uYouWin\Data`.
The application creates this folder for the current user; the installer does not seed it with files.
Existing user data is retained across upgrades and is never expired as cache.
Old executable-adjacent `Data` files are left untouched. To retain them, close the app and
copy the desired library/settings JSON files into the new folder without overwriting newer data.

Disposable metadata is stored in `%LOCALAPPDATA%\uYouWin\Cache`. The existing Newtonsoft.Json
format is used, with one hashed file per entry and streamed, on-demand reads rather than an
in-memory index or whole-cache deserialization. Entries expire 30 days after being written;
reads do not renew them. Expired entries are rejected on access, with background cleanup at
startup and periodically during use. The cache includes opened-video metadata and successful
API responses for searches, recommendations, channels, comments and playlist pages. API
responses are isolated by provider and credentials; keys and API credentials are not stored
as plaintext filenames. Connection tests bypass the cache. Video/audio bytes and expiring
signed playback URLs are not cached. Deleting `Cache` is safe; do not delete `Data` to clear it.

## Release and installer builds

Use VS2022 MSBuild with the .NET Framework 4.8 development tools, WiX 3.x and the VS2022 C++
redistributable build tools. This workspace's VS18 command-line installation does not load
the legacy NuGet package-resolution target correctly.

Build the app using `MSBuild uYouWin.csproj /restore /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64`.
`Directory.Build.props` and `Directory.Build.targets` configure Release/native dependencies
without changing the original application project configuration blocks. `yt-dlp.exe` and
`deno.exe` are copied beside `uYouWin.exe`; the complete `_internal` directory stays beside them.

Build `..\uYouWinInstaller\uYouWinInstaller.wixproj` or
`..\uYouWinBootStraper\uYouWinBootStraper.wixproj` with `/p:Configuration=Release /p:Platform=x86`.
The MSI itself targets x64; x86 is the existing WiX project/launcher configuration.
The imported `Installer` targets always build the app as Release x64 before staging and
harvesting its runtime files. The bootstrapper also builds the Release MSI and UI, embeds
the VC++ x64 redistributable, and supports downloading .NET 4.8 when required. Set
`VCRedistSource` if the redistributable is not in VS2022's default C++ redist folder.
The generated installer is `..\uYouWinBootStraper\bin\Release\uYouWin Installer.exe`.
Use it rather than the standalone MSI on machines without the VC++ runtime.
Reload the two WiX projects in Visual Studio after adding these imports; an already-open
solution can retain the old project configuration even when command-line builds succeed.
Build and MSI-payload checks do not replace a clean-machine installation smoke test.

No `Data`, `Cache`, debug symbols, signing keys or system fonts are harvested.
`Tests\Run-DiskCacheRegression.ps1` verifies cache behavior without live API calls;
`Tests\Test-InstallerPayload.ps1` inspects the generated MSI without installing it.
