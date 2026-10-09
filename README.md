# Extract Audio from Video

A small WPF GUI application for Windows 11 that saves the first audio track of a local video file using `ffmpeg` as AAC audio in an M4A file.

This app is licensed under the MIT License.

## Requirements

- Windows 11
- .NET 10 SDK
- No separate FFmpeg installation is required. The application includes FFmpeg and FFprobe.

## Run

Run from the project folder:

```powershell
dotnet run
```

Select the video file, change the suggested output name if needed, and choose **Extract Audio**. The `.m4a` file is saved next to the video. If the file name is already in use, a free variant such as `Name (1).m4a` is created automatically.

The FFmpeg binaries and their LGPL license are in `tools/ffmpeg/win-x64`. Version and source details are recorded in that folder's README.
