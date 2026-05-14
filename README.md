# Claude Usage Widget

A lightweight Windows tray widget that shows your **Claude Code usage in real time** — percentage used, time until reset, and a live countdown to the next refresh.

```
┌─────────────────────────────────┐
│ ⬤ Claude Usage  [26%]  🔓  ○  ✕│
│ ████████░░░░░░░░░░░░░░░░░░░░░░ │
│ Reset: 2h18m            ↻ 47s  │
└─────────────────────────────────┘
```

## How it works

Claude Code tracks your usage against a **5-hour rolling rate limit** (Claude Max plan). Every 60 seconds, this widget makes a minimal API call and reads the `anthropic-ratelimit-unified-5h-utilization` header directly from the response — the same value shown by the `/usage` command inside Claude Code.

No scraping. No guessing. The exact number, updated every minute.

## Features

- **Real-time usage** — 0–100% from the Anthropic API response headers
- **Reset countdown** — shows exactly when your 5-hour window resets
- **Refresh timer** — live countdown (↻ 59s) until the next update
- **Draggable** — move the widget anywhere on screen
- **Resizable** — drag the bottom-right corner to resize freely
- **Pin button** — 🔓 lock the position so it stays put
- **Minimize to circle** — click ○ to collapse into a compact progress ring; click the ring to expand back
- **System tray** — double-click the tray icon to show/hide
- **Color coded** — green → yellow → red as usage increases
- **Always on top** — stays visible over other windows

## Download

Go to the [Releases](../../releases) page and download the version that fits your setup:

| File | Size | Requirement |
|---|---|---|
| `ClaudeUsageWidget.exe` | ~200 KB | [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) |
| `ClaudeUsageWidget-standalone.exe` | ~108 MB | Nothing — works on any Windows 10/11 PC |

> The standalone version embeds the entire .NET runtime. Use it when distributing to machines that may not have .NET installed.

## Installation

1. Download the `.exe` from [Releases](../../releases)
2. Run it — no installer needed
3. The widget appears in the bottom-right corner of your screen

> **Note:** The widget reads your OAuth token from `~/.claude/.credentials.json`, which Claude Code manages automatically when you authenticate. No manual configuration needed.

## Requirements

- Windows 10 or 11
- Claude Code installed and authenticated (`claude auth login`)
- An active **Claude Max**, Team, or Enterprise subscription

## How the usage percentage is calculated

Every 60 seconds the widget sends a 1-token request to the Anthropic API and reads:

| Header | Value | Meaning |
|---|---|---|
| `anthropic-ratelimit-unified-5h-utilization` | `0.26` | 26% of the 5-hour limit used |
| `anthropic-ratelimit-unified-5h-reset` | Unix timestamp | When the window resets |

This is the exact data source that Claude Code's `/usage` command uses internally.

**Impact on your limit:** the periodic call consumes less than 1% of the 5-hour window. Negligible.  
**Financial cost:** zero. Max/Enterprise plans are flat-rate monthly — no per-token billing.

## Minimize to circle

Click the **○** button to collapse the widget into a compact progress ring:

```
    ╭──────╮
    │  26% │  ← click to expand back
    ╰──────╯
```

The ring color follows the same green → yellow → red scale. You can drag it around the screen; clicking (without dragging) expands it back to the full widget, centered on the ring's position.

## Build from source

```bash
git clone https://github.com/Alan-VSouza/claude-usage-widget
cd claude-usage-widget

# Run in dev mode
dotnet run --project src/ClaudeUsageWidget/

# Run tests
dotnet test tests/ClaudeUsageWidget.Tests/

# Build slim .exe (requires .NET 9 on target machine)
dotnet publish src/ClaudeUsageWidget/ClaudeUsageWidget.csproj \
  -r win-x64 -c Release --no-self-contained \
  -p:PublishSingleFile=true -o publish/

# Build standalone .exe (no runtime required)
dotnet publish src/ClaudeUsageWidget/ClaudeUsageWidget.csproj \
  -r win-x64 -c Release --self-contained \
  -p:PublishSingleFile=true -o publish/standalone/
```

## Tech stack

- C# / .NET 9 / Windows Forms
- xUnit (unit tests)

## License

MIT — free to use, modify and distribute. See [LICENSE](LICENSE).
