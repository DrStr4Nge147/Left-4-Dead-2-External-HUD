# Left 4 Dead 2 Customized Overlay HUD - External

A transparent, click-through, always-on-top window that supplements L4D2's four built-in
survivor slots with roster positions 5 and up. It reads one JSON file. It does not touch
the game process — no injection, no memory reading, no DirectX hooking.

## Running it

```text
overlay-app\dist\OverlayHud.exe
```

Requires the .NET 9 desktop runtime, which is already present if the .NET 9 SDK is
installed. Rebuild with:

```bash
dotnet publish overlay-app/OverlayHud/OverlayHud.csproj -c Release -o overlay-app/dist
```

## Customizing the UI

Choose **Customize UI...** from the notification-area menu. The editor uses the same
survivor-card template and one-column-first layout calculation as the live overlay, so the
16:9 preview tracks what will be drawn in game.

The editor controls card size, opacity, scoreboard offsets, bottom-HUD clearance, and the
one- or two-column limit. Sidebar containment and automatic fitting are calibrated layout
rules rather than user settings. **Preview extra survivors** only changes the sample roster
in the preview, supports up to 27 extras, and is not saved as an overlay setting.
**UI size** intentionally ranges from 0.60× to 1.00×; values above the normal size are not
offered and legacy config values above 1.00 are clamped.
Clicking anywhere on a slider track moves its thumb directly to that position. **Reset UI**
also restores the preview-only survivor count to 6.

**Exit when L4D2 closes** is rendered as a high-contrast checkbox and enabled by default.
Once the app has observed L4D2 running,
it exits when that game process closes. Disable the option to leave the app in the tray for
the next session. Launching the overlay before L4D2 is safe: it waits for the first game
process instead of exiting immediately.

The editor header refreshes once per second with green **L4D2: RUNNING** or amber
**L4D2: NOT RUNNING**. The app is single-instance per Windows session: launching
`OverlayHud.exe` again exits the duplicate before it creates another tray icon, overlay,
or keyboard hook.

- **Save & Apply** writes `config.json` atomically and updates the running overlay.
- **Cancel** closes the editor without changing the live overlay or config file.
- **Reset UI** restores UI defaults in the draft. It does not reset the state-file path,
  game process, hotkey, or debug options, and still requires **Save & Apply**.

For direct access during setup, run `OverlayHud.exe --settings`.

While L4D2 is focused, hold **Tab** and press **Insert** to open the editor directly. The
app consumes that Insert press and its matching release, so an in-game Insert bind does
not also execute. Press **Tab+Insert** again while the editor is active to close it as
**Cancel**, discarding unsaved changes. Tab still reaches L4D2, and Insert pressed without
Tab is never consumed.

There is no main window. It lives in the notification area — right-click the tray icon for
**Open config folder** and **Exit**.

## Using it

- Hold **Tab** to show the panel, release to hide. The key is only observed, never
  swallowed, so the game's own scoreboard behaves exactly as before.
- The panel only draws while L4D2 is the foreground window, so Tab does nothing on the
  desktop.
- The window follows the game window's position and size, so it works on any monitor.
- `Left 4 Dead 2 Customized Overlay HUD - External v0.6.4` appears at the top right while
  L4D2 is focused and the exporter is
  inactive, confirming that the app is running in the main menu, lobby, loading screens,
  and pause menu. It disappears when live round exports resume.

**L4D2 must run borderless windowed** (`-windowed -noborder`). Nothing can draw over
exclusive fullscreen.

## Reading the panel

| Element | Meaning |
|---|---|
| Bar, green → amber → red | Permanent health as a fraction of max |
| Pale segment past the bar | Temp health from pills or adrenaline |
| `48 + 12` | Permanent + temp |
| Bone-white bar | Black and white — one more down and they die |
| Red bar + `DOWN` / `HANGING` / `DYING` | Incapacitated, on a ledge, or bleeding out |
| Greyed card + `DEAD` | Dead |
| Left slot: white bottle / cylinder / biohazard | Molotov, pipe bomb, bile bomb |
| Middle slot: white medkit / lightning / ammo box | Medkit, defibrillator, explosive ammo, incendiary ammo |
| Right slot: white bottle / syringe | Pills, adrenaline |
| Dim empty box | Nothing carried in that slot |

A roster is only ever drawn from a live export. When the addon stops writing — a menu, a
lobby, a load, a paused game — the panel draws nothing at all rather than the last roster it
saw. The panel is a roster and carries no messages.

The top-right badge reports the state instead, and a line under it says why, based on what
is actually in `left4dead2\addons`:

