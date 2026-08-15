# Left 4 Dead 2 Customized Overlay HUD - External

A transparent, click-through, always-on-top window that draws the selected part of L4D2's
survivor roster. **All survivors** includes the four built-in slots; **Extra survivors**
keeps the original slot-5-and-up behavior. It reads one JSON file and does not touch the game
process — no injection, no memory reading, no DirectX hooking.

**Current verification:** the v1.2.0 app and format-v1 addon VPK were live-tested in L4D2 on
2026-08-15, including the Consistent HUD health-number, black-and-white, and Minimalist options.

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

The editor has separate **Scoreboard** and **Consistent HUD** tabs. The Scoreboard tab controls
card size, opacity, scoreboard offsets, bottom-HUD clearance, and the one- or two-column limit.
Sidebar containment and automatic fitting are calibrated layout rules rather than user
settings. **Preview extra survivors** only changes the sample roster in the preview, supports
up to 27 extras, and is not saved as an overlay setting.
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
  game process, consistent-HUD hotkey, or debug options, and still requires **Save & Apply**.

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
- The **Consistent HUD** tab enables a row-major HUD that does not open the game's scoreboard.
  Its **Bottom - Horizontal Grid** is the default, with **Lower Left Vertical Grid** and
  **Lower Right Vertical Grid** as the other templates. **Vertical position** moves any of these
  layouts upward from the bottom edge without changing the scoreboard tab. **Horizontal spacing**
  and **Vertical spacing** control the card gaps; negative values can overlap cards. **Separate
  You** uses roster-left/You-right for Bottom - Horizontal and roster-right/You-left for Lower
  Right Vertical; those spacing values apply only to the remaining roster cards.
- **Set key** captures one key for the consistent-HUD toggle; F7 is the default. The selected
  key is consumed only while L4D2 is in front, and pressing it toggles the saved
  **Show HUD consistently** preference.
- The window follows the game window's position and size, so it works on any monitor.
- `Left 4 Dead 2 Customized Overlay HUD - External v1.2.0` appears at the top right while
  L4D2 is focused and the exporter is
  inactive, confirming that the app is running in the main menu, lobby, loading screens,
  and pause menu. It disappears when live round exports resume.

**L4D2 must run borderless windowed** (`-windowed -noborder`). Nothing can draw over
exclusive fullscreen.

## Consistent HUD

The consistent view is designed to behave like an external vanilla HUD rather than a
scoreboard companion. It removes the scoreboard frame and header, draws survivor cards in a
four-across row-major grid for horizontal templates, and stays visible while the game is focused
once **Show HUD consistently** is on. Horizontal grids use up to three rows and add columns for
larger rosters. The two vertical templates draw one card per row at their named lower corner. The
vertical-position slider uses a bottom inset: `0%` touches the bottom edge, and larger values
move the panel upward.

Horizontal and vertical spacing are layout pixels before the HUD scale is applied. Zero keeps
the original spacing, positive values spread cards apart, and negative values pull them together
or overlap them.

When **Separate You** is checked, the exporter-marked listen-server host card is removed from
the shared grid and drawn as one independent card. **Bottom - Horizontal Grid** puts the shared
roster at lower left and You at lower right, with a reserved gap between the two groups.
On that horizontal template, the shared roster starts at three columns across when Separate You
is enabled so the remaining cards use the available space; without Separate You it starts at
four columns as usual. Larger rosters add columns only when the three-row limit requires them.
**Lower Right Vertical Grid** keeps the shared roster at lower right and mirrors You to lower
left with the same separation. **Lower Left Vertical Grid** keeps the shared roster at lower left
and You at lower right. The selected template, vertical position, and HUD scale still apply to
both elements, but the horizontal and vertical spacing controls do not apply to You. With an
older exporter, or on a dedicated server, no local marker is available and the roster stays in
its original all-in-one layout.

Choose one of these templates in the editor:

| Template | Placement |
|---|---|
| **Bottom - Horizontal Grid** | Default bottom-centered four-across grid; three-across shared roster when Separate You is enabled |
| **Lower Left Vertical Grid** | Previous one-card-per-row HUD at lower left |
| **Lower Right Vertical Grid** | One-card-per-row HUD at lower right |

The consistent view uses the same roster filter and survivor data as the scoreboard view, but
has its own size and opacity values. Live preview on this tab shows the actual plain HUD grid;
it does not hold the game's scoreboard.

Use **HUD design** to choose the card appearance independently of the placement template:

- **Basic** keeps the existing full survivor card with the name, health bar, health value, and
  item slots in one row.
- **Minimalist** puts the name and item icons before the health value above a compact segmented
  health strip. Temporary health keeps the grunge brush texture; items have no bounding boxes and
  use only a black outline. The item column remains fixed-width, so long names are clipped with an
  ellipsis and the icons remain visible. The strip uses five vertically compressed segments.
  **Show health numbers** can hide the numeric value, and **Black & white theme** switches health,
  state, and follower colors to grayscale. These options apply only to the Consistent HUD,
  including its simulated preview and separate You card; the Scoreboard tab is unchanged.

## Reading the panel

| Element | Meaning |
|---|---|
| Bar, green → amber → red | Permanent health as a fraction of max: green from 40%, amber from 25%, red below |
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
| `UPDATE THE OVERLAY APP` | The installed addon is on a newer version than this app |
| `UPDATE THE EXPORTER ADDON` | This app is newer than the installed addon |

Packs are identified by their contents, not their filenames: a Workshop subscription lives
at `addons\workshop\<publishedfileid>.vpk`, which says nothing about what is inside it.

## Version match

The two halves ship under one version, and they arrive by different routes: the addon updates
itself through the Workshop, and the app has to be downloaded. So the app is the half that
goes stale without saying anything, and the addon's version is the one to measure against.

