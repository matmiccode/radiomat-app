# radioMAT

Polish radio for Windows 11 x64. Three station shortcuts, an included MP3 station
catalog, ICY track titles and system tray autostart. Polish and English interface.

**[Pobierz / Download radioMAT](https://github.com/matmiccode/radiomat-app/releases/latest)**

[Strona i instrukcja po polsku](https://matmiccode.github.io/radiomat-app/) ·
[English website and guide](https://matmiccode.github.io/radiomat-app/en/)

## What it does

- Starts with Radio Plus Lublin, RMF FM and Radio ZET; you can replace the presets.
- Remembers your stations, volume and language locally in your Windows account.
- Keeps playing when the window closes. Right-click the tray icon to exit fully.
- Can play automatically after Windows sign-in, without opening the window.
- Checks for updates and verifies the Ed25519 signature, SHA256 and installer
  version before installation.

The installer includes .NET and installs for the current user without administrator
access. It does not have an Authenticode certificate; Windows may show SmartScreen.
Stations may include advertising in their streams and may not provide track titles.
See the website for the connections the program makes.

## Build

Requires .NET SDK 8. Run `dotnet build -c Release`. The app uses NAudio 2.2.1 and
NSec.Cryptography 25.4.0. Building does not start the application.

Packaging with `tools/Build-Release.ps1 -Edition Public -PublishOnly` also requires
Node.js and the release identity checker configured through `-BrandChecker`.
Creating an installer additionally needs Inno Setup 6 and an external signing key
matching the public key in `Branding/Public/edition.json`. A private signing key
is never included in this repository.

This repository contains a clean public source snapshot. Development history and
local configuration are not included. Station data attribution: [Data/README.md](Data/README.md).

MIT license: [LICENSE](LICENSE). Dependency licenses are included in the installer.

[Report a problem](https://github.com/matmiccode/radiomat-app/issues/new) ·
[Buy the author a coffee](https://buycoffee.to/matcode)
