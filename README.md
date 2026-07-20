# CFX Release Tool

Windows desktop app for FiveM developers — build **escrow** and **open-source** ZIPs locally, manage `escrow_ignore`, bump versions, and upload to the CFX Portal.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```powershell
dotnet build -c Release
```

Single-file executable:

```powershell
.\publish.bat
```

Output: `publish\ReleaseTool.exe`

## Quick start

1. Select your FiveM **resources** folder
2. Pick a resource and mark files to keep open (escrow)
3. Click **Release** — get `-esc.zip` and `-os.zip`

Config is stored in `%AppData%\ReleaseTool\config.json`. See `config.example.json` for options.

## CLI

```powershell
ReleaseTool.exe --folder C:\resources --resource my-script --bump patch
ReleaseTool.exe --help
```

## License

MIT
