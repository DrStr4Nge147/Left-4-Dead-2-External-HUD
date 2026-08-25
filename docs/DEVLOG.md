# Dev log

## 2026-08-25 - v2.1.2: the credits are a Director question, not a player one

The exp1 probe answered in one capture, and both candidate Director calls turned out to
exist on this build:

```text
probe: ... hidehud=3961 ... finescape=true  finwon=false ... (seq=206 t=49.2)
probe: ... hidehud=3961 ... finescape=false finwon=true  ... (seq=322 t=73.3)
```

`Director.IsFinaleWon()` flips false to true exactly at the moment the escape ends and the
credits begin, and stays true. That is the fourth read, and the panel now hides on it.

`Director.IsFinaleEscapeInProgress()` exists too and is deliberately **not** used: it is
already true at t=49.2, while the team is still fighting its way to the rescue vehicle. Acting
on it would take the HUD away during the most dangerous stretch of the campaign - the exact
opposite of the point.

Each of the four scenes is visible to a different read, which is worth stating plainly because
it is the thing that made this take four builds:

| Scene | Answers on |
|---|---|
| Finale outro | `m_iHideHUD` mask, `m_hViewEntity` |
| Chapter end, map-start | `FL_FROZEN` |
| End credits | `Director.IsFinaleWon()` |
| Pause menu, console | the mouse cursor, from outside the game |

`won` is read every tick regardless of whether a host player was found, because it is world
state rather than player state, and `wonMode` is probed once like every other route.

The probe stays in the tree behind `PROBE = false`, with `ProbeCall` alongside it. Two scenes
were identified by it now, and the name-lookup-before-call pattern is what made testing four
candidate Director methods free rather than four builds.

**Verification**: **live-tested and confirmed working** 2026-08-25 on v2.1.2 - the overlay
stays drawn through the run to the rescue vehicle, leaves with the escape, and stays away for
the credits. The signal itself was read off the author's probe capture; the build also passes
`dotnet build` Release clean and all 24 checks.

That closes the set. Four scenes, four different reads, none of them guessed:

| Scene | Answers on |
|---|---|
| Finale outro | `m_iHideHUD` mask, `m_hViewEntity` |
| Chapter end, map start | `FL_FROZEN` |
| End credits | `Director.IsFinaleWon()` |
| Pause menu, console | the mouse cursor, from outside the game |

The pattern that got there: when a theory-driven build failed, the next build was a probe
rather than another theory. Three of the four signals came out of capture files, and the two
wrong guesses along the way - `HIDEHUD_ALL` for the chapter end, export staleness for the
menu - were each killed by one play session rather than argued about.

## 2026-08-25 - a UTF-8 BOM killed v2.1.1-exp1 outright

`Set-Content -Encoding utf8` in Windows PowerShell writes a BOM, and it was used to flip the
probe flag for that build and to restore the file afterwards. Squirrel will not parse a file
that starts with one, so the addon loaded as nothing at all: no export loop, no state file,
no HUD, and nothing in `console.log` naming the cause. The author reported it as the overlay
being broken, which is exactly what it looked like.

The BOM reached the committed source as well, so anyone packing from the repo would have got
the same dead addon. The stable v2.1.1 VPK was packed before the bad write and was never
affected.

`Build-AddonVpk.ps1` now refuses to pack any `.nut` that starts with a BOM, naming the file
and the reason. A failure mode with no error message deserves a guard at the one place every
build has to pass through.

## 2026-08-25 - v2.1.1, and why the end credits still draw the HUD

2.1.1 is the editor promotion: the two hiding switches were config-file keys, which is the
same as not having them. No exporter behaviour changed; the addon advances with the app to
keep the pair on one version.

The open bug is the end credits. The author's capture is unambiguous about what happens:

```text
[CF AutoSpawn][DEBUG] finale_vehicle_leaving fired.
[OVLHUD] cinematic reads changed: hud=3963 view=0 frozen=1 -> hidden
[OVLHUD] cinematic reads changed: hud=3961 view=1 frozen=1 -> hidden
[CF AutoSpawn][DEBUG] finale_win fired.
[OVLHUD] cinematic reads changed: hud=2048 view=0 frozen=0 -> drawn
[OVLHUD] cinematic ended (hud=2048 view=0 frozen=0)
```

The outro hides correctly. Then `finale_win` fires, the game unfreezes everyone, drops the
camera, and puts `m_iHideHUD` back to its 2048 baseline - and the credits roll over a map that
is still live, with four healthy survivors still in it. All three reads honestly say "ordinary
play", because from the server's point of view it is. The credits are client-side VGUI, the
same class of problem the pause menu was.

The menu was solved from outside the game, by the cursor. The credits do not show a cursor, so
that route does not extend to them.

Rather than guess a fourth signal, `v2.1.1-exp1` re-enables the probe with candidate reads
aimed at this specific moment - `m_isEscaping`, `m_hasEscaped`, and Director calls
`IsFinaleEscapeInProgress` and `IsFinaleWon`, with `HasAnySurvivorLeftSafeArea` and
`IsTankInPlay` as controls proving Director calls reach the engine at all. Every name is a
candidate, not a documented API; `ProbeCall` looks the name up before calling it, so one that
does not exist prints `?` and costs a caught exception rather than a build.

If all six print `?` through the credits, nothing server-side sees them and the next place to
look is outside the process, as with the menu.

**Verification**: the diagnosis is read off the author's live capture. The 2.1.1 release is
source inspection, `dotnet build` Release clean, and the editor and scene-hiding checks
passing. The credits are NOT fixed in it.

## 2026-08-25 - v2.1.0 goes stable

**Live-tested and confirmed working** 2026-08-25, on `v2.1.0-exp2` plus the cursor build of
the app: the finale outro, the chapter-end transition, and the pause menu and console all
take the overlay off screen, and it comes back on its own each time.

Promoted as-is. The experimental marker is off the banner, `PROBE` is back to `false`, and
`overlay_hud_export_v2.1.0.vpk` is packed from exactly the source that was played. The probe
itself stays in the tree behind its flag - it earned that by answering the chapter-end
question in one capture, and the next scene that needs identifying will want it again.

Three entries below record how each half was found, in the order they were found. The route
that mattered in the end differs per scene: the outro moves `m_iHideHUD` and the view entity,
the chapter end moves only `FL_FROZEN`, and the menu moves nothing at all in the game - it is
read from the mouse cursor, outside the process.

## 2026-08-25 - app: the menu is a cursor, not a pause

The stale-export rule below did nothing for ESC, and the author's report says why in one
line: **a listen server does not pause when the menu opens.** The export loop keeps ticking,
`seq` keeps advancing, the reader never goes stale, and the panel stays drawn over the menu.
The theory was wrong about the mechanism, not about the goal.

The observable difference is the cursor. L4D2 hides it while the player is looking around and
shows the arrow for any menu it draws - the pause menu and the developer console alike - and
cursor visibility is global state `GetCursorInfo` reports to any process. That is one call per
render tick, no hooking and nothing touching the game.

It is paired with the existing foreground gate, because a visible cursor over some other
application says nothing about L4D2, and that case is already covered. `GameMenuProbe` fails
open: a failed read reports "no menu", so a broken API leaves the overlay drawn rather than
hiding it for the session with no way to tell why.

The stale rule stays alongside it. It was never wrong - loading screens, the main menu and a
closed game are all still reasons to be away - it just does not answer this particular
question.

[UNVERIFIED] Whether any in-game state shows a cursor during play - a mod, a vote panel, the
Steam overlay. The debug console now prints the cursor verdict on its `cinematic` line so an
unexpected hide can be read off directly.

**Verification**: source inspection, `dotnet build` Release clean, layout check passing. Not
live-tested. App only; the exporter stays on `v2.1.0-exp2`.

## 2026-08-25 - app: hiding for the pause menu and the console (superseded)

Superseded by the entry above: this shipped the stale-export half only, and it does not fire
for the menu because the server does not pause.

The author's screenshot shows the overlay drawn over the ESC menu, with L4D2's own survivor
HUD correctly gone - the overlay as the only HUD left on a paused screen.

There is nothing for the exporter to see here. The pause menu and the console are client-side
UI; no server-visible property knows either is open, and the FL_FROZEN route that answers the
chapter end does not fire for them.

The consequence is observable even though the cause is not. Opening the menu on a locally
hosted game pauses the server, the export loop stops with it, and `seq` stops advancing -
which is what `StateReader` already calls stale. So the app hides on stale rather than trying
to identify menus, and that one rule covers the main menu, loading screens, the console, and
a game that has exited, all of which are moments the panel should be away for anyway.

`HasExported` gates it: an overlay launched before the game has never seen an export, which is
not the same as having stopped, and that case still draws its empty panel so the thing can be
positioned before a session. The threshold is the existing `staleAfterSeconds`, so a two-second
hitch does not blink the panel, and `hideWhenGamePaused: false` turns the whole rule off.

[UNVERIFIED] Whether a listen server with other humans connected pauses at all on ESC. If it
does not, exports keep advancing and nothing hides - the rule is correct but silent there, and
no client-side signal is available to replace it.

**Verification**: source inspection, `dotnet build` Release clean, layout check passing. Not
live-tested. App only; no addon change, and the exporter stays on `v2.1.0-exp2`.

## 2026-08-25 - v2.1.0-exp2: the chapter end is a freeze, not a hidden HUD

The exp1 probe capture answered it in one run. From the author's `console.log`, the last
export tick before the transition:

```text
[OVLHUD] probe: flags=16546 life=0 obs=0 move=2 hidehud=2048 viewmodel=1 solid=2 (seq=174 t=42.4)
[OVLHUD] probe: flags=16545 ... (seq=176 t=42.8)
---- Host_Changelevel ----
L 08/25/2026 - 14:57:13: -------- Mapchange to c8m2_subway --------
```

`hidehud` never moves. It sits at **2048 as its ordinary baseline** for the whole chapter -
which also means the 2.1.0 mask widening was never going to fire here, and that `HIDEHUD_ALL`
was the wrong family of guess for this transition. What does move is `m_fFlags`, gaining
**32, FL_FROZEN**, two ticks before `Host_Changelevel`.

Across the entire capture that bit appears exactly three times: the spawn freeze at t=6.2,
the intro cinematic at t=9.2, and t=42.4 at the chapter end. Every sample of ordinary play in
between is clear of it. Three scenes, three appearances, no false positives in the sample -
and all three are scenes the overlay should sit out, so acting on the bit is right for the
intro as well as for the chapter end.

So the verdict becomes three reads, any one of which is enough: the `HIDEHUD` mask, the view
camera, and now FL_FROZEN. The outro answers on the first two; the chapter end answers only
on the third.

The probe survives, with two changes. `m_fFlags` is now printed **masked to FL_FROZEN** rather
than raw: the onground and ducking bits flicker with every step, so the raw print fired the
probe several times a second through ordinary play and buried the transitions it exists to
show. And `seq`/`t` ride outside the change comparison, as before.

One thing the capture could not settle: `seq` only prints on a line the comparison already
let through, so a frozen `seq` through a long score screen would not have been visible. It did
not matter here - the freeze bit answered first - but the "is the exporter even still ticking"
question is still open if a later scene needs it.

[UNVERIFIED] Whether anything in ordinary play sets FL_FROZEN - a charger pin, a smoker drag,
a scripted map moment. Nothing in this capture did, but one chapter is one chapter. If the
overlay ever vanishes mid-fight, this is the first thing to suspect, and
`hideDuringCinematics: false` turns it off outright.

