namespace Reviewer.Models;

public class DatabaseConfig
{
    public string Source { get; init; } = "localhost";
    public uint Port { get; init; } = 3306;
    public string Database { get; init; } = "ziyuebot";
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
}
