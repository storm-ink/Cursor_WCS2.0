namespace Wcs.Bs.Domain;

public class PathConfigJson
{
    public string PathCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string DestinationPattern { get; set; } = string.Empty;
    public List<PathStepConfig> Steps { get; set; } = new();
}

public class PathStepConfig
{
    public int StepOrder { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string? RoutingNo { get; set; }
    public string SegmentSource { get; set; } = string.Empty;
    public string SegmentDest { get; set; } = string.Empty;
}

public class PathConfigImportFile
{
    public List<PathConfigJson> Paths { get; set; } = new();
    public List<CraneReachableImport> CraneReachable { get; set; } = new();
}

public class CraneReachableImport
{
    public string DeviceCode { get; set; } = string.Empty;
    public string ReachablePattern { get; set; } = string.Empty;
}
