using System.Text.Json;
using DSLaughTrack.Logging;

namespace DSLaughTrack;

/// Discovered animation IDs. Values come ONLY from animation_ids.json, which records
/// provenance (capture date, game version, method) for every entry. See VERIFICATION.md.
public sealed class AnimationIds
{
    private readonly Dictionary<string, List<int>> _ids;

    public AnimationIds(Dictionary<string, int> ids) =>
        _ids = new Dictionary<string, List<int>>(
            ids.ToDictionary(kv => kv.Key, kv => new List<int> { kv.Value }),
            StringComparer.OrdinalIgnoreCase);

    private AnimationIds(Dictionary<string, List<int>> ids) =>
        _ids = new Dictionary<string, List<int>>(ids, StringComparer.OrdinalIgnoreCase);

    public int? Get(string key) => _ids.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : null;

    public IReadOnlyList<int> GetAll(string key) =>
        _ids.TryGetValue(key, out var v) ? v : Array.Empty<int>();

    public static AnimationIds Load(string path, Log log)
    {
        var result = new Dictionary<string, List<int>>();
        if (!File.Exists(path))
        {
            log.Warn($"animation ids file not found ({path}); all animation-based triggers will be disabled.");
            return new AnimationIds(result);
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ids", out var ids))
                foreach (var prop in ids.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("values", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var values = arr.EnumerateArray()
                            .Where(e => e.TryGetInt32(out _))
                            .Select(e => e.GetInt32())
                            .ToList();
                        if (values.Count > 0)
                            result[prop.Name] = values;
                    }
                    else if (prop.Value.TryGetProperty("value", out var v) && v.TryGetInt32(out var id))
                    {
                        result[prop.Name] = new List<int> { id };
                    }
                }
        }
        catch (JsonException ex)
        {
            log.Error($"animation ids file is malformed, ignoring it: {ex.Message}");
        }
        return new AnimationIds(result);
    }
}
