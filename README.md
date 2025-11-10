# M3U8 to MP4 Converter (WPF)

Desktop utility that downloads long-running HLS playlists (`.m3u8`) and remuxes them into a single `.mp4` file. Designed with 12-hour streams in mind, supports cancellation, detailed progress, and lets you choose between the built-in FFmpeg sequential downloader or the `N_m3u8DL-RE` multi-threaded engine for higher throughput VOD captures.

## Features
- Straightforward WPF interface (URL, output path, downloader selection, tool paths).
- FFmpeg sequential mode remuxes with `-c copy` to avoid unnecessary transcoding.
- Optional `N_m3u8DL-RE` mode fetches HLS segments in parallel before remuxing.
- Parses process output to surface elapsed / total time estimates when available.
- Remembers your output location, preferred tool paths, engine choice, and N_m3u8DL-RE preferences between sessions.
- Cancellation support and log viewer to inspect progress in real time.

## Prerequisites
- Windows 10/11.
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download) or Visual Studio 2022+ with .NET 8 desktop workload.
- FFmpeg 6.x+ Windows binaries. Download a static build (e.g. from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/)) and point the app to the extracted `ffmpeg.exe`. You can also drop `ffmpeg.exe` into `M3U8ConverterApp\ffmpeg\` to have it auto-detected.
- [`N_m3u8DL-RE`](https://github.com/nilaoda/N_m3u8DL-RE/releases) (optional, required only for the parallel downloader mode). Place `N_m3u8DL-RE.exe` somewhere accessible and browse to it inside the app.

## Getting Started
1. Open `M3U8ConverterApp.sln` in Visual Studio (or run `dotnet build M3U8ConverterApp.csproj` from `M3U8ConverterApp` once the SDK is installed).
2. Ensure FFmpeg is accessible on your machine (either on the PATH or browse to the executable inside the app).
3. Run the app:
   - Paste the `.m3u8` URL.
   - Choose an output `.mp4` file path.
   - Select a downloader (`FFmpeg (sequential)` or `N_m3u8DL-RE (parallel)`).
   - Browse to the required executables (`ffmpeg.exe` always, plus `N_m3u8DL-RE.exe` when using the parallel mode).
   - For `N_m3u8DL-RE`, adjust the thread count if needed—the app automatically grabs the best video/audio.
   - Press **Start** and wait for the conversion to finish.

For very long videos (e.g., 12-hour streams) the conversion may take time equal to the download duration. Keep the application open; you can monitor the FFmpeg log output inside the app.

## Notes & Tips
- The FFmpeg mode issues `ffmpeg -protocol_whitelist "file,http,https,tcp,tls,crypto" -c copy -bsf:a aac_adtstoasc`, which works for most HLS feeds without re-encoding.
- The `Aggressive HTTP` checkbox is only relevant for the FFmpeg engine (enables persistent/multiple requests switches).
- In `N_m3u8DL-RE` mode, segments are downloaded in parallel to a temporary directory and then remuxed; ensure both executables remain accessible until completion.
- Cancelling a run stops the active process and removes any partially written output file.
- Progress estimates differ by engine: FFmpeg shows timecodes when available, while `N_m3u8DL-RE` surfaces its console log lines. The app now forces `--auto-select`/`--select-video best`/`--select-audio best`/`--select-subtitle none` so downloads start immediately with the best quality.
- Manual track filters accept the same syntax as `N_m3u8DL-RE --select-video/--select-audio/--select-subtitle` (e.g. `res="1920*":for=best`, `lang=en`, `all`). Leave on “auto select” for the highest bitrate automatically.
