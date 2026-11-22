using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence;

/// <summary>
/// Maps a list of value-object records to one jsonb column as a plain value.
/// Preferred over <c>OwnsMany(...).ToJson()</c> for collections the aggregate reorders or replaces:
/// EF Core's owned JSON collections key elements by position and throw when a tracked list is
/// reordered ("another instance with the same key value for __synthesizedOrdinal is already being
/// tracked"). A value-converted list has no element identity, so any mutation is just a new value.
/// Records give value equality, which the comparer relies on.
/// </summary>
public static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ValueConverter<List<T>, string> Converter<T>() => new(
        list => JsonSerializer.Serialize(list, Options),
        json => string.IsNullOrWhiteSpace(json)
            ? new List<T>()
            : JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>());

    public static ValueComparer<List<T>> Comparer<T>() => new(
        (a, b) => (a ?? new List<T>()).SequenceEqual(b ?? new List<T>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());

    /// <summary>
    /// Any value (one record, or a list of records with nested lists) as jsonb. Change tracking compares
    /// the serialised JSON, so nested collections don't need value equality.
    /// </summary>
    public static ValueConverter<T, string> ValueConverter<T>() => new(
        value => JsonSerializer.Serialize(value, Options),
        json => JsonSerializer.Deserialize<T>(json, Options)!);

    public static ValueComparer<T> ValueComparer<T>() => new(
        (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
        value => JsonSerializer.Serialize(value, Options).GetHashCode(),
        value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!);
}