The app reads `addonversion` straight out of the installed pack's own `addoninfo.txt`. That
is answerable at a main menu — before a map has loaded and before anything has been exported.
The `v` field in `state.json` is the fallback for an install whose manifest cannot be read.

**A mismatch changes nothing.** The HUD, the editor, the roster filters and the scoreboard
hold all keep working across versions; the difference is only reported. The line appears
under the top-right badge in the main menu **and during a round**, since someone who only
holds Tab mid-round would otherwise never see it. **Show status badge** turns the whole
corner off, this line included.

The editor carries the same message with a clickable link to
<https://github.com/DrStr4Nge147/Left-4-Dead-2-External-HUD/releases>, because the overlay
itself is click-through by design and cannot hold one.

If no version can be read — no pack installed, an unreadable manifest, or a version string
that is not a version — nothing is claimed and nothing is shown.

## Who to show

The editor's **Who to show** setting controls the roster drawn in the panel:

| Option | Cards shown |
|---|---|
| **All survivors** | Every mortal survivor, including the four vanilla slots |
| **Extra survivors** | The previous behavior: plain survivors from slot 5 onward, plus mortal soldiers and followers |
| **Mortal soldiers + followers** | Finale Soldiers' mortal soldiers and followers only |
| **Followers only** | Only soldiers currently following a player |

Immortal team-4 holdout soldiers are excluded from every option. In **Extra survivors**, when
there are four or fewer plain survivors and no soldier/follower cards, holding Tab draws no
roster panel. Cards use one column while they fit naturally, then balance across at most two
columns. The panel shrinks inside the scoreboard sidebar instead of adding more columns across
the screen.

## config.json

Sits next to the exe. Edit and restart the app.

| Key | Default | Notes |
|---|---|---|
| `statePath` | `""` | Empty = find `left4dead2\ems\overlay_hud\state.json` automatically, falling back to the pre-v1.0.4 loose `ems\overlay_hud_state.json`. An install that already has a state file is preferred over one that merely exists |
| `gameProcess` | `left4dead2` | Process name to treat as the game |
| `holdKey` | `9` | Virtual-key code. 9 = Tab. 160 = left shift, 164 = left alt |
| `editorKey` | `45` | Key used with `holdKey` to open the editor. 45 = Insert; `0` disables the shortcut |
| `alwaysShow` | `false` | Start the consistent HUD enabled; the configured toggle key changes it during play |
| `consistentKey` | `118` | Virtual-key code for the consistent-HUD toggle. 118 = F7; `0` disables it |
| `ignoreForeground` | `false` | Draw even when the game is not focused. Debug aid |
| `exitWhenGameCloses` | `true` | Exit after an observed L4D2 process closes; `false` keeps the app in the tray |
| `anchor` | `TopLeft` | `Top`/`Middle`/`Bottom` + `Left`/`Center`/`Right` |
| `offsetUnits` | `percent` | `percent` treats offsets as fractions of the game window; `pixels` uses DIPs |
| `offsetX` / `offsetY` | `0.02` / `0.62` | Moves the overlay inside the fixed vanilla-sidebar boundary and below the scoreboard |
| `autoScale` | `true` | Scale with game-window height |
| `baselineHeight` | `1080` | Window height at which automatic scale is 1.0 |
| `scale` | `1.0` | User scale applied on top of automatic scale, clamped to `0.60`–`1.00` |
| `consistentScale` | `0.65` Basic / `1.00` Minimalist | Independent scale for the consistent HUD, clamped to `0.60`–`1.00` |
| `consistentOpacity` | `0.90` | Independent opacity for the consistent HUD |
| `consistentDesign` | `basic` | Consistent HUD card design: `basic` or `minimalist` |
| `consistentShowHealthNumbers` | `true` | Show numeric health values in the Consistent HUD |
| `consistentMonochrome` | `false` Basic / `true` Minimalist | Use grayscale colors in the Consistent HUD |
| `consistentVerticalOffset` | `0.03` | Bottom inset for the consistent HUD; higher values move it upward |
| `consistentHorizontalSpacing` | `10.0` | Extra horizontal card gap in layout pixels; negative values overlap cards |
| `consistentVerticalSpacing` | `0.0` | Extra vertical card gap in layout pixels; negative values overlap cards |
| `consistentSeparateYou` | `true` | Move the current survivor into a separate Consistent HUD card |
| `minScale` | `0.35` | Smallest fraction of the normal resolution-scaled size allowed by fitting |
| `bottomReserve` | `0.0` | Optional bottom clearance for custom HUDs; vanilla Tab uses the full remaining height |
| `opacity` | `0.92` | Panel opacity |
| `consistentTemplate` | `vanilla-bottom-center` | `vanilla-bottom-center`, legacy `vanilla-vertical`, or `lower-right-vertical`; old `bottom-right` and `top-vertical` values migrate to the default |
| `rosterFilter` | `all` | `all` = every mortal survivor, `extras` = the previous extra-only roster, `soldiers` = mortal soldiers and followers, `followers` = followers only |
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
stopped, or has never been seen; the version this app is on and the version the installed
addon claims; the state file being watched; polls completed and the size
of the last roster read; whether L4D2 is in front and at what size; whether the hold key is
down and how many times the keyboard hook has had to be reinstalled; and whether the panel
is drawing.

Underneath is the history — focus and resolution changes, the export starting and stopping,
hook loss and recovery, and the reason the panel is hidden whenever it is. Only changes are
recorded, so the log stays readable, and it is capped at 600 lines. **Save to file** writes
`debug-<timestamp>.log` beside the exe, which is the thing to attach when reporting a
problem. Closing the window turns the console off; the setting is remembered.
