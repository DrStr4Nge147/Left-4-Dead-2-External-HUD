# Dev log

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
thing that works with no game to draw over, and per the user's correction the two are a
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
approximations. That violated the user's requirement that the icons look exactly like the
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
user's correction, reference-image gradients and slight color casts are deliberately not
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
only shown while Tab is held. In the user's HUD setup, that vanilla lower HUD disappears
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
restore later" design can only guess defaults and would permanently corrupt the user's
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
