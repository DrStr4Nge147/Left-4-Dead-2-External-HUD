# Changelog

Two components, released under one shared version: the exporter addon and overlay app.

## Overlay HUD v1.2.0 - 2026-08-15: separate the scoreboard from the consistent HUD

The Tab-held panel is a scoreboard companion; a persistent HUD needs its own visual role,
placement, and controls. The editor now keeps those jobs separate.

- Adds **Scoreboard** and **Consistent HUD** tabs. The scoreboard tab retains the Tab-held
  vanilla-sidebar panel and its real-scoreboard live preview.
- Adds a distinct persistent-HUD renderer: a row-major survivor grid with no scoreboard frame or
  roster header, using the same health, state, and item data. It lays out four cards across and
  up to three rows, adding columns for larger rosters.
- Adds three consistent-HUD templates: **Bottom - Horizontal Grid** (the default), **Lower Left
  Vertical Grid**, and **Lower Right Vertical Grid**. The retired Top - Horizontal Grid is no
  longer selectable; old `top-vertical` configs migrate to the bottom-centered grid. Existing
  `vanilla-vertical` configs retain the previous one-card-per-row behavior, and retired
  `bottom-right` configs migrate to the bottom-centered grid.
- Adds a Consistent HUD-only **HUD design** selector. **Basic** keeps the existing full card;
  **Minimalist** places the name and item icons before the health value above a segmented health
  strip, keeps the temporary-health grunge texture, and uses icon-only items with a black outline.
  Minimalist now uses five vertically compressed segments. The Consistent HUD also has options to
  hide health numbers and switch the complete HUD palette to black and white; the Scoreboard tab
  remains unchanged.
- Adds a saved **Vertical position** slider for the consistent HUD. Zero is the bottom edge;
  higher values move the HUD upward without changing the scoreboard's position.
- Adds independent **Horizontal spacing** and **Vertical spacing** sliders for consistent-HUD
  cards. Negative values are allowed for intentional overlap; the scoreboard is unchanged.
- Adds **Separate You** for the consistent HUD. With **Bottom - Horizontal Grid**, the
  remaining roster moves to the lower left and the listen-server host's current survivor card
  sits at the lower right; **Lower Right Vertical Grid** mirrors that relationship so the
  roster stays right and the You card sits left. The roster group reserves the You card's
  width plus a visible gap, so the two groups cannot overlap. When Separate You is enabled on
  the bottom horizontal grid, the shared roster starts at three columns across so it uses the
  space beside You; with it off, the normal four-column layout remains. Spacing controls govern
  the remaining roster only. Older exporters keep the original all-in-roster layout.
- Adds independent consistent-HUD size and opacity controls, plus a configurable single-key
  toggle (F7 by default). The toggle is consumed by the app, works only while L4D2 is in
  front, and saves its enabled/disabled state.
- Sets the settings section headings to an explicit light foreground so they remain readable
  on the dark control cards.
- Keeps `rosterFilter` shared between both views, so All survivors, Extra survivors, and the
  Finale Soldiers filters behave consistently.
- Advances the exporter addon and desktop overlay together to v1.2.0. The exporter adds the
  optional `local` transport field for the listen-server host and carries the matching version
  stamp; older state files remain readable.
- Rebuilds the Workshop VPK explicitly as VPK format version 1; VPK v2 is rejected by the
  target L4D2 build.

## Overlay HUD v1.1.0 - 2026-08-15: choose the complete roster or the extras-only panel

The old **All survivors** setting was really an extras-only view: it avoided duplicating the
four survivor rows that L4D2 already draws. The panel now makes those two meanings explicit.

- **All survivors** draws every mortal survivor in the export, including the four original
  survivor slots. Immortal team-4 holdout soldiers remain excluded.
- **Extra survivors** preserves the previous All behavior: plain survivors from slot 5 onward,
  plus mortal soldiers and followers.
- The existing **Mortal soldiers + followers** and **Followers only** filters remain unchanged.
- The editor and live panel now expose four mutually exclusive roster choices, with distinct
  `ALL SURVIVORS` and `EXTRA SURVIVORS` headers.
- Advances the exporter addon and desktop overlay together to v1.1.0. The exporter already
  writes the complete roster, so its runtime behavior is unchanged apart from the version stamp.
- Extends the roster-filter layout check to cover both complete-roster and extras-only behavior,
  the legacy no-`cls` export, and all four editor controls.

## Overlay HUD v1.0.10 - 2026-08-15: the app says when it is the half that is out of date

The two halves ship under one version but arrive by different routes. The addon updates
itself through the Workshop; the app has to be downloaded. Nothing told anyone when they had
drifted apart, so an old app could sit there for weeks looking perfectly healthy.

- The app now reads `addonversion` out of the installed pack's own `addoninfo.txt` and
  compares it against its own build. The manifest is read from the same VPK walk that
  already identifies the pack, so this costs one extra file entry, not another scan.
