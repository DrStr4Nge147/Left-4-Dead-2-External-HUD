// Overlay HUD Export - live survivor state exporter.
//
// Writes every survivor currently in the session to a JSON file that an external overlay
// application reads. Read-only with respect to the game: it reads state and writes one
// file. No gameplay is touched.
//
// Output: left4dead2/ems/overlay_hud/state.json   (ems write path verified by v0.1.0-probe1)

::OvlHud <- {}

::OvlHud.VERSION   <- "2.1.3"

// Both files live in an ems subfolder rather than loose at the top of ems/, which is what
// every other addon on a busy install does. StringToFile takes a relative subpath and the
// engine creates the folder on the first write - the same mechanism Finale Soldiers uses
// for ems/finale_soldier/. The folder carries the name, so the files do not repeat it.
::OvlHud.OUT_FILE  <- "overlay_hud/state.json"

// Reverse channel. The overlay app writes "<want> <seq>" here; this script reads it on the
// same tick that writes state out. See docs/STATE_FORMAT.md.
::OvlHud.CMD_FILE  <- "overlay_hud/cmd.txt"

// Seconds without a new command seq before a held scoreboard is released on its own. The
// app rewrites the file about four times a second while it holds, so anything past this
// means the app closed, crashed, or was killed - and a scoreboard latched open by a
// process that no longer exists is not something the player can undo from the game.
::OvlHud.CMD_TIMEOUT <- 2.0

// Export rate. 0.2 = 5 Hz. The overlay only needs to look current, not frame-perfect,
// and every tick is a file write.
::OvlHud.INTERVAL  <- 0.2

// The ammunition channel: the host player's own magazine and reserve, and nothing else.
//
// It exists because 5 Hz cannot count rounds. An Uzi fires about twelve a second, so the
// roster tick drops two or three at a time and the counter jumps rather than counts down.
// Raising the roster rate to match would multiply the cost of the whole export - every
// survivor, every field, every tick - to fix one number belonging to one player. This
// writes about fifteen bytes and reads three properties off one entity.
::OvlHud.AMMO_FILE     <- "overlay_hud/ammo.txt"
::OvlHud.AMMO_INTERVAL <- 0.05
::OvlHud.ammoSeq       <- 0

// Seconds after chapter load before the first export. The roster is not settled at t=0.
::OvlHud.BOOT_WAIT <- 5.0

// The same wait after a same-map round restart, where the map is already loaded and the
// survivors are put back in a fraction of the time a cold load takes. Five seconds there
// is five seconds of blank panel for no gain - the settle guard below covers what the long
// wait was actually protecting against.
::OvlHud.REARM_WAIT <- 1.0

// Seconds after any boot during which an EMPTY roster is not written out. An export that
// honestly reports zero survivors mid-respawn is indistinguishable, in the app, from the
// stale-file false zero this whole restart path exists to fix - and a blank panel for one
// more tick is better than a confident wrong one. After this window a zero is written,
// because by then it is real.
::OvlHud.SETTLE    <- 4.0

// Time() the current generation booted at, for the settle guard.
::OvlHud.bootTime  <- 0.0

// True until the first export of this script load. Distinguishes a cold chapter load, which
// gets BOOT_WAIT, from a round-restart re-entry into a live VM, which gets REARM_WAIT.
::OvlHud.coldLoad  <- true

::OvlHud.DEBUG     <- false

// A survivor is anything on these teams. Team 4 is L4D1_Survivor: Finale Soldiers moves
// its bots there transiently, and a team-2-only filter makes them blink out of the HUD.
::OvlHud.SURVIVOR_TEAMS <- [2, 4]

// Bumped every time this script loads. A tick belonging to an older generation exits,
// so a re-executed entry point cannot leave two loops running.
::OvlHud.gen <- 0

::OvlHud.seq       <- 0
::OvlHud.cmdSeq    <- -1     // last command seq seen from the app
::OvlHud.cmdSeen   <- 0.0    // Time() that seq last changed
::OvlHud.scores    <- false  // is the scoreboard currently held open by us
::OvlHud.consoleRoute <- -1  // -1 unprobed, 1 SendToConsole, 0 no route
::OvlHud.botMode   <- 0      // 0 unknown, 1 IsPlayerABot(), 2 m_fFlags & FL_FAKECLIENT
::OvlHud.botProbed <- false
::OvlHud.clipMode  <- -1     // -1 unprobed, 1 Clip1(), 2 m_iClip1, 0 no route
::OvlHud.ammoMode  <- -1     // -1 unprobed, 1 m_iAmmo[m_iPrimaryAmmoType], 0 no route
::OvlHud.meleeMode <- -1     // -1 unprobed, 1 m_strMapSetScriptName, 0 no route
::OvlHud.dualMode  <- -1     // -1 unprobed, 1 m_isDualWielding, 0 falling back to clip size
::OvlHud.upgMode   <- -1     // -1 unprobed, 1 m_upgradeBitVec + loaded count, 0 no route
::OvlHud.hudMode   <- -1     // -1 unprobed, 1 m_Local.m_iHideHUD, 0 no route
::OvlHud.viewMode  <- -1     // -1 unprobed, 1 m_hViewEntity, 0 no route
::OvlHud.frozenMode <- -1    // -1 unprobed, 1 m_fFlags & FL_FROZEN, 0 no route
::OvlHud.wonMode   <- -1     // -1 unprobed, 1 Director.IsFinaleWon(), 0 no route
::OvlHud.cine      <- 0      // 1 while the game is running a cinematic over the player
::OvlHud.lastHudBits <- -2   // last logged raw reads; -2 is "nothing logged yet"
::OvlHud.lastViewCam <- -2
::OvlHud.lastFrozen  <- -2
::OvlHud.lastWon     <- -2
::OvlHud.lastProbe   <- ""   // last diagnostic probe line, logged only when it changes
::OvlHud.hostProbeWarned <- false
::OvlHud.ammoWarned <- false
::OvlHud.localUid <- -1      // cached listen-server host userid; -1 when unavailable
::OvlHud.decayRate <- 0.34   // overwritten from pain_pills_decay_rate at load if readable

::OvlHud.FL_FAKECLIENT <- 256

::OvlHud.Log <- function (msg)
{
	printl("[OVLHUD] " + msg)
}

// ---------------------------------------------------------------------------
// item classification
//
// m_hMyWeapons is a compacted list, not a slot-indexed array - the index a weapon sits
// at carries no meaning. Classify by classname instead.
// ---------------------------------------------------------------------------

::OvlHud.KIT_ITEMS <- {
	weapon_first_aid_kit          = "medkit",
	weapon_defibrillator          = "defib",
	weapon_upgradepack_explosive  = "explosive_ammo",
	weapon_upgradepack_incendiary = "incendiary_ammo"
}

