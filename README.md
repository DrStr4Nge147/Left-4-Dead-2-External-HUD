# Left 4 Dead 2 Customized Overlay HUD - External

An external survivor HUD for Left 4 Dead 2 that shows **every** survivor in the session,
not just the four the built-in HUD has room for. Built for oversized rosters — Finale
Soldiers and other mods that spawn more than four survivors.

Two pieces:

| Piece | What it is | Where |
|---|---|---|
| **Exporter addon** | VScript addon that reads live survivor state and writes it out on a timer | `overlay_hud_export/` |
| **Overlay app** | Transparent always-on-top Windows app that reads that state and draws the HUD | `overlay-app/` |

Hold **Tab** to show the panel, release to hide it — handled by the overlay app, so no
key binds are changed. Hold **Tab** and press **Insert** to open the UI editor without
also firing an in-game Insert bind; Insert by itself continues to work normally.

Right-click the overlay's notification-area icon and choose **Customize UI...** to adjust
the layout against a live 16:9 preview, then save and apply it without restarting.

## Status

**v1.0.1 — feature complete. Both halves working, not yet run together in-game.**

- **Exporter addon v1.0.1** — exports every survivor plus a `cls` classification field for
  Finale Soldiers rosters, and holds the game's scoreboard open on request. Both still need
  one in-game confirmation.
- **Left 4 Dead 2 Customized Overlay HUD - External v1.0.1** — includes source-faithful
  monochrome item icons, a built-in UI editor with simulated *and* live preview, the
  default Tab+Insert editor shortcut and the three roster filters. The Tab hold, the
  foreground gate and following the game window are written but not yet confirmed in-game.

1.0.x marks feature completeness, not a completed test pass — `docs/TESTING.md` is the
run to make. **Close L4D2 before swapping the addon VPK**; a pack replaced while the game
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

**Show HUD consistently** displays the HUD persistently during play instead of only while
the hold key is down. It still hides when L4D2 loses focus.

## Who the panel shows

**Who to show** in the editor picks one of three rosters:

| Option | Shows |
|---|---|
| **All survivors** | Every mortal soldier, follower, and extra survivor past the four L4D2 already draws |
| **Mortal soldiers + followers** | Finale Soldiers' mortal soldiers and followers only |
| **Followers only** | Only the soldiers currently following a player |

Immortal team-4 holdout soldiers are never drawn in any of the three: nothing can hurt them,
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
- L4D2 running with `-condebug` so console output lands in `console.log`.
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
└── workshop assets/      # images
```
