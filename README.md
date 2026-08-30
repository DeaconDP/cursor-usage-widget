# Deez Fuel Gauge

<p align="center">
  <img src="https://cdn.jsdelivr.net/gh/DeaconDP/Deez-Fuel-Gauge@610e4a40be071f8ec31139429ac307463ea1ebb6/docs/screenshots/hero.png" alt="Deez Fuel Gauge" width="480" />
</p>

Lightweight desktop overlay for Cursor, Codex, Claude, and Gemini usage caps — plus optional Harddrive / CPU / GPU / RAM glance.

![License: MIT](https://img.shields.io/badge/license-MIT-blue)
![Platform: Windows · macOS](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-informational)

## Who it’s for

People who live in Cursor (and adjacent AI tools) and want a always-on glance of remaining plan limits without opening a billing page. Draggable pill overlay on **Windows 10/11** and **macOS 12+**.

## Quick start

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Cursor IDE logged in on the same user profile.

| Platform | File |
|----------|------|
| Windows | Double-click **`run.bat`** |
| macOS | Double-click **`run.command`** |

Rebuilds from this folder and launches the widget. On Windows, missing .NET can install via winget; on macOS the launcher opens the download page. If **`Deez Fuel Gauge.app`** is blocked after first build, right-click → **Open**. macOS failures log to `~/Library/Logs/DeezFuelGauge/setup.log`.

## Features

- Draggable overlay; position saved locally; auto-refresh every 5 minutes
- Right-click for Refresh or Quit; gear opens settings
- Cursor usage from local IDE login (no API key)
- Optional Codex / Claude.ai / Gemini App bars and Platform API spend
- Encrypted credential storage under the settings folder

Provider details and auth setup: **[docs/providers.md](docs/providers.md)**.

## Compact mode abbreviations

When compact (mini) mode is on, each enabled source shows as a short code plus percent (or `—` if disconnected):

| Abbr | Meaning |
|------|---------|
| **C** | Cursor plan (overall) |
| **CM** | Cursor Models (Auto pool, when breakdown is on) |
| **CA** | Cursor API pool (when breakdown is on) |
| **CO** | OpenAI via Cursor |
| **CC** | Claude via Cursor |
| **CG** | Gemini via Cursor |
| **CX** | Codex / ChatGPT limits |
| **OA** | OpenAI API |
| **CL** | Claude.ai Pro limits |
| **AC** | Claude API Console |
| **GM** | Gemini App (Antigravity) |
| **OR** | OpenRouter |
| **OZ** | OpenCode Zen |
| **OG** | OpenCode Go |
| **FA** | fal.ai credits |
| **XA** | xAI credits |
| **GB** | Grok Bot |

## Screenshots

<details>
<summary>More screenshots</summary>

<img src="https://cdn.jsdelivr.net/gh/DeaconDP/Deez-Fuel-Gauge@610e4a40be071f8ec31139429ac307463ea1ebb6/docs/screenshots/01-main.png" alt="Main overlay" width="480" />

</details>

## Limitations

- Uses **undocumented** Cursor (and optional vendor) endpoints that may change without notice
- Not affiliated with or endorsed by Cursor, OpenAI, Anthropic, or Google
- Access tokens are read locally and sent only to the vendor HTTPS APIs needed for usage; they are not stored by the widget beyond encrypted optional API keys you add in settings

## Optional: run at login

**Windows:** build once with `run.bat`, then `Win+R` → `shell:startup` → shortcut to `DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe`.

**macOS:** build once with `run.command`, then System Settings → General → Login Items → add **`Deez Fuel Gauge.app`**.

## Development

Build and run from the repo with `run.command` / `run.bat`, or open the .NET project under `DeezFuelGauge/`. Settings paths and provider plumbing are documented in [docs/providers.md](docs/providers.md).

## Credit

Created by [deac.online](https://deac.online) @ [worldbuild.io](https://worldbuild.io)

## License

MIT — see [LICENSE](LICENSE).