::OvlHud.PILL_ITEMS <- {
	weapon_pain_pills  = "pills",
	weapon_adrenaline  = "adrenaline"
}

::OvlHud.THROW_ITEMS <- {
	weapon_molotov    = "molotov",
	weapon_pipe_bomb  = "pipebomb",
	weapon_vomitjar   = "bile"
}

// Slot 0. Every tier-1 and tier-2 long gun, plus the two uncommon-tier weapons that also
// occupy the primary slot. Classification is by classname for the same reason the item
// tables are: m_hMyWeapons is compacted, so the index a weapon sits at means nothing.
//
// A custom primary from another addon is not in this table and exports as no primary at
// all, which is the honest answer - the alternative is calling an unknown weapon "primary"
// on the strength of it having a clip, and a deployable or a script prop would qualify.
// The overlay does accept an id it has never seen and draws its humanised name, so adding
// one here is the only step needed to support it.
::OvlHud.PRIMARY_WEAPONS <- {
	weapon_smg                = "smg",
	weapon_smg_silenced       = "smg_silenced",
	weapon_smg_mp5            = "smg_mp5",
	weapon_pumpshotgun        = "pumpshotgun",
	weapon_shotgun_chrome     = "shotgun_chrome",
	weapon_autoshotgun        = "autoshotgun",
	weapon_shotgun_spas       = "shotgun_spas",
	weapon_rifle              = "rifle",
	weapon_rifle_ak47         = "rifle_ak47",
	weapon_rifle_desert       = "rifle_desert",
	weapon_rifle_sg552        = "rifle_sg552",
	weapon_rifle_m60          = "rifle_m60",
	weapon_hunting_rifle      = "hunting_rifle",
	weapon_sniper_military    = "sniper_military",
	weapon_sniper_scout       = "sniper_scout",
	weapon_sniper_awp         = "sniper_awp",
	weapon_grenade_launcher   = "grenade_launcher"
}

// Slot 1. Melee is deliberately absent: every melee weapon shares the classname
// weapon_melee and is told apart by its map-set script name, which MeleeId reads. So is
// the second pistol: a pair is still weapon_pistol, and IsDualPistol tells them apart.
::OvlHud.SECONDARY_WEAPONS <- {
	weapon_pistol         = "pistol",
	weapon_pistol_magnum  = "pistol_magnum",
	weapon_chainsaw       = "chainsaw"
}

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------

::OvlHud.JsonEscape <- function (str)
{
	if (str == null) { return "" }

	local out = ""

	foreach (ch in str)
	{
		if      (ch == '"')  { out += "\\\"" }
		else if (ch == '\\') { out += "\\\\" }
		else if (ch == '\n') { out += " " }
		else if (ch == '\r') { out += "" }
		else if (ch == '\t') { out += " " }
		else if (ch < 32)    { out += "" }
		else                 { out += ch.tochar() }
	}

	return out
}

::OvlHud.IsSurvivorTeam <- function (team)
{
	foreach (t in this.SURVIVOR_TEAMS)
	{
		if (t == team) { return true }
	}

	return false
}

// Neither IsBot() nor IsAlive() exist on this build - established by v0.1.0-probe1.
// Work out once which bot test this install supports, then reuse the answer.
//
// This runs on the first export tick that finds a survivor, NOT at load: at chapter load
// no player entity exists yet, so a load-time probe silently detects nothing and every
// survivor reports bot=null forever.
::OvlHud.DetectBotMode <- function (p)
{
	this.botProbed = true

	try
	{
		IsPlayerABot(p)
		this.botMode = 1
		this.Log("bot detection: IsPlayerABot()")
		return
	}
	catch (e) { this.Log("bot detection: IsPlayerABot unavailable - " + e) }

	try
	{
		NetProps.GetPropInt(p, "m_fFlags")
		this.botMode = 2
		this.Log("bot detection: m_fFlags & FL_FAKECLIENT")
		return
	}
	catch (e) { this.Log("bot detection: m_fFlags unavailable - " + e) }

	this.botMode = 0
	this.Log("bot detection: no route available, reporting bot=null")
}

::OvlHud.IsBotPlayer <- function (p)
{
	if (this.botMode == 1)
	{
		try { return IsPlayerABot(p) } catch (e) { return null }
	}

	if (this.botMode == 2)
	{
		try { return (NetProps.GetPropInt(p, "m_fFlags") & this.FL_FAKECLIENT) != 0 }
		catch (e) { return null }
	}

	return null
}

// Which kind of survivor this is, for the overlay's roster filter:
//
//   survivor  not a Finale Soldiers bot - a real survivor, or another addon's extra bot
//   reinforcement  a soldier called in with help!. Follows its caller, forced mortal
//   follower  a soldier told to follow a player by hand. Also forced mortal while it follows
//   soldier   a mortal soldier holding a post
//   holdout   any immortal soldier: a team-4 holdout, or a reinforcement whose timeout ran
//             out and turned it immortal while its body is still on the map
//
// Finale Soldiers marks every soldier it spawns with flags on the player entity's script
// scope, and this addon runs in the same server VM, so they are readable directly:
// cf_soldier_bot (lifecycle.nut, the addon's own identity boundary), cf_soldier_following
// (movement.nut ToggleSoldierFollow) and cf_soldier_mortal /
// cf_soldier_distance_suspended (commands.nut ResolveMortalStateForSoldier).
//
// help! reinforcements run ToggleSoldierFollow too, so cf_soldier_following alone cannot
// separate them from a hand-picked follower. help.nut writes cf_soldier_help_temp at
// adoption, before the follow toggle, and never clears it - both removal paths guard on it -
// so it is the identity test and is checked first. cf_soldier_help_active is deliberately
// not used here: it goes false on a dead reinforcement whose body is still on the map, which
// would make the card fall back to "follower" mid-round. Mortality is still tested before
// either of them: an immortal reinforcement is a holdout, and gets no card at all.
//
// m_iTeamNum cannot answer this. A mortal soldier is moved to team 4 transiently by the
// distance dodge and by the progression bypass, and a holdout is on team 4 permanently -
// the same overlap that made a team-2-only filter wrong in v0.1.0. A distance-suspended
// soldier therefore still reports "soldier", or it would flicker out of the HUD whenever
// it wandered far from the team.
//
// Reads only. GetScriptScope returns null when the entity has no scope yet, which is the
// normal state for a plain survivor and for a soldier mid-spawn; the next tick sees it.
// ValidateScriptScope would create one, so it is deliberately not called.
::OvlHud.Classify <- function (p)
{
	local scope = null

	try { scope = p.GetScriptScope() } catch (e) { return "survivor" }

	if (scope == null)                    { return "survivor" }
	if (!("cf_soldier_bot" in scope))     { return "survivor" }

	// Mortality first, for everyone. An immortal soldier is scenery whatever else it is, and
	// a reinforcement whose timeout has run out is turned immortal well before it despawns -
	// reading its help marker first would keep drawing a card for something that can no longer
	// be hurt. A distance-suspended soldier counts as mortal: the team-4 move is a transient
	// dodge of the engine's bot-catchup teleport, not a change of kind.
	local mortal = (("cf_soldier_mortal" in scope) && scope.cf_soldier_mortal)
		|| (("cf_soldier_distance_suspended" in scope) && scope.cf_soldier_distance_suspended)

	if (!mortal) { return "holdout" }

	if (("cf_soldier_help_temp" in scope) && scope.cf_soldier_help_temp) { return "reinforcement" }
	if (("cf_soldier_following" in scope) && scope.cf_soldier_following) { return "follower" }

	return "soldier"
}

