# Left 4 Dead 2 Customized Overlay HUD - External

An external survivor HUD for Left 4 Dead 2 that shows **every** survivor in the session,
not just the four the built-in HUD has room for. Built for oversized rosters — Finale
Soldiers and other mods that spawn more than four survivors.

**Steam Workshop:** [Left 4 Dead 2 Customized Overlay HUD - External](https://steamcommunity.com/sharedfiles/filedetails/?id=3782550188)
— the exporter addon. The overlay app is not on the Workshop; get it from this repo's
[Releases](https://github.com/DrStr4Nge147/Left-4-Dead-2-External-HUD/releases) page —
it is required for the addon to do anything.

<table>
  <tr>
    <td><img src="workshop%20assets/OverlayHUD%202.jpg" width="260" alt="The overlay panel over a full roster"></td>
    <td><img src="workshop%20assets/OVerlayHUD.jpg" width="260" alt="The overlay panel with health and item cards"></td>
    <td><img src="workshop%20assets/OVerlayHUD%20editor.png" width="260" alt="The built-in UI editor"></td>
    <td><img src="workshop%20assets/Overlay%20HUD%20Live.jpg" width="260" alt="The panel in-game alongside the stock HUD"></td>
  </tr>
  <tr>
    <td align="center">The panel over a full roster</td>
    <td align="center">Health and item cards</td>
    <td align="center">The built-in UI editor</td>
    <td align="center">In-game beside the stock HUD</td>
  </tr>
</table>

Two pieces:

| Piece | What it is | Where |
|---|---|---|
| **Exporter addon** | VScript addon that reads live survivor state and writes it out on a timer | `overlay_hud_export/` |
| **Overlay app** | Transparent always-on-top Windows app that reads that state and draws the HUD | `overlay-app/` |

## App downloads

Choose one app file from the release page:

- **Standalone** — the larger self-contained build. It includes the required .NET components,
  so no separate runtime installation is needed.
- **Compact** — the smaller framework-dependent build. Install the **.NET 9 Desktop Runtime
  for Windows x64** before launching it: [download it from Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0).
  Choose **Desktop Runtime**, not only the base .NET Runtime or ASP.NET Core Runtime.

The Compact build will not start until the Desktop Runtime is installed. Both app variants still
require the matching Workshop exporter addon.

Hold **Tab** to show the panel, release to hide it — handled by the overlay app, so no
key binds are changed. Hold **Tab** and press **Insert** to open the UI editor without
also firing an in-game Insert bind; Insert by itself continues to work normally.

Right-click the overlay's notification-area icon and choose **Customize UI...** to adjust
the layout against a live 16:9 preview, then save and apply it without restarting.

## Status

**v2.1.0 — the Consistent HUD now leaves the screen whenever L4D2 hides its own survivor
HUD, instead of sitting on top of the scene: the finale outro, where it stays away for the
report screen and the load that follow; the chapter end, where the saferoom door closes and
the score panel runs for several seconds before the loading bar; the map-start cinematic; and
the pause menu or developer console. It comes back on its own each time. Each of those three
scenes answers on a different signal — the outro on the game's own hidden-HUD flags and view
camera, the chapter end on the server freezing the player, the menu on the mouse cursor L4D2
shows for it, since a listen server does not pause and nothing in the game can be polled for
a menu. `hideDuringCinematics` and `hideWhenGamePaused` in `config.json` turn the two rules
off. Live-tested in L4D2 on 2026-08-25.**

**v2.0.0 — adds a weapon HUD: your own primary weapon with its magazine and reserve
ammunition, and your pistol, Magnum, chainsaw, or melee weapon beneath it, as a separate
panel with its own corner (lower left or lower right), vertical or horizontal slot
arrangement, and a height slider running the full screen. Weapons use L4D2's own HUD icons — all 33 the game ships,
with drawn silhouettes behind them for the riot shield and for addon weapons — at their
real relative sizes, so a Magnum reads as smaller than an M60; PNGs dropped into `workshop assets/weapon-icons/` override
one weapon's art on the next build. It is
configured under the Consistent HUD tab, has its own size slider on top of that view's
size, and takes its opacity. The panel also carries your own throwable, kit, and pills as a
three-slot row under the weapons, so your own Consistent HUD card no longer draws them;
every other survivor keeps their item slots, and the Scoreboard tab shows the whole team's.
Live-tested in L4D2 on 2026-08-18 — see `docs/TESTING.md`.**

**v1.2.0 — the scoreboard panel now has its own editor tab, while a separate Consistent HUD
tab provides three named templates: Bottom - Horizontal Grid, Lower Left Vertical Grid, and
Lower Right Vertical Grid. The horizontal grid lays out four cards across and up to three rows,
adding columns for larger rosters; with Separate You enabled, the shared bottom roster starts at
three columns so the independent You card can use the fourth area. The HUD design dropdown adds
Basic and Minimalist cards; Minimalist puts the name before the health value above a five-segment
compressed health strip, keeps grunge on temporary health, and truncates long names. The Consistent HUD can hide health numbers or use a complete
black-and-white theme. A vertical-position slider moves the HUD upward from the
bottom edge, while horizontal and vertical spacing sliders can pull cards together or overlap
them. A configurable hotkey toggles it during play. Both halves also compare
their versions and report a mismatch without blocking the HUD. **Separate You** splits the
bottom horizontal layout as roster-left/You-right and mirrors the lower-right vertical layout
as roster-right/You-left, while leaving roster spacing unchanged. Live-tested and confirmed
working in L4D2 on 2026-08-15, including the health-number option, black-and-white theme, and
five-segment Minimalist layout. v1.0.8's
exporter restart recovery is confirmed in-game: a full-team wipe that restarts the map no
longer leaves the panel showing an empty roster.**

**Both halves are required.** The addon alone exports a file and draws nothing; the app alone
has nothing to read.

- **Exporter addon v2.1.1** — exports every survivor plus `cls` classification, a `local`
  marker for the listen-server host, and each survivor's weapon slots with ammunition, and
  holds the game's scoreboard open on request. It also reports when the game has hidden its
  own HUD, so the overlay can leave with it. The v2.0.0 app/VPK pair has been live-tested in
  L4D2, weapon fields included, and the outro hiding on the 2.1.0 exporter with it. Its two transport files live in
  `left4dead2\ems\overlay_hud\`;
  builds up to v1.0.3 put them loose at the top of `ems\`, and those leftovers are safe to
  delete.
- **Left 4 Dead 2 Customized Overlay HUD - External v2.1.1** — includes source-faithful
  monochrome item icons, separate Scoreboard and Consistent HUD editor tabs, a live/simulated
  preview, the default Tab+Insert editor shortcut, a configurable consistent-HUD hotkey, Basic
  and Minimalist HUD designs, the four roster filters, and the optional Separate You split card.
  The Consistent HUD templates, the weapon HUD, and the presentation options are confirmed
  in-game with the v2.0.0 app/VPK pair.
  Both halves ship under one version; the app reads the installed addon's `addoninfo.txt`
  and reports a mismatch rather than enforcing one.

Both are live-tested, and `docs/TESTING.md` carries the standing regression procedure. **Close L4D2 before swapping the addon VPK**; a pack replaced while the game
is running is not reloaded, and the old one stops working.

The editor previews either way round. **Live** is the default: it draws the real panel over
the real game window with the live roster, updating as you move a slider. **Simulated**
draws sample cards against a mock scoreboard inside the editor window, and is the fallback
for laying out with L4D2 closed. In live mode, **Cancel** puts the overlay back exactly as
it was, and nothing is written until **Save & Apply**. **Hold the game's scoreboard open** asks
the addon to run `+showscores` on your own client, so the real scoreboard stays up while you
work in the editor — an external app cannot do that, but the addon is already inside the
game. The sidebar edge and vertical start are marked with guide lines the game cannot draw
at all.

**Scoreboard** keeps the Tab-held panel beside the vanilla scoreboard. **Consistent HUD**
keeps a survivor HUD visible during play instead of only while the hold key is down. Choose the
bottom-centered horizontal grid or either lower-corner vertical grid in the Consistent HUD tab,
then adjust its vertical position without changing the scoreboard. It still hides when L4D2
loses focus.

## Who the panel shows

Each view has its own **Who to show**, and the two rosters are independent. The lower two
options are grouped under **For Finale Soldiers Mod** — without that addon nothing is ever
classified as a soldier or a follower, so they would show an empty panel:

| Option | Shows | Where |
|---|---|---|
| **All survivors** | Every mortal survivor, including the four L4D2 would draw itself | Consistent HUD only |
| **Extra survivors** | Mortal soldiers, followers, and plain survivors from slot 5 onward | Both |
| **Mortal soldiers + followers** | Finale Soldiers' mortal soldiers and followers only | Both |
| **Followers only** | Only the soldiers currently following a player | Both |

The scoreboard panel has no **All survivors**: it is drawn beside L4D2's own scoreboard,
which lists the original four already, so it always excludes them. The Consistent HUD keeps
the option because the vanilla survivor HUD is hidden while it is up.

Immortal team-4 holdout soldiers are never drawn under any option: nothing can hurt them,
so a health card for one carries no information.

Followers carry a blue **FOLLOW** marker in the first two options, where the roster is
mixed. In **Followers only** they are not marked — every card would carry it.

The editor includes an **Exit when L4D2 closes** option. It is enabled by default; turn it
off if the overlay should remain in the tray between game sessions.

Only one overlay process can run per Windows session; launching a second copy says so and
points at the tray icon rather than exiting silently. The editor header also reports
whether L4D2 is currently running.

Docs: `docs/OVERLAY_APP.md` (usage and config), `docs/STATE_FORMAT.md` (the transport),
`docs/TESTING.md` (how to run the test), `docs/DEVLOG.md` (why things are the way they are).

## Requirements

- L4D2 running **borderless windowed** (`-windowed -noborder`). An external overlay cannot
  draw over exclusive fullscreen.
- You host the session (single player or you host coop). VScript is server-side — on
  someone else's server without this addon, there is no data to export.

## Design constraints

The overlay never touches the game process. No injection, no memory reading, no DirectX
hooking. It reads a file the addon writes and draws its own window — the same category of
program as the Steam or Discord overlay.

## Layout

```text
overlay-custom-hud/
├── overlay_hud_export/   # VPK source tree for the addon
├── overlay-app/          # Windows overlay application (phase 3)
├── docs/                 # changelog, devlog, testing notes
├── compiled vpks/        # versioned build output - the only delivery surface
├── tools/                # packaging scripts
└── workshop assets/      # images
```

Repack the addon with `tools\Build-AddonVpk.ps1`. It reads the version from
`overlay_hud_exportddoninfo.txt` and writes `compiled vpks\overlay_hud_export_v<version>.vpk`
as VPK format version 1 — the game's own `vpk.exe` writes v2, which the target L4D2 build
rejects with `Unknown version 2`.

## License

Copyright © 2026 DrStr4nge. Licensed under the **GNU General Public License v3.0** — see
[LICENSE](LICENSE).

Use it, study it, fork it. If you distribute a modified version, or anything built on this
source, it has to ship under the same license with its source available. That is the whole
condition.

Two things the license does not cover:

- **The item icons** under `workshop assets/item-icons/` are monochrome masks derived from
  Left 4 Dead 2's own HUD artwork, which belongs to Valve. They are here because the overlay
  has to look like the game it sits next to; they are not mine to license.
- **Left 4 Dead 2 itself**, and anything Valve. This project is an unofficial addon.
