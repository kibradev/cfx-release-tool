# FiveM Release Tool (Desktop)

FiveM script geliştiricileri için yerel Windows masaüstü uygulaması. Tek tıkla **Escrow** ve **Open Source** ZIP üretir.

## Gereksinimler

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Derleme

```powershell
cd ReleaseTool.Desktop
dotnet restore
dotnet build -c Release
```

## Tek .exe yayınlama (self-contained, win-x64)

```powershell
dotnet publish ReleaseTool.Desktop\ReleaseTool.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

Çıktı: `publish\ReleaseTool.exe`

## Kullanım

1. **Klasör seç** — `resources` veya `[0res]` gibi FiveM resources kök klasörünü seçin
2. Sol listeden bir **script/resource** seçin
3. Dosya ağacında **escrow'da açık kalacak** dosyaları işaretleyin
4. **Release oluştur** — `{resource}-v.{version}-esc.zip` ve `-os.zip` çıktı klasörüne kaydedilir
5. **Patch +1** — yalnızca manifest sürümünü artırır (onay ile, ZIP oluşturmaz)

## Ayarlar

Config dosyası sırasıyla:

1. `%AppData%\ReleaseTool\config.json`
2. Uygulama yanındaki `config.json`

Örnek yapı için `ReleaseTool.Desktop\config.example.json` dosyasına bakın.

## Port edilen mantık

Node/React referans projesindeki şu modüller birebir C#'a taşındı:

| Referans | Desktop |
|----------|---------|
| `localRelease.ts` | `ReleaseService`, `ZipService` |
| `manifest.ts` | `ManifestService` |
| `escrowIgnore.ts` | `EscrowIgnoreService` |
| `exclude.ts` | `ExcludeHelper` |
| `zip.js` | `ZipService` |
| `changelog.js` | `ChangelogService` |

## ZIP kuralları (özet)

- **Escrow ZIP**: `react_source`, `vue_source` vb. hariç; `web/` altında yalnızca `web/dist/**` dahil
- **OS ZIP**: kaynak kodlar dahil; manifest'te tüm `.lua` dosyaları için otomatik `escrow_ignore` glob'ları
- Her iki modda `web/node_modules` asla dahil edilmez
