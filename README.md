# Extract Audio from Video

A small WPF application for Windows 11 that saves the first audio track of a local video file using `ffmpeg` as AAC audio in an M4A file.

This app is licensed under the MIT License.

## Requirements

- Windows 11
- .NET 10 SDK
- `ffmpeg` and `ffprobe`, installed and available via `PATH` (both are typically part of the same FFmpeg installation)

## Run

Run from the project folder:

```powershell
dotnet run
```

Select the video file, change the suggested output name if needed, and choose **Extract Audio**. The `.m4a` file is saved next to the video. If the file name is already in use, a free variant such as `Name (1).m4a` is created automatically.
