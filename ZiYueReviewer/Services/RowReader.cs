using System.Data.Common;

namespace ZiYueReviewer.Services;

/// <summary>
/// DbDataReader 包装，支持按列名取值（内部缓存列序号）。
/// </summary>
public class RowReader
{
    private readonly DbDataReader _reader;
    private readonly Dictionary<string, int> _ordinals = new();

    public RowReader(DbDataReader reader) => _reader = reader;

    private int Ordinal(string name)
    {
        if (!_ordinals.TryGetValue(name, out int index))
        {
            index = _reader.GetOrdinal(name);
            _ordinals[name] = index;
        }
        return index;
    }

    public bool IsDBNull(string name) => _reader.IsDBNull(Ordinal(name));
    public string GetString(string name) => _reader.GetString(Ordinal(name));
    public int GetInt32(string name) => _reader.GetInt32(Ordinal(name));
    public DateTime GetDateTime(string name) => _reader.GetDateTime(Ordinal(name));
    public ulong GetUInt64(string name) => Convert.ToUInt64(_reader.GetValue(Ordinal(name)));
}
