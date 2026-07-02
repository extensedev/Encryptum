# Login window full-size

> Created: 2026-07-02. Rev: 69b33cf. Detail: fast. Source: The inline login error text is not visible (clipped by the fixed-height login window); make the login window the same size as the main window and resizable; wrap the login form in a Card so the big window doesn't look empty; add a minimize-to-tray setting.

## Decisions

- ~~Login window gets the main window's default size (1000x700), resizable.~~ Superseded by user feedback: window stays compact (560, SizeToContent=Height, CanResize=false) with the Card kept.
- Errors render in a fixed-height (88px) banner slot at the top of the card that swaps between the new-vault info banner and a destructive "Unlock failed" banner — fixed slot means error appearance never changes window height, so nothing can clip. The unexpected-error Dialog is removed; all login errors go through the banner (wrong password → "Wrong password", anything else → "ExceptionType: message").
- Verification is headless from here on (user directive): build + code-review verifier agents + source-compiling console harnesses; no GUI automation on the user's desktop.

## Verification

- tests: `-` · lint: `-` · types/build: `dotnet build Encryptum.csproj -c Debug`

## Tasks

### login-window-fullsize: Full-size resizable login window [done]

- **What:** Make LoginWindow 1000x700, resizable and maximizable; center the login column; drop the SizeToContent/height hack in the keyboard toggle so the inline error is always visible.
- **Files:** Views/Windows/LoginWindow.axaml, Views/Windows/LoginWindow.axaml.cs
- **Depends on:** —
- **UI:** Login column (480px) centered both axes in a 1000x700 resizable window; error line stays directly under the Unlock button.
- **Done when:** Build succeeds; launching the app shows a 1000x700 resizable login window and entering a wrong password shows the red error text on screen (live launch + screenshot).
- **Result:** Implemented and verified live, then superseded by user feedback (window back to compact) — see login-compact-card-banner.

### login-compact-card-banner: Compact login window with card and top error banner [done]

- **What:** Keep the Card but return the window to compact fixed size; show all login errors in a destructive banner in a fixed-height top slot that replaces the new-vault info banner; remove the error Dialog.
- **Files:** Views/Windows/LoginWindow.axaml, Views/Windows/LoginWindow.axaml.cs, ViewModels/Windows/LoginViewModel.cs
- **Depends on:** login-window-fullsize
- **UI:** Card (480 column) in a 560-wide SizeToContent window; 88px Panel slot at top of card swaps info banner ↔ error banner (ShowInfoBanner = IsNewVault && !IsErrorVisible); keyboard toggle keeps the original manual-height mechanism.
- **Done when:** Build succeeds; headless verifier confirms banner swap logic and that no error path can change window height.
- **Result:** Compact 560px window restored with Card and CardTitle/Description; fixed 88px banner slot swaps info/error banners; Dialog and bottom error text removed; all errors routed through ShowError with concise text.

### tray-option: Minimize-to-tray setting [done]

- **What:** Add a MinimizeToTray setting (default true) to ISettingsService, a checkbox in SettingsWindow/SettingsViewModel, and gate the minimize→hide behavior in App on it.
- **Files:** Services/SettingsService.cs, ViewModels/Windows/SettingsViewModel.cs, Views/Windows/SettingsWindow.axaml, App.axaml.cs
- **Depends on:** —
- **UI:** Checkbox in the existing settings list, same row pattern as the other toggles.
- **Done when:** Build succeeds; with the option off, minimizing keeps the window in the taskbar (App handler checks the setting at event time, not setup time).
- **Result:** MinimizeToTray setting (default on) added to service + settings checkbox; App minimize→hide handler gated at event time. Verified live before the headless directive: off → window stays in taskbar (visible+iconic), on → window hidden to tray.

## Out of scope

- Persisting login window size to settings.