**Verification**: the chapter-end signal is identified from the author's live capture; the
build acting on it is source inspection, `dotnet build` Release clean and the layout check
passing. Not yet played. Packed as `overlay_hud_export_v2.1.0-exp2.vpk`; `addonversion` and
the app stay at 2.1.0.

## 2026-08-25 - v2.1.0-exp1: the chapter end does not move either read

2.1.0 widened the mask to `HIDEHUD_ALL | HIDEHUD_HEALTH` on the theory that the chapter-end
score screen is the outro seen from another angle. The author played it: the panel did not
disappear. So the transition moves neither `m_iHideHUD` nor `m_hViewEntity` on the host, or
the export loop is no longer running by the time it would - and those are different failures
with different fixes.

Guessing a third property here would be the third guess in a row, so this build guesses
nothing. It adds a probe that reads a spread of host-player state every tick and logs the
whole line whenever any field in it changes, each field behind its own try so a property this
build cannot read prints `?` instead of taking the line with it:

```text
[OVLHUD] probe: flags=257 life=0 obs=0 move=2 hidehud=0 viewmodel=1 solid=3 (seq=812 t=204.6)
```

`seq` and `t` ride outside the change comparison - they advance every tick, and a line
carrying them inside it could never match the previous one, which would print the probe at
5 Hz. They are there because **a frozen `seq` through the transition is the answer on its
own**: it would mean the exporter stops before the score screen and no polled property could
ever catch it, which points the fix at the app rather than the addon.

The live possibility this build is set up to prove or kill: the end-of-chapter panel may be
drawn entirely client-side, with nothing server-visible moving at all. If the capture shows
every field flat from the saferoom door to the loading bar, polling is the wrong layer and
the next candidate is the app noticing the game's own transition instead.

`PROBE` is a flag on the table and ships false in a release build; only exp builds set it.

**Verification**: source inspection only. Packed as `overlay_hud_export_v2.1.0-exp1.vpk`;
`addonversion` and the app both stay at 2.1.0, and the 2.1.0 outro behaviour is unchanged.

## 2026-08-25 - v2.1.0: hiding the consistent HUD when the game hides its own

**Live-tested and confirmed working** 2026-08-25 for the finale outro, as
`overlay_hud_export_v2.0.0-exp1.vpk`: the panel leaves with the escape scene and comes back
on the next map. That confirmation is what promoted the work from exp1 to 2.1.0.

The author then reported the other half of the same problem: at the chapter end the saferoom
door closes, the screen blurs, the score panel comes up and the music plays, and only then
does the loading bar appear. The overlay sat through all of that and left at the load, which
is seconds too late. It is the same mechanism - the game has stopped drawing its own HUD -
so it is the same detector, with the mask widened from `HIDEHUD_ALL` alone to
`HIDEHUD_ALL | HIDEHUD_HEALTH`: `HIDEHUD_HEALTH` hides precisely the survivor HUD this
overlay stands in for, so either bit answers the question being asked.

The exporter now also logs **every change in the raw reads**, not only the ones that move the
verdict. If some scene hides the vanilla HUD through a third bit, that value lands in
`console.log` as a state this build declined to act on, which makes the next build a
correction rather than another guess.

[UNVERIFIED] The chapter-end half. The outro is confirmed; whether the same two reads move
for the saferoom transition is exactly what the widened mask and the new logging are there to
settle. A false negative leaves the pre-2.1.0 behaviour in place and nothing else.

The rest of this entry is the exp1 work the confirmation promoted.

The consistent HUD stands in for the vanilla survivor HUD, so it has to leave when that one
leaves. The finale outro was where it did not: L4D2 hides its own HUD for the escape scene
and the overlay stayed drawn over it, which is the only thing on screen at the one moment
nothing should be.

The obvious mechanism is a game event - `finale_win` would name the moment exactly. It is
also the one mechanism this addon has already proven it cannot use: v1.0.7-exp1 and exp2
registered a listener from both the mapspawn and director phases and the callback was never
delivered in either build, while other addons' handlers for the same event fired in the same
log. That is settled and is not being re-litigated; detection has to come from the export
tick that is already running.

So the tick polls the host player for two independent reads, either of which is enough:

- `m_Local.m_iHideHUD` with `HIDEHUD_ALL` set - the engine's own record of what it has
  stopped drawing, which makes it the signal by definition rather than by inference;
- `m_hViewEntity` pointing at anything other than the player - a scripted camera owns the
  view.

Which of the two L4D2 actually moves during an outro was unverified when this was written,
which is why both
raw values ride `state.json` as `hud` and `view` beside the `cine` verdict, and why the debug
console prints all three. If the panel stays up through an outro, the capture says which read
was silent instead of costing another guess. Both reads are probed once and remembered, like
every other route in this exporter, and a missing route reports -1 rather than a confident 0.

Two lifecycle details decide whether this is correct rather than merely working:

- `cine` is cleared in `Boot`, so the verdict cannot survive a round restart. The script VM
  does survive one - that is the whole reason the re-entry files exist - so a latched
  cinematic would otherwise hide the panel for the entire next round.
- The app honours the last `cine` it read **even after the file goes stale**. That is
  deliberate: the outro is the last thing exported before the map ends and the exports stop,
  so a freshness gate would put the panel back up for exactly the report screen it is meant
  to sit out. The next chapter's first export clears it.

`hideDuringCinematics` in `config.json` turns the whole thing off, since a detector built on
raw engine reads deserves a switch.

**Verification**: the finale outro is live-confirmed on `v2.0.0-exp1`, as recorded at the top
of this entry. The chapter-end widening that ships with 2.1.0 is source inspection,
`dotnet build` Release clean and the layout check passing - it has not been played yet.
Packed as `overlay_hud_export_v2.1.0.vpk`; exporter and app advance together, as always.

## 2026-08-18 - v2.0.0: your own HUD, and what to do when the engine will not answer

**Live-tested and confirmed working** 2026-08-18: the v2.0.0 app and format-v1 VPK together
in L4D2, over `docs/TESTING.md` - the weapon slots and their ammunition, the carried-item
row, the green marking of the held slot, the incendiary and explosive marks, and the two
independent rosters. Two things the engine only answered by being played: the upgrade bits
sit one place lower than assumed, and the magazine is the wrong number to show while an
upgrade is loaded. Both are written up below.

Classified weapons by classname, in two tables beside the existing item tables, rather than
by position in `m_hMyWeapons`. That array is compacted, so the index a weapon sits at says
nothing about which slot it occupies - the same reason the kit/pill/throwable scan already
worked this way. A weapon in neither table exports as an empty slot instead of being
guessed at: "has a magazine" would also describe a deployable or a script prop.

Melee needed a second step. Every melee weapon is `weapon_melee`, so the useful identity is
`m_strMapSetScriptName` - `katana`, `fireaxe`, `frying_pan`. When that read fails the
exporter still reports a usable generic `melee` rather than an empty secondary slot.

Probed the ammunition routes once per script load and logged the answer, following
`DetectBotMode`. `Clip1()` is tried first as the semantic accessor, with `m_iClip1` behind
it; reserve ammunition comes from the player's `m_iAmmo` indexed by the weapon's
`m_iPrimaryAmmoType`, because the weapon itself only carries its magazine. None of these
routes is verified on this build yet - which is exactly why every one of them is a probe
with a logged failure and a `-1` fallback, and not a bare read. A silently missing ammo
route would have looked identical to a survivor permanently out of bullets.

Kept `-1` distinct from `0` all the way through the transport and into the card. The app
prints nothing for `-1`, because a full rifle showing `0 / 0` is a worse failure than a
rifle showing no numbers.

Made the weapon icon set optional rather than required. The nine item icons ship with the
app and a missing one is a build error; weapon art arrives one PNG at a time, so a slot
with no icon draws a short text label instead and switches to the icon by itself once the
file lands in `workshop assets/weapon-icons/`. The embedded-resource loader was split for
this: item art still throws on a miss, weapon art returns null.

Gave ammunition its own transport rather than speeding up the roster. Five exports a
second cannot count an Uzi's magazine down - it empties at about twelve rounds a second, so
the number arrived in jumps. Raising `INTERVAL` to match would have quadrupled the cost of
the whole export, for every survivor and every field, to fix one number belonging to one
player. `ammo.txt` is fifteen bytes written at 20 Hz from three property reads on one
entity, and the roster tick is untouched.

Kept weapon identity on the slow channel deliberately. Which gun is being held changes
rarely and nobody can see a fifth of a second of lag on an icon; the rounds change
constantly and everybody can see the counter stutter. Splitting them that way is what keeps
the fast file tiny.

Stopped exporting weapons for anyone but the host at the same time. Only their weapon HUD
is drawn, so classifying every bot's rifle and reading its magazine was work whose result
nothing displayed - and at 20 Hz it would have been repeated four times a second per bot.
Items stayed per-survivor: those really do appear on every card.

The channel's own check caught a bug worth keeping the lesson from: a leftover `ammo.txt`
from the previous session was believed on its first sighting, because a file that has been
read once looks exactly like one being written. It has to be seen to ADVANCE before it
counts - the same rule `StateReader` already applies to `state.json`, arrived at the same
way and for the same reason.

Built the weapon row into the survivor cards first, and took it back out. On a card it
was a fourth column competing with the health bar for the same glance, and it repeated for
every survivor something only your own ammunition count actually answers. It is now a
separate root-level panel following the listen-server host, placed like the Separate You
card: its own corner, its own height, independent of the roster template and its spacing.
The cards are back to exactly their v1.2.0 layout, and the check asserts that none of the
three card templates draws a weapon.

Gave it its own size multiplier for the same reason it got its own corner. It inherits the
Consistent HUD's scale, so it tracks resolution and the roster's tuning, but a HUD size that
suits four survivor cards is rarely the size you want a number you read mid-fight to be. The
setting is a multiplier on that scale rather than an absolute one: the roster's size stays
the reference point, and 1.00x still means "same as everything else".

Then took the item slots off the player's own consistent card and gave them to this panel.
Only theirs: the first cut removed the column from every card, which was wrong the moment it
was on screen - a teammate's card is the only thing that says what that teammate is carrying,
and the weapon HUD answers for one person. So the roster keeps items for everyone else, the
player's own card drops the three the panel now draws larger, and nothing is said twice. The
scoreboard cards kept the whole team's, the player included: the weapon HUD is hidden while
Tab is held, so removing them there would have meant nobody's items were visible at all.

Kept all three item places whether or not they are filled, which is the one place this copies
vanilla rather than the cards. A row that reflows as items are used means the eye has to read
the icons; a row with fixed places means position alone answers "do I have pills", which is
the whole point of a HUD you do not look at directly.

The panel's hide rule had to change with it. It hid when there were no weapon slots, which
was right when weapons were all it drew and wrong the moment it carried items - unarmed with
a kit in your pocket has something to say. Now it hides only for genuinely empty hands and
empty pockets.

Read the loaded ammunition kind from two properties together, because either alone is
wrong. `m_upgradeBitVec` says which upgrade the weapon has been given and stays set once
given - it never returns to normal, so a HUD driven by it alone would show a flame over
plain rounds for the rest of the map. `m_nUpgradedPrimaryAmmoLoaded` is the count that
actually runs out. So the count decides whether anything is marked and the bit vector only
decides which mark, and the whole thing returns to a plain cartridge by being fired.

Had the upgrade bits one place too high on the first pass, and only the game could say so.
The first build read incendiary at `1 << 1` and explosive at `1 << 2`, which in a live round
gave a plain cartridge for incendiary and a flame for explosive. Both symptoms together fix
the layout: incendiary is `1 << 0` and explosive `1 << 1`. Worth writing down because there
is no way to see it from outside the game, and because one observation would have been
ambiguous where two pinned it.

