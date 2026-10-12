using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ExactNumbers.Core;

/// <summary>Another game version's measurements, kept as they were read so they survive a save untouched.</summary>
public sealed record OtherVersion(string Version, string Json);

/// <summary>What the file held: this game version's measurements and the other versions it carried.</summary>
public sealed record MeasureLoad(MeasureStore Store, IReadOnlyList<OtherVersion> Others, int Skipped);

/// <summary>
/// The measurements file. A game update can change any function's numbers, so measurements are kept per game version:
/// {"format":1,"versions":{"version":{"seq":n,"records":[record,...]}}}, where a record is
/// {"f":furniture,"u":func,"s":stat,"k":"use"|"hour","n":count,"lo":low,"hi":high,"fill":bool,"max":bool,"seq":n,
/// "recent":[[amount,reach],...]}. The current version is written first, and two others are kept besides it.
/// </summary>
public static class MeasureFile
{
    public const int Format = 1;
    public const int MaxOtherVersions = 2;

    /// <summary>Far above a full file (every record full); a bigger file is not read.</summary>
    public const long MaxBytes = 8_000_000;

    private const string PerUse = "use";
    private const string PerHour = "hour";

    /// <returns>Null when the file is not a measurements file this mod can read; the caller sets it aside and starts fresh.</returns>
    public static MeasureLoad? Read(string json, string version)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number) || number != Format) return null;
            if (!root.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Object) return null;

            MeasureLoad? load = null;
            var others = new List<OtherVersion>();

            foreach (var entry in versions.EnumerateObject())
            {
                if (entry.Name == version)
                {
                    load = ReadVersion(entry.Value);

                    // This version's measurements in a shape the mod cannot read: starting over would overwrite them.
                    if (load == null) return null;
                }
                else if (others.Count < MaxOtherVersions && entry.Value.ValueKind == JsonValueKind.Object) others.Add(new OtherVersion(entry.Name, entry.Value.GetRawText()));
            }

            return (load ?? new MeasureLoad(new MeasureStore(), Array.Empty<OtherVersion>(), 0)) with { Others = others };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    public static string Write(MeasureStore store, string version, IReadOnlyList<OtherVersion> others)
    {
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", Format);
            writer.WriteStartObject("versions");
            writer.WritePropertyName(version);
            WriteVersion(writer, store);

            foreach (var other in others.Where(other => other.Version != version).Take(MaxOtherVersions))
            {
                writer.WritePropertyName(other.Version);
                writer.WriteRawValue(other.Json);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <returns>Null when the entry is not an object with a records array.</returns>
    private static MeasureLoad? ReadVersion(JsonElement data)
    {
        var seq = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("seq", out var seqValue) && seqValue.ValueKind == JsonValueKind.Number && seqValue.TryGetInt64(out var read) ? read : 0;
        var store = new MeasureStore(seq);
        var skipped = 0;

        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return null;

        foreach (var element in records.EnumerateArray())
        {
            if (RecordOf(element) is not { } record || !store.Restore(record)) skipped++;
        }

        return new MeasureLoad(store, Array.Empty<OtherVersion>(), skipped);
    }

    private static MeasureRecord? RecordOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (Int(element, "f") is not { } furniture || Int(element, "u") is not { } func || Text(element, "s") is not { } statName) return null;
        if (StatNamed(statName) is not { } stat || KindNamed(Text(element, "k")) is not { } kind) return null;
        if (Int(element, "n") is not { } count || Number(element, "lo") is not { } low || Number(element, "hi") is not { } high) return null;
        if (Flag(element, "fill") is not { } isFillTo || Flag(element, "max") is not { } hasEndedAtMax || Long(element, "seq") is not { } seq) return null;

        var recent = Recent(element);

        if (recent == null) return null;

        return new MeasureRecord(new MeasureKey(new MeasureSite(furniture, func), stat), kind, count, low, high, isFillTo, hasEndedAtMax, recent, seq);
    }

    private static List<MeasuredAmount>? Recent(JsonElement element)
    {
        if (!element.TryGetProperty("recent", out var list) || list.ValueKind != JsonValueKind.Array) return null;

        var recent = new List<MeasuredAmount>();

        foreach (var pair in list.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2) return null;
            if (pair[0].ValueKind != JsonValueKind.Number || pair[1].ValueKind != JsonValueKind.Number) return null;

            recent.Add(new MeasuredAmount(pair[0].GetDouble(), pair[1].GetDouble()));
        }

        return recent;
    }

    private static void WriteVersion(Utf8JsonWriter writer, MeasureStore store)
    {
        writer.WriteStartObject();
        writer.WriteNumber("seq", store.Seq);
        writer.WriteStartArray("records");

        foreach (var record in store.Records) WriteRecord(writer, record);

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRecord(Utf8JsonWriter writer, MeasureRecord record)
    {
        writer.WriteStartObject();
        writer.WriteNumber("f", record.Key.Site.Furniture);
        writer.WriteNumber("u", record.Key.Site.Func);
        writer.WriteString("s", record.Key.Stat.ToString());
        writer.WriteString("k", record.Kind == MeasureKind.PerHour ? PerHour : PerUse);
        writer.WriteNumber("n", record.Count);
        writer.WriteNumber("lo", record.Low);
        writer.WriteNumber("hi", record.High);
        writer.WriteBoolean("fill", record.IsFillTo);
        writer.WriteBoolean("max", record.HasEndedAtMax);
        writer.WriteNumber("seq", record.Seq);
        writer.WriteStartArray("recent");

        foreach (var seen in record.Recent)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(seen.Amount);
            writer.WriteNumberValue(seen.Reach);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static long? Long(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static bool? Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Stat? StatNamed(string name) => Enum.GetValues<Stat>().Cast<Stat?>().FirstOrDefault(stat => stat.ToString() == name);

    private static MeasureKind? KindNamed(string? name) => name switch
    {
        PerUse => MeasureKind.PerUse,
        PerHour => MeasureKind.PerHour,
        _ => null,
    };
}
