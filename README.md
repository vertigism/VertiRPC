# VertiRPC

A small Windows tray app for setting a custom Discord Rich Presence.

Pick an application ID, fill in the text, images and buttons, and VertiRPC pushes
it to the Discord desktop client over its local IPC pipe. No bot, no token, no
account access.

## Features

- Playing / Listening / Watching / Competing activity types
- Two presence lines, large and small images with hover text, and up to two
  action buttons
- Four timestamp modes: since the app started, since the last update, your local
  clock, or a custom start and end
- Auto-connect, which waits for Discord and pushes the presence when it opens
- Closes to the tray, and can start with Windows
- A theme hiding behind the title bar icon

## Installing

Grab the latest `VertiRPC-x.y.z-Setup.exe` from
[Releases](https://github.com/vertigism/VertiRPC/releases) and run it. It installs
per user, so there is no UAC prompt.

VertiRPC needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0/runtime);
the installer offers to fetch it if it is missing.

## Using it

1. Create an application at the
   [Discord Developer Portal](https://discord.com/developers/applications) and
   copy its **Application ID** into Client ID.
2. Upload any images you want under **Rich Presence → Art Assets**, then use
   their names as the image keys. A direct image URL works too.
3. Fill in the rest and press **Update Presence**.

Settings live in `%APPDATA%\VertiRPC\config.json`.

## Building

Needs the .NET 10 SDK, plus [Inno Setup 6](https://jrsoftware.org/isdl.php) for
the installer.

```powershell
.\build.ps1                  # test, publish and build the installer
.\build.ps1 -SkipInstaller   # test and publish only
dotnet test                  # tests on their own
```

## Layout

| Project | What it holds |
| --- | --- |
| `src/VertiRPC.Core` | Settings, the activity builder, the IPC client, the startup entry. No UI. |
| `src/VertiRPC` | The WPF app: window, view model, themes. |
| `tests/VertiRPC.Tests` | Tests for everything in Core. |

The split is deliberate: nothing in Core may reference WPF, which keeps the parts
worth testing testable.

## History

Versions up to 1.2.0 were a PyQt6 app. 2.0.0 is a rewrite in C# / WPF with an
installer instead of the old self-replacing updater. Configuration files from 1.x
are not read by 2.x.

## License

[MIT](LICENSE)
