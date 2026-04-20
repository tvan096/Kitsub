# 🧰 Kitsub — Video & Subtitle Build Tool

[![Build](https://github.com/AstraidLabs/Kitsub/actions/workflows/ci.yml/badge.svg)](https://github.com/AstraidLabs/Kitsub/actions/workflows/ci.yml)
[![Release](https://github.com/AstraidLabs/Kitsub/actions/workflows/release.yml/badge.svg)](https://github.com/AstraidLabs/Kitsub/actions/workflows/release.yml)
[![License](https://img.shields.io/github/license/AstraidLabs/Kitsub?label=license)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Type](https://img.shields.io/badge/Type-CLI-2F6F8F)](#-overview)

🎬 Video & subtitle build tool

Kitsub is a .NET CLI tool that builds video outputs with burned-in or muxed subtitles.
It assembles final media files through explicit commands and does not provide an interactive editor.
Documentation is organized into stable, standalone guides and a docs/ hub for long-term reference.

## 🧱 Overview

- Command-line tool for media processing tasks.
- Video and subtitle processing, including muxing and burning.
- Explicit commands for inspection, extraction, conversion, and release packaging.
- External tooling via configurable paths.

## 🧩 Capabilities

- Media inspection and diagnostics.
- Subtitle muxing and burning.
- Track extraction.
- Subtitle conversion.
- Subtitle translation with OpenAI.
- Font handling for MKV outputs.
- External tool management and provisioning.

Note: The available command set depends on the build and on external tool availability.

## 📦 Installation

### Requirements

- .NET SDK 10.0
- External media tools (optional, can be provisioned by Kitsub when missing):
  - FFmpeg
  - MKVToolNix
  - MediaInfo

### Installation (Windows)

Recommended (PowerShell):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "iwr -useb https://raw.githubusercontent.com/AstraidLabs/Kitsub/master/scripts/install.ps1 | iex"
```

Verify:

```powershell
kitsub --help
```

Update: rerun the installer command above.

Uninstall:

1. Delete `%LOCALAPPDATA%\Kitsub\bin`
2. Remove `%LOCALAPPDATA%\Kitsub\bin` from your user PATH

Manual install fallback:

1. Download the latest `Kitsub-<tag>-win-x64.zip` from GitHub Releases
2. Extract to `%LOCALAPPDATA%\Kitsub\bin`
3. Add `%LOCALAPPDATA%\Kitsub\bin` to your user PATH

Security note: always review scripts before running them, especially when using `iex`.

### Build from source

```bash
dotnet build Kitsub.sln
```

## 🚀 Usage

```bash
kitsub --help
kitsub <command> --help
```

Run from source:

```bash
dotnet run --project src/Kitsub.Cli -- --help
```

Examples and command details: [Instructions.md](Instructions.md)

## 📚 Documentation

- [Instructions.md](Instructions.md)
- [docs/README.md](docs/README.md)
- [TESTING.md](TESTING.md)
- [docs/TESTING.md](docs/TESTING.md)
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
- [LICENSE.txt](LICENSE.txt)

## 🛠️ Project status

Open-source project maintained by a single author.

## 📄 License

MIT License. See [LICENSE.txt](LICENSE.txt).