// Temp health from pills/adrenaline. m_healthBuffer is the amount granted and
// m_healthBufferTime is the Time() stamp it was granted at; it decays from there.
::OvlHud.TempHealth <- function (p)
{
	local buffer = 0.0
	local stamp  = 0.0

	try
	{
		buffer = NetProps.GetPropFloat(p, "m_healthBuffer")
		stamp  = NetProps.GetPropFloat(p, "m_healthBufferTime")
	}
	catch (e) { return 0 }

	if (buffer <= 0.0) { return 0 }

	local decayed = buffer - ((Time() - stamp) * this.decayRate)

	return (decayed <= 0.0) ? 0 : decayed.tointeger()
}

// ---------------------------------------------------------------------------
// weapon ammunition
//
// Every route below is probed once and the answer reused, the same way DetectBotMode
// works, and every failure is logged rather than swallowed: a silently missing ammo route
// would look exactly like a survivor who is permanently out of bullets, which is worse
// than a card that honestly says it does not know. -1 means "not readable", and the
// overlay draws no ammo text for it.
// ---------------------------------------------------------------------------

// Rounds in the magazine. Clip1() is the semantic accessor; m_iClip1 is the field behind
// it and is the fallback for a build that does not expose the method.
::OvlHud.ClipOf <- function (w)
{
	if (this.clipMode == 0) { return -1 }

	if (this.clipMode != 2)
	{
		try
		{
			local clip = w.Clip1()

			if (this.clipMode != 1)
			{
				this.clipMode = 1
				this.Log("clip read: Clip1()")
			}

			return clip
		}
		catch (e)
		{
			if (this.clipMode == -1) { this.Log("clip read: Clip1 unavailable - " + e) }
		}
	}

	try
	{
		local clip = NetProps.GetPropInt(w, "m_iClip1")

		if (this.clipMode != 2)
		{
			this.clipMode = 2
			this.Log("clip read: m_iClip1")
		}

		return clip
	}
	catch (e)
	{
		if (this.clipMode != 0)
		{
			this.clipMode = 0
			this.Log("clip read: no route available, reporting -1 - " + e)
		}
	}

	return -1
}

// Rounds in reserve. The count lives on the PLAYER, in m_iAmmo, indexed by the ammo type
// the weapon declares - not on the weapon, which only carries its magazine.
::OvlHud.ReserveOf <- function (p, w)
{
	if (this.ammoMode == 0) { return -1 }

	try
	{
		local slot = NetProps.GetPropInt(w, "m_iPrimaryAmmoType")
		if (slot < 0) { return -1 }

		local reserve = NetProps.GetPropIntArray(p, "m_iAmmo", slot)

		if (this.ammoMode != 1)
		{
			this.ammoMode = 1
			this.Log("reserve ammo read: m_iAmmo[m_iPrimaryAmmoType]")
		}

		return reserve
	}
	catch (e)
	{
		if (this.ammoMode != 0)
		{
			this.ammoMode = 0
			this.Log("reserve ammo read: no route available, reporting -1 - " + e)
		}
	}

	return -1
}

// Which kind of rounds are actually loaded: 0 normal, 1 incendiary, 2 explosive.
//
// Two fields together, because either alone is wrong. m_upgradeBitVec says which upgrade
// the weapon has been given and stays set once given; m_nUpgradedPrimaryAmmoLoaded counts
// the upgraded rounds still to be fired and is what runs out. The HUD marks the
// ammunition, so the count is what decides, and the bit vector only says which mark.
//
// The count is exported as well as the kind. It is the upgrade's own pool and survives a
// reload, unlike the magazine, so it is the number the HUD shows while an upgrade holds -
// a magazine that jumps back to full on reload says nothing about how much fire is left.
// Bit values confirmed in game rather than taken from a header: the first build assumed
// incendiary was 1 << 1 and explosive 1 << 2, and live testing gave a plain cartridge for
// incendiary and a flame for explosive, which places the pair one bit lower. Any other bit
// in the vector is ignored - only these two change what is in the magazine.
::OvlHud.UPGRADE_INCENDIARY <- 1   // 1 << 0
::OvlHud.UPGRADE_EXPLOSIVE  <- 2   // 1 << 1

// Rounds left in the upgrade, set by the call below. Kept as a field rather than returned
// alongside the kind because this runs twenty times a second and a table per tick would be
// twenty allocations a second for two integers.
::OvlHud.upgLoaded <- 0

::OvlHud.UpgradeOf <- function (w)
{
	this.upgLoaded = 0

	if (this.upgMode == 0) { return 0 }

	try
	{
		local loaded = NetProps.GetPropInt(w, "m_nUpgradedPrimaryAmmoLoaded")
		local bits   = NetProps.GetPropInt(w, "m_upgradeBitVec")

		if (this.upgMode != 1)
		{
			this.upgMode = 1
			this.Log("ammo upgrade read: m_upgradeBitVec + m_nUpgradedPrimaryAmmoLoaded")
		}

		if (loaded <= 0) { return 0 }

		if (bits & this.UPGRADE_INCENDIARY) { this.upgLoaded = loaded; return 1 }
		if (bits & this.UPGRADE_EXPLOSIVE)  { this.upgLoaded = loaded; return 2 }

		return 0
	}
	catch (e)
	{
		if (this.upgMode != 0)
		{
			this.upgMode = 0
			this.Log("ammo upgrade read: no route available, reporting normal - " + e)
		}
	}

	return 0
}

