# Claude Usage Widget

A lightweight Windows tray widget that shows your **Claude Code usage in real time** — percentage used, time until reset, and a live countdown to the next refresh.

```
┌─────────────────────────────────┐
│ ⬤ Claude Usage  [26%]  🔓  ✕  │
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
- **Pin button** — 🔓 lock the position so it stays put
- **System tray** — minimize to tray, double-click to show/hide
- **Color coded** — green → yellow → red as usage increases
- **Always on top** — stays visible over other windows

## Requirements

- Windows 10/11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Claude Code installed with an active **Claude Max** subscription
- `~/.claude/.credentials.json` present (created automatically by Claude Code)

## Installation

1. Download the latest `ClaudeUsageWidget.exe` from [Releases](../../releases)
2. Run it — no installer needed
3. The widget appears in the bottom-right corner of your screen

> **Note:** The widget reads your OAuth token from `~/.claude/.credentials.json`, which Claude Code manages automatically. No configuration needed.

## Build from source

```bash
git clone https://github.com/Alan-VSouza/claude-usage-widget
cd claude-usage-widget

# Run in dev mode
dotnet run --project src/ClaudeUsageWidget/

# Run tests
dotnet test tests/ClaudeUsageWidget.Tests/

# Build standalone .exe
dotnet publish src/ClaudeUsageWidget/ClaudeUsageWidget.csproj \
  -r win-x64 -c Release --no-self-contained \
  -p:PublishSingleFile=true -o publish/
```

## How the usage percentage is calculated

Every 60 seconds the widget sends a 1-token request to the Anthropic API and reads:

| Header | Value | Meaning |
|---|---|---|
| `anthropic-ratelimit-unified-5h-utilization` | `0.26` | 26% of the 5-hour limit used |
| `anthropic-ratelimit-unified-5h-reset` | Unix timestamp | When the window resets |

This is the exact data source that Claude Code's `/usage` command uses internally.

## Tech stack

- C# / .NET 9
- Windows Forms
- xUnit (tests)

## License

MIT — free to use, modify and distribute. See [LICENSE](LICENSE).
