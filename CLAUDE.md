# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Gearbox is a PowerToys-style Windows utility: .NET 10 + WinUI 3 (Windows App SDK), unpackaged, living in the
system tray. Each tool is an independently toggleable **module**. The README (in Portuguese) has the full
step-by-step guide for creating a module; `docs/ARCHITECTURE.md` has the startup flow, fault isolation table and
per-module internals. Code identifiers are English; comments, UI strings, test names and docs are pt-BR — keep it
that way.

## Commands

```powershell
dotnet build                                   # x64 by default; there is no AnyCPU (x64/ARM64 only)
dotnet run --project src\Gearbox                # opens only in the tray; click the icon or run again to show the window
dotnet test                                    # xUnit v3 on Microsoft.Testing.Platform (enabled in global.json)
dotnet test --project tests\Gearbox.Core.Tests --filter-class "*ModuleManagerTests"
dotnet test --project tests\Gearbox.Core.Tests --filter-method "*NomeDoTeste"
cd src\Gearbox; dotnet publish -c Release -r win-x64   # publish from src\Gearbox, never the root (it would publish test projects)
.\installer\build-installer.ps1 [-Arch arm64]  # Inno Setup 6; output in artifacts\installer
```

- `TreatWarningsAsErrors` is on for every project (`Directory.Build.props`), so analyzer warnings break the build.
  Notably CsWinRT1032: in WinUI projects, don't target `IReadOnlyList<T>`/`IEnumerable<T>` with a collection
  expression — use a concrete type (`string[]`, `List<T>`) or an explicit cast.
- Package versions live only in `Directory.Packages.props` (Central Package Management); `PackageReference`s have no
  `Version`. `Directory.Build.targets` strips unused Windows App SDK sub-packages (AI/ML/Search/Widgets).
- Release version: change `<Version>` in `Directory.Build.props`. Never change the `AppId` in `installer\Gearbox.iss`.
- New projects added via `dotnet sln add` may get `Any CPU`/`x86` entries; map them to `x64` in `Gearbox.sln`.

## Architecture

Layered, dependencies point down only:

- `src/Gearbox` — WinUI shell: `Program` (single-instance via `AppInstance`), `App.BuildHost()` (generic Host + DI +
  Serilog), tray, main window, navigation. Modules are wired in `BuildHost()` with `builder.Services.AddXxxModule()`
  plus a `ProjectReference` in `Gearbox.csproj`.
- `src/Gearbox.Core` — no UI: `IGearboxModule`, `ModuleManager` (start/stop, fault handling, `modules.json`
  on/off state), `ISettingsService<T>` / `JsonSettingsService` (atomic writes; corrupt file → `{id}.json.corrupt`,
  defaults used), startup registry, paths, logging.
- `src/Gearbox.Interop` — the **only** place that touches Win32, via CsWin32. Add API names to `NativeMethods.txt`
  and expose typed wrappers (e.g. in `NativeWindow`). No hand-written `DllImport`, no CsWin32 types leaking out.
  Also hosts the single shared `WinEventHookManager` (modules call `hooks.Subscribe(Id, handler)`; new Win32 events
  go into `WinEventKind` and the hook range), `DesktopLayer`, `DesktopWallpaper`, `SystemStateMonitor`.
- `src/Gearbox.Modules.*` — one project per tool, layout: `XxxModule.cs` (implements `IGearboxModule`),
  `XxxSettings.cs` (POCO → `settings\{ModuleId}.json`), `ServiceCollectionExtensions.cs`
  (`AddGearboxSettings<T>` + `AddGearboxModule<TModule, TPage>`), `Logic/` (pure, Win32-free, unit-tested),
  `Native/` (applies to the system via Interop), `UI/` (settings page + `ObservableObject` ViewModel),
  `Strings/pt-BR/Resources.resw`. `Gearbox.Modules.RoundedMaximize` is the reference implementation.
- `tests/` — one xUnit v3 project per production project (`OutputType=Exe`), testing mainly the `Logic/` classes.

The shell derives navigation, tray toggles and persisted on/off state from `IEnumerable<IGearboxModule>`; the
module's settings page is created via DI (ViewModel injected through the constructor) and embedded inside the
shell's `Views/ModulePage.xaml`, which draws the header, "Ativar módulo" toggle and fault message.

### Module contract (enforced by convention, easy to break)

- Everything runs on the UI thread — the out-of-context WinEvent hook delivers on the installing thread's message
  loop, so no locks. `Start()`/`Stop()` must not block (no long I/O, no `.Wait()`). Events from other threads are
  marshalled with `DispatcherQueue.TryEnqueue`.
- `Stop()` must undo **everything** the module changed in the system; it runs on disable, app exit and module fault.
  `Start()` must clean up on failure rather than stay half-enabled, then rethrow.
- Exceptions in `Start`/`Stop` are caught by `ModuleManager`; in hook handlers by `WinEventHookManager`; in the
  module's own callbacks (settings `Changed`, timers) the module must call `IModuleFaultReporter.ReportFault`.
  A fault stops the module and persists it as disabled.
- `ViewModel` saves with `settings.Save(...)`, which raises `Changed`; the module applies immediately, no restart.
- UI text goes in `.resw` (accessed via the module's `ModuleStrings`, resource map `"<AssemblyName>/Resources"`),
  never hardcoded in code or XAML.
- `ModuleId` is stable — it is the settings filename.

### Runtime data

Settings `%LocalAppData%\Gearbox\settings\{id}.json`, logs `%LocalAppData%\Gearbox\logs\gearbox-YYYYMMDD.log`,
autostart `HKCU\...\Run` value `Gearbox`. The installer shuts Gearbox down with `Gearbox.exe --exit`, the same path as the
tray's "Sair", so modules can restore windows/wallpaper — killing the process leaves the system altered.
