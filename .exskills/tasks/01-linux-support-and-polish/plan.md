# Linux support and polish

> Created: 2026-07-02. Rev: af43949. Detail: fast. Source: Adapt the app for Linux (replace registry settings with a config file next to the exe), fix the double titlebar on Linux, show unexpected login errors in a dialog, and update ShadowUI to the latest version.

## Decisions

- Settings storage unified on ALL platforms: plain `key=value` file `settings.cfg` next to the executable (same location as `vault.dat`), AOT-safe, no JSON. Existing registry values are not migrated (few low-stakes settings, defaults re-apply).
- `RunAtStartup` stays platform-specific: Windows keeps the `HKCU\...\Run` registry key; Linux writes `~/.config/autostart/encryptum.desktop`.
- Double titlebar: on non-Windows the ShadowUI custom `TitleBar` is removed from the visual tree at window construction, so native window decorations are used (ShadowUI's `TitleBar` forces `ExtendClientAreaToDecorationsHint`, which X11/Wayland handle poorly). App-side fix; a library-level fix in ShadowUI can follow separately.
- Login errors: wrong password (`CryptographicException`) keeps the inline red text; any other exception opens a `shadui:Dialog` showing the full exception text (selectable). This is what currently masks real errors as "Wrong password".
- ShadowUI target version: **1.1.4** — latest existing build (local pack from C:\PROJECTS\ShadowUI, restorable via the configured `ShadowUI-local` source + cache). nuget.org only has 1.1.3; if 1.1.4 must come from nuget.org later, publish it first.
- `tests` setting absent ≡ never → no new tests authored; no test project exists anyway.

## Verification

- tests: `-` · lint: `-` · types/build: `dotnet build Encryptum.csproj -c Debug`

## Tasks

### shadowui-bump: Update ShadowUI to 1.1.4 [done]

- **What:** Bump the ShadowUI PackageReference from 1.0.0 to 1.1.4 and fix any API breaks surfaced by the build.
- **Files:** Encryptum.csproj
- **Depends on:** —
- **Done when:** `dotnet build Encryptum.csproj -c Debug` succeeds with ShadowUI 1.1.4 restored.
- **Result:** Version bumped 1.0.0 → 1.1.4; restore + build green, no API breaks.

### settings-config-file: File-based settings instead of registry [done]

- **What:** Rewrite SettingsService to persist settings in a `key=value` file `settings.cfg` in AppContext.BaseDirectory on all platforms; keep RunAtStartup via the Windows Run registry key (guarded by OperatingSystem.IsWindows()) and add a Linux `~/.config/autostart/encryptum.desktop` implementation.
- **Files:** Services/SettingsService.cs
- **Depends on:** —
- **Done when:** Build succeeds; running the app writes/reads `settings.cfg` next to the exe (verified by toggling a setting and inspecting the file); no `Microsoft.Win32` usage outside the IsWindows-guarded startup code.
- **Result:** Rewritten to key=value settings.cfg (cache + sorted atomic-ish write); round-trip verified PASS via a console harness compiling the real source; registry only in IsWindows-guarded startup, Linux autostart .desktop added.

### linux-native-titlebar: No double titlebar on Linux [done]

- **What:** On non-Windows, remove the `shadui:TitleBar` from LoginWindow, MainWindow, and SettingsWindow at construction so native decorations provide the single titlebar.
- **Files:** Views/Windows/LoginWindow.axaml(.cs), Views/Windows/MainWindow.axaml(.cs), Views/Windows/SettingsWindow.axaml(.cs)
- **Depends on:** —
- **Done when:** Build succeeds; on Windows the custom titlebar still renders (app launch check); the removal branch is exercised only when `!OperatingSystem.IsWindows()`.
- **Result:** TitleBar named `Chrome` in all three windows; constructors remove it from the tree on non-Windows so it never attaches and native decorations stay; Windows launch screenshot confirms the custom titlebar still renders. Startup checkbox label generalized to "Run at system startup".

### login-error-dialog: Unexpected login errors shown in a dialog [done]

- **What:** In LoginViewModel, catch CryptographicException as inline "Wrong password" and any other exception as a dialog: add ErrorDialog (shadui:Dialog) to LoginWindow with selectable full exception text bound from the VM.
- **Files:** ViewModels/Windows/LoginViewModel.cs, Views/Windows/LoginWindow.axaml
- **Depends on:** shadowui-bump
- **UI:** Dialog overlays the login window root Grid; body = read-only selectable TextBox with the exception text; single Close button in the footer.
- **Done when:** Build succeeds; corrupting `vault.dat` (non-decryptable garbage still fails as CryptographicException → inline) vs. an injected non-crypto exception path shows the dialog — verified by temporarily forcing a test exception in TryLoad, launching, observing the dialog, then removing the forced throw.
- **Result:** CryptographicException → inline "Wrong password"; any other exception → shadui:Dialog with a concise "ExceptionType: message" line in a SelectableTextBlock + Close button (per user feedback, full stack dump dropped for readability). Verified live: injected IOException in TryLoad opened the dialog on screen; injection reverted, final build green.

## Out of scope

- Library-level Linux fix inside ShadowUI's TitleBar (separate repo/release).
- Migration of existing registry values to settings.cfg.
- Linux packaging (.desktop for the app menu, icons, publish profiles).