- On a mismatch the status corner carries `UPDATE THE OVERLAY APP` - or
  `UPDATE THE EXPORTER ADDON` when the app is the newer half - in the main menu **and**
  during a round, since a mid-round Tab is the only time some players look at it.
- The editor shows the same message with a clickable link to the releases page. The overlay
  is click-through by design and cannot hold a link; the editor can.
- **A mismatch blocks nothing.** The HUD, the roster filters, the editor and the scoreboard
  hold all keep working; the difference is only reported.
- Unknown stays silent: no pack installed, an unreadable manifest, or a version string that
  is not a version claims nothing rather than warning about nothing.
- `state.json`'s `v` field is the fallback source, for an install whose manifest cannot be
  read. The manifest is preferred because it answers at a main menu, before any map has
  loaded and before anything has been exported.
- The debug console's top block now names both versions and the verdict.
- Advances the exporter addon and desktop overlay together to v1.0.10. The exporter's
  behaviour is unchanged; it carries the version so the pair stays on one number.
- Adds a `version-gate` layout check covering manifest parsing in both storage forms, all
  four verdicts, the in-game notice and the editor's link.

## Overlay HUD v1.0.9 - 2026-08-15: the health colour bands land on their stated numbers

The panel's documented bands are green at 40-100 HP, amber at 25-39, red at 1-24. The bar
did not draw them: 40 HP came out amber, and the amber band ran all the way down to 21, so
24 HP was amber where it should have been red.

- Both band comparisons are now inclusive, and the amber floor sits at 25% of max rather
  than 20%. 40 HP is green, 39 is amber, 25 is amber, 24 is red.
- Bands remain a fraction of the survivor's own maximum, so a 50-max survivor scales.
- Advances the exporter addon and desktop overlay together to v1.0.9. The exporter's
  behaviour is unchanged; it carries the version so the pair stays on one number.
- Adds a `health-colour` layout check covering every band boundary and the scaled maximum.

## Overlay HUD v1.0.8 - 2026-08-14: the HUD survives a same-map round restart

A full-team wipe restarts the map in place, and extra survivors restored by another addon -
Finale Soldiers' followers, Spawn L4D1/L4D2 Survivors' bots - came back into a panel that
read zero. The exporter's export loop is an `EntFire` chain living on the map's worldspawn,
and a restart tears that down without re-running `mapspawn_addon.nut`, so nothing wrote the
state file again for the rest of the session. The app was reading a frozen file and doing
the right thing with it.

- The exporter now ships `scriptedmode_addon.nut` and `director_base_addon.nut` alongside
  `mapspawn_addon.nut`. Both re-run on a same-map restart and re-arm the export loop; the
  existing generation guard means the doubled call costs nothing.
- Re-arming after a restart waits 1 second before the first export, not the 5 seconds a
  cold chapter load needs, so the panel comes back about as fast as the survivors do.
- An empty roster is never written during the first 4 seconds after a boot. A tick that
  lands mid-respawn holds the panel blank rather than publishing a confident zero, which is
  the failure this release exists to remove. Past that window a zero is written, because by
  then it is true.
- Chapter progression was never affected and is unchanged.
- Advances the exporter addon and desktop overlay together to v1.0.8. The app's restart
  behavior is unchanged - its `EXTRA SURVIVORS 0` was the correct reading of a stale file
  throughout.

The overlay's health bars also read like the game's now:

- Temp health from pills or adrenaline draws as a scratched overlay in the bar's **own**
  colour, the way the vanilla HUD draws the buffer, instead of the fixed pale tint that
  came out looking grey next to every bar colour.
- A black-and-white survivor keeps a grey bar, and his buffer is grey with it - grey
  because the bar is grey on the last strike, not because buffers are grey.
- A downed survivor's bar is scratched across its whole length rather than a flat block,
  which is how the game draws incap health in its own panels.
- A black-and-white survivor's card outline pulses red. It is the one state worth catching
  without reading the panel, and the pulse is shared, so several cards alarm in step
  instead of each on its own beat.

Supersedes the unreleased v1.0.6 and v1.0.7 builds, which tried to recover through a
`round_start_post_nav` listener. That callback is never delivered to this addon, from any
entry point tried; see the dev log.

## Overlay HUD v1.0.5 - 2026-08-13: the hold key stops dying mid-session

Reported as the overlay showing at first and then never again after an alt-tab, with a
restart of the app as the only fix. Windows removes a `WH_KEYBOARD_LL` hook whose callback
overruns `LowLevelHooksTimeout` — no notification, no error, and the handle stays valid. The
callback was running a full WPF layout pass on a layered window before returning, which is
exactly the kind of budget that gets a hook killed under a game load spike.

- The hook callback now decides whether to swallow the key and returns. `HeldChanged` and
  `ShortcutPressed` are posted to the UI thread instead of being raised inside it, so a
  render pass is never charged against the hook timeout.
