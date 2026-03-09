namespace Wcs.Bs.Domain;

public class PipelineConfig
{
    public bool EnableDispatchFilter { get; set; } = false;
    public bool EnableDeviceTaskCompletedHandler { get; set; } = false;
    public bool EnableTaskCompletedHandler { get; set; } = false;
}
