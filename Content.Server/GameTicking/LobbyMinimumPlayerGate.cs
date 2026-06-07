namespace Content.Server.GameTicking;

public static class LobbyMinimumPlayerGate
{
    public static bool HasEnoughPlayers(int playerCount, int minimumPlayers)
    {
        return playerCount >= Math.Max(0, minimumPlayers);
    }
}
