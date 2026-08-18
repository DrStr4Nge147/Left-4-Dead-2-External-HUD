# Transport format — `overlay_hud/state.json`

The exporter rewrites this file at 5 Hz while a round is running. It re-arms itself from
`round_start_post_nav`, including same-map restarts, so a round wipe cannot leave the
transport frozen while restored survivor bots are already present.

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
  "v": "2.0.0",
  "seq": 412,
  "time": 183.40,
  "count": 8,
  "survivors": [
    {
      "uid": 2,
      "name": "Cpl. Blake",
      "team": 2,
      "char": 8,
      "local": false,
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
      "pri": "rifle_ak47",
      "priclip": 40,
      "priammo": 180,
      "priupg": 0,
      "priupgn": 0,
      "sec": "pistol_magnum",
      "secclip": 8,
      "weapon": "weapon_rifle_ak47",
      "slot": "primary"
    }
  ]
}
```

## Fields

| Field | Meaning |
|---|---|
| `v` | Exporter version that wrote the file. The app's version check prefers the installed pack's `addoninfo.txt`, which answers at a main menu, and falls back to this |
| `seq` | Increments every write. Same `seq` twice = the game is paused, stopped, or gone |
| `time` | Server `Time()` in seconds since map load |
| `count` | Length of `survivors` |
| `uid` | Player user id. Stable within a session; **not** stable across map changes |
| `name` | Display name — "Cpl. Blake", "Louis", or the human's Steam name |
| `team` | 2 or 4. Both are survivors (see below) |
| `char` | `m_survivorCharacter`. **Unreliable for spawned soldiers** — all four read 8 |
| `local` | `true` for the listen-server host's current survivor; omitted by older exporters |
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
| `pri` | Slot-0 weapon id — `rifle_ak47`, `autoshotgun`, … — or `""`. **Host player only.** Added in 2.0.0 |
| `priclip` | Rounds in the primary's magazine. `-1` = the install exposed no readable route |
| `priammo` | Primary rounds in reserve. `-1` = unreadable |
| `sec` | Slot-1 weapon id: `pistol`, `pistol_dual`, `pistol_magnum`, `chainsaw`, or a melee script name such as `katana`. `""` when the slot is empty |
| `secclip` | Rounds in the secondary's magazine, or chainsaw fuel. `-1` for melee and for an unreadable route |
| `priupg` | Rounds loaded in the primary: `0` normal, `1` incendiary, `2` explosive. Added in 2.0.0 |
| `priupgn` | Upgraded rounds left to fire, `0` when none. The upgrade's own pool, which survives a reload |

**The upgrade bits are `1 << 0` incendiary and `1 << 1` explosive.** That was established by
testing in game, not read from a header: the first build had both one bit higher and produced
a plain cartridge for incendiary and a flame for explosive. Any other bit in the vector is
ignored - only these two change what is in the magazine.
| `weapon` | Active weapon classname |
| `slot` | Which of this survivor's own slots is in their hands: `primary`, `secondary`, `throwable`, `kit`, `pills`, or `""`. Added in 2.0.0 |

**`slot` names a place, not a weapon.** The held slot cannot be identified by comparing
`weapon` against `pri`/`sec`: a pair of pistols carries the same classname as one, and every
melee weapon shares `weapon_melee`. The exporter compares entity handles inside the
inventory loop it already runs and reports which slot matched, so the app only has to know
which box to highlight. Anything the classname tables do not know - a gas can, another
addon's weapon - reports `""`, since nothing the HUD draws is being held.

## Weapon fields

Added in 2.0.0. An older exporter sends none of them, and the app treats that as "no
weapon data" rather than as an unarmed survivor — the consistent HUD simply draws no
weapon row.

**`-1` is not zero.** Every ammunition field distinguishes "the exporter could not read
this" from "the magazine is empty", because a card that confidently prints `0 / 0` for a
full rifle is worse than one that prints nothing. The exporter probes each route once per
script load and logs which one answered:

```text
[OVLHUD] clip read: m_iClip1
[OVLHUD] reserve ammo read: m_iAmmo[m_iPrimaryAmmoType]
[OVLHUD] melee id read: m_strMapSetScriptName
```

A `no route available` line instead means that value will be `-1` for the rest of the
session, and is the first thing to check when ammunition never appears.

**Weapon fields are written for the host player only.** Every other survivor reports `""`
and `-1`. Only the host's own weapon HUD is ever drawn, so classifying each bot's rifle and
reading its magazine was work whose result nothing displayed. Items — `kit`, `pill`,
`throw` — are still per-survivor, because those do appear on every card.

**`pri` and `sec` are classified by classname**, from the exporter's own `PRIMARY_WEAPONS`
and `SECONDARY_WEAPONS` tables — `m_hMyWeapons` is compacted, so the array index a weapon
sits at carries no slot meaning. A weapon from another addon is in neither table and
exports as an empty slot; adding its classname to the table is all that is needed, because
the app humanises any id it does not recognise rather than dropping it.

**Melee is one classname.** Every melee weapon is `weapon_melee`, told apart by
`m_strMapSetScriptName` — `fireaxe`, `katana`, `frying_pan`. An install that will not give
that up reports the generic `melee`.

**So is the second pistol.** A pair is still `weapon_pistol`; `m_isDualWielding` separates
them and the exporter reports `pistol_dual`, which the app draws with the game's own
two-pistol icon. If that property cannot be read the exporter falls back to the magazine —
over 15 rounds means a pair — and says so once in `console.log`. That fallback is one-way:
a pair down to its last rounds reports as a single pistol.

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

**`local` identifies the host, not an arbitrary remote client.** This addon requires the
application to run on the listen-server host's machine, so `GetListenServerHost()` is the
identity used by the app's optional **Separate You** consistent-HUD setting. A dedicated
server or an older exporter has no usable local marker; in that case the app keeps the
original all-in-roster layout.

**Reads can tear.** The addon rewrites the file in place with no atomic swap, so a read can
catch a half-written file. The app must treat a parse failure as "keep the last good state"
and retry, never as an error.

**Staleness.** If `seq` has not changed for ~2 seconds, the game is paused, alt-tabbed at a
menu, or closed. Show a stale/disconnected state rather than frozen numbers.

## Ammunition channel — `overlay_hud/ammo.txt`

Added in 2.0.0. The addon writes it at 20 Hz, beside the state file:

```text
E:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2\ems\overlay_hud\ammo.txt
```

One line, six fields:

```text
418 27 172 15 1 44
```

| Field | Meaning |
|---|---|
| seq | Increments on every write. Not advancing = the game is paused, gone, or between maps |
| primary clip | Rounds in the host's primary magazine. `-1` when unreadable |
| primary reserve | Host's primary rounds in reserve. `-1` when unreadable |
| secondary clip | Host's secondary magazine, or chainsaw fuel. `-1` for melee |
| primary ammo kind | `0` normal, `1` incendiary, `2` explosive. Optional: a four-field line is read as `0` |
| upgraded left | Upgraded rounds still to fire, `0` when none. Optional, same as the field before it |

**The upgraded count is not the magazine.** `m_iClip1` goes back to full on a reload, which
says nothing about how much fire is left; `m_nUpgradedPrimaryAmmoLoaded` is the upgrade's
own pool and keeps counting down across reloads until it reaches zero, which is the moment
the slot goes back to ordinary rounds. The HUD shows the pool while an upgrade holds.

**The ammunition kind rides this channel rather than the roster** because it runs out by
being fired. The mark beside the magazine has to stop on the same round the counter does,
and at 5 Hz it would linger for a fifth of a second after the last upgraded round.

**It exists because 5 Hz cannot count rounds.** An Uzi fires about twelve a second, so the
roster tick drops two or three at a time and the counter jumps instead of counting down.
Raising the roster rate to match would have multiplied the cost of the entire export —
every survivor, every field — to fix one number belonging to one player. This writes about
fifteen bytes and reads three properties off one entity.

**Which weapons those numbers belong to still comes from `state.json`.** A weapon change is
therefore up to a fifth of a second late while the rounds themselves are current, which is
the right way round: nobody can see the icon change late, and everybody can see the counter
stutter.

**The app must treat this channel as optional in every direction.** An exporter older than
2.0.0 never writes it; a torn read must be discarded rather than half-applied; and a file
that has not advanced recently — or that was left behind by the previous session and has
never been seen to advance at all — must be ignored in favour of the roster's own numbers,
which are correct, just coarser.

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