Showed the upgrade's own pool rather than the magazine while one is loaded, which the first
build got wrong in a way only playing it reveals: reload with incendiary up and the count
jumped back to 30, because the count was `m_iClip1` and that is exactly what a reload does.
The magazine is the wrong number to show for an upgrade - it answers "how many before I
reload" when the question is "how much fire is left". `m_nUpgradedPrimaryAmmoLoaded` counts
down across reloads and hits zero at the moment the slot goes back to ordinary rounds, so
it is both the honest number and the one that makes the mark's disappearance make sense.

Put that kind on the 20 Hz channel rather than the roster, which is the opposite of where
weapon identity went and for the same reason. Identity changes on a keypress; this changes
on the trigger, alongside the counter it sits beside, and a mark that outlives the last
upgraded round by a fifth of a second is a mark on the wrong bullet. The field is appended,
so a four-field line from an older writer still reads and simply means "normal".

Marked the held slot from the exporter rather than working it out in the app. The obvious
version compares the active weapon's classname against the exported slot ids, and it is
wrong twice over: a pair of pistols is still `weapon_pistol`, and every melee weapon is
`weapon_melee`, so the two cases the HUD most wants to mark are the two the comparison
cannot separate. The exporter is already walking `m_hMyWeapons` with `GetActiveWeapon()` in
reach, so it compares entity indices there and exports a place - `primary`, `secondary`,
`throwable`, `kit`, `pills` - and the app lights that box. An id it does not recognise, a
gas can included, exports nothing and marks nothing, which is the honest answer.

The mark rides the 5 Hz roster rather than the 20 Hz ammunition channel. Switching weapons
is a keypress, not a stream, and a fifth of a second on a border is not the same as a fifth
of a second on a counter that has to follow the trigger.

Split the two views' rosters at the same time, which the weapon HUD made obvious. The
scoreboard panel exists beside L4D2's own scoreboard and the consistent HUD exists instead
of L4D2's survivor HUD, and one shared filter forced the same answer on both: "All
survivors" printed the original four next to the vanilla scoreboard already listing them,
and the only way to avoid that was to lose them from the persistent HUD too. So the
scoreboard's filter drops All entirely - its floor is now Extras, which is the honest
behaviour for a panel that supplements rather than replaces - and the consistent HUD gets
its own copy of all four. An older config set to All migrates to Extras on the scoreboard
and All on the HUD, so the view that was showing everyone still does.

The weapon HUD is deliberately not filtered by either. The roster filter answers "which survivors do
I want listed"; the weapon HUD is the player's own HUD, and blanking their ammunition
because they set the filter to Followers Only would be a bug wearing a setting's clothes.

Drew the weapons as geometry first, one silhouette per family in a shared 64x24 box. Two
shapes had to be redrawn after rendering them: a long triangular tip made both melee
families read as arrows, and the "unknown weapon" crate drew solid because a Path with a
fill and no stroke ignores line segments - it is a hollow box now, which EvenOdd gives for
free.

Then replaced them with the game's own art. Getting anything out took two throwaway
readers, because the shipped `vpk.exe x` wrote correctly-sized files full of zeroes on this
install: a VPK directory walker, and a VTF decoder that finds the largest mip and hands it
to Pillow wrapped in a DDS header.

Cut the first set out of `materials/vgui/hud/iconsheet.vtf` and its two siblings, which
meant segmenting the atlases and identifying 29 shapes by eye - and getting the SCAR and
the SG552 the wrong way round, which the author caught. The right source turned up while
looking for an M60: the update pak carries one texture per weapon, named for the weapon -
`materials/vgui/hud/icon_rifle_m60.vtf`, `icon_chainsaw.vtf`, `icon_tonfa.vtf`. Switched
the whole set to those. It covers 33 weapons instead of 29, it is the art the game itself
draws, and the filenames settle which rifle is which instead of leaving it to judgement.

Flattened every icon to white through its alpha, which the game's textures are not - the
frying pan is grey and would have read as disabled next to the other slots.

Kept one icon from the atlas after all: the single pistol. The game's `icon_pistol.vtf` is
the PAIR, and there is no separate texture for one, so a lone pistol was drawing as two.
The atlas carries both, so the single came from there and the pair from the update pak. The
exporter tells them apart with `m_isDualWielding`, and falls back to the magazine size when
that read fails - over 15 rounds is a pair. The fallback is one-way and known to be: a pair
firing down to its last rounds reports as a single pistol until it reloads.

Scaled them by one shared factor rather than per weapon. The game already draws them in
proportion to each other - a Magnum is 63 pixels wide where an M60 is 256 - so a single
factor carries that relationship onto the HUD, with a floor so the smallest do not become
smudges and a ceiling set by what the slot holds beside its ammunition column. The drawn
silhouettes stay as the fallback for the riot shield, which has no HUD icon, and for ids
from other addons; the text label remains the last resort behind both.

## 2026-08-15 - v1.2.0: the scoreboard and persistent HUD are different jobs

Replaced the old `alwaysShow` behavior, which kept the scoreboard-shaped panel persistent.
That was useful for layout work, but it made the persistent view look like a scoreboard left
open. Split the editor into two explicit modes: **Scoreboard** keeps the Tab-held
vanilla-sidebar geometry, while **Consistent HUD** uses a borderless row-major grid renderer.

Used named templates for the persistent view instead of exposing another set of raw
coordinates. The default bottom-centered horizontal grid, preserved lower-left vertical grid,
and lower-right vertical grid cover the intended HUD roles while keeping the panel inside the
screen at every resolution. Set four cards per horizontal row, with no more than three rows;
larger rosters add columns. Gave the persistent HUD separate scale and opacity values so
changing the scoreboard does not unexpectedly change the always-visible HUD.

Added a third, single-key path to the global input state machine alongside the pass-through
scoreboard key and the consumed Tab+Insert editor chord. Repeats and the matching release are
consumed after one toggle, and the toggle is gated to the game foreground. Save the choice to
`config.json` so the editor setting and hotkey cannot drift apart.

Kept the existing complete-roster transport for both presentations. Extended it for the
Separate You feature with one optional local-player marker. Packed the release VPK as
format v1 because the target L4D2 build rejects VPK v2.

Changed the first consistent-HUD layout from four vertical stacks to the vanilla-like reading
order: rows fill left-to-right, with four cards across and up to three rows, followed by
additional columns for larger rosters. The editor preview and live overlay both use that same
row-major split.

Retained **Lower Left Vertical Grid** as a separate renderer. Its legacy `vanilla-vertical`
config value still maps to the one-card-per-row layout, while the new lower-right vertical option
uses `lower-right-vertical`. Kept `bottom-right` as a migration alias for
`vanilla-bottom-center`, so the main default remains useful for older configs. The retired
`top-vertical` value follows the same migration path. Added `consistentVerticalOffset` for the
bottom inset, so the persistent HUD can move upward without changing the scoreboard's `offsetY`.
Passed horizontal and vertical spacing through the shared card template's
ItemsControl margin, allowing the simulated preview and live overlay to use overlap-friendly
spacing without changing the scoreboard template.

Added a design selector separate from placement. **Basic** keeps the existing card template.
**Minimalist** uses a compact 260-pixel segmented health strip with the name and item icons
before the health value above it; temporary health keeps the existing grunge brush, and the
icons have no slot boxes, only a black outline. The items use an Auto-width column, so a long
name is ellipsized instead of pushing the items out of view. Applied the selected design to the
live roster, vertical roster, separate You card, and simulated preview, while the Scoreboard tab
keeps its own template.

Kept the follow-up presentation controls scoped to the Consistent HUD. The saved
`consistentShowHealthNumbers` flag hides the numeric health value in both Basic and Minimalist
cards without changing the scoreboard. The saved `consistentMonochrome` flag routes health bars,
state text, and follower markers through grayscale brushes. The item artwork was already
monochrome, and the pulsing warning edge uses a white brush in this mode. Reduced Minimalist's
strip to five segments and compressed it vertically so the compact design uses less screen
height.

For the final v1.2.0 addition, separated the current survivor without changing the default.
Updated the exporter to compare each survivor's player ID with the verified L4D2
`GetListenServerHost()` handle and write an optional `local` boolean. The app removes that
marked card only when **Separate You** is enabled in the Consistent HUD tab. Rendered it in its
own root-level element and left the selected roster's spacing margin on the remaining cards.
The bottom horizontal template places the roster left and the player card right; the lower-right
vertical template mirrors the player card to the left so the two elements do not occupy the same
corner. The simulated preview uses its first sample card as the stand-in for the host. Older
exporters omit the field, so the option has a safe no-marker fallback: no card is moved.

First live report exposed two implementation faults before release: the render path cleared
the independent card before a no-op geometry render returned, and a transient null/failed host
probe could remove the `local` marker for one export tick. Fixed the renderer to preserve the
card through no-op renders and mark a hidden state dirty for the next show. Made the addon cache
the last valid host userid for the current script generation. These fixes keep layout
selection and state detection from making the player card blink.

Follow-up in-game screenshot found a second, independent geometry fault: making the two
elements root-level siblings did not reserve the player card's width, so a four-card roster row
could grow underneath it. Subtracted the rendered player-card width and a fixed inter-group gap
from the roster's usable width. The measurement helper also
settles newly generated WPF item containers before fitting, and the fit pass converges in one
render instead of waiting for later state ticks. The horizontal Separate You layout now starts
the shared roster at three columns, reserving the fourth area for the independent player card;
the ordinary four-column split remains unchanged when the option is off.

Live-tested the current app and format-v1 VPK in L4D2, including the Consistent HUD health-number
switch, black-and-white theme, and five-segment Minimalist layout.

**Live-tested and confirmed working** 2026-08-15: Confirmed the v1.2.0 Consistent HUD
presentation and its new options in-game.

## 2026-08-15 - v1.0.10: which half is stale, and how it can know

The addon half updates itself. Someone subscribes on the Workshop and Steam keeps it
current without them thinking about it. The app half is a zip on a releases page, and
nothing on this machine ever goes looking for a newer one. That asymmetry is the whole
problem: the app is the half that quietly falls behind, and it was the half with no way to
find out.

The version had to come from the addon, and there were two places to read it. `state.json`
already carries a `v` field, which is nearly free - the app parses that file five times a
second anyway. But it only answers during a round, or from whatever leftover file the last
session left behind, and the main menu is exactly where someone is in a position to go and
download something. `addoninfo.txt` inside the installed pack answers at any time, on a
cold install that has never loaded a map.

