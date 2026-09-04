# SnapRestore

SnapRestore is an Avalonia desktop application that reconstructs media from a
Snapchat memories export. It combines overlay assets with their source media
and applies capture time and location metadata from `memories_history.json`.

## Development

Requirements:

- .NET 10 SDK
- FFmpeg and ExifTool on `PATH`, or the matching binaries under
  `SnapRestore/Tools/<runtime-id>/`

Run the application with:

```sh
dotnet run --project SnapRestore/SnapRestore.csproj
```

Run the automated tests with:

```sh
dotnet test SnapRestore.slnx
```

## Supported input

Select the extracted `memories` directory and an output folder. The corresponding
`memories_history.json` is optional: when supplied, SnapRestore restores capture
dates and locations; without it, SnapRestore restores the media and overlays but
skips metadata matching and writing.