// One pistol or two. Both are weapon_pistol, and the HUD draws a different icon for each,
// so this has to be answered per weapon rather than assumed.
//
// m_isDualWielding is the direct answer. The fallback is the magazine: a single pistol
// holds 15 and a pair holds 30, so anything past 15 is a pair. That fallback is one-way -
// a pair down to its last rounds reads as a single pistol - which is why it is a fallback
// and not the primary route, and why the failure is logged rather than swallowed.
::OvlHud.IsDualPistol <- function (w, clip)
{
	if (this.dualMode != 0)
	{
		try
		{
			local dual = NetProps.GetPropInt(w, "m_isDualWielding") != 0

			if (this.dualMode != 1)
			{
				this.dualMode = 1
				this.Log("dual pistol read: m_isDualWielding")
			}

			return dual
		}
		catch (e)
		{
			if (this.dualMode != 0)
			{
				this.dualMode = 0
				this.Log("dual pistol read: m_isDualWielding unavailable, "
				         + "falling back to magazine size - " + e)
			}
		}
	}

	return clip > 15
}

// Which melee weapon this is. All of them share the classname weapon_melee; the specific
// one is the map-set script name ("fireaxe", "katana", ...). An install that will not give
// it up still reports a usable "melee".
::OvlHud.MeleeId <- function (w)
{
	if (this.meleeMode == 0) { return "melee" }

	try
	{
		local id = NetProps.GetPropString(w, "m_strMapSetScriptName")

		if (this.meleeMode != 1)
		{
			this.meleeMode = 1
			this.Log("melee id read: m_strMapSetScriptName")
		}

		if (id == null || id.len() == 0) { return "melee" }

		return id
	}
	catch (e)
	{
		if (this.meleeMode != 0)
		{
			this.meleeMode = 0
			this.Log("melee id read: no route available, reporting \"melee\" - " + e)
		}
	}

	return "melee"
}

// ---------------------------------------------------------------------------
// one survivor -> one JSON object
// ---------------------------------------------------------------------------

::OvlHud.SurvivorJson <- function (p, localUid = -1)
{
	local name = ""
	local uid  = -1
	local team = -1
	local chr  = -1
	local hp   = 0
	local maxhp = 100

	try { name  = p.GetPlayerName() }                              catch (e) { name = "?" }
	try { uid   = p.GetPlayerUserId() }                            catch (e) { }
	try { team  = NetProps.GetPropInt(p, "m_iTeamNum") }           catch (e) { }
	try { chr   = NetProps.GetPropInt(p, "m_survivorCharacter") }  catch (e) { }
	try { hp    = p.GetHealth() }                                  catch (e) { }
	try { maxhp = p.GetMaxHealth() }                               catch (e) { }

	// State, most severe first. IsDead() exists on this build; IsAlive() does not.
	local state = "alive"

	try
	{
		if      (p.IsDead())              { state = "dead"   }
		else if (p.IsHangingFromLedge())  { state = "ledge"  }
		else if (p.IsIncapacitated())     { state = "incap"  }
		else if (p.IsDying())             { state = "dying"  }
	}
	catch (e) { state = "unknown" }

	local revives = 0
	local bw      = false

	try { revives = NetProps.GetPropInt(p, "m_currentReviveCount") } catch (e) { }
	try { bw      = NetProps.GetPropInt(p, "m_bIsOnThirdStrike") != 0 } catch (e) { }

	// Inventory
	local kit    = ""
	local pill   = ""
	local throwable = ""
	local active = ""
	local activeIdx = -1
	local slot    = ""
	local primary   = ""
	local priClip   = -1
	local priAmmo   = -1
	local priUpg    = 0
	local priUpgN   = 0
	local secondary = ""
	local secClip   = -1

	try
	{
		local w = p.GetActiveWeapon()
		if (w != null) { active = w.GetClassname(); activeIdx = w.GetEntityIndex() }
	}
	catch (e) { }

	// Weapons are read for the host player only. Only their own weapon HUD is drawn, so
	// classifying every bot's rifle and reading its magazine was work whose result nothing
	// ever displayed - and the ammunition channel below repeats part of it twenty times a
	// second. Items stay per-survivor: those DO appear on every card.
	local isLocal = localUid >= 0 && uid == localUid

	try
	{
		local size = NetProps.GetPropArraySize(p, "m_hMyWeapons")

		for (local i = 0; i < size; i++)
		{
			local w = NetProps.GetPropEntityArray(p, "m_hMyWeapons", i)
			if (w == null) { continue }

			local cn = w.GetClassname()

			// Which of this survivor's own slots is in their hands. Compared by entity
			// index against GetActiveWeapon() rather than by classname: a pair of pistols
			// and a melee weapon share their classname with what they are held beside, and
			// the HUD has to highlight a place, not a name. Anything the tables do not know
			// - a gas can, another addon's weapon - leaves the slot empty, which is the
			// honest answer: nothing the HUD draws is being held.
			local held = activeIdx >= 0 && w.GetEntityIndex() == activeIdx

			if (cn in this.KIT_ITEMS)
			{
				kit = this.KIT_ITEMS[cn]

				if (held) { slot = "kit" }
			}
			else if (cn in this.PILL_ITEMS)
			{
				pill = this.PILL_ITEMS[cn]

				if (held) { slot = "pills" }
			}
			else if (cn in this.THROW_ITEMS)
			{
				throwable = this.THROW_ITEMS[cn]

				if (held) { slot = "throwable" }
			}
			else if (!isLocal) { continue }
			else if (cn in this.PRIMARY_WEAPONS)
			{
				primary = this.PRIMARY_WEAPONS[cn]
				priClip = this.ClipOf(w)
				priAmmo = this.ReserveOf(p, w)
				priUpg  = this.UpgradeOf(w)
				priUpgN = this.upgLoaded

				if (held) { slot = "primary" }
			}
			else if (cn in this.SECONDARY_WEAPONS)
			{
				secondary = this.SECONDARY_WEAPONS[cn]
				secClip   = this.ClipOf(w)

				if (cn == "weapon_pistol" && this.IsDualPistol(w, secClip))
				{
					secondary = "pistol_dual"
				}

				if (held) { slot = "secondary" }
			}
			else if (cn == "weapon_melee")
			{
				secondary = this.MeleeId(w)

				if (held) { slot = "secondary" }
			}
		}
	}
	catch (e)
	{
		if (this.DEBUG) { this.Log("inventory read failed for " + name + " : " + e) }
	}

	local bot = this.IsBotPlayer(p)

	local json = "{"
	json += "\"uid\":" + uid
	json += ",\"name\":\"" + this.JsonEscape(name) + "\""
	json += ",\"team\":" + team
	json += ",\"char\":" + chr
	json += ",\"local\":" + (isLocal ? "true" : "false")
	json += ",\"cls\":\"" + this.Classify(p) + "\""
	json += ",\"bot\":" + ((bot == null) ? "null" : (bot ? "true" : "false"))
	json += ",\"hp\":" + hp
	json += ",\"maxhp\":" + maxhp
	json += ",\"temp\":" + this.TempHealth(p)
	json += ",\"state\":\"" + state + "\""
	json += ",\"revives\":" + revives
	json += ",\"bw\":" + (bw ? "true" : "false")
	json += ",\"kit\":\"" + kit + "\""
	json += ",\"pill\":\"" + pill + "\""
	json += ",\"throw\":\"" + throwable + "\""
	json += ",\"pri\":\"" + this.JsonEscape(primary) + "\""
	json += ",\"priclip\":" + priClip.tointeger()
	json += ",\"priammo\":" + priAmmo.tointeger()
	json += ",\"priupg\":" + priUpg
	json += ",\"priupgn\":" + priUpgN
	json += ",\"sec\":\"" + this.JsonEscape(secondary) + "\""
	json += ",\"secclip\":" + secClip.tointeger()
	json += ",\"weapon\":\"" + this.JsonEscape(active) + "\""
	json += ",\"slot\":\"" + slot + "\""
	json += "}"

	return json
}

