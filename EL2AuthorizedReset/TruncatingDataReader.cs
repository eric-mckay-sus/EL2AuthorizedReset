using System.Data;

/// <summary>
/// IDataReader wrapper that trims/shortens string values on the fly.
/// Any column whose name is specified in the constructor will be truncated
/// to the provided maximum length.  The underlying reader is streamed, so
/// no intermediate table is built.
/// </summary>
internal sealed class TruncatingDataReader : IDataReader, IDisposable
{
    private readonly IDataReader _inner; // The IDataReader implementation for which this is a wrapper
    private readonly Dictionary<int,int> _maxLengthsByOrdinal; // The mapping of ordinal values to their max lengths

    /// <summary>
    /// Constructs a TruncatingDataReader from an implementation of IDataReader and mapping of strings to their maximum lengths
    /// </summary>
    /// <param name="inner">The IDataReader from which this TruncatingDataReader is derived</param>
    /// <param name="maxLengthsByName">A dictionary mapping strings to max lengths</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null</exception>
    public TruncatingDataReader(IDataReader inner, Dictionary<string,int> maxLengthsByName)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxLengthsByOrdinal = [];

        foreach (var kvp in maxLengthsByName)
        {
            try
            {
                int ord = _inner.GetOrdinal(kvp.Key);
                _maxLengthsByOrdinal[ord] = kvp.Value;
            }
            catch (IndexOutOfRangeException)
            {
                // ignore missing headers
            }
        }
    }

    /// <summary>
    /// Truncates a value given its ordinal value according to the mapping in <see cref="_maxLengthsByOrdinal" />
    /// </summary>
    /// <param name="ord">The value's ordinal</param>
    /// <param name="value">The value to truncate</param>
    /// <returns><paramref name="value"/>, truncated</returns>
    private object Truncate(int ord, object value)
    {
        if (value is string s && _maxLengthsByOrdinal.TryGetValue(ord, out var max) && s.Length > max)
            return s[..max];
        return value;
    }

    // Relevant overrides/delegates
    public object this[int i] => Truncate(i, _inner[i]);
    public object this[string name] => Truncate(_inner.GetOrdinal(name), _inner[name]);
    public int FieldCount => _inner.FieldCount;
    public bool Read() => _inner.Read();
    public bool NextResult() => _inner.NextResult();
    public void Close() => _inner.Close();
    public DataTable GetSchemaTable() => _inner.GetSchemaTable();
    public bool IsClosed => _inner.IsClosed;
    public int RecordsAffected => _inner.RecordsAffected;
    public void Dispose() => _inner.Dispose();

    // Most methods just forward to their IDataReader
    public bool GetBoolean(int i) => _inner.GetBoolean(i);
    public byte GetByte(int i) => _inner.GetByte(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => _inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
    public char GetChar(int i) => _inner.GetChar(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => _inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
    public IDataReader GetData(int i) => _inner.GetData(i);
    public string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
    public DateTime GetDateTime(int i) => _inner.GetDateTime(i);
    public decimal GetDecimal(int i) => _inner.GetDecimal(i);
    public double GetDouble(int i) => _inner.GetDouble(i);
    public Type GetFieldType(int i) => _inner.GetFieldType(i);
    public float GetFloat(int i) => _inner.GetFloat(i);
    public Guid GetGuid(int i) => _inner.GetGuid(i);
    public short GetInt16(int i) => _inner.GetInt16(i);
    public int GetInt32(int i) => _inner.GetInt32(i);
    public long GetInt64(int i) => _inner.GetInt64(i);
    public string GetName(int i) => _inner.GetName(i);
    public int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public string GetString(int i)
    {
        var s = _inner.GetString(i);
        if (_maxLengthsByOrdinal.TryGetValue(i, out var max) && s.Length > max)
            return s.Substring(0, max);
        return s;
    }
    public object GetValue(int i) => Truncate(i, _inner.GetValue(i));
    public int GetValues(object[] values)
    {
        int count = _inner.GetValues(values);
        for (int i = 0; i < count; i++)
            values[i] = Truncate(i, values[i]);
        return count;
    }
    public bool IsDBNull(int i) => _inner.IsDBNull(i);
    public int Depth => _inner.Depth;
}