- The 250 ms geometry timer now checks the hook's health: the tracked hold state is compared
  against the physical key, corrected immediately when they disagree, and the hook is
  reinstalled when the disagreement survives two polls. One replacement per failure.
- That poll is also a fallback. If the reinstall fails the panel still follows the hold key;
  only the Tab+Insert suppression needs the hook to be live.
- The overlay re-asserts itself topmost when the game takes focus back. A fullscreen window
  can come up above a topmost overlay on alt-tab, and nothing was putting it back.

Holding Tab at the main menu also brought up a panel full of the previous session's
survivors. `state.json` is still on disk between sessions, and reading it once was being
treated as an export:

- A stale read now contributes no roster at all. Old cards are never drawn, in any state.
- The first sighting of the file is no longer counted as an advance. Only watching `seq`
  move proves an exporter is running, so a leftover file no longer looks freshly written
  for a whole staleness window.
- Once a live export has been seen, staleness is the menu, a lobby, a load, or the end of a
  map, and the panel stays hidden there. The top-right badge reports the state either way.
- **The `NO EXPORT` message is gone from the panel.** It claimed the addon might not be
  loaded whenever the state file was not advancing, but a file that is not advancing is also
  exactly what a main menu looks like - so on a healthy install it fired at the first Tab of
  every session and was simply wrong. It was also unreadable: a long single line beside the
  header made the panel's natural width several times the sidebar, and the fit pass shrank
  the whole panel to about a third to compensate.
- In its place, a line **under the top-right badge**, and it is backed by evidence rather
  than inference — the app now looks in `left4dead2\addons`:
  - nothing installed → `ADDON NOT INSTALLED`
  - two copies (a subscription and a manual drop, say) → `MORE THAN ONE COPY`
  - installed but switched off in the Add-ons screen → `ADDON TURNED OFF`
  - installed and no round running yet → `WAITING FOR A ROUND`, which is a statement of
    fact rather than an accusation
  - proven install, currently at a menu → nothing at all; the badge already says it
- Packs are identified by **content, not filename**. A Workshop subscription is stored as
  `addons\workshop\<publishedfileid>.vpk`, so name matching would report a subscribed addon
  as missing. Each pack's VPK directory tree is read for the exporter's script; the tree
  sits at the front of the file, so a 200 MB map pack costs no more than a small one. The
  scan runs off the UI thread and is cached until the folder's contents change.
- `addonlist.txt` is read for the enabled flag, so an addon that is installed but turned off
  is named as such instead of looking like a silent exporter. Turning it back on is picked
  up within five seconds: the cache is keyed on that file's contents as well as the packs,
  because enabling an addon rewrites one character of it and touches no VPK at all.
- The panel is now a roster and nothing else. Being seen working is still remembered, as
  `exporterProven` in `config.json`, and that is what retires the waiting line for good.

New **Debug console**, from the editor checkbox or the tray menu:

- A live block at the top answers "is this actually working": whether the exporter is live,
  stopped or never seen, the state file being watched, poll count and last roster size,
  whether L4D2 is in front and at what size, whether the hold key is down, how many times
  the keyboard hook has had to be reinstalled, and whether the panel is drawing.
- Under it, a log of transitions rather than polls — focus changes, resolution changes, the
  export starting and stopping, hook loss and recovery, and why the panel is hidden when it
  is. **Copy all** and **Save to file** produce something to attach to a report.
- Kept honest by construction: the sources are 100 ms and 250 ms timers, so the log records
  only what changed, and the buffer is bounded at 600 lines. Nothing is written from the
  keyboard hook callback, for the timeout reason above.
- App and exporter advance together to v1.0.5. No behavior change in the exporter.

## Overlay HUD v1.0.4 - 2026-08-13: the transport gets its own folder

