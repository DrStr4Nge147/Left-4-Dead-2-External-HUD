namespace OverlayHud.Services;

/// <summary>
/// Distinguishes "the game has closed" from "the overlay was launched before the game".
/// </summary>
internal sealed class GameLifetimeState
{
    public bool HasObservedGame { get; private set; }

    public bool ShouldExit(bool gameProcessRunning, bool exitWhenGameCloses)
    {
        if (gameProcessRunning)
        {
            HasObservedGame = true;
            return false;
        }

        return exitWhenGameCloses && HasObservedGame;
    }
}
