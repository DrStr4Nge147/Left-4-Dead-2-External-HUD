# Testing — exporter v2.0.0 + overlay app v2.0.0

The exporter and the v2.0.0 overlay app/VPK pair were live-tested in L4D2 on 2026-08-18,
including the weapon HUD and its ammunition reads. This checklist remains the repeatable
regression procedure for future builds: whether the overlay appears over the game, follows
Tab, and stays out of the way.

## Scoreboard hold vs the Consistent HUD (v2.0.0)

1. With the Consistent HUD on (F7), hold Tab. The persistent HUD — roster, You card, and
   weapon panel — must disappear and the scoreboard panel take its place, alongside L4D2's
   own scoreboard.
2. Release Tab. The Consistent HUD must come straight back, in the same position and at the
   same size as before.
3. Turn the Consistent HUD off and repeat: holding Tab still shows the scoreboard panel,
   releasing shows nothing at all.
4. Hold and release quickly several times and confirm neither panel is left behind and the
   layout does not drift.

## Weapon HUD (v2.0.0)

1. Confirm `console.log` carries one line for each read route shortly after the first
   survivor is seen:

   ```text
   [OVLHUD] clip read: Clip1()
   [OVLHUD] reserve ammo read: m_iAmmo[m_iPrimaryAmmoType]
   ```

   Either clip line is fine — `Clip1()` or `m_iClip1`. A `no route available` line means
   that value stays `-1` for the session and no ammunition will ever draw.
2. Confirm `ems\overlay_hudmmo.txt` exists once a round is running, and that its first
   number changes several times a second. That is the 20 Hz ammunition channel.
3. Fire a full magazine from an Uzi or an M60 and watch the counter: it should count down
   round by round rather than jumping in twos. Delete `ammo.txt` mid-round and confirm the
   counter keeps working — coarser — and recovers on its own when the file returns.
4. Turn the Consistent HUD on (F7 by default) with **Show your weapons and ammo** checked.
   Confirm the weapon panel appears in the lower right with your primary weapon, the
   magazine large and the reserve under it, and your pistol below that. Fire and reload:
   both numbers must track the real in-game ammo counter exactly.
5. Confirm the silhouettes are distinguishable at a glance and correctly sized against each
   other — a pistol clearly smaller than a rifle, a sniper the longest.
6. Pick up a Magnum, then a chainsaw, then a melee weapon. The secondary slot must follow
   each one, and a melee weapon must draw its own icon rather than a plain box. A plain box
   for every melee means the `melee id read` route failed; check `console.log`.
7. Start with one pistol and confirm the slot draws a single pistol, then pick up a second
   and confirm it changes to the pair. If `console.log` carries
   `dual pistol read: m_isDualWielding unavailable`, the exporter is guessing from the
   magazine instead — in that case a pair below 16 rounds will show as a single pistol,
   which is expected, and worth reporting.
8. Drop the primary entirely and confirm its slot disappears while the secondary stays,
   with the panel still sitting in its corner rather than shifting.
9. Switch **Weapon HUD corner** to Lower Left and confirm the panel moves without disturbing
   the roster or the Separate You card. Set the roster to the lower-left template as well
   and confirm the two do not overlap in a way you cannot live with.
10. Switch **Weapon slot arrangement** to Horizontal and confirm the slots sit side by side
   with one gap between them and none trailing after the last.
11. Drag **Weapon HUD height** from 0% to the top of its range and confirm the panel travels
   the full height of the screen and stays fully on screen at both ends.
12. Uncheck **Show your weapons, ammo, and items**, Save & Apply, and confirm the panel
   disappears entirely — no empty box, no reserved space.
13. Confirm the Tab scoreboard never shows the weapon panel, with the setting either way,
    and that the survivor cards themselves show no weapons in either HUD design.
14. Confirm the panel fades with the Consistent HUD's opacity slider, and that changing
    **HUD size** moves it too.
15. Drag **Weapon HUD size** across its range and confirm the panel grows and shrinks on
    its own, with the roster and the Separate You card untouched, and that it stays in its
    corner at both ends. Reset returns it to 1.00x.
16. Confirm the item row under the weapons follows what you carry: pick up and throw a
    Molotov, use a medkit, take pills. Each slot must fill and empty on its own, and the
    three places must stay put — an emptied slot draws a dim box rather than closing up.
17. Confirm your own Consistent HUD card draws no items, in both Basic and Minimalist, while
    every other survivor's card still shows theirs, and that the Tab scoreboard shows the
    whole team's including your own.
