// <copyright file="TruncatingDataReader.cs" company="Stanley Electric US Co. Inc.">
// Copyright (c) 2026 Stanley Electric US Co. Inc. Licensed under the MIT License.
// </copyright>

namespace CmmsCsvReader;

using System.Data;

/// <summary>
/// IDataReader wrapper that trims/shortens string values on the fly.
/// Any column whose name is specified in the constructor will be truncated
/// to the provided maximum length. The underlying reader is streamed, so
/// no intermediate table is built.
/// </summary>
internal sealed class TruncatingDataReader : IDataReader, IDisposable
{
    /// <summary>
    /// The IDataReader implementation for which this is a wrapper.
    /// </summary>
    private readonly IDataReader inner;

    /// <summary>
    /// The mapping of ordinal values to their max lengths.
    /// </summary>
    private readonly Dictionary<int, int> maxLengthsByOrdinal;

    /// <summary>
    /// Initializes a new instance of the <see cref="TruncatingDataReader"/> class.
    /// Constructs a TruncatingDataReader from an implementation of IDataReader and mapping of strings to their maximum lengths.
    /// </summary>
    /// <param name="inner">The IDataReader from which this TruncatingDataReader is derived.</param>
    /// <param name="maxLengthsByName">A dictionary mapping strings to max lengths.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    public TruncatingDataReader(IDataReader inner, Dictionary<string, int> maxLengthsByName)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.maxLengthsByOrdinal = [];