- Both transport files move into `ems\overlay_hud\`: `state.json` and `cmd.txt`. They were
  loose at the top of `ems\` as `overlay_hud_state.json` and `overlay_hud_cmd.txt`, which is
  three unexplained files in a folder every other addon keeps one named directory in. The
  folder carries the name now, so the files no longer repeat it.
- The app reads the old loose state file when the new one is absent, and then writes its
  command file under the old name as well. The two halves are downloaded separately, so a
  current app will meet a v1.0.3 addon; it keeps working instead of reporting `NO EXPORT` at
  an exporter that is exporting fine one folder up.
- The app creates `ems\overlay_hud\` if it is not there. It can ask for the scoreboard
  before any map has loaded, which is before the addon has written anything at all.
- **Delete the old files by hand** once the new build has written the folder:
  `overlay_hud_state.json`, `overlay_hud_cmd.txt`, and `overlay_hud_probe.txt` (left by
  `v0.1.0-probe1`). A stale state file is what the app falls back to, so leaving it there can
  hide a folder that never got written.

## Overlay HUD v1.0.3 - 2026-08-13: the addon says it needs the app

- Rewrites the addon's Workshop description to open with the requirement: the separate
  overlay application is mandatory and the addon does nothing without it, with the download
  link, the host-only requirement, and the windowed-borderless requirement. A subscriber
  who reads only the first line now knows what they are getting.
- Adds `tools/Publish-Release.ps1`, which produces the two single-file release shapes:
  self-contained (~71 MB, no runtime to install) and framework-dependent (~0.5 MB, needs the
  .NET 9 Desktop Runtime). The ordinary `dist` build is a launcher stub plus a DLL and
  cannot be distributed as one file.
- App and exporter advance together to v1.0.3. No behavior change in either half.

## Overlay HUD v1.0.2 - 2026-08-13: say when the addon is not writing

Reported as three separate faults — the scoreboard hold not working, the badge showing
constantly in game, and the panel not drawing at all. All three were one fact: the exporter
in that session was v1.0.0 and had stopped writing. **A VPK replaced while L4D2 is running
is not reloaded**; the mounted pack goes away and the script does not come back.

- Draws the panel with a reason on it when the hold key is pressed and nothing is
  exporting, instead of drawing nothing: `NO EXPORT - IS THE ADDON LOADED? RESTART L4D2
  AFTER UPDATING ITS VPK.` A healthy export with an empty roster still draws nothing, which
  is the v0.2.0 behavior and unchanged.
- Adds a **Show status badge** editor checkbox for the top-right name/version, default on.
  It reports "the exporter is not writing", which is useful during setup and noise
  afterwards.
- Sections the **Who to show** card: **All survivors** on its own, then a rule and a
  **FOR FINALE SOLDIERS MOD** heading above the two options that only mean anything with
  that addon installed. All three stay in one radio group, so the split is purely visual.
- Makes **Live, on the real overlay** the default preview on a fresh install. The simulated
  canvas remains as the fallback for laying out with L4D2 closed.
- Adds a `no-export` regression check for both, and an `editor-controls` assertion that a
  fresh configuration opens on live preview.
- App and exporter advance together to v1.0.2. Exporter behavior is unchanged from v1.0.1.

## Overlay HUD v1.0.1 - 2026-08-13: the scoreboard hold, done at the right layer

- **Fixes Hold the game's scoreboard open, which did nothing visible in game.** v1.0.0 held
  the scoreboard key with synthesised input; that input goes to the focused window, which
  during editing is the editor, not L4D2.
- Adds a reverse channel to the transport: the app writes `ems/overlay_hud_cmd.txt` and the
  addon runs `SendToConsole("+showscores")` / `("-showscores")` on the listening host. The
  scoreboard now stays up while the editor is used, instead of only while the game has focus.
- Releases the hold on its own if the app stops refreshing the file for two seconds, so an
  overlay that is killed mid-hold cannot leave the scoreboard latched open. The addon also
  assumes it is closed at every chapter load.
- Drops all input synthesis and the foreground grab that went with it. The editor no longer
  pulls L4D2 forward, and there is no key that can be left stuck.
- Falls back to marking the scoreboard region when the ask cannot be delivered — no located
  install, or an unwritable game folder.
- Puts the settings in their own sunken well — a container darker than the window, with its
  own border — and fades the bottom edge while more is scrollable. The list now reads as a
  bounded region with content under it rather than as the end of the page.
- Rewrites the `scoreboard-hold` check for the file transport, including heartbeat, single
  release, and releasing a hold whose first write failed. Extends `live-preview` to assert
  the command file's contents and the fallback marker.
- App and exporter advance together to v1.0.1.

## Overlay HUD v1.0.0 - 2026-08-13: first full release

- Promotes both halves to v1.0.0. **No behavior, transport, or layout change from v0.6.6** —
  the exporter and app are byte-identical in logic, and only the version strings move.
- The feature set is complete for its stated purpose: every survivor in an oversized roster
  on screen, three Finale Soldiers roster filters, a UI editor with simulated and live
  preview, and a versioned VPK plus a published app folder as the delivery surface.
- Fixes the `live-preview` check reaching a real running L4D2: it now points at a
  guaranteed-absent process name, so the check can never pull the game forward or hold its
  scoreboard key. It passed with the game closed and failed with it open.

## Overlay HUD v0.6.6 - 2026-08-13: live preview on the real overlay

- Adds a **Preview** choice to the editor: **Simulated, in this window** (the existing
  canvas) or **Live, on the real overlay**.
- Live mode draws the actual panel over the actual game window at real geometry, with the
  live roster, updating on every slider move. Nothing is written to disk until **Save &
  Apply**; **Cancel** or closing the window restores the pre-edit settings.
- Live mode falls back to sample cards when no round is exporting, so layout can still be
  tuned at a menu or with L4D2 closed, and marks the sidebar edge, vertical start, and any
  bottom clearance with guide lines.
- Folds the editor away in live mode and docks it clear of the sidebar the panel occupies.
- Enlarges the editor to 1360x980, caps the control area at 300 px so it can no longer
  squeeze the simulated preview into a thumbnail, and shrinks to fit smaller displays.
- Replaces the editor's stock scrollbar with a permanently visible 16 px bar and a blue
  thumb, plus a "More settings below" line that clears once the list is scrolled to the
  end. The settings under the fold were easy to miss entirely.
- Adds **Hold the game's scoreboard open** to live preview. Ticking it brings L4D2 forward
  and holds its own scoreboard key down, so the panel is judged against the genuine
  in-game scoreboard rather than a drawn stand-in. The key follows focus — held while the
  game is in front, released the instant it is not — and is released on every exit path,
  including app shutdown. While the game is not in front the region is marked out instead.
- Remembers the preview choice: the editor reopens on whichever preview and scoreboard
  setting was last used, persisted as `previewMode` and `previewScoreboard`. Only those two
  reach disk on toggle — unsaved slider values still need **Save & Apply**. Preview roster
  size stays deliberately non-persistent, as decided in v0.6.4.
- Adds **Show HUD consistently** to the editor, exposing the existing `alwaysShow` setting
  so the HUD displays persistently during play instead of only while the hold key is down.
  Like **Exit when L4D2 closes**, it is outside the Reset UI copy, so a layout reset cannot
  silently turn it off.
- Tells a duplicate launch that the overlay is already running and where to find its tray
  icon, instead of exiting silently as v0.6.2 did.
- Adds a blue **FOLLOW** marker to follower cards in **All survivors** and **Mortal
  soldiers + followers**. Followers-only mode does not mark them: every card would carry it.
- Adds an application icon — three survivor health bars on the panel's own dark plate, in
  the panel's own green/amber/red — used by the executable, the tray, and the editor window
  from one embedded `.ico`. Replaces the borrowed Windows shield on the tray.
- Adds `tools/Build-AppIcon.ps1`, which draws the icon from code at seven sizes, so it can
  always be regenerated.
- Credits **DrStr4nge** as author in the editor header, the tray menu, and the assembly's
  Company/Copyright metadata, from one `AppIdentity.Author` constant.
- Adds `live-preview` and `scoreboard-hold` regression checks, and extends `roster-filter`
  to cover the marker through the rendered card template and `app-name` to cover the author.
- App and exporter advance together to v0.6.6. Exporter behavior is unchanged.

## Overlay HUD v0.6.5 - 2026-08-13: Finale Soldiers roster filters

- Adds a **Who to show** editor setting with three options: **All survivors**,
  **Mortal soldiers + followers**, and **Followers only**.
- Stops drawing immortal team-4 holdout soldiers in every mode. They cannot be hurt, so a
  health card for one was noise.
- Adds a `cls` field to the transport (`survivor`, `soldier`, `follower`, `holdout`), read
  from Finale Soldiers' own per-player scope markers rather than from `m_iTeamNum`, which
  cannot separate a holdout from a mortal soldier that is temporarily on team 4.
- Keeps the vanilla four-slot skip for plain survivors only; soldiers and followers are
  never subject to it.
- Adds a `roster-filter` regression check covering all three modes, holdout exclusion,
  editor load/write, and an exporter older than v0.6.5.
- Moves the default **Vertical start** from 62% to 59%.
- Widens the usable panel budget from 36% to 37.6% of window width, measured from an
  in-game capture where the vanilla scoreboard's rows end at x=722 of 1920. A short roster
  now reaches that edge exactly instead of stopping 25 px short of it.
- Shifts the automatic column boundary accordingly: at 1920x1080 ten extras stay in one
  column and eleven switch to two.
- App and exporter advance together to v0.6.5.

## Overlay HUD v0.6.4 - 2026-08-12: direct-click sliders and complete reset

- Enables direct point movement on every editor slider, so clicking a track moves the
  thumb to that location instead of taking one directional step.
- Makes **Reset UI** restore the preview-only survivor count to 6 as well as visual layout
  defaults.
- Adds editor-level regression coverage for both behaviors.
- App and exporter advance together to v0.6.4. Exporter behavior is unchanged.

## Overlay HUD v0.6.3 - 2026-08-12: cap manual UI size at normal

- Reduces the **UI size** slider range from 0.60×–1.40× to 0.60×–1.00×.
- Clamps manual scale in the live renderer as well as the editor, so legacy config values
  above 1.00× cannot keep applying an invisible unsupported setting.
- Keeps automatic measured fitting separate and unchanged.
- Extends editor regression coverage to lock the useful scale range.
- App and exporter advance together to v0.6.3. Exporter behavior is unchanged.

## Overlay HUD v0.6.2 - 2026-08-12: single instance and game status

- Enforces one overlay process per Windows session with a named local mutex acquired
  before WPF creates a window, tray icon, or keyboard hook.
- Makes duplicate launches exit silently without disturbing the active overlay.
- Adds a live editor-header badge: green **L4D2: RUNNING** or amber
  **L4D2: NOT RUNNING**, refreshed once per second from the configured process name.
- Adds deterministic singleton-ownership and process-status regression checks.
- App and exporter advance together to v0.6.2. Exporter behavior is unchanged.

## Overlay HUD v0.6.1 - 2026-08-12: make automatic exit readable and verifiable

- Renders the **Exit when L4D2 closes** label with an explicit high-contrast white
  `TextBlock` instead of relying on WPF's default checkbox-content foreground.
- Confirms the reported non-exit occurred with the live published configuration set to
  `exitWhenGameCloses: false`; restores this installation to checked/automatic mode.
- Extends the lifecycle regression from the state helper through the real game-process
  lookup and `MainWindow` close path.
- App and exporter advance together to v0.6.1. Exporter behavior is unchanged.

## Overlay HUD v0.6.0 - 2026-08-12: optional game-linked lifetime

- Adds an **Exit when L4D2 closes** editor checkbox, enabled by default.
- Exits the overlay after it has observed L4D2 running and then sees the game process
  close; disabling the option retains the tray app between sessions.
- Safely supports launching the overlay before L4D2 by distinguishing startup waiting from
  a real game shutdown.
- Tracks process existence separately from the main window handle so a window recreation
  or video-mode transition is not mistaken for the game closing.
- Persists the option as `exitWhenGameCloses` and adds deterministic lifecycle coverage.
- App and exporter advance together to v0.6.0. Exporter behavior is unchanged.

## Overlay HUD v0.5.4 - 2026-08-12: rename the external app

- Renames the application to **Left 4 Dead 2 Customized Overlay HUD - External**.
- Applies the exact name to the tray identity, settings and overlay window titles,
  main-menu/lobby running badge, README, and Windows Product/Title metadata.
- Keeps `OverlayHud.exe`, internal namespaces, configuration paths, and exporter addon name
  stable so existing launch commands and integration points continue working.
- Centralizes the visible app identity and adds a regression check covering every runtime
  and assembly-metadata surface.
- App and exporter advance together to v0.5.4. Exporter behavior is unchanged.

## Overlay HUD v0.5.3 - 2026-08-12: keep the vanilla sidebar fixed

- Corrects **Horizontal inset** so it moves only the overlay HUD; the simulated and live
  vanilla-sidebar boundary no longer expands with the inset.
- Subtracts the inset from available panel width, preserving containment at every inset.
- Raises **Preview extra survivors** from 16 to 27.
- Extends regression coverage to lock the fixed sidebar edge, the 27-survivor editor
  limit, and a contained 27-card two-column layout.
- App and exporter advance together to v0.5.3. Exporter behavior is unchanged.

## Overlay HUD v0.5.2 - 2026-08-12: simplify layout controls

- Removes **Automatic enlargement** and **Sidebar width** from the UI editor.
- Removes `maxFitScale` and `sidebarWidth` from configuration so old saved values cannot
  keep changing layout after their controls disappear.
- Retains the proven 1.4× fit cap and 36% vanilla-sidebar calibration as internal policy,
  shared by the gameplay overlay and editor preview.
- Adds a regression check ensuring the controls/configuration stay removed while those
  containment rules remain active.
- App and exporter advance together to v0.5.2. Exporter behavior is unchanged.

## Overlay HUD v0.5.1 - 2026-08-12: toggle the editor shortcut

- Makes **Tab+Insert** close the active editor as well as open it.
- Treats the overlay's own active editor as an allowed shortcut context while keeping the
  chord disabled in unrelated applications.
- Closes through the editor's existing Cancel behavior, discarding its unsaved draft.
- Adds an executable open/close regression check.
- App and exporter advance together to v0.5.1. Exporter behavior is unchanged.

## Overlay HUD v0.5.0 - 2026-08-12: in-game editor shortcut

- Adds **Tab+Insert** as the default shortcut for opening the UI editor while L4D2 is
  focused, avoiding a trip through the notification-area menu.
- Continues forwarding Tab so the vanilla scoreboard works normally, but consumes the
  shortcut's Insert down, repeat, and matching up messages so an in-game Insert bind does
  not execute. Insert by itself remains fully forwarded.
- Keeps suppression latched until Insert is released even if Tab is released first, and
  ignores the chord outside L4D2 unless foreground-gating debug mode is enabled.
- Adds `editorKey` configuration (`45` = Insert, `0` = disabled); the modifier remains the
  existing `holdKey`.
- Adds deterministic regression coverage for forwarding, ordering, repeats, release
  order, and the foreground gate.
- App and exporter advance together to v0.5.0. Exporter behavior is unchanged.

## Overlay HUD v0.4.3 - 2026-08-12: enlarge item silhouettes

- Enlarges source-faithful item masks from 22x18 to 26x20 inside the unchanged 30x22
  black slots for easier recognition.
- Preserves source proportions, vanilla slot order, card width, and all established
  one/two-column thresholds.
- Adds a rendered-template check locking both the larger image size and original slot size.
- App and exporter advance together to v0.4.3. Exporter behavior is unchanged.

## Overlay HUD v0.4.2 - 2026-08-12: match vanilla inventory-slot order

- Orders every card's item icons left-to-right as throwable, kit/ammo pack, then
  pills/adrenaline, matching L4D2's vanilla HUD order.
- Defines the order once in the survivor-card view model so live overlay and editor preview
  cannot diverge.
- Adds an executable item-order regression check.
- App and exporter advance together to v0.4.2. Exporter behavior is unchanged.

## Overlay HUD v0.4.1 - 2026-08-12: use the supplied item silhouettes exactly

- Replaces the hand-drawn v0.4.0 approximations with binary masks derived directly from
  the nine supplied HUD screenshots, preserving each shape, aspect ratio, padding,
  orientation, cutout, wire, rag, needle, and ammo graphic.
- Converts every light source pixel to opaque pure white and every background pixel to
  transparency over the existing pure-black item slot; no gradient or tint remains.
- Embeds the nine PNG masks into the application assembly, with the saved source images
  and deterministic mask-build script retained in the repository.
- App and exporter advance together to v0.4.1. Exporter behavior is unchanged.

## Overlay HUD v0.4.0 - 2026-08-12: replace item letters with vector icons

- Replaces all item abbreviations with native WPF vector silhouettes for medkit,
  defibrillator, explosive ammo, incendiary ammo, pills, adrenaline, Molotov, pipe bomb,
  and bile bomb.
- Uses flat pure-white icons on pure-black slots with no gradients or tint.
- Keeps the existing 30x22 item-slot dimensions, preserving established card width and
  one/two-column layout behavior at every overlay scale.
- Expands the six-survivor customization preview sample to show all nine item icons.
- Adds automated coverage and monochrome checks for every exporter item ID.
- App and exporter advance together to v0.4.0. Exporter behavior is unchanged.

## Overlay HUD v0.3.4 - 2026-08-12: make editor geometry match gameplay

- Extends the preview's simulated scoreboard region through the panel's horizontal inset,
  so its right boundary matches the same contained edge used by the live overlay.
- Disables last-child filling in setting headers and adds a 12-DIP title/value gap,
  eliminating merged labels such as `UI size1.00x`.
- Adds editor regression checks for preview containment and control-label separation.
- App and exporter advance together to v0.3.4. Exporter behavior is unchanged.

## Overlay HUD v0.3.3 - 2026-08-12: contain the freshly split columns

- Invalidates the generated card visual tree before each natural-size measurement, so a
  newly selected two-column layout cannot reuse the cached one-column dimensions.
- Fits ten and eleven extra survivors from a natural width of 966 px to the configured
  691.2 px sidebar at 1920x1080 instead of extending to x=985.
- Extends the executable WPF regression check to assert both the right and bottom edges.
- App and exporter advance together to v0.3.3. Exporter behavior is unchanged.

## Overlay HUD v0.3.2 - 2026-08-12: wrap before cards are clipped

- Measures the panel's unconstrained child content instead of its screen-clamped WPF
  `ActualHeight`, allowing real bottom overflow to trigger the second column.
- Uses the same natural-size measurement for final width/height fitting and the editor
  preview, including extremely large rosters that still overflow after splitting.
- Adds an executable WPF layout regression check covering 9, 10, and 11 extras at
  1920x1080 plus 16 extras at 1280x720.
- App and exporter advance together to v0.3.2. Exporter behavior is unchanged.

## Overlay HUD v0.3.1 - 2026-08-12: use the vacated vanilla HUD area

- Removes the default 12% bottom exclusion while the Tab roster is visible, allowing the
  first column to continue into the area vacated by L4D2's vanilla survivor HUD.
- Delays the two-column transition until the rendered cards genuinely reach the bottom of
  the game window.
- Keeps bottom clearance as an optional editor setting, now adjustable from 0-25%, for
  custom HUDs that retain bottom-left elements while Tab is held.
- App and exporter advance together to v0.3.1. Exporter behavior is unchanged.

## Overlay HUD v0.3.0 - 2026-08-12: built-in UI customization preview

- Adds a tray-accessible UI editor with a responsive 16:9 preview.
- Shares the exact survivor-card resources between the preview and live overlay to prevent
  the editor from drifting away from the in-game presentation.
- Exposes size, opacity, spare-space enlargement, offsets, sidebar width, bottom-HUD
  clearance, and the maximum column count.
- Lets the preview simulate 1-16 extra survivors; this sample count is deliberately not
  written to the runtime configuration.
- Keeps changes in a draft until **Save & Apply**, writes `config.json` atomically, and
  preserves transport, hotkey, and debug settings when resetting the UI.
- Adds `--settings` for opening the editor directly.
- App and exporter advance together to v0.3.0. Exporter behavior is unchanged.

## Overlay HUD v0.2.2 - 2026-08-12: fill downward before splitting

- Replaces estimated card-height column breaks with a measurement of the actual rendered
  one-column panel at the current resolution.
- Keeps extra survivors in one full-size column while it fits (about six at 1080p), then
  balances the roster across at most two columns.
- Reduces the reserved bottom band from 14% to 12%, matching the safe space visible above
  the vanilla player HUD in the 1920x1080 in-game screenshot.
- Applies shrinking only after the chosen one- or two-column layout genuinely exceeds the
  measured sidebar width or height.
- Enlarges short one-column rosters up to 1.4x to use otherwise empty sidebar width and
  height; the same measured bounds prevent the enlarged panel from crossing the sidebar.
- App and exporter advance together to v0.2.2.

## Overlay HUD v0.2.1 - 2026-08-12: contain two columns inside scoreboard sidebar

- Reduces the responsive sidebar width from 50% to 36% of the game window. At 1920x1080,
  the panel now occupies at most 691 px and ends near x=729 after its 2% left inset,
  matching the vanilla scoreboard's dark sidebar instead of extending into gameplay.
- Keeps the two-column split and uniformly scales both columns, cards, text, health bars,
  and item chips to the corrected width.
- App and exporter advance together to v0.2.1.

## Exporter addon v0.2.0 - 2026-08-12: align release version with overlay app

- Version-only release matching overlay app v0.2.0. Export format and runtime behavior are
  unchanged from exporter v0.1.2.

## Overlay app v0.2.0 - 2026-08-12: compact extra-survivor sidebar

- Moves the Tab panel below the top-left scoreboard and scales it with game-window height.
- Draws only roster positions 5 and up, leaving L4D2's four vanilla survivor slots alone.
- Uses one column while cards fit naturally, then balances larger rosters across at most
  two columns and shrinks them inside the left-half sidebar instead of growing sideways.
- Reserves the vanilla bottom-HUD band and retains a measured fit pass for real rendered
  dimensions, long names, and resolution changes.
- Shows an assembly-versioned `OVERLAY HUD v0.2.0` badge at the top right while L4D2 is
  focused but exports are inactive.

## Overlay app v0.1.0 - 2026-08-12: first working overlay

- Transparent, click-through, always-on-top window that follows the L4D2 window's position
  and size. Never takes focus, never appears in alt-tab, never touches the game process.
- Hold Tab to show, release to hide. The key is observed through a global hook and always
  passed on, so the game's own scoreboard is unaffected and no key binds are written.
- Only draws while L4D2 is the foreground window.
- One card per survivor: name, health bar with a separate temp-health segment, downed and
  black-and-white state, and carried kit / pills / throwable.
- Finds the L4D2 install through the Steam library folders. An install that already has a
  state file wins over one that merely exists, which matters on a machine with two.
- Keeps the last good state through torn reads, and shows a `STALE` marker when the addon
  stops writing.
- Everything configurable through `config.json` next to the exe. Tray icon to exit.

## Exporter addon v0.1.2 - 2026-08-12: fix bot flag always null

- Bot detection ran at chapter load, before any player entity exists, so it silently
  detected nothing and every survivor reported `bot: null`. It now runs on the first export
  tick that finds a survivor.
- Failed detection routes are logged instead of being swallowed.

## Exporter addon v0.1.1 - 2026-08-12: fix addon failing to load

- The exporter script shipped with a UTF-8 BOM, which Squirrel cannot compile past.
  `IncludeScript` failed and nothing ran. Script is now written without a BOM.
- No behaviour change. v0.1.0 never executed a single line.

## Exporter addon v0.1.0 - 2026-08-12: live survivor state export

- Exports every survivor in the session to `ems/overlay_hud_state.json` at 5 Hz: name,
  user id, team, health, temp health, downed state, revive count, black-and-white, held
  medkit/defib/pills/adrenaline/throwable, and active weapon.
- Survivors on team 4 are included. Finale Soldiers moves its bots to `L4D1_Survivor`
  transiently, so a team-2-only filter would make soldiers blink out of the HUD.
- Items are classified by weapon classname. `m_hMyWeapons` is a compacted list, not a
  slot-indexed array — the index a weapon sits at carries no meaning.
- Temp health is decayed server-side using `pain_pills_decay_rate`, falling back to 0.34
  when the convar cannot be read.
- Export loop is generation-guarded, so re-running the entry point on a new chapter cannot
  leave two loops writing the same file.
- Probe script removed.

## Exporter addon v0.1.0-probe1 - 2026-08-12: API discovery probe

- Repository scaffolded: addon source tree, docs, build output and asset folders.
- Probe addon added. Ships no features and changes no gameplay.
- Dumps, per survivor-team player: identity methods, health and downed-state methods, a
  candidate netprop list, and the `m_hMyWeapons` inventory array.
- Probes `StringToFile` / `FileToString` to establish whether a VPK-loaded addon can write
  a transport file, and where that file lands.
- Runs two independent timer mechanisms — delayed `EntFire` and a spawned `logic_timer` —
  tagged separately in the log so the working one can be identified.
