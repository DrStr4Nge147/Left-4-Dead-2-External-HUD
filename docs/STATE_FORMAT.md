# Transport format — `overlay_hud/state.json`

The exporter rewrites this file at 5 Hz while a map is loaded.

```text
E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\ems\overlay_hud\state.json
```

The `ems` write path was verified live by `v0.1.0-probe1`. Both files moved into the
`overlay_hud` subfolder in v1.0.4; `StringToFile` takes a relative subpath and the engine
creates the folder on first write, the same as `ems/finale_soldier/`.

**Up to v1.0.3 the files sat loose at the top of `ems/`**, as `overlay_hud_state.json` and
`overlay_hud_cmd.txt`. The app still reads the old state file when the new one is absent, so
a current app paired with an older addon keeps working; it then writes its command file
under the old name too, because that is the only name an older addon reads.

## Shape

```json
{
  "v": "1.0.5",
  "seq": 412,
  "time": 183.40,
  "count": 8,
  "survivors": [
    {
      "uid": 2,
      "name": "Cpl. Blake",
      "team": 2,
      "char": 8,
      "cls": "soldier",
      "bot": true,
      "hp": 87,
      "maxhp": 100,
      "temp": 12,
      "state": "alive",
      "revives": 0,
      "bw": false,
      "kit": "medkit",
      "pill": "adrenaline",
      "throw": "pipebomb",
      "weapon": "weapon_rifle_ak47"
    }
  ]
}
```

## Fields

| Field | Meaning |
|---|---|
| `v` | Exporter version that wrote the file |
| `seq` | Increments every write. Same `seq` twice = the game is paused, stopped, or gone |
| `time` | Server `Time()` in seconds since map load |
| `count` | Length of `survivors` |
| `uid` | Player user id. Stable within a session; **not** stable across map changes |
| `name` | Display name — "Cpl. Blake", "Louis", or the human's Steam name |
| `team` | 2 or 4. Both are survivors (see below) |
| `char` | `m_survivorCharacter`. **Unreliable for spawned soldiers** — all four read 8 |
| `cls` | `survivor`, `soldier`, `follower`, or `holdout` (see below). Absent before v0.6.5 |
| `bot` | `true` / `false` / `null` when the install exposes no bot test |
| `hp` | Permanent health |
| `maxhp` | Max health |
| `temp` | Temp health from pills/adrenaline, already decayed to now. `0` when none |
| `state` | `alive`, `dying`, `incap`, `ledge`, `dead`, or `unknown` |
| `revives` | `m_currentReviveCount` — times incapacitated this life |
| `bw` | Black-and-white / third strike |
| `kit` | `medkit`, `defib`, `explosive_ammo`, `incendiary_ammo`, or `""` |
| `pill` | `pills`, `adrenaline`, or `""` |
| `throw` | `molotov`, `pipebomb`, `bile`, or `""` |
| `weapon` | Active weapon classname |

## Notes for the overlay app

**Teams 2 and 4 are both survivors.** Team 4 is `L4D1_Survivor`. Finale Soldiers moves its
bots there transiently — a live probe caught three soldiers on team 4 in one sample and
back on team 2 twenty seconds later. Filtering to team 2 makes them blink out of the HUD.

**`cls` is what the roster filter runs on, and `team` cannot replace it.**

| `cls` | Meaning |
|---|---|
| `survivor` | Not a Finale Soldiers bot: a real survivor, or another addon's extra bot |
| `soldier` | A mortal soldier holding a post — shootable, killable, worth a health card |
| `follower` | A soldier following a player. Always forced mortal while it follows |
| `holdout` | An immortal team-4 holdout soldier. The overlay never draws these |

The exporter reads Finale Soldiers' own per-player script-scope markers — `cf_soldier_bot`,
`cf_soldier_following`, `cf_soldier_mortal`, `cf_soldier_distance_suspended` — because both
addons run in the same server VM. A distance-suspended mortal soldier still reports
`soldier`: it is sitting on team 4 only to dodge the engine's bot-catchup teleport, and
letting that flip its class would make it flicker out of the HUD whenever it wandered.

Without the addon installed every player reports `survivor`, and so does everything from an
exporter older than v0.6.5, which the overlay treats as the previous behavior.

**Do not identify survivors by `char`.** All four spawned soldiers report `char: 8`. Use
`uid` for identity and `name` for display. `char` is only a hint for picking a portrait for
the four original survivors.

**Reads can tear.** The addon rewrites the file in place with no atomic swap, so a read can
catch a half-written file. The app must treat a parse failure as "keep the last good state"
and retry, never as an error.

**Staleness.** If `seq` has not changed for ~2 seconds, the game is paused, alt-tabbed at a
menu, or closed. Show a stale/disconnected state rather than frozen numbers.

## Reverse channel — `overlay_hud/cmd.txt`

Added in v1.0.1. The overlay app writes it, the addon reads it on the same 5 Hz tick that
writes state out. It lives beside the state file:

```text
E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\ems\overlay_hud\cmd.txt
```

The app creates the folder if it is not there yet. On a fresh install it can ask for the
scoreboard before any map has loaded, which is before the addon has written anything.

One line, two fields:

```text
1 274
```

| Field | Meaning |
|---|---|
| want | `1` hold the scoreboard open, `0` release it |
| seq | Increments on every write. The app rewrites this about four times a second while holding |

The addon runs `SendToConsole("+showscores")` / `("-showscores")` on the listening host's own
client when want changes. This is the only way the scoreboard can be shown while the editor
has keyboard focus: L4D2 draws it while the host holds `+showscores`, and an external app
cannot make it hold anything — v1.0.0 tried with synthesised keystrokes and nothing appeared
in game.

**`seq` is a heartbeat, not a counter.** If want is 1 and seq has not advanced for two
seconds, the addon releases the scoreboard on its own. Without that, an overlay killed while
holding would leave the scoreboard latched open with nothing left to release it.

The addon also assumes the scoreboard is closed at every chapter load, so the first hold
after a map change always sends its command.

**`temp` is computed server-side** from `m_healthBuffer` decayed by `pain_pills_decay_rate`.
Treat it as approximate until it has been compared against the in-game HUD side by side.