| Line | Meaning |
|---|---|
| *(nothing)* | The addon has exported before and is simply between rounds |
| `WAITING FOR A ROUND` | Addon installed, no round has exported yet. Not a fault |
| `ADDON NOT INSTALLED` | No pack containing the exporter is installed, subscribed or manual |
| `MORE THAN ONE COPY` | Installed twice — one mounts, unpredictably. Keep one |
| `ADDON TURNED OFF` | Installed but disabled in the game's Add-ons screen |
| `GAME NOT FOUND` | No install located. Set `statePath` by hand |

Packs are identified by their contents, not their filenames: a Workshop subscription lives
at `addons\workshop\<publishedfileid>.vpk`, which says nothing about what is inside it.

Only roster positions 5 and up receive cards; the first four remain on L4D2's vanilla HUD.
When there are four or fewer survivors, holding Tab draws no roster panel. Extra cards use
one column while they fit naturally, then balance across at most two columns. The panel
shrinks inside the scoreboard sidebar instead of adding more columns across the screen.

## config.json

Sits next to the exe. Edit and restart the app.

| Key | Default | Notes |
|---|---|---|
| `statePath` | `""` | Empty = find `left4dead2\ems\overlay_hud\state.json` automatically, falling back to the pre-v1.0.4 loose `ems\overlay_hud_state.json`. An install that already has a state file is preferred over one that merely exists |
| `gameProcess` | `left4dead2` | Process name to treat as the game |
| `holdKey` | `9` | Virtual-key code. 9 = Tab. 160 = left shift, 164 = left alt |
| `editorKey` | `45` | Key used with `holdKey` to open the editor. 45 = Insert; `0` disables the shortcut |
| `alwaysShow` | `false` | Ignore the hold key and stay visible. Useful for positioning |
| `ignoreForeground` | `false` | Draw even when the game is not focused. Debug aid |
| `exitWhenGameCloses` | `true` | Exit after an observed L4D2 process closes; `false` keeps the app in the tray |
| `anchor` | `TopLeft` | `Top`/`Middle`/`Bottom` + `Left`/`Center`/`Right` |
| `offsetUnits` | `percent` | `percent` treats offsets as fractions of the game window; `pixels` uses DIPs |
| `offsetX` / `offsetY` | `0.02` / `0.62` | Moves the overlay inside the fixed vanilla-sidebar boundary and below the scoreboard |
| `autoScale` | `true` | Scale with game-window height |
| `baselineHeight` | `1080` | Window height at which automatic scale is 1.0 |
| `scale` | `1.0` | User scale applied on top of automatic scale, clamped to `0.60`–`1.00` |
| `minScale` | `0.35` | Smallest fraction of the normal resolution-scaled size allowed by fitting |
| `bottomReserve` | `0.0` | Optional bottom clearance for custom HUDs; vanilla Tab uses the full remaining height |
| `opacity` | `0.92` | Panel opacity |
| `cardsPerColumn` | `0` | `0` measures the real one-column height first; positive values override it |
| `maxColumns` | `2` | Hard horizontal column limit; overflow is balanced and scaled vertically |
| `staleAfterSeconds` | `2.0` | Seconds without a new `seq` before the export counts as stopped |
| `exporterProven` | `false` | Written by the app the first time it sees this install export. Once set, the `WAITING FOR A ROUND` line stays off — the addon has been observed working, so there is nothing left to explain |
| `debug` | `false` | Open the debug console at startup. Also toggled from the editor or the tray menu |

Set `alwaysShow` and `ignoreForeground` to `true` together to lay the panel out on the
desktop without launching the game.

## When the panel is empty

The status line says why, and includes the path it is watching:

| Message | Meaning |
|---|---|
| `LEFT 4 DEAD 2 NOT FOUND` | No install located. Set `statePath` by hand |
| `WAITING FOR THE ADDON - NO STATE FILE YET` | Game found, exporter has not written yet. Check the addon VPK is in `addons\` |
| `STATE FILE DID NOT PARSE` | Shows the failure count and the parser message |
| `GAME PAUSED OR NOT RUNNING` | The file exists but `seq` has stopped advancing |

## Debug console

**Customize UI... → Debug console**, or the same item on the tray menu. It answers whether
the app is working when the panel cannot, because the panel is the thing missing.

The top block is current state, refreshed twice a second: whether the exporter is live,
stopped, or has never been seen; the state file being watched; polls completed and the size
of the last roster read; whether L4D2 is in front and at what size; whether the hold key is
down and how many times the keyboard hook has had to be reinstalled; and whether the panel
is drawing.

Underneath is the history — focus and resolution changes, the export starting and stopping,
hook loss and recovery, and the reason the panel is hidden whenever it is. Only changes are
recorded, so the log stays readable, and it is capped at 600 lines. **Save to file** writes
`debug-<timestamp>.log` beside the exe, which is the thing to attach when reporting a
problem. Closing the window turns the console off; the setting is remembered.
