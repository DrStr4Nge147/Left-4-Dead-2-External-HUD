# Weapon icons

The PNGs here are the weapon HUD's art. They are embedded into the app on the next build —
the `.csproj` globs this folder as `OverlayHud.Assets.WeaponIcons.<id>.png` — and nothing
else needs editing to add, replace, or remove one.

## Where they came from

Left 4 Dead 2's own per-weapon HUD icons, one texture each, from the update pak:

```text
E:\SteamLibrary\steamapps\common\Left 4 Dead 2\update\pak01_dir.vpk
    materials/vgui/hud/icon_<weapon id>.vtf
```

The game names those textures for the weapon, which is why the ids here match exactly and
why there is no guesswork about which rifle is which. (The three `iconsheet*.vtf` atlases
in the base pak carry most of the same art, but as one packed sheet with no names.)

`pistol.png` and `pistol_dual.png` are the exception: `icon_pistol.vtf` holds BOTH of them
side by side in one texture — the pair on the left, a single pistol on the right, with a
gap between. Each was cut out separately. Shipping the whole texture as the dual icon draws
a pair *and* a spare pistol, which is exactly what it looked like. The exporter reports
which one is being carried; see `m_isDualWielding` in `docs/STATE_FORMAT.md`.

Each was trimmed to its alpha bounds and flattened to flat white through that alpha, which
is what makes them match the item icons — several are grey or tinted in the game texture,
and a grey weapon on the HUD reads as disabled.

These are Valve's assets, used by an add-on for the game they ship with. Keep that in mind
before reusing them anywhere else.

## What is here, and what is not

All 33 weapons the game ships an icon for: every gun, and every melee weapon except the
riot shield. Two ids have no game art and fall back to a drawn silhouette for their family,
from `overlay-app/OverlayHud/WeaponIcons.xaml`:

`riotshield` · the generic `melee` (what an install reports when it cannot say which melee
weapon is being carried)

Anything an addon adds falls back the same way, so a slot is never empty.

## Adding or replacing one

The filename is the exporter's weapon id, exactly. Those ids come from
`overlay_hud_export.nut` — `PRIMARY_WEAPONS`, `SECONDARY_WEAPONS`, and, for melee, the
weapon's own map-set script name.

### Primary

`smg` · `smg_silenced` · `smg_mp5` · `pumpshotgun` · `shotgun_chrome` · `autoshotgun` ·
`shotgun_spas` · `rifle` · `rifle_ak47` · `rifle_desert` · `rifle_sg552` · `rifle_m60` ·
`hunting_rifle` · `sniper_military` · `sniper_scout` · `sniper_awp` · `grenade_launcher`

### Secondary

`pistol` · `pistol_dual` · `pistol_magnum` · `chainsaw`

### Melee

`fireaxe` · `frying_pan` · `machete` · `baseball_bat` · `cricket_bat` · `crowbar` ·
`electric_guitar` · `golfclub` · `katana` · `knife` · `tonfa` · `pitchfork` · `shovel` ·
`riotshield` — plus `melee.png` as the catch-all for an install that will not report which
melee weapon it is.

## Format

Flat white silhouette on transparency, no gradients or colored tint, trimmed to the
weapon's own bounds with no padding.

**Size carries meaning.** Every icon is scaled by one shared factor, so its pixel width
decides how large it draws next to the others — the Magnum is small on the HUD because it
is small in the game's own texture. Author a new icon at the same scale as these (an M60 is
256px wide, a rifle around 190px, a Magnum 63px) rather than filling the canvas, or it will
draw out of proportion to everything beside it.