18. Drop everything, empty-handed, and confirm the panel hides. Pick up pills alone and
    confirm it comes back with the item row and no weapon slots above it.
19. Switch between primary, secondary, throwable, kit, and pills and confirm the green
    outline follows your hands, one slot at a time, and clears when you put everything
    away. With a pistol pair and with a melee weapon, confirm the secondary slot marks
    correctly rather than the primary.
20. Confirm `console.log` carries `[OVLHUD] ammo upgrade read: m_upgradeBitVec +
    m_nUpgradedPrimaryAmmoLoaded` once a round is running. A `no route available` line
    means every magazine will read as normal rounds for the session.
21. Deploy an incendiary pack and confirm the mark beside the magazine becomes an orange
    flame and the reserve count under it disappears. Confirm the number is the upgraded
    rounds left rather than the magazine: reload mid-upgrade and it must not jump back up,
    it must keep counting down from where it was. Fire the upgraded rounds off and confirm the grey cartridge and the reserve
    count both return on the round they run out - not a magazine later. Repeat with an
    explosive pack and confirm the yellow burst.
22. With an older exporter VPK installed (v1.2.0), confirm the panel stays hidden and
    nothing breaks — the version badge is expected to report the mismatch.

## Setup

1. **Close L4D2 first.** Addon VPKs are mounted at startup; swapping one while the game runs
   does not load the new script and silently unloads the old one. Every "the overlay stopped
   working" report so far has been this.
