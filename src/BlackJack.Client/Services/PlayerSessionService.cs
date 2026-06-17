namespace BlackJack.Client.Services;

public sealed class PlayerSessionService
{
    private const string NameKey = "bj_player_name";
    private const string PlayerIdKey = "bj_player_id";
    private const string TableIdKey = "bj_table_id";

    public string? GetStoredName() => Read(NameKey);

    public void SaveName(string name) => Write(NameKey, name);

    public string? GetStoredPlayerId() => Read(PlayerIdKey);

    public void SaveSession(string playerId, string tableId)
    {
        Write(PlayerIdKey, playerId);
        Write(TableIdKey, tableId);
    }

    public string? GetStoredTableId() => Read(TableIdKey);

    public void ClearSession()
    {
        Remove(PlayerIdKey);
        Remove(TableIdKey);
    }

    private static string? Read(string key)
    {
        try
        {
            return null; // populated at runtime via JS interop in components
        }
        catch
        {
            return null;
        }
    }

    private static void Write(string key, string value) { }
    private static void Remove(string key) { }
}