        foreach (KeyValuePair<string, int> kvp in maxLengthsByName)
        {
            try
            {
                int ord = this.inner.GetOrdinal(kvp.Key);
                this.maxLengthsByOrdinal[ord] = kvp.Value;
            }
            catch (IndexOutOfRangeException)
            {
                // ignore missing headers
            }
        }
    }

    /// <summary>
    /// Gets the depth of the current row.
    /// </summary>
    public int Depth => this.inner.Depth;

    /// <summary>
    /// Gets a value indicating whether the internal data reader is closed.
    /// </summary>
    public bool IsClosed => this.inner.IsClosed;

    /// <summary>
    /// Gets the number of rows affected by a SQL statement.
    /// </summary>
    public int RecordsAffected => this.inner.RecordsAffected;

    /// <summary>
    /// Gets the length of the current row.
    /// </summary>
    public int FieldCount => this.inner.FieldCount;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="i">The integer index to access.</param>
    /// <returns>The object in <see cref="inner"/> stored at index <paramref name="i"/>.</returns>
    public object this[int i] => this.Truncate(i, this.inner[i]);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="name">The named index to access.</param>
    /// <returns>The object in <see cref="inner"/> stored at named index <paramref name="name"/>.</returns>
    public object this[string name] => this.Truncate(this.inner.GetOrdinal(name), this.inner[name]);

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns>Whether there are more rows.</returns>
    public bool Read() => this.inner.Read();

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns>Whether there are more rows.</returns>
    public bool NextResult() => this.inner.NextResult();

    /// <summary>
    /// Closes the internal <see cref="IDataReader"/>.
    /// </summary>
    public void Close() => this.inner.Close();

    /// <summary>
    /// Gets a DataTable containing the schema metadata.
    /// </summary>
    /// <returns>An empty DataTable with column names matching that of <see cref="inner"/>.</returns>
    public DataTable? GetSchemaTable() => this.inner.GetSchemaTable();

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void Dispose() => this.inner.Dispose();

    // Must implement the following to comply with IDataReader. All simply foward to the inner data reader.

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a boolean.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A boolean value representing the contents of the column.</returns>
    public bool GetBoolean(int i) => this.inner.GetBoolean(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a byte.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A byte representing the contents of the column.</returns>
    public byte GetByte(int i) => this.inner.GetByte(i);

    /// <summary>
    /// Streams bytes from <see cref="inner"/> to <paramref name="buffer"/>, starting at index <paramref name="i"/>.
    /// </summary>
    /// <param name="i">The field from which to begin streaming.</param>
    /// <param name="fieldOffset">The index in the buffer from which to start streaming.</param>
    /// <param name="buffer">The buffer to hold the streamed content (pass by reference win).</param>
    /// <param name="bufferoffset">The index in the buffer at which to start streaming into.</param>
    /// <param name="length">The target number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => this.inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a char.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A char representing the contents of the column.</returns>
    public char GetChar(int i) => this.inner.GetChar(i);

    /// <summary>
    /// Streams chars from <see cref="inner"/> to <paramref name="buffer"/>, starting at index <paramref name="i"/>.
    /// </summary>
    /// <param name="i">The field from which to begin streaming.</param>
    /// <param name="fieldoffset">The index in the buffer from which to start streaming.</param>
    /// <param name="buffer">The buffer to hold the streamed content (pass by reference win).</param>
    /// <param name="bufferoffset">The index in the buffer at which to start streaming into.</param>
    /// <param name="length">The target number of chars to read.</param>
    /// <returns>The number of chars actually read.</returns>
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => this.inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);

    /// <summary>
    /// Creates an IDataReader for reading nested data.
    /// </summary>
    /// <param name="i"><inheritdoc/></param>
    /// <returns>A new IDataReader for the nested data at index <paramref name="i"/>.</returns>
    public IDataReader GetData(int i) => this.inner.GetData(i);

    /// <summary>
    /// Gets the datatype of the data at index <paramref name="i"/> as a string.
    /// </summary>
    /// <param name="i">The index at which to check the datatype.</param>
    /// <returns>A string representing the datatype name of the data at index <paramref name="i"/>.</returns>
    public string GetDataTypeName(int i) => this.inner.GetDataTypeName(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a DateTime.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A Datetime representing the contents of the column.</returns>
    public DateTime GetDateTime(int i) => this.inner.GetDateTime(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a decimal.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A decimal representing the contents of the column.</returns>
    public decimal GetDecimal(int i) => this.inner.GetDecimal(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a double.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A double representing the contents of the column.</returns>
    public double GetDouble(int i) => this.inner.GetDouble(i);

    /// <summary>
    /// Retrieves the datatype of the data at <paramref name="i"/> as a Type.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A Type representing the datatype of the column.</returns>
    public Type GetFieldType(int i) => this.inner.GetFieldType(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a float.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A float representing the contents of the column.</returns>
    public float GetFloat(int i) => this.inner.GetFloat(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a GUID.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A GUID representing the contents of the column.</returns>
    public Guid GetGuid(int i) => this.inner.GetGuid(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a short.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A short representing the contents of the column.</returns>
    public short GetInt16(int i) => this.inner.GetInt16(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as an int.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>An int representing the contents of the column.</returns>
    public int GetInt32(int i) => this.inner.GetInt32(i);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a long.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A long representing the contents of the column.</returns>
    public long GetInt64(int i) => this.inner.GetInt64(i);

    /// <summary>
    /// Gets the column name for index <paramref name="i"/>.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A string of the column name for index <paramref name="i"/>.</returns>
    public string GetName(int i) => this.inner.GetName(i);

    /// <summary>
    /// Gets the index of the column labeled with <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The column name for which to get the index.</param>
    /// <returns>The index of the column labeled <paramref name="name"/>.</returns>
    public int GetOrdinal(string name) => this.inner.GetOrdinal(name);

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as a string.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>A string representing the contents of the column.</returns>
    public string GetString(int i)
    {
        string s = this.inner.GetString(i);
        if (this.maxLengthsByOrdinal.TryGetValue(i, out int max) && s.Length > max)
        {
            return s.Substring(0, max);
        }

        return s;
    }

    /// <summary>
    /// Retrieves the data at <paramref name="i"/> as an object.
    /// </summary>
    /// <param name="i">The target column.</param>
    /// <returns>An object representing the contents of the column.</returns>
    public object GetValue(int i) => this.Truncate(i, this.inner.GetValue(i));

    /// <summary>
    /// Copies the current data record to <paramref name="values"/> as an array of objects.
    /// </summary>
    /// <param name="values">The target array for insertion.</param>
    /// <returns>The array length.</returns>
    public int GetValues(object[] values)
    {
        int count = this.inner.GetValues(values);
        for (int i = 0; i < count; i++)
        {
            values[i] = this.Truncate(i, values[i]);
        }

        return count;
    }

    /// <summary>
    /// Gets a value indicating the data at index <paramref name="i"/> is null.
    /// </summary>
    /// <param name="i">The index to check.</param>
    /// <returns>Whether the field at index <paramref name="i"/> is null.</returns>
    public bool IsDBNull(int i) => this.inner.IsDBNull(i);

    /// <summary>
    /// Truncates a value given its ordinal value according to the mapping in <see cref="maxLengthsByOrdinal" />.
    /// </summary>
    /// <param name="ord">The value's ordinal.</param>
    /// <param name="value">The value to truncate.</param>
    /// <returns><paramref name="value"/>, truncated.</returns>
    private object Truncate(int ord, object value)
    {
        if (value is string s && this.maxLengthsByOrdinal.TryGetValue(ord, out int max) && s.Length > max)
        {
            return s[..max];
        }

        return value;
    }
}