2. Repack the addon if any script changed, then remove any older
   `overlay_hud_export_*.vpk` from `left4dead2\addons\`:

   ```text
   powershell -ExecutionPolicy Bypass -File tools\Build-AddonVpk.ps1
   ```
3. Copy `compiled vpks\overlay_hud_export_v2.0.0.vpk` into:

   ```text
   E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\addons\
   ```

   Use the rebuilt VPK with format version **1**. The target L4D2 build rejects VPK v2 with
   `Unknown version 2`.

4. Launch options must include **`-windowed -noborder`**. Without borderless, nothing can
   draw over the game. Keep `-condebug`.
5. Delete `left4dead2\console.log`.
6. Start L4D2, and confirm `console.log` carries
   `[OVLHUD] Overlay HUD Export 2.0.0 loaded - exporting to ems/overlay_hud/state.json`. If
   that line is absent, the addon is not mounted and nothing below will work.
7. Once the new exporter has written `ems\overlay_hud\state.json`, delete the three files
   the older builds left loose at the top of `ems\`: `overlay_hud_state.json`,
   `overlay_hud_cmd.txt`, and `overlay_hud_probe.txt`. Nothing reads them any more, but a
   stale `overlay_hud_state.json` is exactly what the app falls back to when the new one is
   missing, so leaving it there can mask a folder that never got written.
8. Start the overlay:

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
- Confirm the ammunition mark is on the primary slot only, never beside a pistol or melee
  count.
- Confirm every slot row orders like vanilla L4D2 - throwable on the left, kit/ammo in the
  middle, pills/adrenaline on the right - on the Scoreboard cards, on the other survivors'
  Consistent HUD cards, and on the weapon HUD's own row.
- Move each UI control and confirm the preview updates immediately.
- Confirm **UI size** stops at 1.00× and still allows shrinking to 0.60×.
- Confirm **Vertical start** defaults to 59% and that the preview panel's right edge meets
  the simulated sidebar edge with one to four preview cards.
- Click several positions along every slider track. Each thumb should jump directly to the
  pointer instead of stepping one increment left or right.
- Confirm there are no **Automatic enlargement** or **Sidebar width** controls. These are
  fixed layout rules because the vanilla scoreboard dimensions are not user-adjustable.
- Confirm the Scoreboard tab's **Who to show** offers **Extra survivors**, **Mortal soldiers +
  followers**, and **Followers only** — no **All survivors** — that **Extra survivors** is
  selected by default, and that the preview header changes to `EXTRA SURVIVORS` /
  `SOLDIERS + FOLLOWERS` / `FOLLOWERS` as it is changed.
- Confirm the Consistent HUD tab has its own **Who to show** with all four options, defaulting
  to **All survivors**, and that changing either tab's filter leaves the other's alone.
- With a config carried over from v1.2.0 where the filter was **All survivors**, confirm the
  Scoreboard tab opens on **Extra survivors** and the Consistent HUD on **All survivors**.
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
- Open the **Consistent HUD** tab. Confirm **Show HUD consistently** is separate from the
  Scoreboard tab, the default toggle key reads **F7**, and **Set key** captures a different
  key while **Clear** disables it. Save a changed key and confirm it is still shown after
  reopening the editor.
- In the Consistent HUD tab, switch through **Bottom - Horizontal Grid**, **Lower Left Vertical
  Grid**, and **Lower Right Vertical Grid**. Horizontal previews should show four-across rows
  without the scoreboard block or its header: six cards should appear as 4+2, and 12 cards as
  4+4+4. The bottom horizontal grid should be centered, and the vertical grids should keep one
  card per row at their named lower corner. Move **Vertical position** from `0%` upward and
  confirm both simulated and live previews move the HUD without moving the scoreboard. Set
  **Horizontal spacing** and **Vertical spacing** to positive values, then negative values, and
  confirm cards spread apart and can overlap. Live preview should show the selected plain HUD
  layout and must not hold L4D2's real scoreboard.
- In the Consistent HUD tab, switch **HUD design** between **Basic** and **Minimalist**. Basic
  should keep the existing full cards. Minimalist should show the survivor name and item icons
  before the health value above a five-segment, vertically compressed strip. Give a survivor temporary health and confirm
  the buffer portion keeps the brush/grunge texture; item icons should have no rectangular slot
  box, only a black outline. Try a long survivor name and confirm it truncates while all icons
  remain present. Confirm the same design is used by live preview, simulated preview, vertical
  cards, and the separate You card; the Scoreboard tab should not change.
- Toggle **Show health numbers** off and confirm the numeric value disappears from both designs
  while the bars remain. Toggle **Black & white theme** on and confirm health bars, state labels,
  follower markers, and warning edges use only grayscale colors. The Scoreboard tab should retain
  its normal colored health/state presentation.
- Tick **Separate You** in the Consistent HUD tab. In simulated preview, the first sample card
  should leave the shared grid and appear as one independent card at the lower right for the
  bottom horizontal and lower-left vertical templates; six total samples should then show three
  shared roster cards on the first row and two on the second. This is the three-column layout
  used when Separate You is enabled; unticking it should restore six cards as 4+2. For **Lower
  Right Vertical Grid**, the shared roster stays lower right and the You card mirrors to lower
  left. Change horizontal and
  vertical spacing, including negative values: only the shared roster cards should move or
  overlap, while the You card keeps its own spacing-free position and a visible gap remains
  between the two groups. With the rebuilt exporter in a hosted game, confirm the current host
  survivor is the separated card and does not blink when the overlay is redrawn. Untick it and
  confirm all cards return to the original shared roster layout.
- Tick **Show HUD consistently**, **Save & Apply**, and confirm in game that the compact HUD
  stays on screen without holding Tab and still hides when L4D2 loses focus. Press the selected
  toggle key and confirm it turns off/on once per press, with key-repeat not causing extra
  toggles. Untick it afterwards to return to hold-to-show.
- Set **Preview roster size** to 27, click **Reset UI**, and confirm **Show HUD consistently**,
  its template, the toggle key, and **Exit when L4D2 closes** keep their values.
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
  `Left 4 Dead 2 Customized Overlay HUD - External v2.0.0` appears at the top right
  without holding Tab. It should disappear shortly after the round begins exporting and
  disappear immediately when L4D2 loses focus.
- **Hold Tab at the main menu, after having played at least one round this session.**
  Nothing should be drawn — no panel, no message, and above all no cards from the round just
  finished. Restart the app and hold Tab at the menu again: still nothing, because
  `exporterProven` in `config.json` is now `true`.
- **The status corner.** Before the first round of a fresh install, the badge should carry
  `WAITING FOR A ROUND` beneath it, saying the addon is installed. Confirm it is gone once a
  round has exported. Then test that it tells the truth about the addon: move the VPK out of
  `addons\` and restart both — it must say `ADDON NOT INSTALLED`. Put it back, disable the
  addon in the in-game **Add-ons** screen — `ADDON TURNED OFF`. **Turn it back on without
  restarting the app**: the line must clear within a few seconds. Subscribe to the Workshop
  copy while the manual VPK is still in `addons\` — `MORE THAN ONE COPY`. A Workshop-only
  install must be recognised as installed, despite being stored under a numeric filename.
- **Version match.** With the shipped pair installed, confirm nothing about versions appears
  anywhere — no line under the badge, no banner in the editor. Then edit `addonversion` in a
  copy of the pack's `addoninfo.txt`, rebuild it as `1.1.1`, and restart both:
  - The status corner must read `UPDATE THE OVERLAY APP` **at the main menu and during a
    round**, naming both versions and the releases URL. The panel, the roster filters and
    the scoreboard hold must all keep working exactly as before.
  - The editor must show an amber **Update the overlay app** banner with a clickable link
    that opens the releases page in the default browser.
  - Set the pack back to a version *below* the app's and confirm the message becomes
    `UPDATE THE EXPORTER ADDON`, with no link in the editor — that half comes from the
    Workshop, not from a download.
  - Untick **Show status badge** and confirm the in-game line goes with the rest of the
    corner.
- **Tick Debug console** in the editor, or open it from the tray menu. Confirm the top block
  shows `exporter live` during a round and `export stopped` at the menu, that it names this
  app's version, the addon's and the verdict, that the state file
  path is the one the addon is writing, and that alt-tabbing produces focus lines. Close the
  window and confirm the checkbox and the tray tick both clear.
- **Hold Tab.** The panel should appear at top left, directly below the scoreboard, within
  a frame or two and vanish on release. The game's own scoreboard will also appear — that
  is expected, since both react to the same key.
- Bind Insert to a harmless visible command for this check. Press **Insert** by itself and
  confirm its in-game bind still executes. Then hold **Tab**, press **Insert**, and confirm
  the editor opens while that Insert bind does not execute. Release Tab before Insert once
  as well; the app must still suppress the matching Insert release without leaving either
  key stuck.
- **Alt-tab away and back, several times, and after a map load.** Hold Tab each time. The
  panel must still appear, and it must draw over the game rather than behind it. This is the
  v1.0.4 fault: the hold key stopped working mid-session and only restarting the app fixed
  it. Play a full campaign before calling it confirmed — the hook was lost to a timing
  overrun, so a handful of clean alt-tabs proves nothing on its own.
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
  you, so all three classes are present at once. The scoreboard panel and the Consistent HUD
  have separate filters, so check each view against its own. For each **Who to show** option:
  - **All survivors** (Consistent HUD only) — every mortal soldier, the follower, the four
    original survivors, and any extra plain survivor appear; no card for an immortal holdout
    soldier. The four vanilla survivors appearing here is intentional: the vanilla survivor
    HUD is hidden while this one is up.
  - **Extra survivors** — the four original plain survivors drop off; the extra plain survivor,
    mortal soldier, and follower remain. This is what the scoreboard panel always does with
    plain survivors, whichever of its three options is selected.
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
- Check the bar colour at the boundaries, easiest with `give health` / `hurtme` on a listen
  server: 40 HP must be green, 39 amber, 25 amber, 24 red. Exactly 40 and exactly 25 were
  one step off through v1.0.8.
- With an addon that restores extra survivor bots, let the whole team go down and wait for
  the same-map round restart. Confirm `console.log` prints
  `[OVLHUD] re-arming exporter (scriptedmode, first export in 1s)` or the same line for
  `director`, then confirm the `state.json` timestamp resumes advancing and the extra cards
  return within a second or two. This is the regression case for v1.0.8: through v1.0.7 the
  exporter stopped writing at the wipe and the panel consequently showed zero while the
  restored bots were already standing in the room.
  - Chapter progression is a separate path and was never affected - it goes through
    `mapspawn_addon.nut`, which a restart does not re-run. Worth walking one map end to end
    anyway, since the same re-entry files now fire on that path too.
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
| Version match | Silent while both halves agree; names the stale half in the menu and in game when they do not, and blocks nothing either way |
| Survivor count | The scoreboard never draws the original four; the Consistent HUD's All survivors does, and its Extra survivors keeps only positions 5 and up for plain survivors |
| Holdout soldiers | Never on the panel, in any of the four filters |
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

## If nothing is exporting

The addon is not writing `ems\overlay_hud\state.json`. Check its timestamp: if it is not
advancing while a map is loaded, the script is not running. Almost always the VPK was
swapped while L4D2 was open — close the game, confirm exactly one exporter pack is
installed, start it again, and look for the `[OVLHUD] ... loaded` line in `console.log`.

The line under the top-right badge names the cause when the cause is on disk — not
installed, installed twice, or turned off in the Add-ons screen. The **Debug console** has
the rest: the file being watched, the poll count, and whether `seq` is advancing.

Turn the whole status corner off with **Show status badge** once the setup is proven.

## If the panel never appears

Open the **Debug console** from the tray menu. Its top block says whether the hold key is
being seen, whether L4D2 is in front, whether the exporter is live, and whether the panel is
drawing — which is the whole chain, in order.

To take the game out of the picture entirely, set both of these in
`overlay-app\dist\config.json` and restart the app:

```json
"alwaysShow": true,
"ignoreForeground": true
```

The panel then stays on screen on the desktop, with no hold key and no foreground gate
involved. Set both back to `false` afterwards.
