// Overlay HUD Export - live survivor state exporter.
//
// Writes every survivor currently in the session to a JSON file that an external overlay
// application reads. Read-only with respect to the game: it reads state and writes one
// file. No gameplay is touched.
//
// Output: left4dead2/ems/overlay_hud_state.json   (verified write path, v0.1.0-probe1)

::OvlHud <- {}

::OvlHud.VERSION   <- "1.0.2"
::OvlHud.OUT_FILE  <- "overlay_hud_state.json"

// Reverse channel. The overlay app writes "<want> <seq>" here; this script reads it on the
// same tick that writes state out. See docs/STATE_FORMAT.md.
::OvlHud.CMD_FILE  <- "overlay_hud_cmd.txt"

// Seconds without a new command seq before a held scoreboard is released on its own. The
// app rewrites the file about four times a second while it holds, so anything past this
// means the app closed, crashed, or was killed - and a scoreboard latched open by a
// process that no longer exists is not something the player can undo from the game.
::OvlHud.CMD_TIMEOUT <- 2.0

// Export rate. 0.2 = 5 Hz. The overlay only needs to look current, not frame-perfect,
// and every tick is a file write.
::OvlHud.INTERVAL  <- 0.2

// Seconds after chapter load before the first export. The roster is not settled at t=0.
::OvlHud.BOOT_WAIT <- 5.0

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
//   follower  a soldier following a player. Always forced mortal while it follows
//   soldier   a mortal soldier holding a post
//   holdout   an immortal team-4 holdout soldier
//
// Finale Soldiers marks every soldier it spawns with flags on the player entity's script
// scope, and this addon runs in the same server VM, so they are readable directly:
// cf_soldier_bot (lifecycle.nut, the addon's own identity boundary), cf_soldier_following
// (movement.nut ToggleSoldierFollow) and cf_soldier_mortal /
// cf_soldier_distance_suspended (commands.nut ResolveMortalStateForSoldier).
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

	if (("cf_soldier_following" in scope) && scope.cf_soldier_following) { return "follower" }
	if (("cf_soldier_mortal" in scope) && scope.cf_soldier_mortal)       { return "soldier"  }

	if (("cf_soldier_distance_suspended" in scope) && scope.cf_soldier_distance_suspended)
	{
		return "soldier"
	}

	return "holdout"
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
// one survivor -> one JSON object
// ---------------------------------------------------------------------------

::OvlHud.SurvivorJson <- function (p)
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

	try
	{
		local w = p.GetActiveWeapon()
		if (w != null) { active = w.GetClassname() }
	}
	catch (e) { }

	try
	{
		local size = NetProps.GetPropArraySize(p, "m_hMyWeapons")

		for (local i = 0; i < size; i++)
		{
			local w = NetProps.GetPropEntityArray(p, "m_hMyWeapons", i)
			if (w == null) { continue }

			local cn = w.GetClassname()

			if      (cn in this.KIT_ITEMS)   { kit       = this.KIT_ITEMS[cn]   }
			else if (cn in this.PILL_ITEMS)  { pill      = this.PILL_ITEMS[cn]  }
			else if (cn in this.THROW_ITEMS) { throwable = this.THROW_ITEMS[cn] }
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
	json += ",\"weapon\":\"" + this.JsonEscape(active) + "\""
	json += "}"

	return json
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
// export tick
// ---------------------------------------------------------------------------

::OvlHud.Export <- function ()
{
	local body  = ""
	local count = 0

	for (local ent; ent = Entities.FindByClassname(ent, "player");)
	{
		local team = -1
		try { team = NetProps.GetPropInt(ent, "m_iTeamNum") } catch (e) { continue }

		if (!this.IsSurvivorTeam(team)) { continue }

		if (!this.botProbed) { this.DetectBotMode(ent) }

		if (count > 0) { body += "," }
		body += this.SurvivorJson(ent)
		count++
	}

	this.seq++

	local json = "{"
	json += "\"v\":\"" + this.VERSION + "\""
	json += ",\"seq\":" + this.seq
	json += ",\"time\":" + format("%.2f", Time())
	json += ",\"count\":" + count
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

// ---------------------------------------------------------------------------
// load
// ---------------------------------------------------------------------------

::OvlHud.Boot <- function ()
{
	this.gen++

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

	// A chapter change reloads this script while the engine keeps running. Anything the
	// previous chapter believed about the scoreboard is void: assume it is closed, so the
	// first command that asks for it open actually sends the command.
	this.scores  = false
	this.cmdSeq  = -1
	this.cmdSeen = 0.0

	this.Schedule(this.BOOT_WAIT)
}

::OvlHud.Log("Overlay HUD Export " + ::OvlHud.VERSION + " loaded - exporting to ems/" + ::OvlHud.OUT_FILE)
::OvlHud.Boot()
