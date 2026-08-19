# LoginGuard

**Webcam intrusion alerts for Windows.** When a login attempt fails, LoginGuard
captures a photo and a short video from your webcam and sends them — together
with the failed-attempt details — to **your own** Telegram bot.

Event-driven and lightweight: there is no polling service burning CPU/RAM in the
background. The capture engine is triggered by the Windows Security event log
(**Event ID 4625**) and runs for only a few seconds per attempt.

> Privacy & scope: LoginGuard is an anti-theft / intrusion-awareness tool for a
> machine **you own or administer**. It sends media only to the Telegram chat
> **you** configure with **your** bot token. No data goes anywhere else. Do not
> deploy it on devices you are not authorized to monitor.

---

## Features

- 📸 **Photo + video** captured from the webcam on every failed sign-in
- 🔔 **Telegram delivery** to a single chat you own (bot token + chat id)
- 🧾 **Attempt details**: computer, username, time, logon type, failure reason, source IP
- 🪶 **Low footprint**: event-triggered (no resident polling); capture lasts a few seconds
- 🖥️ **Tray app** (bottom-right icon): status, settings, test capture, log access
- 🚀 **Autostart** at Windows logon
- 🧯 **Session & power logging**: PC start/stop, lock/unlock, logon/logoff
- 🔒 **Locked-screen coverage**: capture runs as SYSTEM, so it fires even at the login screen
- 🛡️ **Concurrency-safe**: a named mutex serializes camera access under rapid attempts

## How it works

```
Failed sign-in ─► Windows Security log (Event ID 4625)
                      │
                      ▼
        Task Scheduler event trigger (SYSTEM, HighestAvailable)
                      │  passes EventRecordID
                      ▼
        LoginGuard.exe --capture --record <id>
           1. Named mutex (camera collision guard)
           2. Read 4625 details (user, time, IP, logon type, reason)
           3. ffmpeg → photo  ─► Telegram sendPhoto (with details caption)
           4. ffmpeg → video  ─► Telegram sendVideo
```

A separate **tray app** (`LoginGuard.exe` with no arguments) runs in the logged-in
user's session for status, configuration and session/power logging. The capture
engine stays on the SYSTEM scheduled task so it also fires when nobody is logged in
(the login screen), where a per-user tray app cannot run.

## Install

1. Download `LoginGuardSetup.exe` from the [latest release](../../releases/latest).
2. Run it and approve the UAC prompt (it installs to `Program Files`, registers the
   SYSTEM task and autostart).
3. In the tray icon → **Settings**, enter your Telegram **bot token** and **chat id**,
   then **Test** and **Save**.

**Get a bot token & chat id**
- Message **@BotFather** → `/newbot` → copy the token.
- Open your new bot, press **Start**, send any message.
- Your chat id is the `id` in `https://api.telegram.org/bot<TOKEN>/getUpdates`.

**Silent / enterprise install**
```powershell
LoginGuardSetup.exe /token:<BOT_TOKEN> /chat:<CHAT_ID> /duration:5 /silent
```

**Uninstall**
```powershell
LoginGuardSetup.exe /uninstall
```

## Configuration

Stored at `C:\ProgramData\LoginGuard\config.json` (ACL: SYSTEM/Administrators full,
Users modify). Edit via the tray Settings dialog rather than by hand.

| Key | Meaning | Default |
|---|---|---|
| `BotToken` | Telegram bot token | (required) |
| `ChatId` | Destination chat id | (required) |
| `VideoDurationSec` | Video length in seconds | `5` |
| `CameraName` | DirectShow device name (empty = auto-detect) | `""` |
| `Enabled` | Master on/off | `true` |

Logs: `C:\ProgramData\LoginGuard\loginguard.log`.

## Camera access note

At the login screen the capture runs as **SYSTEM** in session 0. Windows must allow
desktop apps to use the camera:
**Settings → Privacy & security → Camera → "Let desktop apps access your camera"** = On.
While the session is merely locked (you are logged in), camera access is unaffected.

## Build from source

Requirements: Windows 10/11 with the in-box .NET Framework 4.8 C# compiler
(`csc.exe`) — no SDK needed.

```powershell
git clone https://github.com/devrim-1283/LoginGuard
cd LoginGuard
powershell -ExecutionPolicy Bypass -File build\build.ps1   # -> dist\LoginGuard.exe, dist\LoginGuardSetup.exe
```

`ffmpeg.exe` is not committed (it is large and third-party). For a local install,
place an `ffmpeg.exe` (a static Windows build) next to the setup, or let the setup
download it from the release. Releases built by CI attach `ffmpeg.exe` automatically.

## Project layout

```
src/LoginGuard/         Tray app + capture engine (C#, .NET Framework 4.8, WinForms)
src/LoginGuardSetup/    Self-elevating installer (C#)
build/build.ps1         Compiles both exes with csc
.github/workflows/      CI: build and publish a release on tag
```

## License

MIT — see [LICENSE](LICENSE). Bundles **ffmpeg** (used as an external executable);
ffmpeg is distributed under its own license (LGPL/GPL depending on build).
