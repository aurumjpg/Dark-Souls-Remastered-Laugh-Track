using System.Text.Json;
using DSLaughTrack.Logging;

namespace DSLaughTrack;

/// Discovered animation IDs. Values come ONLY from animation_ids.json, which records
/// provenance (capture date, game version, method) for every entry. See VERIFICATION.md.
public sealed class AnimationIds
{
    private readonly Dictionary<string, int> _ids;

    public AnimationIds(Dictionary<string, int> ids) =>
        _ids = new Dictionary<string, int>(ids, StringComparer.OrdinalIgnoreCase);

    public int? Get(string key) => _ids.TryGetValue(key, out var v) ? v : null;

    public static AnimationIds Load(string path, Log log)
    {
        var result = new Dictionary<string, int>();
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
                    if (prop.Value.TryGetProperty("value", out var v) && v.TryGetInt32(out var id))
                        result[prop.Name] = id;
        }
        catch (JsonException ex)
        {
            log.Error($"animation ids file is malformed, ignoring it: {ex.Message}");
        }
        return new AnimationIds(result);
    }
}
