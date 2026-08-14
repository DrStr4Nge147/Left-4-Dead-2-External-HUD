// Overlay HUD Export - second re-entry point for round restarts.
//
// Same job as scriptedmode_addon.nut, from the DirectorScript scope instead. Both are kept
// because they run at different points in the restart sequence and this is the last entry
// point before the map goes live: if the restart tears the world down again after scripted
// mode has already re-initialised, this is the one that lands on the surviving world.
//
// Calling Rearm twice in one restart costs nothing - Boot's generation bump retires the
// first chain, so there is still exactly one live loop afterwards.
//
// Additive, like every *_addon.nut entry point: this does not replace another addon's
// Director script, and nothing about the Director is touched here.

try
{
	if (!("OvlHud" in getroottable()))
	{
		IncludeScript("overlay_hud_export", getroottable())
	}
	else
	{
		::OvlHud.Rearm("director")
	}
}
catch (e)
{
	printl("[OVLHUD] FATAL: director re-entry failed - " + e)
}
