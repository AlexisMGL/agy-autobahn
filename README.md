# Antigravity Autobahn

A small Windows app that watches Antigravity for an enabled **Submit** / **Submit ↵** button and invokes it automatically. The window shows the session count, saved total, last submission time, and timestamped activity.

## Run

Open `dist/AntigravityAutobahn.exe`. The executable is self-contained for Windows x64; no separate .NET installation is needed.

The language selector at the top offers **EN · English**, **FR · Français**, and **DE · Deutsch**. The first launch defaults to English. Changing language updates the interface immediately and saves the choice in `%LOCALAPPDATA%\AntigravityAutobahn\settings.json` for subsequent launches. Language changes do not change the auto-submit state or submission history.

1. Open Antigravity and the conversation containing the approval prompt.
2. Click **Activer l’auto-submit** in Autobahn. It starts paused each time you launch it.
3. Use **Mettre en pause**, the tray menu, or **Ctrl+Alt+F8** to toggle it.
4. **Masquer dans ^** or minimize to keep it running in the background. Closing the window or choosing **Quitter** stops the app.

### Retrouver la fenêtre masquée

- Cliquez sur **^** près de l’horloge Windows, puis sur l’icône bleue Autobahn.
- Ou utilisez **Ctrl+Alt+F9**.
- Ou relancez `AntigravityAutobahn.exe` : la fenêtre existante revient, sans lancer une deuxième instance.

Réafficher la fenêtre ne change pas l’état de l’auto-submit. Une notification rappelle ces moyens de retour au premier masquage. Si Windows utilise déjà un raccourci, le journal le signale.

### Interface noir et bleu

La voiture accélère sur une autoroute de nuit lorsque l’auto-submit est actif. En pause, le feu passe au rouge et la voiture freine jusqu’à la ligne d’arrêt. La désactivation réelle est immédiate ; le freinage est une animation visuelle. L’animation s’arrête lorsque la fenêtre est masquée ou que la voiture est immobilisée.

Le journal de bord affiche les événements récents en premier, avec icônes, messages lisibles et horaires. Les compteurs et l’ancien historique sont conservés.

### Fenêtre compacte

Redimensionnez la fenêtre en faisant glisser un bord ou un coin. Le journal disparaît lorsque la place manque, puis les compteurs et l’en-tête s’effacent. Au format minimal, seuls la voiture animée avec son feu et le bouton d’activation/pause restent visibles. Agrandissez la fenêtre pour retrouver les autres contrôles. L’auto-submit et l’enregistrement du journal continuent indépendamment du format affiché.

La scène compacte affiche aussi le nombre de Submits envoyés pendant la session. À chaque envoi réussi, le compteur grossit brièvement et un **+1** bleu apparaît au-dessus de la voiture, monte et s’efface. Le compteur reste à jour si la fenêtre est masquée ou agrandie ; il repart à zéro au lancement, comme le compteur de session de la vue complète.

When enabled, it submits matching buttons in all open Antigravity windows, including approval prompts. A click already dispatched cannot be recalled by disabling the app. It does not navigate into inactive conversation tabs.

## Background operation

Uses Windows UI Automation's `InvokePattern`, without moving the mouse or explicitly activating Antigravity. The locally installed Antigravity exposes its `Submit ↵` button through this interface. It can attempt clicks while another app is in the foreground, although the target application's accessibility provider may bring its window forward. Windows accessibility may take a few seconds to expose Antigravity's controls on the first scan.

Keep Antigravity open; it can sit behind other windows. A minimized window, inactive tab, locked desktop, or suspended renderer may stop exposing its buttons. If detection stalls, restore Antigravity. There is no screenshot/OCR or coordinate-based fallback. If Antigravity is running as administrator, Autobahn may need the same privilege level.

Only button labels `Submit`, optionally followed by an Enter arrow, match. It does not match `Submit feedback`, `Send`, `Accept`, or `Allow`. It polls every 750 ms, spaces invocations at least two seconds apart, and clicks each accessible button once until that element disappears for two complete scans. A continuously reused element will not be clicked again unless it disappears; a completely recreated element is treated as new. An uncertain invocation error pauses automation instead of retrying blindly.

The count records successful UI Automation invocation calls, not confirmation that an underlying command completed. Counts and activity persist in `%LOCALAPPDATA%\AntigravityAutobahn\activity.jsonl`. Logs contain timestamps and window titles, not command bodies. The UI displays the latest 500 entries; the log file retains all entries.

## Build and test

Requires the .NET 10 SDK on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
dotnet run --project Autobahn.Tests -c Release
dotnet run --project Autobahn.Tests -c Release -- --languages
```

Tests use a separate harmless Windows form and the real UI Automation watcher. They check paused startup, invocation, duplicate suppression, disabling/re-enabling, new prompts, and disabled/unrelated buttons. They never invoke real Antigravity buttons.

Implementation references: [Microsoft UI Automation Invoke](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.invokepattern.invoke) and [Electron accessibility](https://github.com/electron/electron/blob/main/docs/tutorial/accessibility.md).
