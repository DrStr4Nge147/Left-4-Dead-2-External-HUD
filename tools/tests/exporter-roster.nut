// Test doubles stay outside the packable addon tree. Run through Test-ExporterRoster.ps1.
::NetProps <- { GetPropInt = function(p, prop) { return p.team } }
::Director <- { map = "cf_m1_town", GetMapName = function() { return this.map } }
class RosterPlayer
{
    team = 2
    scope = null
    failScope = false
    constructor(t, s = null) { team = t; scope = s }
    function GetScriptScope() { if (failScope) throw "scope unavailable"; return scope }
}
local failures = 0
local checks = 0
function Check(label, p, expected)
{
    local actual = OvlHud.Classify(p)
    checks++
    if (actual != expected)
    {
        failures++
        printl("FAIL: " + label + " expected=" + expected + " actual=" + actual)
    }
}
// Captured Cold Front roster: four team-2 teammates and these three unmarked team-4 NPCs.
foreach (name in ["Bill", "Zoey", "Louis"])
    Check("Cold Front room " + name, RosterPlayer(4), "holdout")
Check("Passing bridge support", RosterPlayer(4, {}), "holdout")
Check("Passing finale support", RosterPlayer(4), "holdout")
Check("ordinary teammate", RosterPlayer(2), "survivor")
Check("extra traveling bot", RosterPlayer(2, {}), "survivor")
local changing = RosterPlayer(4)
Check("before joining", changing, "holdout")
changing.team = 2
Check("after joining", changing, "survivor")
changing.team = 4
Check("leaving group", changing, "holdout")
local unreadable = RosterPlayer(4)
unreadable.failScope = true
Check("missing scope does not admit support", unreadable, "holdout")
foreach (team in [2, 4])
{
    Check("mortal soldier", RosterPlayer(team, {cf_soldier_bot=true, cf_soldier_mortal=true}), "soldier")
    Check("distance-suspended soldier", RosterPlayer(team, {cf_soldier_bot=true, cf_soldier_distance_suspended=true}), "soldier")
    Check("follower", RosterPlayer(team, {cf_soldier_bot=true, cf_soldier_mortal=true, cf_soldier_following=true}), "follower")
    Check("reinforcement", RosterPlayer(team, {cf_soldier_bot=true, cf_soldier_mortal=true, cf_soldier_following=true, cf_soldier_help_temp=true}), "reinforcement")
    Check("expired reinforcement", RosterPlayer(team, {cf_soldier_bot=true, cf_soldier_help_temp=true}), "holdout")
    Check("immortal soldier", RosterPlayer(team, {cf_soldier_bot=true}), "holdout")
}
local mike = RosterPlayer(4)
::cf_npc_script <- {npc_ent=mike}
::cf_npc_script_data <- {bJoined=false}
Director.map = "cf_m4_outskirts"
Check("Mike has not joined", mike, "holdout")
mike.team = 2
Check("Mike traveling", mike, "survivor")
cf_npc_script_data.bJoined = true
mike.team = 4
Check("joined Mike dead or transitioning", mike, "survivor")
Check("room Bill is not Mike", RosterPlayer(4), "holdout")
cf_npc_script_data.bJoined = false
Director.map = "cf_m3_evac"
Check("automatic chapter-3 companion after death", mike, "survivor")
cf_npc_script.npc_ent = null
Check("old companion handle is not adopted", mike, "holdout")
delete getroottable().cf_npc_script
delete getroottable().cf_npc_script_data
Check("next campaign", RosterPlayer(4), "holdout")
if (failures) throw failures + " roster checks failed"
printl("PASS: exporter roster fixtures (" + checks + " checks)")
