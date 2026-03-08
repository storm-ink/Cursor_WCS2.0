using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Infrastructure;

namespace Wcs.Bs.Services;

public class PathConfigService
{
    private readonly WcsDbContext _db;
    private readonly ILogger<PathConfigService> _logger;

    public PathConfigService(WcsDbContext db, ILogger<PathConfigService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ImportFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Path config file not found: {FilePath}", filePath);
            return;
        }

        var json = await File.ReadAllTextAsync(filePath);
        await ImportFromJsonAsync(json);
    }

    public async Task ImportFromJsonAsync(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var importData = JsonSerializer.Deserialize<PathConfigImportFile>(json, options);
        if (importData == null) return;

        if (importData.Paths?.Count > 0)
        {
            _db.PathConfigs.RemoveRange(_db.PathConfigs);
            foreach (var path in importData.Paths)
            {
                _db.PathConfigs.Add(new PathConfigEntity
                {
                    PathCode = path.PathCode,
                    Source = path.Source,
                    DestinationPattern = path.DestinationPattern,
                    ConfigJson = JsonSerializer.Serialize(path, options),
                    IsActive = true
                });
            }
        }

        if (importData.CraneReachable?.Count > 0)
        {
            _db.CraneReachableConfigs.RemoveRange(_db.CraneReachableConfigs);
            foreach (var cr in importData.CraneReachable)
            {
                _db.CraneReachableConfigs.Add(new CraneReachableConfigEntity
                {
                    DeviceCode = cr.DeviceCode,
                    ReachablePattern = cr.ReachablePattern,
                    IsActive = true
                });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Imported {PathCount} paths and {CraneCount} crane configs",
            importData.Paths?.Count ?? 0, importData.CraneReachable?.Count ?? 0);
    }

    public async Task<PathConfigJson?> MatchPathAsync(string source, string destination)
    {
        var configs = await _db.PathConfigs
            .Where(p => p.IsActive)
            .ToListAsync();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var config in configs)
        {
            if (!MatchesPattern(source, config.Source)) continue;
            if (!MatchesPattern(destination, config.DestinationPattern)) continue;

            return JsonSerializer.Deserialize<PathConfigJson>(config.ConfigJson, options);
        }

        return null;
    }

    public async Task<bool> IsDestinationReachableAsync(string deviceCode, string destination)
    {
        var configs = await _db.CraneReachableConfigs
            .Where(c => c.DeviceCode == deviceCode && c.IsActive)
            .ToListAsync();

        if (configs.Count == 0) return true;

        foreach (var config in configs)
        {
            if (IsInRange(destination, config.ReachablePattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == value) return true;
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(value, regexPattern);
        }
        if (pattern.Contains(':'))
        {
            return IsInRange(value, pattern);
        }
        return false;
    }

    private static bool IsInRange(string value, string rangePattern)
    {
        var parts = rangePattern.Split(':');
        if (parts.Length != 2) return false;

        var start = parts[0].Trim();
        var end = parts[1].Trim();

        var valueParts = value.Split('-');
        var startParts = start.Split('-');
        var endParts = end.Split('-');

        if (valueParts.Length != startParts.Length || valueParts.Length != endParts.Length)
            return false;

        for (int i = 0; i < valueParts.Length; i++)
        {
            if (int.TryParse(valueParts[i], out var v) &&
                int.TryParse(startParts[i], out var s) &&
                int.TryParse(endParts[i], out var e))
            {
                if (v < s || v > e) return false;
            }
            else
            {
                if (string.Compare(valueParts[i], startParts[i], StringComparison.Ordinal) < 0 ||
                    string.Compare(valueParts[i], endParts[i], StringComparison.Ordinal) > 0)
                    return false;
            }
        }

        return true;
    }
}
