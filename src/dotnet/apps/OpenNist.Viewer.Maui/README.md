# OpenNist.Viewer.Maui

Minimal MAUI desktop viewer for opening WSQ and common image formats, previewing them, and converting between WSQ and standard image files.

## Current scope

- Target: `net10.0-maccatalyst`
- WSQ encode/decode: `OpenNist.Wsq`
- Other image formats: native Apple image codecs (`UIKit`, `CoreGraphics`, `ImageIO`)

## Build prerequisite

This project requires the .NET MAUI workload on the local machine:

```bash
dotnet workload install maui
```

This app is included in the main `OpenNist.slnx` solution.