// ---------------------------------------------------------------------------
// ammunition channel
//
// One line, four fields, rewritten at 20 Hz:
//
//     <seq> <primary clip> <primary reserve> <secondary clip> <ammo kind> <upgraded left>
//
// The ammo kind - 0 normal, 1 incendiary, 2 explosive - rides this channel rather than the
// roster because it runs out by being fired, so it has to stop at the same round the count
// does. A reader that only knows the first four fields is unaffected.
//
// Only the host player's, because only their weapon HUD is drawn. Which weapons those
// numbers belong to still comes from state.json at 5 Hz - a weapon change is a fifth of a
// second late, which nobody can see, while the rounds themselves are current.
// ---------------------------------------------------------------------------

::OvlHud.ExportAmmo <- function ()
{
	local host = null

	try { host = GetListenServerHost() } catch (e) { return }
	if (host == null) { return }

	local priClip = -1
	local priAmmo = -1
	local secClip = -1
	local priUpg  = 0
	local priUpgN = 0

	local size = NetProps.GetPropArraySize(host, "m_hMyWeapons")

	for (local i = 0; i < size; i++)
	{
		local w = NetProps.GetPropEntityArray(host, "m_hMyWeapons", i)
		if (w == null) { continue }

		local cn = w.GetClassname()

		if (cn in this.PRIMARY_WEAPONS)
		{
			priClip = this.ClipOf(w)
			priAmmo = this.ReserveOf(host, w)
			priUpg  = this.UpgradeOf(w)
			priUpgN = this.upgLoaded
		}
		else if (cn in this.SECONDARY_WEAPONS)
		{
			secClip = this.ClipOf(w)
		}
	}

	this.ammoSeq++

	StringToFile(this.AMMO_FILE,
	             this.ammoSeq + " " + priClip.tointeger() + " " + priAmmo.tointeger()
	             + " " + secClip.tointeger() + " " + priUpg + " " + priUpgN)
}

::OvlHud.AmmoTick <- function (generation)
{
	if (generation != this.gen) { return }   // superseded by a newer load

	try
	{
		this.ExportAmmo()
	}
	catch (e)
	{
		// Deliberately quiet past the first: this runs twenty times a second, and a fault
		// that logged every tick would bury the console. The roster export carries the same
		// numbers at 5 Hz, so the HUD keeps working through it.
		if (!this.ammoWarned)
		{
			this.ammoWarned = true
			this.Log("ammo tick threw, falling back to the 5 Hz numbers : " + e)
		}
	}

	this.ScheduleAmmo(this.AMMO_INTERVAL)
}

::OvlHud.ScheduleAmmo <- function (delay)
{
	try
	{
		EntFire("worldspawn", "RunScriptCode", "::OvlHud.AmmoTick(" + this.gen + ")", delay)
	}
	catch (e)
	{
		this.Log("!! ammo scheduling FAILED, ammo is now 5 Hz : " + e)
	}
}

// ---------------------------------------------------------------------------
// scoreboard hold
//
// The overlay app cannot show the game's scoreboard from outside: L4D2 draws it while the
// host's client holds +showscores, and an external app that has the keyboard focus cannot
// make the game hold anything. From in here it is one console command on the listening
// host, which is the same machine and the same person - and this addon already requires
// that you host the session.
//
// SendToConsole runs on the listen-server host's own client. It is probed once, on first
// use, and every failure prints: a silently missing route would look exactly like a
// checkbox that does nothing, which is the bug this replaces.
// ---------------------------------------------------------------------------

::OvlHud.SetScoreboard <- function (wanted)
{
	if (wanted == this.scores) { return }

	local command = wanted ? "+showscores" : "-showscores"

	if (this.consoleRoute == 0)
	{
		this.scores = wanted   // no route: track intent so we stop retrying every tick
		return
	}

	try
	{
		SendToConsole(command)

		if (this.consoleRoute != 1)
		{
			this.consoleRoute = 1
			this.Log("scoreboard hold: SendToConsole()")
		}

		this.scores = wanted
	}
	catch (e)
	{
		this.consoleRoute = 0
		this.scores = wanted
		this.Log("scoreboard hold unavailable, SendToConsole failed - " + e)
	}
}

// Reads the app's command file and decides whether the scoreboard should be open.
//
// Two independent things can ask for a release: the app writing 0, and the app going away
// entirely. The second is why the seq matters - without a heartbeat there is no difference
// between "still holding" and "the process died holding".
::OvlHud.PollCommand <- function ()
{
	local text = null

	try { text = FileToString(this.CMD_FILE) } catch (e) { text = null }

	if (text == null || text.len() == 0)
	{
		this.SetScoreboard(false)
		return
	}

	local want = false
	local seq  = -1

	try
	{
		local parts = split(text, " \t\r\n")
		if (parts.len() >= 2)
		{
			want = parts[0] == "1"
			seq  = parts[1].tointeger()
		}
	}
	catch (e)
	{
		// A torn read of a file being rewritten four times a second. The next tick sees a
		// whole one; the last known intent stands until then.
		return
	}

	if (seq != this.cmdSeq)
	{
		this.cmdSeq  = seq
		this.cmdSeen = Time()
	}
	else if (want && (Time() - this.cmdSeen) > this.CMD_TIMEOUT)
	{
		// Held, but nothing is refreshing it any more.
		this.SetScoreboard(false)
		return
	}

	this.SetScoreboard(want)
}

