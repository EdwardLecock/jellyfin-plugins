# Version Labels

Makes switching between versions of a movie or episode easier by adding quality and audio language information to the names in Jellyfin's version selector.

| Before | After (default template) |
|---|---|
| `EN-NL` | `EN-NL · 1080p · English, Dutch` |
| `DE` | `DE · 4K HDR10 · German` |

The names are changed in the server's API responses, so the labels appear in every Jellyfin app (web, Android TV, mobile, etc.). Nothing is renamed on disk or in the database; disabling the plugin restores the original names immediately.

## Configuration

**Dashboard → Plugins → Version Labels**

| Option | Description | Default |
|---|---|---|
| Enabled | Turn relabelling on or off | On |
| Only relabel items that have more than one version | Leave single-version items untouched | On |
| Label template | How labels are built (see placeholders) | `{name} · {resolution} {hdr} · {languages}` |

The settings page has presets and a live preview: search for a movie and it shows the version names exactly as the apps will receive them.

### Placeholders

| Placeholder | Example |
|---|---|
| `{name}` | Jellyfin's own version name, usually from the filename (`EN-NL`) |
| `{resolution}` | `4K`, `1080p`, `720p`, `SD` |
| `{hdr}` | `DV`, `HDR10+`, `HDR10`, `HLG`, `HDR` (empty for SDR) |
| `{videocodec}` | `HEVC`, `H.264`, `AV1` |
| `{audiocodec}` | Default audio track: `TrueHD Atmos`, `DTS-HD MA`, `DD+`, `AAC` |
| `{channels}` | Default audio track: `7.1`, `5.1`, `Stereo` |
| `{languages}` | All audio languages: `English, Dutch` |
| `{languagecodes}` | All audio languages as codes: `ENG/DUT` |
| `{defaultlanguage}` | Language of the default audio track |
| `{subtitles}` | All subtitle languages |
| `{size}` | File size: `12.3 GB` |

Placeholders that turn out empty are removed together with their separator (`·`, `|`, `/`, `-`), and empty brackets like `[]` disappear.

## How versions are detected

Jellyfin groups files as versions of one item when they are in the same folder and named like:

```
Movie (2024)/
  Movie (2024) - EN-NL.mkv
  Movie (2024) - DE.mkv
```

or when you select items in the web UI and use **Merge versions**.

## Building

```
cd Jellyfin.Plugin.VersionLabels
dotnet publish -c Release -o publish_out
```

Copy `publish_out/Jellyfin.Plugin.VersionLabels.dll` into `<jellyfin data>/plugins/VersionLabels_1.0.0.0/` and restart Jellyfin.

Requires Jellyfin 10.11.
