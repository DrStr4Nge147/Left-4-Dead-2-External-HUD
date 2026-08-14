// Overlay HUD Export - addon entry point.
//
// mapspawn_addon.nut is an additive Valve entry point: several addons may ship one and
// all of them run. It executes once per chapter, in the root table, so everything reached
// from here must tolerate being run again on the next chapter.
//
// Once per chapter is all it is. A same-map round restart does not re-run this file, so
// the restart path belongs to scriptedmode_addon.nut and director_base_addon.nut.

try
{
	IncludeScript("overlay_hud_export")
}
catch (e)
{
	printl("[OVLHUD] FATAL: could not include overlay_hud_export - " + e)
}