// ---------------------------------------------------------------------------
// cinematic detection
//
// The finale outro, the chapter-end stats screen, and any other scripted camera the game
// takes the player through all hide L4D2's own survivor HUD. An overlay drawn on top of one
// of those scenes is the one thing still on screen, which is exactly what the consistent HUD
// must not be.
//
// The chapter end is the same mechanism seen from a different angle: the saferoom door
// closes, the screen blurs, the score panel comes up and the music plays, and only then does
// the loading bar appear. The overlay currently survives all of that and leaves at the load,
// which is several seconds too late.
//
// There is no game-event route to this: event callbacks are registered but never delivered
// to this addon on this install (see DEVLOG, v1.0.7-exp1/exp2), so the same polling loop
// that exports the roster has to observe it. Two independent reads are taken from the host
// player, either of which is enough on its own:
//
//   m_Local.m_iHideHUD  the engine's own "what is hidden" bitfield. HIDEHUD_ALL hides the
//                       lot and HIDEHUD_HEALTH hides precisely the survivor HUD this
//                       overlay stands in for, so either bit means the vanilla HUD is gone.
//   m_hViewEntity       set while the view is bound to a camera entity rather than the
//                       player's own eyes, which is what a scripted camera does.
//
// Both are probed once and remembered like every other route here. Both raw values ride
// state.json beside the verdict, and every change in either one is logged whether or not it
// moves the verdict - a scene that hides the HUD through some third bit shows up in the
// capture as a value this build did not act on, which is one test session rather than
// another guessing round.
//
// The finale outro is live-confirmed on 2.1.0. The chapter end is NOT: the widened mask did
// not fire there, so neither read moves for it, or the export loop is no longer running by
// the time it would. That is what the probe below is for - it is diagnostic scaffolding for
// one capture, not a second theory.
//
// [UNVERIFIED] What server-visible state the chapter-end transition moves at all. The stats
// panel may well be drawn entirely client-side, in which case no polled property will ever
// see it and the answer has to come from somewhere else.
// ---------------------------------------------------------------------------

::OvlHud.HIDEHUD_ALL    <- 4
::OvlHud.HIDEHUD_HEALTH <- 8

// Either bit means L4D2 has stopped drawing the survivor HUD.
::OvlHud.HIDEHUD_MASK   <- 12

// m_fFlags bit 5. The server freezes the player for a scene it is running: the map-start
// intro, and - the reason this is here - the chapter-end transition, where the door closes
// and the score panel comes up.
//
// [OBSERVED - console.log 2026-08-25] Across one full chapter this bit appears three times
// and only three: the spawn freeze at t=6.2, the intro cinematic at t=9.2, and t=42.4, one
// tick before Host_Changelevel. It is absent for every sample of ordinary play in between.
// m_iHideHUD stayed flat at its 2048 baseline through that same transition, which is why
// the 2.1.0 mask widening did nothing there.
::OvlHud.FL_FROZEN      <- 32

// ---------------------------------------------------------------------------
// chapter-end probe  [DIAGNOSTIC - exp builds only]
//
// Reads a spread of host-player state every tick and logs the whole line whenever any field
// in it changes. It decides nothing and gates nothing; the point is that one saferoom finish
// captured with this loaded says which field - if any - the transition moves, instead of
// costing a build per guess.
//
// Every field is read behind its own try. A property this build cannot read prints `?` and
// the rest of the line still lands, because a probe that dies on its first missing property
// tells you nothing about the twelve that follow.
//
// PROBE is switched off in a release build. It prints on change, not per tick, but a
// transition that flickers a value would still be noisy in someone's console.
// ---------------------------------------------------------------------------

::OvlHud.PROBE <- false

::OvlHud.ProbeInt <- function (ent, prop)
{
	try { return "" + NetProps.GetPropInt(ent, prop) } catch (e) { return "?" }
}

// Calls a no-argument Director method by name and returns its value as text, or "?" when
// this build does not have it.
//
// Name-driven rather than written out as calls, deliberately: the point of a probe is to
// find out which of several candidates exists on the install in front of us, and a name that
// is not there costs one caught exception instead of a whole build. Nothing here decides
// anything - see the credits note in Probe.
::OvlHud.ProbeCall <- function (name)
{
	try
	{
		if (!(name in ::Director)) { return "?" }
		return "" + ::Director[name]()
	}
	catch (e) { return "?" }
}

::OvlHud.Probe <- function (p)
{
	if (!this.PROBE) { return }

	// m_fFlags is masked rather than printed raw. The onground and ducking bits flicker with
	// every step the player takes, so a raw print made the probe fire several times a second
	// through ordinary play and buried the transitions it exists to show.
	local flags = "?"
	try { flags = "" + (NetProps.GetPropInt(p, "m_fFlags") & this.FL_FROZEN) } catch (e) {}

	local line = "frozenbit=" + flags
	           + " life=" + this.ProbeInt(p, "m_lifeState")
	           + " obs=" + this.ProbeInt(p, "m_iObserverMode")
	           + " move=" + this.ProbeInt(p, "m_MoveType")
	           + " hidehud=" + this.ProbeInt(p, "m_Local.m_iHideHUD")
	           + " viewmodel=" + this.ProbeInt(p, "m_Local.m_bDrawViewmodel")
	           + " solid=" + this.ProbeInt(p, "m_Collision.m_nSolidType")

	           // Candidates for "the campaign is over and the credits are rolling", which
	           // none of the three shipped reads can see: at finale_win the game unfreezes
	           // everyone and returns every flag to its ordinary-play baseline while the
	           // credits roll over a map that is still live with healthy survivors in it.
	           // [OBSERVED - console.log 2026-08-25: hud=2048 view=0 frozen=0 across the
	           // whole credits, which is the same triple as ordinary play.]
	           //
	           // Every name below is a CANDIDATE, not a documented API. A "?" means this
	           // build does not have it and the name can be struck off; a value that moves
	           // when the credits start is the answer. leftsafe and tank are controls - if
	           // those two also print "?" then no Director call is reaching the engine and
	           // the others prove nothing.
	           + " esc=" + this.ProbeInt(p, "m_isEscaping")
	           + " escaped=" + this.ProbeInt(p, "m_hasEscaped")
	           + " finescape=" + this.ProbeCall("IsFinaleEscapeInProgress")
	           + " finwon=" + this.ProbeCall("IsFinaleWon")
	           + " leftsafe=" + this.ProbeCall("HasAnySurvivorLeftSafeArea")
	           + " tank=" + this.ProbeCall("IsTankInPlay")

	// seq is appended after the comparison, never inside it: it advances every tick, so a
	// line carrying it could never match the previous one and the probe would print at 5 Hz.
	// It is here so the capture shows whether the export loop is still ticking at all
	// through the transition - a frozen seq is a different failure from an unmoved property.
	if (line != this.lastProbe)
	{
		this.lastProbe = line
		this.Log("probe: " + line + " (seq=" + this.seq + " t=" + format("%.1f", Time()) + ")")
	}
}

