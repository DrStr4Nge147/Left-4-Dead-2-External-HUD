// Exercise the production exporter with Finale Soldiers lifecycle snapshots.
::Time <- function() { return 100.0 }
class HelpSoldier
{
    scope = null
    constructor(s) { scope = s }
    function IsValid() { return true }
    function GetScriptScope() { return scope }
}
::cf_soldier_spawner <- {
    help_duration = 120.0, help_cooldown = 60.0, help_arrive_timeout = 30.0,
    help_groups = { squad = 200.0 }, help_cooldowns = { ["7"] = 290.0 },
    help_cooldown_started = {}, spawned = []
}
local fs = cf_soldier_spawner
local scope = { cf_soldier_help_owner = 7, cf_soldier_help_active = true,
    cf_soldier_help_group = "squad", cf_soldier_help_leaving = false }
fs.spawned.append(HelpSoldier(scope))
local failures = 0
local checks = 0
function CheckHelp(label, state, left, total)
{
    checks++
    local expected = ",\"help\":{\"st\":\"" + state + "\",\"left\":" + format("%.2f", left)
        + ",\"total\":" + format("%.2f", total) + "}"
    local actual = OvlHud.HelpJson(7)
    if (actual != expected) { failures++; printl("FAIL: " + label + " actual=" + actual) }
}
CheckHelp("active squad", "active", 100.0, 120.0)
// BeginHelpWithdrawal retains active and expiry while setting leaving and starting cooldown.
scope.cf_soldier_help_leaving = true
fs.help_cooldown_started.squad <- true
fs.help_cooldowns["7"] = 160.0
CheckHelp("early checkpoint withdrawal", "cooling", 60.0, 60.0)
scope.cf_soldier_help_leaving = false
CheckHelp("other members have not begun walking out", "cooling", 60.0, 60.0)
fs.help_cooldowns["7"] = 90.0
CheckHelp("withdrawal outlasts cooldown", "ready", 0.0, 0.0)
delete fs.help_cooldown_started
scope.cf_soldier_help_leaving = true
fs.help_cooldowns["7"] = 150.0
CheckHelp("older build leaving marker", "cooling", 50.0, 60.0)
scope.cf_soldier_help_leaving = false
CheckHelp("older build active squad", "active", 100.0, 120.0)
fs.spawned.clear()
CheckHelp("squad removed", "cooling", 50.0, 60.0)
fs.help_cooldowns["7"] = 290.0
CheckHelp("pending arrival", "calling", 10.0, 30.0)
if (failures) throw failures + " help fixture(s) failed"
printl("PASS: exporter help fixtures (" + checks + ")")
