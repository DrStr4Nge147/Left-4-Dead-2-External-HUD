# Testing — exporter v1.0.2 + overlay app v1.0.2

The exporter is live-tested. This run is about the overlay app: whether it appears over the
game, follows Tab, and stays out of the way.

## Setup

1. **Close L4D2 first.** Addon VPKs are mounted at startup; swapping one while the game runs
   does not load the new script and silently unloads the old one. Every "the overlay stopped
   working" report so far has been this.
2. Remove any older `overlay_hud_export_*.vpk` from `left4dead2\addons\`.
3. Copy `compiled vpks\overlay_hud_export_v1.0.2.vpk` into:

   ```text
   E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\addons\
   ```

4. Launch options must include **`-windowed -noborder`**. Without borderless, nothing can
   draw over the game. Keep `-condebug`.
5. Delete `left4dead2\console.log`.
6. Start L4D2, and confirm `console.log` carries
   `[OVLHUD] Overlay HUD Export 1.0.2 loaded`. If that line is absent, the addon is not
   mounted and nothing below will work.
7. Start the overlay:

   ```text
   overlay-app\dist\OverlayHud.exe
   ```

   No window appears — it lives in the tray. Right-click the tray icon to exit.

## UI editor check

Before starting the campaign, open **Customize UI...** from the tray menu:

- Change **Preview roster size** between 4, 6, 10, and 27. It should fill downward in one
  column while there is room, then balance across two columns without crossing the
  simulated scoreboard sidebar or the bottom of the screen.
- Move **Horizontal inset** from minimum to maximum. The dark vanilla-sidebar boundary
  must remain stationary while only the overlay moves right and resizes to stay inside it.
- Confirm every setting title and blue value are separated and the preview panel's right
  edge aligns with, rather than protrudes from, the simulated dark sidebar.
- With six preview survivors, confirm all nine item types appear as crisp, flat white
  silhouettes on black slots—no letter abbreviations, gradients, or colored tint.
- Confirm every card orders its slots like vanilla L4D2: throwable on the left, kit/ammo
  in the middle, pills/adrenaline on the right.
- Move each UI control and confirm the preview updates immediately.
- Confirm **UI size** stops at 1.00× and still allows shrinking to 0.60×.
- Confirm **Vertical start** defaults to 59% and that the preview panel's right edge meets
  the simulated sidebar edge with one to four preview cards.
- Click several positions along every slider track. Each thumb should jump directly to the
  pointer instead of stepping one increment left or right.
- Confirm there are no **Automatic enlargement** or **Sidebar width** controls. These are
  fixed layout rules because the vanilla scoreboard dimensions are not user-adjustable.
- Confirm **Who to show** offers **All survivors**, **Mortal soldiers + followers**, and
  **Followers only**, that **All survivors** is selected by default, and that the preview
  header changes to `EXTRA SURVIVORS` / `SOLDIERS + FOLLOWERS` / `FOLLOWERS` as it is
  changed.
- Confirm **Exit when L4D2 closes** has clearly readable white text, is checked by default,
  and persists after Save & Apply.
- Confirm the editor header shows amber **L4D2: NOT RUNNING** before launch and changes to
  green **L4D2: RUNNING** within one second after starting the game.
- Confirm the editor header reads **by DrStr4nge**, and that the tray menu's first line
  carries the same credit.
- Confirm the editor opens large enough to read the preview, and that the control area
  scrolls rather than shrinking the preview when the window is made small.
- Confirm the settings sit in a visibly darker, bordered container, that the bottom edge of
  that container fades while there is more below, and that the fade clears when the list is
  scrolled to the end.
- Confirm the settings list shows an obvious blue scrollbar without hovering it, that
  dragging that bar scrolls the list, and that the blue **More settings below** line under
  the buttons disappears once the list is scrolled to the bottom.
- Tick **Show HUD consistently**, **Save & Apply**, and confirm in game that the HUD stays
  on screen without holding Tab and still hides when L4D2 loses focus. Untick it afterwards
  to return to hold-to-show.
- Set **Preview roster size** to 27, click **Reset UI**, and confirm **Show HUD
  consistently** and **Exit when L4D2 closes** keep their values.
- Confirm the editor opens on **Live, on the real overlay** on a fresh configuration, and on
  whichever preview was last used afterwards. With **Preview** on live, the editor should
  fold its preview away, move clear of the top-left sidebar, and the real panel should appear over the game
  (or over the desktop with L4D2 closed) with sample cards, a blocked-out scoreboard area,
  and guide lines for the sidebar edge and vertical start. Move **Vertical start** and **UI size** and confirm the real
  panel follows immediately.
- **Scoreboard hold.** With a campaign loaded and live preview on, tick **Hold the game's
  scoreboard open**. L4D2's real scoreboard should appear and **stay up while you keep using
  the editor** — that is the whole point; it no longer depends on which window has focus.
  Untick it and the scoreboard should close within about a fifth of a second.
  - `console.log` should carry `[OVLHUD] scoreboard hold: SendToConsole()` the first time.
    If instead it says `scoreboard hold unavailable`, that route does not exist on this
    build — send the line back, and the panel will keep marking the region instead.
  - With the hold on, kill `OverlayHud.exe` from Task Manager. The scoreboard must close by
    itself within about two seconds; the addon times the hold out rather than leaving it
    latched.
  - With the hold on, change chapter. The scoreboard must not come back on its own.
  - Confirm the game is never pulled to the foreground by ticking the box, and that Tab
    still toggles the scoreboard by hand afterwards.
- Repeat and click **Save & Apply** instead: the panel should stay where live preview left
  it, and the values should persist after restarting the app.
- Switch back to **Simulated, in this window** and confirm the canvas returns and the
  overlay stops drawing.
- Close the editor with **Cancel**, reopen it, and confirm it comes back on the preview and
  scoreboard setting you last chose. Confirm the same after restarting the app.
- Click **Cancel**, reopen the editor, and confirm the discarded values did not affect the
  live overlay.
- Click **Save & Apply**, restart the app, and confirm the saved UI values remain.
- Set **Preview roster size** to 27, click **Reset UI**, and confirm it returns to 6.
  Also confirm reset leaves `statePath`, `gameProcess`, `holdKey`, `editorKey`, and debug
  options unchanged after saving.

## Run

Start a campaign with the soldiers spawning, then:

- At the main menu or in a lobby, confirm
  `Left 4 Dead 2 Customized Overlay HUD - External v1.0.2` appears at the top right
  without holding Tab. It should disappear shortly after the round begins exporting and
  disappear immediately when L4D2 loses focus.
- **Hold Tab.** The panel should appear at top left, directly below the scoreboard, within
  a frame or two and vanish on release. The game's own scoreboard will also appear — that
  is expected, since both react to the same key.
- Bind Insert to a harmless visible command for this check. Press **Insert** by itself and
  confirm its in-game bind still executes. Then hold **Tab**, press **Insert**, and confirm
  the editor opens while that Insert bind does not execute. Release Tab before Insert once
  as well; the app must still suppress the matching Insert release without leaving either
  key stuck.
- Change a slider without saving, then press **Tab+Insert** again while the editor is
  active. Confirm the editor closes and reopening it shows the old saved value—the second
  shortcut is equivalent to **Cancel**.
- Try 1920x1080 and one smaller resolution (1280x720 or 960x540). The panel should retain
  the same relative position and size. With eight total survivors it should show only
  survivors 5–8 in one column and use the space vacated by the vanilla survivor HUD.
- Test a larger roster if available. The overlay should fill one full-size column all the
  way down first. Only a measured screen-height overflow should balance the
  cards across exactly two columns; shrinking should occur only if those two columns still
  exceed the sidebar bounds.
- With the default 1920x1080 layout, ten extras should remain in one column; eleven and
  twelve extras should switch to two columns with no card clipped at the bottom edge and
  no part of the right column crossing the scoreboard sidebar boundary.
- With a short roster (four extras), confirm the panel's right edge lines up with the right
  edge of the vanilla scoreboard rows above it rather than stopping short of them.
- **Click and shoot while holding Tab.** Clicks must pass straight through. If the game
  loses focus or minimises when the panel appears, stop and say so.
- **Roster filters.** Spawn soldiers, turn `!cfmortal` on, and send one soldier to follow
  you, so all three classes are present at once. Then, for each **Who to show** option:
  - **All survivors** — every mortal soldier, the follower, and any extra survivor appear;
    no card for an immortal holdout soldier, and no duplicate of the vanilla four.
  - **Mortal soldiers + followers** — extra plain survivors drop off; soldiers stay.
  - **Followers only** — only the soldier following you remains. Press the follow key
    again and the panel should empty within a fraction of a second.
  - The follower's card should show a blue **FOLLOW** marker in the first two options and
    no marker in **Followers only**. The marker must appear and disappear as you toggle
    follow, alongside `DOWN` / `B&W` rather than replacing it.
- Walk a mortal soldier far enough away to trip the distance suspension (it goes team 4 to
  dodge the bot-catchup teleport). It must **stay** on the panel, not flicker off and back.
- Get incapped, get revived, go black-and-white, let a soldier die, pop pills. Check each
  reads correctly on the panel.
- Alt-tab out. The panel must disappear entirely, and Tab on the desktop must do nothing.
- Alt-tab back and hold Tab again.
- With **Exit when L4D2 closes** checked, close L4D2 and confirm the overlay tray icon exits
  within about one second. Relaunch the overlay before L4D2 and confirm it waits instead of
  immediately exiting. Finally disable the option, Save & Apply, close L4D2, and confirm
  the overlay remains in the tray.
- While one overlay is running, launch `OverlayHud.exe` again. Confirm a dialog says the
  overlay app is already running and points at the notification area, that dismissing it
  closes only the duplicate, and that there is still only one process, tray icon, and
  editor; the active instance must remain unaffected.

## What to check

| Thing | Expected |
|---|---|
| Menu/lobby badge | Top-right version appears only while L4D2 is focused and exports are inactive |
| Survivor count | Only roster positions 5 and up; the vanilla first four are not duplicated |
| Holdout soldiers | Never on the panel, in any of the three filters |
| Panel header | Names the active filter and counts what it drew |
| Oversized roster | At most two balanced columns, contained within the scoreboard sidebar |
| Health bar | Matches the in-game HUD |
| Temp health | Pale segment appears on pills/adrenaline and shrinks over time |
| `DOWN` / `DEAD` / `B&W` | Match what is actually happening |
| Item icons | Medkit, defib, ammo packs, pills, adrenaline, Molotov, pipe bomb, and bile match what each survivor carries |
| Frame rate | No new hitching |

## What to send back

- `left4dead2\console.log`
- Whether Tab show/hide felt instant
- Whether clicks passed through
- Anything on the panel that disagreed with the real HUD

## If the panel says NO EXPORT

The addon is not writing `ems\overlay_hud_state.json`. Check its timestamp: if it is not
advancing while a map is loaded, the script is not running. Almost always the VPK was
swapped while L4D2 was open — close the game, confirm exactly one
`overlay_hud_export_*.vpk` in `addons\`, start it again, and look for the
`[OVLHUD] ... loaded` line in `console.log`.

Turn the top-right badge off with **Show status badge** once the setup is proven; it exists
to report this same condition.

## If the panel never appears

The status line in the panel says why — but you can only see it if the panel is drawing.
Set both of these in `overlay-app\dist\config.json` and restart the app:

```json
"alwaysShow": true,
"ignoreForeground": true
```

The panel will then stay on screen on the desktop and report what it is watching and what
went wrong. Set both back to `false` afterwards.
