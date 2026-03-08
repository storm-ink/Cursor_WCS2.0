namespace Wcs.Bs.Plc;

public class PlcMessage
{
    public Dictionary<string, string> Fields { get; set; } = new();

    public static PlcMessage Parse(string raw)
    {
        var msg = new PlcMessage();
        var content = raw.Trim().TrimStart('[').TrimEnd(']');
        var pairs = content.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var eqIdx = pair.IndexOf('=');
            if (eqIdx > 0)
            {
                var key = pair[..eqIdx].Trim();
                var value = pair[(eqIdx + 1)..].Trim();
                msg.Fields[key] = value;
            }
        }

        return msg;
    }

    public string Serialize()
    {
        var pairs = Fields.Select(kv => $"{kv.Key}={kv.Value}");
        return $"[{string.Join(";", pairs)}]";
    }

    public string GetField(string key, string defaultValue = "")
    {
        return Fields.TryGetValue(key, out var value) ? value : defaultValue;
    }
}