// The engine's hidden-HUD bitfield for this player, or -1 when there is no route to it.
::OvlHud.HideHudBits <- function (p)
{
	if (this.hudMode == 0) { return -1 }

	try
	{
		local bits = NetProps.GetPropInt(p, "m_Local.m_iHideHUD")

		if (this.hudMode != 1)
		{
			this.hudMode = 1
			this.Log("cinematic read: m_Local.m_iHideHUD")
		}

		return bits
	}
	catch (e)
	{
		if (this.hudMode != 0)
		{
			this.hudMode = 0
			this.Log("cinematic read: m_iHideHUD unavailable, reporting -1 - " + e)
		}
	}

	return -1
}

// 1 once the finale has been won, 0 before that, -1 when there is no route to it.
//
// This is the end credits, and nothing on the player can see them. At finale_win the game
// unfreezes everyone, drops the camera and returns m_iHideHUD to its ordinary 2048 baseline
// while the credits roll over a map that is still live with healthy survivors in it - so all
// three player reads say "ordinary play", correctly, and the overlay came back.
//
// [OBSERVED - console.log 2026-08-25] Director.IsFinaleWon() exists on this build and flips
// false -> true at exactly that moment: t=49.2 escape running and won false, t=73.3 escape
// over and won true. Director.IsFinaleEscapeInProgress() exists too and is deliberately NOT
// used: it is already true while the team is still fighting its way to the rescue vehicle,
// which is the last moment to take the HUD away.
//
// The verdict is not cleared until the script VM reloads on the next map, which is what
// keeps the panel away for the whole credits roll rather than for one tick of it.
::OvlHud.IsFinaleWon <- function ()
{
	if (this.wonMode == 0) { return -1 }

	try
	{
		if (!("IsFinaleWon" in ::Director)) { throw "Director.IsFinaleWon not on this build" }

		local won = ::Director.IsFinaleWon()

		if (this.wonMode != 1)
		{
			this.wonMode = 1
			this.Log("cinematic read: Director.IsFinaleWon()")
		}

		return won ? 1 : 0
	}
	catch (e)
	{
		if (this.wonMode == -1)
		{
			this.wonMode = 0
			this.Log("cinematic read: IsFinaleWon unavailable, reporting -1 - " + e)
		}
	}

	return -1
}

// 1 while the server has the player frozen for a scene it is running, 0 while they have
// control of themselves, -1 when there is no route to it.
::OvlHud.IsFrozen <- function (p)
{
	if (this.frozenMode == 0) { return -1 }

	try
	{
		local flags = NetProps.GetPropInt(p, "m_fFlags")

		if (this.frozenMode != 1)
		{
			this.frozenMode = 1
			this.Log("cinematic read: m_fFlags & FL_FROZEN")
		}

		return ((flags & this.FL_FROZEN) != 0) ? 1 : 0
	}
	catch (e)
	{
		if (this.frozenMode == -1)
		{
			this.frozenMode = 0
			this.Log("cinematic read: m_fFlags unavailable, reporting -1 - " + e)
		}
	}

	return -1
}

// 1 while the player's view is bound to a camera entity, 0 when it is their own eyes,
// -1 when there is no route to it.
::OvlHud.HasViewCamera <- function (p)
{
	if (this.viewMode == 0) { return -1 }

	try
	{
		local cam = NetProps.GetPropEntity(p, "m_hViewEntity")

		if (this.viewMode != 1)
		{
			this.viewMode = 1
			this.Log("cinematic read: m_hViewEntity")
		}

		// The player's own view entity is the player. Anything else is a camera. Compared
		// by entity index rather than by handle: two handles to one entity are not the
		// same Squirrel instance, and == on instances compares the instance.
		return (cam == null || cam.GetEntityIndex() == p.GetEntityIndex()) ? 0 : 1
	}
	catch (e)
	{
		// Only an unprobed route is retired on a throw. Once this one has answered, a
		// later throw is a dangling camera handle in one frame, not a missing route, and
		// retiring it there would blind the detector for the rest of the session.
		if (this.viewMode == -1)
		{
			this.viewMode = 0
			this.Log("cinematic read: m_hViewEntity unavailable, reporting -1 - " + e)
		}
	}

	return -1
}

// ---------------------------------------------------------------------------
// export tick
// ---------------------------------------------------------------------------

::OvlHud.Export <- function ()
{
	local body  = ""
	local count = 0
	local localUid = this.localUid
	local host = null

	// The app runs on the listen-server host's machine, so the host survivor is the
	// current player's card. GetListenServerHost is a native on this L4D2 build; keep the
	// probe inside a guarded boundary so a dedicated-server context still exports the full
	// roster instead of killing the export loop. A null host at map setup is normal. Keep the
	// last valid userid through that transient gap so the optional local marker cannot blink
	// on and off between exporter ticks.
	try
	{
		host = GetListenServerHost()
		if (host != null)
		{
			local candidate = host.GetPlayerUserId()
			if (candidate != null && candidate >= 0) { this.localUid = candidate }
		}
		localUid = this.localUid
	}
	catch (e)
	{
		if (!this.hostProbeWarned)
		{
			this.hostProbeWarned = true
			this.Log("local player detection unavailable - exporting local=false: " + e)
		}
	}

	for (local ent; ent = Entities.FindByClassname(ent, "player");)
	{
		local team = -1
		try { team = NetProps.GetPropInt(ent, "m_iTeamNum") } catch (e) { continue }

		if (!this.IsSurvivorTeam(team)) { continue }

		if (!this.botProbed) { this.DetectBotMode(ent) }

		if (count > 0) { body += "," }
		body += this.SurvivorJson(ent, localUid)
		count++
	}

	// Still inside the settle window with nobody found: the survivors are mid-respawn, not
	// absent. Write nothing and let the app hold on the previous frame, which it already
	// treats as stale, rather than publishing an authoritative empty roster.
	if (count == 0 && (Time() - this.bootTime) < this.SETTLE) { return }

	// First real export of this script load - anything from here on is a re-entry into a
	// live VM, which is a round restart and gets the short wait.
	this.coldLoad = false

	this.seq++

	// Cinematic state rides the roster tick rather than the 20 Hz ammunition channel: a
	// fifth of a second either side of a camera cut is not visible, and the app already
	// holds the last roster it read through a stale window, which is exactly the window a
	// finale outro runs into as the map ends.
	local hudBits = -1
	local viewCam = -1
	local frozen  = -1
	local won     = this.IsFinaleWon()   // world state, not the player's - read regardless

	if (host != null)
	{
		hudBits = this.HideHudBits(host)
		viewCam = this.HasViewCamera(host)
		frozen  = this.IsFrozen(host)
		this.Probe(host)
	}

	// Four reads, any one of which is enough, because each scene is only visible to some of
	// them. The outro answers on the first two; the chapter end only on the third, where
	// m_iHideHUD never leaves its baseline; the end credits only on the fourth, where all
	// three player reads are back to their ordinary-play values because the game really has
	// handed control back - it is just rolling credits over the top.
	local cine = ((hudBits > 0 && (hudBits & this.HIDEHUD_MASK) != 0)
	              || viewCam == 1
	              || frozen == 1
	              || won == 1) ? 1 : 0

	// Logged on every change in the raw reads, not only when the verdict flips. A scene that
	// hides the vanilla HUD through something this build does not act on leaves its value in
	// the capture, which is what makes the next build a correction rather than another guess.
	if (hudBits != this.lastHudBits || viewCam != this.lastViewCam
	    || frozen != this.lastFrozen || won != this.lastWon)
	{
		this.lastHudBits = hudBits
		this.lastViewCam = viewCam
		this.lastFrozen  = frozen
		this.lastWon     = won
		this.Log("cinematic reads changed: hud=" + hudBits + " view=" + viewCam
		         + " frozen=" + frozen + " won=" + won
		         + " -> " + (cine == 1 ? "hidden" : "drawn"))
	}

	if (cine != this.cine)
	{
		this.cine = cine
		this.Log("cinematic " + (cine == 1 ? "started" : "ended")
		         + " (hud=" + hudBits + " view=" + viewCam + " frozen=" + frozen
		         + " won=" + won + ")")
	}

	local json = "{"
	json += "\"v\":\"" + this.VERSION + "\""
	json += ",\"seq\":" + this.seq
	json += ",\"time\":" + format("%.2f", Time())
	json += ",\"count\":" + count
	json += ",\"cine\":" + cine
	json += ",\"hud\":" + hudBits
	json += ",\"view\":" + viewCam
	json += ",\"frz\":" + frozen
	json += ",\"won\":" + won
	json += ",\"won\":" + won
	json += ",\"survivors\":[" + body + "]"
	json += "}"

	StringToFile(this.OUT_FILE, json)
}