So the manifest is the source and `v` is the fallback. The cost turned out to be near
nothing: `VpkReader` already walks the directory tree of every pack in `addons\` to decide
which one is the exporter, and it was throwing each file entry away as it went. Now it keeps
the one for `addoninfo.txt` and reads the handful of bytes behind it. Two storage forms had
to be handled - a small file can sit inline in the tree as preload data, or in the data
section after it - plus the numbered sibling archive a `_dir.vpk` uses. Both inline forms
are covered by the check; the sibling-archive path is written but has no fixture, because
nothing builds an addon this small as a multi-chunk pack.

The message shows during a round as well as at the menu. That is a deliberate break from how
the status corner has worked until now - it has always been a menu-only thing, on the
principle that a round is not the time for the app to talk about itself. But the badge is
menu-only because *staleness* is a menu condition, and a version mismatch is not: it is true
the whole time. Someone whose only habit is holding Tab mid-round would never see a menu-only
notice at all.

What it deliberately does not do is block anything. The transport has been compatible in
both directions for its whole life - the app still reads the pre-v1.0.4 loose state file,
and an exporter older than v0.6.5 that writes no `cls` field is treated as the old behaviour
rather than as an error. A version mismatch is not a fault, and the wording says so on both
screens: the HUD keeps working, there is simply a newer build.

The overlay cannot carry the link - it is `WS_EX_TRANSPARENT`, so a hyperlink in it would be
decoration. The editor gets the real one.

Unknown is silent. No pack, an unreadable manifest, a version string that is not a version:
all of them claim nothing. That is the same rule the addon probe already runs on - a pack
that cannot be parsed is not evidence of an addon being absent - and it is what keeps this
from becoming another message nobody trusts.

The `menu-stale` check failed on the first full run, which is the version gate doing its
job: its fixture wrote a hard-coded `"v":"1.0.9"` into a fake state file, and against a
1.0.10 build that is an addon one version behind, with a notice to match. The fixture now
writes the running build's own version.

## 2026-08-15 - v1.0.9: the health bands were off by one step at both edges

Reported from play: 24 HP still drew yellow, and so did 40. Both are single-character bugs
in the same expression, and they have different causes.

The upper edge was an exclusive comparison - `hp > max * 0.40` - against a table that reads
40-100 green. 40 HP is not greater than 40, so the boundary value fell through to the amber
arm. The lower edge was a plain wrong constant: the amber floor was `0.20`, which put the
amber band at 21-39 and left red starting at 20 rather than at 24.

Both bands are now `>=` and the floor is `0.25`. The comparison stays proportional to the
survivor's own maximum rather than becoming a literal 40/25, because a reduced-max survivor
should band off what he can actually hold; at the default 100 max the two forms are the
same numbers.

Checked with a new `health-colour` mode in the layout-check harness, which builds real
`SurvivorCard` instances at 100/40/39/25/24/1 HP and reads the colour back off
`HealthBrush`. The boundary values are the whole point of the test - a check that only
sampled band middles would have passed against the broken code.

## 2026-08-14 - v1.0.8: the temp-health bar was the wrong colour by construction

The buffer segment was a fixed pale blue at 55% opacity over a dark bar. Against green,
amber or red it landed somewhere near grey, which is what the author reported after
comparing it side by side with the vanilla HUD: in game the buffer is the bar's own colour,
scratched over, not a colour of its own.

Two things had to change together. `TempBrush` is now derived from the health colour rather
than being a constant, and the XAML `Opacity="0.55"` came off the rectangle - with alpha
baked into the brush, leaving the opacity there knocked it back a second time and put the
grey straight back.

The texture is a tiled `DrawingBrush`: the colour at 74% brightness, with three uneven
slanted streaks at 40% over it. Uneven on purpose - evenly spaced stripes read as a
progress bar or a loading indicator, which is the one thing a health bar must not look
like. Brushes are frozen and cached per colour, because cards are rebuilt on every poll and
five new tiled brushes a second is work the render thread does not need.

This also settles the black-and-white case the author raised: nothing special is needed for
it. The buffer follows the bar, and the bar is already grey on the last strike, so a
black-and-white survivor who drinks pills gets a grey buffer for the right reason.

Checked by rendering every bar state offscreen through the shipping `SurvivorCard` code with
the same two-rectangle layering the card template uses, rather than by eye in a live
session.

A side-by-side screenshot then settled the downed state too: the game's own panels draw
incap health as a hatched red bar, and the overlay was drawing a flat block. Down now takes
the same grunge brush for the whole bar, not only for the buffer past current health.

The black-and-white pulse is the one piece here that is not a colour choice. The card list
is rebuilt on every poll - `Columns.ItemsSource` is assigned a new list - so a storyboard
declared in the card template is thrown away and restarted roughly five times a second, and
never travels far enough from its start value to be seen moving. The animation therefore
runs once, at startup, on a single shared `SolidColorBrush` resource, and the template's
`DataTrigger` only swaps `BorderBrush` to it. That also puts every marked card on one clock,
which reads as a single alarm rather than several unrelated flashes. `BorderBrush` had to
move from a local attribute into the style for the trigger to win at all.

## 2026-08-14 - v1.0.7-exp3, shipped as v1.0.8: the restart delay was a cold-load wait in the wrong place

Promoted to v1.0.8 after the author confirmed the restart recovery live. The three exp
builds were deleted on promotion; v1.0.6 and v1.0.7 never shipped either, and the changelog
folds all five into the one release. The trail below is why the shipped fix looks nothing
like the first two attempts.


exp2 confirmed live: the panel repopulates after a wipe restart. The author's follow-up was
the delay, and it was not deliberate - `BOOT_WAIT` is 5 seconds because a chapter load has
no settled roster at t=0, and the re-arm path inherited it without anyone deciding it
should.

Rather than trade the wait against a mid-respawn misread, the two concerns are now
separate. `REARM_WAIT` (1s) covers a restart into a live VM, `BOOT_WAIT` (5s) still covers a
cold load, and the case the long wait was really guarding - exporting an empty roster while
survivors are still being put back - is handled directly: within `SETTLE` (4s) of any boot,
a zero-survivor result is not written at all. The app holds its stale frame, blank, which is
the honest state; publishing `count: 0` there would reproduce the original false zero from
the opposite direction.

`coldLoad` is what tells the two apart, and it is cleared on the first export rather than at
boot: the second and third entry points of a normal chapter load run before anything has
been exported, so they correctly keep the long wait.

**Verification**: source inspection. Not live-tested.

## 2026-08-14 - v1.0.7-exp2: stop waiting for an event that is never delivered

exp1's theory was that the mapspawn phase was the wrong place to register from. It was
wrong. exp1 registered from both phases, the log shows both lines -
`listener registered (mapspawn)` at 415167 and `(director)` at 415223 - and on the restart
that followed, `[CF AutoSpawn][DEBUG] round_start_post_nav fired` appears at 430640 while
`[OVLHUD] re-arming` still appears nowhere. Two builds, two registration sites, zero
callbacks. Why the delivery does not reach this addon is unresolved and is now moot: the
event route is abandoned rather than debugged further.

The author also narrowed the report usefully: chapter progression carries the HUD fine,
and the failure is only the same-map restart. That fits exactly - `mapspawn_addon.nut` runs
on a chapter load and not on a restart.

What actually works on this install was sitting in the same log the whole time.
`[ADS] thinker re-armed after a round restart` is aim-all-guns recovering from the same
restarts, and it uses no game event at all: it ships `scriptedmode_addon.nut` and
`director_base_addon.nut`, both of which DO re-run on a restart, and re-does its bootstrap
idempotently from each. Its re-arm branch also proves the script VM survives that restart -
the addon's own booted flag is still set - so what the restart destroys is the map's
entities, which is precisely what kills an `EntFire`-on-worldspawn chain.

So the exporter now does the same: two re-entry files, each calling `Rearm` -> `Boot`,
with the existing generation guard making a double call free. The `events` table and
`RegisterRoundEvents` are deleted; a dead mechanism sitting next to a live one is how the
next person loses a day.

Both re-entry files aim their fallback include at `getroottable()` explicitly. They execute
in the map-script and DirectorScript scopes, not the root table, and `::OvlHud` has to be
in the root for the scheduled `RunScriptCode` ticks to resolve it.

**Verification**: source inspection and the console-log capture above. Not live-tested.
Packed as `overlay_hud_export_v1.0.7-exp2.vpk`.

## 2026-08-14 - v1.0.7-exp1: the listener was registered into the wrong phase

v1.0.7 shipped the right mechanism and registered it in a place where it does nothing. The
author reported the same false zero roster with 1.0.7 loaded, and added that it reproduces
with any addon that adds extra survivors - Spawn L4D1/L4D2 Survivors included - which rules
out Finale Soldiers' carryover restore as the cause and puts the fault squarely in the
exporter.

`console.log` settles it. With 1.0.7 loaded the addon prints its own
`round_start_post_nav listener registered` on every map load. On the same-map restart that
follows, `[CF AutoSpawn][DEBUG] round_start_post_nav fired` and the other addons' round
handlers all appear - and `[OVLHUD] round_start_post_nav: re-arming exporter` appears
nowhere in 238,000 lines. The event fired. The callback was not delivered. The frozen
`state.json` (`seq` 101, `time` 26.83) is that dead export chain, and the app's
`EXTRA SURVIVORS 0` is `MainWindow` line 717 refusing to draw a stale file - the app was
right at every step.

The difference between the addons whose handlers fire and this one is the entry point.
Finale Soldiers registers from `director_base_addon.nut`, which the console log shows
loading after `scriptedmode_addon.nut` and `ScriptMode_Init`. This addon registered from
`mapspawn_addon.nut`, which loads before both. A registration made in the mapspawn phase is
not present in the table the dispatcher reads once scripted mode has initialised. The exact
engine-side reason is unverified - `scriptedmode.nuc` is compiled bytecode in `pak01_dir`
and was not decompiled - but the behavioural difference is reproduced in the capture and it
is enough to place the registration correctly.

Fix: keep the mapspawn registration and add an additive `director_base_addon.nut` that
registers again. Two live callbacks would call `Boot()` twice in one frame and the second
generation bump retires the first chain, so the duplicate is free; having none is not.

**Verification**: source inspection and the console-log capture above. Not live-tested.
Packed as `overlay_hud_export_v1.0.7-exp1.vpk`; `addonversion` deliberately stays at 1.0.7.

## 2026-08-14 - re-arm the exporter after a same-map round restart

The supplied captures narrowed this to the HUD transport, not the survivor-spawn addon.
The state file's last write was 09:50:52, exactly when the team wipe restarted the round;
the `EXTRA SURVIVORS 0` capture was 09:51:00. The overlay was therefore doing the safe
thing with a stale file, but the VScript export chain had never been restarted for the
new round. `mapspawn_addon.nut` is not guaranteed to execute again when the Director
restarts the same map.

The exporter now registers `round_start_post_nav` through the additive event callback
collector. That event is observed on both a fresh map and a same-map restart, after the
real survivor entities have settled. It calls the existing generation-safe `Boot()`;
the old `EntFire` chain is superseded and one new chain begins after the normal five-
second roster-settle delay. No survivor addon or Finale Soldiers code is changed.

**Verification**: source inspection and app build/layout checks; live confirmation still
needs the supplied reproduction with the new v1.0.7 exporter loaded from a fresh L4D2
launch.

## 2026-08-14 - version alignment for the restart follower fix

The follower-count failure belongs to Finale Soldiers' carryover restore, where a
replacement soldier could keep following through native survivor behavior without
having its `cf_soldier_following` scope marker rebuilt. The exporter is intentionally
read-only and already classified that state correctly, so no HUD workaround was added.
The exporter and desktop app advance together to v1.0.6 so users do not pair the fixed
Finale Soldiers build with an older overlay package by version accident.

**Verification**: source and VPK structure checks pass; the overlay app build and layout
checks pass. Not yet live-tested - the combined run must confirm the state file emits
`cls: "follower"` after a restart and the overlay draws the restored cards.

## Overlay HUD v1.0.5 - 2026-08-13: a hook can be gone without anything saying so

The report was "it showed, I alt-tabbed, it never came back, restarting the app fixed it".
Restart-fixes-it narrows the field hard: it is state held in the app, not the game, not the
addon, and not the state file — the exporter kept writing throughout.

Two candidates fitted. A fullscreen window can come up above a topmost overlay after an
alt-tab, and `Topmost="True"` in XAML is a one-time assertion. And `WH_KEYBOARD_LL` is
removed by Windows when its callback overruns `LowLevelHooksTimeout`, silently, with the
handle left non-null and no way to ask whether the hook is still installed. The second one
explains the restart perfectly, and the callback here was raising `HeldChanged`, which ran
`Render()` — measure, arrange, fit — on a layered, software-rendered window, synchronously,
inside the hook. Under a load spike that is not a hypothetical overrun.

What made this fixable was giving up on detecting the hook directly. There is no query for
it. `GetAsyncKeyState` reports the physical key regardless, so the disagreement between it
and the tracked hold state is the evidence, and correcting the state is worth doing on its
own — it also repairs a keyup missed during the focus switch. Reinstalling waits for the
disagreement to survive two polls, because a key pressed on the poll boundary can be seen
before the callback has run and one sample is not proof.

The part worth keeping as a habit: the repair is a poll, so it is also a fallback. If every
reinstall failed the overlay would still follow Tab at 250 ms granularity, because the same
comparison that detects the fault also supplies the answer. A watchdog that only restarts
something is worth less than one that can carry the feature while the restart is failing.

Both fixes shipped. The z-order re-assert is cheap, fires only on the focus transition, and
if it was never the cause it costs one API call per alt-tab.

The same session turned up the menu panel: hold Tab at the main menu and last session's
roster appears. Two separate mistakes, one symptom. The reader treated the first successful
read as an advance, so a file written an hour ago looked live for a full staleness window;
and the renderer kept drawing `Current` while stale, on the reasoning that a stale roster is
better than none. It is not. A stale roster is wrong and looks exactly as authoritative as a
correct one, which is the worst thing a HUD can be.

The question worth asking was whether the `NO EXPORT` panel from v1.0.2 should go with it.
The first answer was that it should be earned rather than removed: it exists so a broken
setup is not a blank screen, and that is only owed while the setup is unproven. Once `seq`
has been seen to move, the app knows the addon works, so staleness after that is the menu or
a load and the panel keeps quiet. Explain yourself until you have been proven right, then be
silent — a rule that reads well as a sentence, which is usually the sign, and which turned
out to be only half the fix. See below.

Proven had to outlive the process, though. Held only in memory it resets every launch, so
the first Tab of every session — at the menu, before any map has loaded — showed the help
again on an install that has worked for weeks. `exporterProven` in `config.json` is one bool
and it makes the message mean what it says.

Then the message turned out to be wrong on its own terms. Reported as confusing, and it was:
the game was running, the addon was in `addons`, the exporter was writing every round, and
holding Tab at a main menu still produced "IS THE ADDON LOADED?". Checked on this machine
and the accusation was baseless - one `overlay_hud_export_v1.0.5.vpk`, enabled, and a
`state.json` at seq 677. The message was inferring "not installed" from "not advancing",
and those are the same observation at every menu.

So look at the addons folder. That is the evidence, and it was available the whole time.
The one trap is that filenames cannot identify a pack: a Workshop subscription is stored as
`addons\workshop\<publishedfileid>.vpk`, a number with nothing of the addon's name in it,
and matching on the name would report a subscribed install as missing - the same false
alarm one layer down. So packs are identified by opening them and reading the VPK directory
tree for the exporter's script. The tree is at the front of the file with its size in the
header, so identifying a 200 MB map pack costs the same as a small one. This install has 135
of them and the whole sweep takes about a second, cached until the folder changes and run
off the UI thread regardless.

`addonlist.txt` came along with it, since "installed but switched off in the Add-ons screen"
is a real cause and looks identical to everything else from the transport's side.

The lesson worth keeping is not about VPKs. Every version of this message has been a guess
dressed as a diagnosis, and each one was wrong in the same way: the app knew the difference
between a fault and a menu was unavailable to it, and said something confident anyway. The
fix was not better wording, it was finding a second source.

That leaves the honest gap: a setup that used to work and has since broken now gets silence
where it used to get an explanation. Which is the argument for the debug console rather than
against the rule. The panel is a HUD - it has room for a state, not a diagnosis - and it is
also the thing that goes missing when something is wrong, so it is the worst possible place
to put the answer. The console is the opposite: a live block of current values first,
because "is it working" is a question about now, and the log of transitions under it for how
it got there. Both are built on the same discipline the log itself needs - everything here
is a 100 ms or 250 ms poll, so recording every sample would bury the one line that matters.
`Note` keeps changes only, per key.

## Overlay HUD v1.0.4 - 2026-08-13: the answer was already in the same ems folder

Whether `StringToFile` accepts a subpath, and whether the engine creates the directory, are
exactly the kind of questions v0.1.0-probe1 exists to warn about — community lore, no
observation. Both were already answered on this machine and neither needed a probe build:
Finale Soldiers calls `StringToFile("finale_soldier/" + map + ".txt", ...)`, and
`ems/finale_soldier/` is full of files written during play. A VPK addon, a relative subpath,
a folder nothing else created. That is the whole proof.

Worth recording as a habit rather than a fact: the install already running the mod is a
primary source, and reading a sibling addon's source beat guessing at the API.

The compatibility case is the part that needed thought. The two halves are separate
downloads — an addon on the Workshop, an app on GitHub releases — so they are not upgraded
together, and a path change breaks the pair in one direction. A new app meeting a v1.0.3
addon would find no `overlay_hud/state.json` and draw `NO EXPORT` over a session that was
exporting perfectly. So the app falls back to the old loose path, and — this is the half
that is easy to miss — its command file has to follow, because an old addon reads only
`overlay_hud_cmd.txt`. Writing the new name beside the old state file would be a hold
nothing ever picks up, which is a silent no-op, which is the failure mode this project keeps
finding. The check now asserts both pairings.

The other direction is not defended: an old app with a new addon reads a stale file, and the
staleness rule already covers it. Only one of the two can be fixed from here.

## Overlay HUD v1.0.2 - 2026-08-13: three reports, one fact

Reported together: the scoreboard hold did nothing, the top-right badge was on constantly in
game, and the panel would not draw. Three symptoms, one cause, and the evidence settled it
without a single guess:

- `overlay_hud_state.json` began `{"v":"1.0.0"`, while `overlay_hud_export_v1.0.1.vpk` was
  placed in `addons` at 01:22 and the game had been running since 23:48. **Source mounts
  addon VPKs at startup.** The half that reads the command file was never loaded.
- `overlay_hud_cmd.txt` existed and read `0 28`, so the app was writing correctly and
  nothing was reading.
- The state file's last write was 01:19:45 while `console.log` was still being appended at
  01:28 — the exporter had stopped while the game ran on. Replacing a mounted VPK mid-session
  does that: the pack the engine had open is gone, and the next map load has no script.

With nothing being written, the badge ("L4D2 focused, exporter not writing") was telling the
literal truth and the panel had no roster to draw. Both were correct, and both looked like
the overlay was broken.

That is the actual defect worth fixing. "Nothing is exporting" and "nobody to show" were
rendered identically - as an empty screen - so the panel now draws with the reason on it, and
names the cause it will have nine times in ten. The v0.2.0 rule that a healthy export with
an empty roster draws nothing is deliberately kept; the two cases are now distinguishable,
which is the whole point.

The badge got a switch rather than a redesign. It cannot distinguish menu, lobby, loading,
and pause - that limit is inherent to a one-way file transport and was recorded in v0.2.0 -
so the honest options are "show it" and "do not", and which one is right depends on whether
the setup is still being proven.

## Overlay HUD v1.0.1 - 2026-08-13: the right layer was inside the game all along

v1.0.0's scoreboard hold was reported as doing nothing in game, and the reason is the same
sentence the v0.6.6 entry used to explain why it was hard: **input goes to the focused
window.** Holding the key with `SendInput` while the editor has focus types into the editor.
The v0.6.6 design worked around that by making the hold follow focus - which meant it could
only ever be seen while the editor was *not* being used, i.e. never, in practice.

The layer was wrong. The addon is already running inside the game, on the machine that hosts
the session, and `SendToConsole` runs a command on that host's own client. One line of
Squirrel does what no amount of external input can: `+showscores`, held, regardless of which
window has the keyboard.

That needs a channel back to the addon, which this transport never had. It is deliberately
the smallest thing that works: one file, `ems/overlay_hud_cmd.txt`, one line, `"<want>
<seq>"`, read on the export tick that was already running. No new timer, no new dependency,
and the same directory `StringToFile` already writes into.

The seq is the part worth keeping. A pure want flag is safe as long as the writer lives long
enough to write a 0, and an overlay killed while holding does not. Because the app rewrites
the file about four times a second, "seq stopped advancing for two seconds" means the writer
is gone, and the addon releases the scoreboard by itself. State that only one side can clear
needs the other side to be able to time it out.

Two ordering bugs surfaced from the checks rather than from the game. The panel decided
whether to draw its fallback marker before the hold had been attempted, so it always drew on
the first tick; and a failed write left the hold believing it was still held, so no release
was ever sent. Both are the same shape - reading a result before producing it - and both are
now asserted.

Also worth recording: this returns the project to no input synthesis at all, which is where
v0.5.0 deliberately left it.

## Overlay HUD v1.0.0 - 2026-08-13: what the number claims

Both halves go to 1.0.0 with no logic change. Worth recording what that number is being
used to mean here, because it is not "tested": the exporter's soldier classification, the
three roster filters, live preview, and the scoreboard hold are all source-level and
check-level only, and `docs/TESTING.md` exists precisely because they have not been run
together in a session yet.

The bump also caught a defect - in the checks, not the product. `live-preview` asserted
"no key is held, because L4D2 is not the foreground window here", which is only true when
L4D2 is closed. Run with the game open, the check found the real process, pulled it to the
foreground and held its scoreboard key: a check reaching out of its own process and into a
live session. It now points at a process name nothing can have. Worth keeping as a shape:
an assertion whose truth depends on what else happens to be running is not an assertion,
and this one only failed because the game happened to be open on the second run.

What 1.0.0 does claim is that the scope is closed. The original problem - L4D2's HUD has
room for four survivors and Finale Soldiers spawns more - is solved end to end: a VPK that
writes state, an app that draws it, an editor to place it, and filters for which part of an
oversized roster matters. Everything after this is fixes and refinements against real
sessions, which is exactly what a 1.x line is for.

## Overlay HUD v0.6.6 - 2026-08-13: the game's scoreboard cannot be part of a live preview

The request was to drop the simulated preview and instead have the editor put the game into
its Tab state - real scoreboard, real overlay, adjust against both. Half of that is free and
half is impossible.

Free: the overlay panel itself. It already follows the game window by process rather than by
focus, it is click-through and topmost, and the editor already tracks its own activation.
Live preview is therefore one extra reason to draw inside `ShouldShow`, plus a path that
pushes the editor's draft into the running configuration without saving it.

Impossible as first framed: the scoreboard cannot be on screen *while the editor has focus*.
It is rendered only while the game holds input focus and `+showscores` is held, and
synthesized input goes to whatever is foreground - which, with a slider under the cursor, is
the editor. The only mechanism that reaches an unfocused game is a console command over
`-netconport`, i.e. talking to the game process, which this project has refused since
v0.1.0-probe1.

The reframing is what makes it work: the scoreboard does not have to be visible at the same
instant as the slider. **Hold the game's scoreboard open** brings L4D2 forward and holds the
configured hold key down, so the real scoreboard and the real panel are on screen together;
clicking back to the editor releases it and marks the region out instead. Focus is the state
variable, so the rule is simply "held while the game is in front". That is a state machine,
not a timer, and it lives in `ScoreboardHold` with an injectable sender so every transition
is asserted without touching the input queue.

The failure worth engineering against is a latched `+showscores` outliving the overlay.
Release therefore happens on focus loss, on unticking, on leaving live preview, on editor
close, and first thing in the window's `Closed` handler - and `Release` is idempotent, so an
overlapping path cannot double-send. Note what this still is not: OS-level input aimed at
whatever window is focused, exactly like a keypress. The game process is never opened,
hooked, injected into, or written to.

The guide lines stay for the boundaries the game cannot draw at all - sidebar edge, vertical
start, bottom clearance - now measured against real pixels rather than simulated ones.

The simulated preview stays as the other mode rather than being deleted. It is the only
thing that works with no game to draw over, and per the player's correction the two are a
choice rather than a replacement. The consequence is the preview-parity risk the v0.3.x
entries are about, which is why both modes still build their cards from one
`SampleRoster` and one card template.

Three follow-ups from the same pass. The stock WPF scrollbar is a thin grey sliver against
this window's dark background and reads as a border, so a capped control area quietly hid
settings; it is now a 16 px bar that is always present rather than `Auto`, with a hint line
that disappears once the list is scrolled to the end.

The app icon is drawn from code rather than from a supplied image, deliberately: the item
icons lost their source screenshots and can no longer be regenerated, and this one can be
rebuilt by running `tools/Build-AppIcon.ps1`. It is the product rather than a symbol - three
health bars on the panel's plate, in the panel's own colours - and it owes nothing to Valve
artwork.

Two things that cost a build each. **GDI+ cannot decode PNG-compressed frames**, which is
how `System.Drawing.Icon` loads the tray icon, so an all-PNG `.ico` renders correctly in
Explorer and throws at runtime; frames up to 128px are now BMP/DIB and only 256 is PNG. And
**PowerShell unrolls a `byte[]` returned from a function** into an `Object[]` of boxed
bytes. `Length` still read correctly, so the directory entries were right while
`BinaryWriter.Write` bound to another overload and emitted one byte per frame: a 5 KB file
whose own header claimed 107 KB. Both are recorded in the script.

`alwaysShow` existed in config from v0.1.0 and had no way to reach it but a text editor.
It is now **Show HUD consistently**, and it sits outside `CopyUiFrom` next to
`exitWhenGameCloses` for the reason v0.6.0 recorded: Reset UI restores visual layout, and
neither of those is layout.

v0.6.2 made a duplicate launch exit silently, on the reasoning that it must not disturb the
running overlay. Correct about the overlay, wrong about the person: the running copy has no
window, so a silent exit is indistinguishable from the app failing to start. A duplicate now
says so and points at the tray icon, then closes. The active instance is still untouched -
the message is shown by the duplicate, before it exits, and the mutex is already released by
then.

Reversibility is the part worth stating explicitly: live preview mutates the *running*
configuration. The first push snapshots it, Cancel and any other close path restore that
snapshot, and Save marks the draft as committed so the close path keeps it instead. Without
that snapshot, "look at it live, then cancel" would silently keep the last slider position.

## Overlay HUD v0.6.5 - 2026-08-13: the team number cannot answer the question

Immortal holdout soldiers are on team 4, so hiding them looks like a one-line team filter.
It is not. v0.1.0's probe already caught mortal soldiers sitting on team 4 transiently, and
reading Finale Soldiers' source explains why there are two separate mechanisms doing it: the
`mortal_distance_suspend` dodge parks a mortal soldier on team 4 so the engine's bot-catchup
teleport cannot yank it, and the progression bypass does the same for a fraction of a second
while a real survivor uses a finale button. A team-4 filter would blink those soldiers out
of the HUD exactly as a team-2-only filter blinked them in.

Both addons run in the same server VM, so the exporter reads the identity Finale Soldiers
already maintains for itself instead of inferring one: `cf_soldier_bot` is that addon's own
identity boundary, `cf_soldier_following` marks a follower, and `cf_soldier_mortal` marks a
mortal soldier. `cf_soldier_distance_suspended` is folded into "mortal" deliberately —
during the dodge `cf_soldier_mortal` is false while the soldier is still logically a mortal
one, and without that fold a distant soldier would disappear from the panel and come back.

Reads are non-mutating on purpose. `GetScriptScope()` returns null for an entity with no
scope, which is the normal state for a plain survivor and for a soldier mid-spawn;
`ValidateScriptScope()` would create one, and the exporter's whole design contract is that
it changes nothing in the game.

### The sidebar boundary was an estimate, and a screenshot measured it

v0.2.1 derived the 36% width budget from a screenshot where the vanilla scoreboard "ended
around x=765", then v0.5.3 made that value an absolute boundary. A new 1920x1080 capture
puts the scoreboard's actual survivor rows at x=43 to x=722 — 37.6%, not 36%. The panel was
therefore doing exactly what it was told and still finishing 25 px inside the region it was
allowed to fill, which reads as a misaligned edge rather than as a margin.

The budget is now 0.376. A four-card roster is width-bound, so its fit pass grows from
1.321 to 1.384 and its right edge lands on 721.92 px — the measured boundary, to the pixel.
`MaxFitScale` did not need raising; 1.384 is still under the existing 1.4 ceiling. Taller
rosters stay height-bound and do not reach the edge, which is inherent to scaling the panel
uniformly rather than stretching cards.

The default vertical start moves to 59%, which hands the panel 442.8 px of height instead of
410.4 at 1080p. That is worth one more full-size card: the automatic one-column/two-column
boundary moves from 9/10 to 10/11.

### The vanilla-four skip

The vanilla-four skip also stopped being positional across the whole roster. It now applies
only to entries classified `survivor`, so a soldier can never be swallowed by it, and a
state file from an exporter older than v0.6.5 — where everything classifies as `survivor` —
reproduces the old behavior exactly.

## Overlay HUD v0.6.4 - 2026-08-12: WPF defaults did exactly what was reported

The editor had no custom slider template or mouse handler. WPF's default
`IsMoveToPointEnabled = false` therefore explained the click behavior exactly: a track
click issued a directional step instead of mapping the pointer to a value. All seven
sliders now opt into direct point movement explicitly.

The reset path copied default UI configuration and reloaded controls, but preview count is
intentionally not part of persistent configuration. `LoadControls` only replaced an
invalid preview value, so any valid value from 1–27 survived reset. Reset now assigns 6
while change events are suspended. An editor-level check was first observed red on both
facts (`allSlidersJumpToClick=False`, `resetPreviewCountIs6=False`) and now passes.

## Overlay HUD v0.6.3 - 2026-08-12: manual scale only needs to shrink

The manual UI-size slider allowed 1.40× even though values above the normal 1.00× size were
not useful in the calibrated sidebar. The range now expresses the actual user choice:
shrink from 1.00× when desired, or use normal size.

Changing the XAML limit alone would leave old `scale` values above 1.00 active until the
editor saved again. The live `BaseScale` calculation therefore clamps through the same
central policy. This is separate from the measured fit multiplier, which still enlarges a
short roster or shrinks overflow to use the available fixed sidebar safely.

## Overlay HUD v0.6.2 - 2026-08-12: singleton before side effects

Checking the process list after startup is race-prone: two simultaneous launches can both
observe no peer and then each create a tray icon and global keyboard hook. A named
per-session Windows mutex makes ownership atomic. It is acquired before `base.OnStartup`,
which is before WPF processes `StartupUri`; duplicates therefore create no main window or
input hook. The primary holds the mutex until `OnExit` and releases it deterministically.

The editor's game status uses a separate read-only process probe once per second. It reads
the same configurable process name as geometry/lifetime tracking but does not mutate their
state, so opening the editor before L4D2 cannot make automatic exit think a game was
previously observed.

## Overlay HUD v0.6.1 - 2026-08-12: two symptoms, two different mechanisms

The lifecycle report combined an unreadable control with a retained process. Inspection
after L4D2 closed found `OverlayHud.exe` still alive and the published config explicitly
contained `"exitWhenGameCloses": false`. That is the designed retain mode, not a missed
process transition, but the black checkbox label made its state hard to understand or
correct.

The default WPF checkbox content inherited an unsuitable foreground under the custom dark
window. Its label is now an explicit white `TextBlock`. The earlier lifecycle test covered
only `GameLifetimeState`; it could not prove the window caller actually closed. The check
now configures a real `MainWindow` with a guaranteed-absent process name, seeds an observed
game, invokes the actual tracker, and asserts that the window's `Closed` event fires.

This separated the ranked hypotheses: configuration/visibility caused the observed
behavior, while process lookup and the window shutdown path both pass. The current
published config is reset to automatic exit for the requested behavior.

## Overlay HUD v0.6.0 - 2026-08-12: closing requires process history

“No L4D2 process exists” has two meanings: the game closed, or the overlay was launched
first. Exiting on absence alone makes the latter workflow impossible. A small lifetime
state therefore remembers whether this overlay run has ever observed the configured game
process. Automatic exit becomes eligible only after that transition from present to absent.

Process existence is also distinct from `MainWindowHandle`. L4D2 can temporarily lack a
usable main window during startup or display-mode changes while its process remains alive.
The existing 250 ms tracker now returns both facts: process presence drives lifetime, while
the handle continues to drive geometry. An enumeration failure conservatively preserves
the observed-running state for that poll instead of treating an API race as a shutdown.

The option is deliberately outside `CopyUiFrom`: Reset UI remains limited to visual layout.
Save & Apply persists and updates the lifetime preference explicitly.

## Overlay HUD v0.5.4 - 2026-08-12: visible identity without breaking integration

The old name was duplicated in XAML, tray construction, and the runtime badge, while the
project file supplied no explicit Windows Product/Title metadata. One identity constant
now feeds the runtime surfaces, and build metadata carries the same exact user-facing
name.

`OverlayHud.exe`, assembly name, namespaces, resource identifiers, and config location do
not need to change to rename the product. Keeping them stable avoids breaking documented
launch commands, embedded resource loading, test assembly access, and any shortcuts the
user already created. The exporter retains its distinct “Overlay HUD Export” title because
it identifies the data-producing VPK rather than the external app.

## Overlay HUD v0.5.3 - 2026-08-12: inset belongs inside the boundary

The earlier preview-parity correction treated the 36% sidebar width as usable space after
the horizontal inset. That made the displayed vanilla region grow from `36%` to
`36% + inset`, even though an external overlay cannot move L4D2's actual scoreboard edge.

The 36% value now represents an absolute boundary from the window edge. Horizontal inset
moves the panel start inward, and the same amount is removed from its width budget. At
1920x1080 with the default 2% inset, the boundary stays at x=691.2 and usable panel width
becomes 652.8 px. The preview applies the identical relationship at its 720px scale.

The preview roster limit also moves from 16 to 27. Sample names and item states already
cycle safely, and the existing two-column measured fit contains all 27 cards without a
special rendering path.

## Overlay HUD v0.5.2 - 2026-08-12: constraints are not preferences

The editor exposed two implementation constraints as if they were meaningful user choices.
The sidebar width describes the observed vanilla scoreboard region, which this external
overlay cannot change. Automatic enlargement is the measured fit pass that makes a short
roster use available space without changing the established overflow boundary.

Both now live in one internal layout policy consumed by gameplay and preview. Their JSON
properties were removed as well as their sliders; otherwise a stale value from an older
config could continue changing behavior through an invisible setting. System.Text.Json
ignores those old property names on load, and the next Save & Apply omits them.

## Overlay HUD v0.5.1 - 2026-08-12: the editor needs a narrow focus exception

The initial shortcut correctly required L4D2 foreground, but opening the editor transfers
foreground focus away from L4D2. Applying that same gate to the second press makes a toggle
impossible. The gate now accepts either L4D2 or this process's active settings window; it
does not accept an inactive editor hidden behind another application. The active flag is
updated by WPF activation events and read synchronously by the hook.

Closing from the chord calls the same window close path as the Cancel button. The editor's
draft remains private until Save & Apply, so the toggle cannot accidentally commit slider
changes.

## Overlay HUD v0.5.0 - 2026-08-12: suppress exactly one side of Tab+Insert

Opening the editor from the tray is disruptive in a fullscreen game, but rebinding or
injecting game input would violate the overlay's separation from L4D2. The existing
low-level watcher is therefore extended with a narrow chord state machine. Tab remains an
observe-only key and always reaches the game. Insert is consumed only when its key-down
begins while Tab is already held and L4D2 is foreground.

Suppression stays latched through key repeat and the matching Insert key-up. This matters
when the player releases Tab first: deciding each message only from the current modifier
state would forward an orphaned Insert release after swallowing its press. A pure state
machine now defines and tests that entire contract without synthesizing input into the
running game. `editorKey = 0` disables the chord; otherwise it uses the existing
configurable `holdKey` as its modifier.

## Overlay HUD v0.4.3 - 2026-08-12: larger image, same slot

Icon readability increased without changing layout geometry. The mask element grows from
22x18 to 26x20, while its containing item slot remains 30x22 plus the existing two-pixel
inter-slot margin. A rendered-template regression check verifies both dimensions so later
visual tuning cannot silently widen the survivor card or move the column breakpoint.

## Overlay HUD v0.4.2 - 2026-08-12: inventory order belongs to the card model

The item data was already correctly classified into throwable, kit/ammo, and
pills/adrenaline categories, but the presentation template displayed kit, pills, then
throwable. Rather than merely swapping three presenters in XAML, the card model now exposes
one ordered `ItemSlots` sequence: throwable, kit/ammo, pills/adrenaline. Both live and
preview templates bind that sequence, and an automated check locks the vanilla order.

## Overlay HUD v0.4.1 - 2026-08-12: exact meant using the supplied pixels

The v0.4.0 implementation interpreted the screenshots and hand-authored new vector paths.
Although the item identities were correct and the style was monochrome, the shapes were
approximations. That violated the player's requirement that the icons look exactly like the
screenshots.

The supplied files are now the authoritative sources. Semantic copies live under
`workshop assets/item-icon-references`, and `tools/Build-ItemIconMasks.ps1` applies a fixed
luminance threshold of 48 without cropping, rotating, resampling, or tracing. It preserves
the source canvas and every surviving silhouette pixel, replacing only color/alpha. The
generated masks are embedded resources rather than loose runtime files, so published-app
deployment remains self-contained.

## Overlay HUD v0.4.0 - 2026-08-12: item silhouettes instead of raster thumbnails

The old three-letter chips were functional but slower to scan than L4D2's familiar item
silhouettes. Nine supplied HUD references identified bile/biohazard, pipe bomb, Molotov,
medkit, pills, defibrillator, explosive ammo, incendiary ammo, and adrenaline. The shapes
were recreated as native WPF geometry instead of copying the small reference rasters.

Vector geometry stays crisp through the overlay's automatic enlargement and shrink pass
and is shared automatically between live cards and the customization preview. Per the
player's correction, reference-image gradients and slight color casts are deliberately not
preserved: every occupied slot is exactly white on black. Slot dimensions remain unchanged
so the icon feature cannot reintroduce the recently fixed sidebar overflow.

## Overlay HUD v0.3.4 - 2026-08-12: preview width excluded its own inset

The gameplay screenshot confirmed the v0.3.3 panel correctly contained six full-size
cards and resized larger rosters. The editor preview still looked wrong: its dark simulated
sidebar stopped at x=259.2 while the preview panel started at the configured 14.4 px inset
and received 259.2 px of usable width, ending at x=273.6. The preview background described
an absolute edge while the live `sidebarWidth` setting describes usable panel width after
positioning.

The simulated region now ends at `left inset + usable width`, matching the live overlay's
geometry. A second editor check captured the merged control text: WPF's default
`DockPanel.LastChildFill` ignored the final value's right dock, producing a zero-pixel gap.
Control headers now disable that behavior and reserve 12 DIPs between title and value.

## Overlay HUD v0.3.3 - 2026-08-12: the second column inherited first-column geometry

The v0.3.2 screenshot confirmed that ten cards now wrapped vertically, but the resulting
two-column panel extended to about x=985 instead of stopping near the sidebar edge around
x=730. The layout regression check reproduced that exact width: natural size 966 px,
effective scale 0.98, rendered right edge 985.41 px.

Immediately after assigning the two-column source, WPF's generated visual tree still
reported the cached one-column desired size of 472x400.63. The fit pass therefore used the
old tall/narrow geometry and selected 0.98. Invalidating only the parent content element
did not reach the cached generated item containers. Recursively invalidating the small HUD
visual tree before measuring exposed the correct 966x228.63 two-column size and selected
0.716, placing the right edge exactly at the 729.6 px sidebar boundary.

## Overlay HUD v0.3.2 - 2026-08-12: ActualHeight described the clip, not the content

The in-game screenshot showed 11 extra survivors remaining in one column with the last
card cut by the bottom edge. The deterministic WPF repro measured the panel at exactly
410.4 px — precisely the available height from y=669.6 to y=1080 — and therefore selected
one column. WPF had already arranged the panel into that bounded slot, so `ActualHeight`
reported the clip boundary rather than the taller content inside it. Comparing that value
to the same 410.4 px budget was self-fulfilling and could never report overflow.

The layout now asks the panel child for its desired size under an unconstrained measure,
then adds border and padding. This natural size drives both the column decision and final
fit pass. The original 11-card scenario changed from one clipped column to two columns
ending at y=936.23. Boundary checks retain one column for nine cards, switch at ten, and
fit 16 cards into two columns at 1280x720.

## Overlay HUD v0.3.1 - 2026-08-12: the reserved band is absent during Tab

The 12% bottom reserve was based on the normal in-game survivor HUD, but this overlay is
only shown while Tab is held. In the player's HUD setup, that vanilla lower HUD disappears
with the scoreboard and returns after Tab is released. Reserving its former area therefore
forced an early second column to avoid content that was not present at the same time.

The default reserve is now zero, so measured layout uses every pixel from the configured
vertical start to the bottom of the game window before splitting. The setting remains in
the editor as an optional 0-25% compatibility control for custom HUDs with persistent
bottom-left content.

## Overlay HUD v0.3.0 - 2026-08-12: preview and live HUD share one visual source

A customization preview is only useful if it cannot quietly diverge from the real HUD.
The survivor card and item-chip templates therefore moved into one application-level WPF
resource dictionary consumed by both the transparent overlay and the settings preview.
The preview also follows the live measured one-column-first rule, including a manual
`cardsPerColumn` override when an existing config uses one.

The settings window edits a cloned configuration. **Cancel** discards it; **Save & Apply**
first writes a temporary JSON file and atomically replaces `config.json`, then copies only
UI fields into the running overlay. **Reset UI** also copies only UI defaults, so transport
paths, the game process, hotkey, foreground behavior, and debug settings cannot be erased
by a layout reset.

The editor process and window title passed a startup smoke test. Windows UI inspection was
not approved in this session, so exact pixels and interactive save/cancel behavior remain
explicitly unverified rather than being inferred from a successful build.

## Overlay HUD v0.2.2 - 2026-08-12: estimates left usable height empty

The v0.2.1 screenshot showed four extra cards ending around y=860 while safe sidebar space
continued to about y=950. Nevertheless, the fifth card triggered two columns because the
decision used fixed `CardHeight` and `HeaderHeight` estimates rather than the WPF panel's
actual rendered height. Those conservative estimates contradicted what was visibly on
screen.

Automatic layout now temporarily measures all cards in one full-size column. It retains
that layout if the measured height fits the scoreboard-to-bottom-HUD band; only a real
overflow switches to two balanced columns. Width fitting remains a later independent pass,
so a one-column roster stays as large as the available width and height permit. Unlike the
old shrink-only pass, it can enlarge a short roster up to 1.4x; five or six cards are
limited by remaining height while shorter rosters can use more width. The bottom reserve
was calibrated from the same screenshot from 14% to 12%.

## Overlay HUD v0.2.1 - 2026-08-12: half-screen was not the sidebar

The first two-column constraint used `sidebarWidth = 0.5`, treating the left half of the
screen as the available sidebar. A 1920x1080 in-game screenshot disproved that assumption:
the vanilla dark scoreboard region ended around x=765, while the overlay began around
x=38 and extended to x=998. It was already split 3+2, but two full-width cards still made
the whole panel too wide.

The width budget is now 36% of the game window. At 1920x1080 that is 691 px; after the 2%
left inset the panel ends near x=729, inside the observed sidebar boundary. The existing
measured fit pass scales the complete two-column panel to that width, preserving its
internal proportions.

## Exporter addon v0.2.0 - 2026-08-12: shared release number

The overlay app and exporter previously advanced independently to v0.2.0 and v0.1.2,
which made an otherwise healthy installation look mismatched. The exporter is repackaged
as v0.2.0 with no logic or transport-format change. From this release onward, both halves
advance under the same release number even when a change affects only one component.

## Overlay app v0.2.0 - 2026-08-12: responsive extra-survivor sidebar

### Responsive scoreboard sidebar

The panel was moved under the top-left scoreboard using window-relative offsets and a
scale derived from game-window height. Geometry is followed by configured process even
while the game is not focused; the foreground check controls visibility only. Otherwise a
resolution change made while alt-tabbed leaves the overlay at its old bounds when focus
returns.

Height below the scoreboard is not all usable space. At 1080p, eight cards in one column
reach into L4D2's built-in bottom-left survivor HUD. A configurable bottom fraction is now
reserved, so automatic capacity wraps eight survivors into two columns of four at 1080p,
720p, and 540p. A measured fit pass remains as a backstop for horizontal overflow.

Direct pixel verification was not viable during this pass: `PrintWindow` returned black
for the transparent layered WPF window because its alpha is not composited into that
capture, while desktop capture remained covered by the game. This is recorded as a dead
end rather than evidence that rendering is correct; the responsive placement still needs
visual in-game confirmation.

### Menu/lobby running indicator

The overlay app version now comes from the project assembly version and is displayed in a
separate top-right badge whenever L4D2 is focused but the state reader is stale. Fresh
sequence updates hide it during active play; foreground loss hides it immediately. This
uses the transport state the app already owns and adds no game-process access.

The file transport cannot distinguish the main menu, lobby, loading screen, and pause
menu: all four stop advancing exporter sequence numbers. The badge therefore represents
"L4D2 focused, exporter inactive," and may also appear after two seconds in the pause
menu. Exact UI-screen detection remains unverified and is deliberately not guessed.

### Extra survivors only

The overlay now preserves the exporter roster order and skips its first four entries,
because L4D2 already renders those survivor slots. The normal Tab panel is absent when no
fifth survivor exists; `alwaysShow` still exposes the empty/diagnostic panel for setup.

This is project-observed behavior, not an engine ordering guarantee. In the current live
Finale Soldiers state, positions 1–4 were DrStr4nge, Ellis, Coach, and Rochelle, while
position 5 was Pvt. Chambers. Other addons that insert their extra survivors ahead of the
vanilla four may need an explicit identity field in the transport rather than positional
filtering.

### Two-column bound

Height-derived capacity alone can create an arbitrary number of horizontal columns as the
roster grows. The panel now calculates how many columns the natural height would require,
caps that count at two, and redistributes the cards evenly. The existing measured fit pass
then shrinks both columns together into the reserved height and a configurable half-screen
sidebar width.

`minScale` is now a multiplier of the resolution-derived scale, rather than an absolute
WPF scale. This matters below 1080p: an absolute floor of 0.45 prevents the same large
roster from shrinking proportionally at 720p or 540p.

## Overlay app v0.1.0 - 2026-08-12: two bugs that hid each other

The panel came up transparent, click-through and on top on the first run, and then sat
there reading `SURVIVORS 0 / STARTING UP` while a perfectly good state file with eight
survivors was on disk.

### The visible symptom was not the bug

`STARTING UP` is the reader's *initial* status string. Seeing it after four seconds means
the UI had never re-rendered — not that the reader had failed. Adding a poll counter to the
status line turned one ambiguous symptom into a fact: `POLLS 0`.

That fact was still misleading. It looked like a dead timer, and a plausible mechanism was
right there: both `DispatcherTimer`s were created at `DispatcherPriority.Background`, and
`AllowsTransparency="True"` forces WPF onto the software render path, which posts
Render-priority work continuously. Background sits below Render, so starvation was a real
possibility. Raising both timers to `Normal` changed nothing. **The plausible mechanism was
not the actual one** — worth remembering, since it survived a full rebuild before being
ruled out.

### Root cause 1: StringToFile writes a NUL terminator

Reading the file's raw bytes ended the argument. The last eight bytes:

```text
66 6C 65 22 7D 5D 7D 00
```

`}]}` followed by `0x00`. VScript's `StringToFile` NUL-terminates what it writes.
`System.Text.Json` does not accept NUL as trailing whitespace, so **every** parse threw
`JsonException`. Fixed by trimming NUL and whitespace before parsing.

This is a general fact about the transport, not a quirk of this file: anything reading a
`StringToFile` output has to handle the terminator.

### Root cause 2: a failed parse never reached the UI

`Poll()` returned early on `JsonException` without invoking `Updated`, on the reasoning
that a torn read is normal and the last good state should be kept. True — but it also meant
a *permanent* parse failure produced no UI update at all, so the panel froze on its startup
text and the poll counter never advanced past the value it was first drawn with.

A transient failure and a permanent one were indistinguishable from outside, which is what
sent the investigation to the timer in the first place.

Fixed in two parts: `Updated` now fires from a `finally` so every exit path reaches the UI,
and consecutive parse failures are counted and surfaced with the parser's own message
rather than silently swallowed.

**Lesson worth keeping:** "keep the last good value on failure" is correct for the *data*
and wrong for the *status*. The status has to keep moving or a stuck reader looks exactly
like a stuck clock.

### Also fixed while in there

- The locator picked the first L4D2 install it found. This machine has two - a stub on C:
  and the real one on E: - so it could watch a path that would never receive a file. An
  install that already has a state file now wins over one that merely exists.
- Header and status text ran together with no gap in the panel header.

## Exporter addon v0.1.2 - 2026-08-12: bot flag was always null

`bot` came back `null` for all eight survivors, and none of `DetectBotMode`'s three
outcome lines appeared in the log — so it had never run, not failed.

`Boot()` calls it at chapter load, and at that moment `Entities.FindByClassname(null,
"player")` returns nothing because no player entity exists yet. The guard `if (anyone !=
null)` then skipped detection permanently, and `botMode` stayed 0 for the whole session.

Moved to the first export tick that actually finds a survivor. The empty catch blocks in
the detection routes now log, since with players present a failure is worth seeing.

Same shape as the bug in the probe's own design that made `BOOT_WAIT` necessary: **the
roster does not exist when the script loads.** Anything that inspects players has to run
after players exist, not at load.

## Exporter addon v0.1.1 - 2026-08-12: a BOM stopped the whole addon

v0.1.0 produced exactly two console lines and no state file:

```text
[OVLHUD] FATAL: could not include overlay_hud_export - Failed to include script "overlay_hud_export"
```

**Root cause.** `overlay_hud_export.nut` was written correctly, then patched afterwards
with PowerShell `Set-Content -Encoding utf8`. In Windows PowerShell 5.1 that writes a
UTF-8 **BOM** (`EF BB BF`). Squirrel cannot compile a file that starts with one, so
`IncludeScript` failed and the entire addon was inert. The probe script had no BOM because
it was never touched by that command, which is why v0.1.0-probe1 loaded and v0.1.0 did not.

**Fix.** File rewritten without a BOM. All packed files are now BOM-checked before the VPK
is built.

**Worth keeping.** The failure was diagnosable in one line only because the entry point
wraps `IncludeScript` in try/catch and prints the exception. A bare `IncludeScript` would
have produced a completely silent addon with no clue which of a dozen things was wrong.
That try/catch cost two lines and saved a debugging session.

Note that `Failed to include script` is also what a Squirrel **syntax error** produces —
the message does not distinguish "file missing", "file unreadable" and "file will not
compile". BOM was confirmed by reading the file's first three bytes, not inferred from the
message.

## Exporter addon v0.1.0 - 2026-08-12: what the probe settled, and two bugs it prevented

Probe log: 3615 `[OVLHUD]` lines, five full runs, eight survivors (four originals plus
Blake, Nguyen, Chambers, Foster).

### Settled

- **`StringToFile` works and writes to `left4dead2/ems/`.** Confirmed by finding the file
  on disk, not by inference. The `console.log` tailing fallback is not needed and was
  dropped from the design.
- **`EntFire` scheduling works; `logic_timer` does not.** Five of five `EntFire` runs
  fired. The spawned `logic_timer` with `ConnectOutput("OnTimer", ...)` produced **zero**
  heartbeats. That is why two independent timer paths were probed in one build — the
  export loop would otherwise have been built on the dead one and looked like a silent
  no-op with no way to tell which half was broken.
- Present and readable: `GetPlayerName`, `GetPlayerUserId`, `GetHealth`, `GetMaxHealth`,
  `IsIncapacitated`, `IsDying`, `IsHangingFromLedge`, `IsDead`, `GetActiveWeapon`,
  `m_healthBuffer`, `m_healthBufferTime`, `m_currentReviveCount`, `m_bIsOnThirdStrike`,
  `m_survivorCharacter`, `m_iTeamNum`.
- **`IsBot()` and `IsAlive()` do not exist** on this build. `IsDead()` does. Bot detection
  falls back to `IsPlayerABot()`, then `m_fFlags & FL_FAKECLIENT`, then reports `null` —
  neither fallback is confirmed yet, and the field is cosmetic, so it fails soft.
- Only classname `player` matters. `survivor_bot` returned 0 across every run.

### Two bugs the probe caught before they shipped

**Team 4.** Run 4 found entities 5, 6 and 7 — three of the four soldiers — on
`m_iTeamNum=4`, and back on team 2 by run 5. Team 4 is `L4D1_Survivor`; Finale Soldiers
uses it transiently. A team-2-only filter, which is the obvious way to write this, would
have made soldiers disappear from the HUD at random and looked like a race condition in
the overlay app.

**`m_hMyWeapons` is not slot-indexed.** The array is 56 long but compacted, and position
carries no meaning. Foster: kit at 0, sniper at 1, pills at 2. The host: pistol 0, pipe
bomb 1, kit 2, AK 3, adrenaline 4. Reading "slot 3 = the medkit slot" from the community
slot map would have produced item icons that were wrong per player and inconsistent
between them. Classification is by classname.

**`m_survivorCharacter` is useless for soldiers.** All four spawned soldiers report `8`.
Identity comes from `uid`, display from `GetPlayerName()`, which returned "Cpl. Blake",
"Cpl. Nguyen", "Pvt. Chambers", "Cpl. Foster" correctly.

### Known soft spots in v0.1.0

- Temp health decay uses `pain_pills_decay_rate` and a linear decay from
  `m_healthBufferTime`. `Convars.GetFloat` was never probed, so the 0.34 fallback may be
  what actually runs. Needs a side-by-side comparison against the real HUD.
- The file is rewritten in place with no atomic swap. A reader can catch a torn write.
  Pushed to the app, which keeps the last good parse — VScript has no rename primitive.
- 5 Hz was chosen without measuring write cost. If it hitches, the rate drops.

**Verification**: source-level only. Not yet live-tested.

## Exporter addon v0.1.0-probe1 - 2026-08-12: why the first build is a probe

### The design

Overlay drawn by an external Windows app, fed by a VScript addon that writes survivor
state to a file. Decided against every alternative that touches the game process — no
DirectX hooking, no injection, no memory reading. The overlay is a separate program that
reads a file and draws a window.

Tab detection lives in the **app**, not the addon. Two reasons: no round-trip latency on
show/hide, and VScript cannot read a player's existing key binds, so a "rebind Tab,
restore later" design can only guess defaults and would permanently corrupt the player's
config. The app hooks the keyboard for itself and gates rendering on L4D2 being the
foreground window.

Borderless windowed is a hard requirement. Exclusive fullscreen takes the display and no
external window can draw over it.

### Why nothing was implemented yet

The exporter needs facts that were not established:

- **Where, and whether, `StringToFile` writes.** The call is engine-documented, but the
  `left4dead2/ems/` write path is widely repeated community lore that was never confirmed
  by observing a written file. Whether a VPK-loaded addon may write at all is also open.
  Fallback if it cannot: the addon prints state to console and the app tails `console.log`
  (`-condebug`), which is engine-solid but noisier and unbounded on disk.
- **How to read inventory.** `m_hMyWeapons` as an entity array is the likely route. The
  L4D2 slot map (0 primary, 1 secondary, 2 throwable, 3 kit/defib, 4 pills/adrenaline) is
  folklore until dumped from a live player.
- **Temp health.** `m_healthBuffer` + `m_healthBufferTime` with a decay rate from
  `pain_pills_decay_rate` is the shape of it. Neither prop was confirmed to exist here.
- **Downed state.** `IsIncapacitated()` is attested. Black-and-white / third-strike state
  is not — `m_currentReviveCount` and `m_bIsOnThirdStrike` are both guesses in this build.
- **Which timer mechanism to build the export loop on.** Delayed `EntFire` and a spawned
  `logic_timer` are both plausible; neither was verified in this install.

Guessing any of these produces an addon that fails silently, which is the worst possible
outcome to debug. So the first build asks the questions instead.

### Probe design notes

- Every probe is wrapped in try/catch and **every catch prints**. A silent catch turns
  "this API does not exist" into "nothing happened", which is exactly the failure mode
  this build exists to avoid.
- `Run()` reschedules itself outside its own try/catch, because an uncaught throw in a
  self-rescheduling `EntFire` loop kills the loop permanently.
- The two timer paths log different tags and different content — full dump vs one-line
  heartbeat — so they can never be mistaken for each other in the log.
- Entry point is `mapspawn_addon.nut`, the additive Valve entry point, so other addons
  shipping their own copy are unaffected. It runs once per chapter, so everything reached
  from it is written to tolerate re-execution — the `logic_timer` is killed by targetname
  before a new one is spawned.
- No global hooks are claimed. In particular `InterceptChat` is untouched: claiming it at
  load prevents VSLib from installing its chat system at all, which silently kills chat
  commands in every VSLib addon.
- No game-event callbacks are registered either. Root-table event registration from
  `mapspawn_addon` was not verified, and the probe does not need it — scheduling alone
  avoids that entire risk class.

**Verification**: source-level only. Not yet live-tested.
