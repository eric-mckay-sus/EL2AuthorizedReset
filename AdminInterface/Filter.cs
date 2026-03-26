namespace AdminInterface;

/// <summary>
/// Container for the value and polarity of a filter
/// </summary>
/// <typeparam name="T">One of string, int, or DateTime</typeparam>
/// <param name="key">The key associated with the filter</param>
/// <param name="value">The value used in filtering</param>
/// <param name="isNegated">Whether to filter out (or filter by)</param>
public class Filter<T> : IFilter
{
    public string Key { get; set; } // The name of this filter (for self-identification)
    public bool IsActive { get; private set; } // Whether this filter is being used in the current query (thus its value should be used). Automatically updated on value change

    private T? _value; // The internal value held by the filter
    public T? Value // The methods of accessing and modifying the filter's value
    {
        get => _value;
        set
        {
            _value = value;
            IsActive = !IsDefault(value);
        }
    }

    /// <summary>
    /// Builds a filter using its key, value, and negation status (activity status is automatically determined)
    /// </summary>
    /// <param name="key">The name for the new filter</param>
    /// <param name="value">The value for which to filter</param>
    public Filter(string key, T? value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Creates a deep copy of this filter
    /// </summary>
    /// <returns>The deep copy</returns>
    public IFilter Clone() => new Filter<T>(Key, Value);

    /// <summary>
    /// Gets the value of this filter as a nullable object
    /// </summary>
    /// <returns>An object representing the generic type used by the value</returns>
    public object? GetValue() => Value;

    /// <summary>
    /// Resets this filter to its default state
    /// </summary>
    public void Reset()
    {
        Value = default!;
    }

    /// <summary>
    /// Determine if the user wishes to use this filter
    /// </summary>
    /// <param name="val">The value to check against default</param>
    /// <returns>Whether the value is its default (i.e. deactivated, and thus should not be used in a query)</returns>
    private static bool IsDefault(T? val) => val switch
    {
        string s => string.IsNullOrWhiteSpace(s), // Strings shouldn't be used if they're null OR whitespace
        _ when Equals(val, default(T)) => true, // All other datatypes only use null to denote default
        _ => false // Any value not covered by the above should be used in the query
    };

    public override string ToString()
    {
        return $"{Key}: {Value} ({(!IsDefault(Value) ? "active" : "not active")})";
    }
}

/// <summary>
/// Interface to bypass the complications of Filter's generic type
/// </summary>
public interface IFilter
{
    string Key { get; set; }
    bool IsActive { get; }
    object? GetValue();
    IFilter Clone();
    void Reset();
}