::OvlHud.Tick <- function (generation)
{
	if (generation != this.gen) { return }   // superseded by a newer load

	try
	{
		this.Export()
	}
	catch (e)
	{
		this.Log("export tick threw, loop continues : " + e)
	}

	// Separate try: a command-file problem must never stop state from being exported, and
	// an export problem must never leave the scoreboard latched open.
	try
	{
		this.PollCommand()
	}
	catch (e)
	{
		this.Log("command tick threw, loop continues : " + e)
	}

	// Reschedule outside the try: an uncaught throw here would kill the loop for good.
	this.Schedule(this.INTERVAL)
}

::OvlHud.Schedule <- function (delay)
{
	try
	{
		EntFire("worldspawn", "RunScriptCode", "::OvlHud.Tick(" + this.gen + ")", delay)
	}
	catch (e)
	{
		this.Log("!! scheduling FAILED, export has stopped : " + e)
	}
}

// Called by the two re-entry points, scriptedmode_addon.nut and director_base_addon.nut,
// on a map load AND on a same-map round restart.
//
// A restart tears down the map's entities without re-running mapspawn_addon.nut, so the
// EntFire chain - which lives on the old round's worldspawn - dies there and the exporter
// goes silent while the restored survivor bots are already standing in the new round. That
// is the false zero roster: the app is reading a file nothing writes any more.
//
// This does NOT go through round_start_post_nav. v1.0.7 and v1.0.7-exp1 both tried that -
// registering from mapspawn, then from mapspawn AND the director phase - and the callback
// was never delivered in either build, while other addons' handlers for that same event
// fired on the same restart, in the same log. [OBSERVED - console.log 2026-08-14]
//
// Re-entry is the mechanism the aim-all-guns addon uses on this install and it is seen
// working on every restart in that capture. Boot() is what makes it safe to call blind:
// the generation bump retires whatever chain came before, so a restart that runs both
// re-entry points, or a map load that runs all three entry points, still ends with exactly
// one live chain.
//
// A re-entry that has already exported this script load is a restart, and waits REARM_WAIT
// rather than the cold-load BOOT_WAIT - the map is already up and the survivors are back in
// a fraction of the time. The other two re-entries of a normal chapter load land while
// coldLoad is still true and keep the long wait.
::OvlHud.Rearm <- function (entry)
{
	local wait = this.coldLoad ? this.BOOT_WAIT : this.REARM_WAIT

	this.Log("re-arming exporter (" + entry + ", first export in " + wait + "s)")
	this.Boot(wait)
}

// ---------------------------------------------------------------------------
// load
// ---------------------------------------------------------------------------

::OvlHud.Boot <- function (wait = null)
{
	this.gen++
	this.bootTime = Time()

	try
	{
		local rate = Convars.GetFloat("pain_pills_decay_rate")
		if (rate != null && rate > 0.0) { this.decayRate = rate }
		this.Log("temp health decay rate = " + this.decayRate)
	}
	catch (e)
	{
		this.Log("pain_pills_decay_rate unreadable, using " + this.decayRate + " : " + e)
	}

	// Bot detection deliberately does not happen here - no players exist yet. The first
	// export tick that finds a survivor does it.
	this.botProbed = false
	// The ammo and melee routes are re-probed per script load for the same reason: what
	// answered on one build is not proof for the next one this VM comes up on.
	this.clipMode  = -1
	this.ammoMode  = -1
	this.meleeMode = -1
	this.dualMode  = -1
	this.hudMode   = -1
	this.viewMode  = -1
	this.frozenMode = -1
	this.wonMode    = -1
	// A round restart runs through a live VM, so a cinematic verdict from the round that
	// just ended would otherwise stay latched and keep the overlay hidden for the whole
	// next round.
	this.cine      = 0
	this.lastHudBits = -2
	this.lastViewCam = -2
	this.lastFrozen  = -2
	this.lastWon     = -2
	this.lastProbe   = ""
	this.hostProbeWarned = false
	this.ammoWarned = false
	this.localUid = -1

	// A chapter change reloads this script while the engine keeps running. Anything the
	// previous chapter believed about the scoreboard is void: assume it is closed, so the
	// first command that asks for it open actually sends the command.
	this.scores  = false
	this.cmdSeq  = -1
	this.cmdSeen = 0.0

	local delay = (wait == null) ? this.BOOT_WAIT : wait

	this.Schedule(delay)

	// The ammunition loop waits with the roster loop. Starting it early would write a file
	// full of -1 at a loading screen, which the app would then have to distinguish from a
	// real "no weapon" answer.
	this.ScheduleAmmo(delay)
}

::OvlHud.Log("Overlay HUD Export " + ::OvlHud.VERSION + " loaded - exporting to ems/" + ::OvlHud.OUT_FILE)
::OvlHud.Boot()
