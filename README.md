# QStickerManager

QStickerManager is a Windows desktop sticker library built with WinUI 3 and the Windows App SDK. It keeps images, thumbnails, descriptions, and keywords together so a large sticker collection is easy to browse and reuse.

## Features

- Import stickers from image files, folders, ZIP archives, or QQ's local sticker folder.
- Search stickers and filter them by keywords.
- Add or edit descriptions and keywords, including batch keyword edits.
- Copy stickers to the clipboard, export individual files, or export a ZIP archive.
- Reorder stickers by dragging, move items to the front, or shuffle the library.
- Choose a custom library location and clear generated GIF cache files.

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11.
- .NET 8 SDK.
- Visual Studio 2022 with the **.NET desktop development** and **Windows application development** workloads, including the Windows App SDK tooling.

The project targets `net8.0-windows10.0.19041.0` and supports `x86`, `x64`, and `ARM64` builds.

## Build and run

1. Clone the repository and open `QStickerManager.slnx` in Visual Studio.
2. Restore NuGet packages when prompted.
3. Select `x64` (or another supported platform) and the `Debug` configuration.
4. Start the project with **F5**.

From a developer PowerShell, the project can also be built with:

```powershell
dotnet restore
dotnet build QStickerManager.csproj -c Debug -p:Platform=x64
```

## Library storage

The default library is stored in the app's local data folder under `QStickerManager`. Use **Settings** to choose another folder or move an existing library. The selected folder contains `meta.json`, a `stickers` directory, thumbnails, and any generated GIFs.

## QQ import

The QQ importer looks for local sticker data under `Documents\Tencent Files\<QQ user>\nt_qq\nt_data\Emoji\personal_emoji\Ori`. If QQ data is stored elsewhere, import the image files or their containing folder directly.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
