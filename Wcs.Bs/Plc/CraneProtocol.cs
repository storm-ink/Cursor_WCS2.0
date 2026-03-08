using Wcs.Bs.Domain;

namespace Wcs.Bs.Plc;

public static class CraneProtocol
{
    public static string BuildTaskCommand(DeviceTaskEntity dt, int cmd = 1)
    {
        var msg = new PlcMessage();
        msg.Fields["CMD"] = "CRANE_TASK";
        msg.Fields["Cmd"] = cmd.ToString();
        msg.Fields["EquipmentTaskId"] = $"{dt.TaskCode}-{dt.StepOrder}";

        var sourceParts = ParseLocation(dt.SegmentSource);
        var destParts = ParseLocation(dt.SegmentDest);

        if (sourceParts.IsShelfLocation)
        {
            msg.Fields["PickCVNO"] = "";
            msg.Fields["ForkPickRow"] = sourceParts.Row;
            msg.Fields["ForkPickColumn"] = sourceParts.Column;
            msg.Fields["ForkPickLevel"] = sourceParts.Level;
        }
        else
        {
            msg.Fields["PickCVNO"] = dt.SegmentSource;
            msg.Fields["ForkPickRow"] = "";
            msg.Fields["ForkPickColumn"] = "";
            msg.Fields["ForkPickLevel"] = "";
        }

        if (destParts.IsShelfLocation)
        {
            msg.Fields["PutCVNO"] = "";
            msg.Fields["ForkPutRow"] = destParts.Row;
            msg.Fields["ForkPutColumn"] = destParts.Column;
            msg.Fields["ForkPutLevel"] = destParts.Level;
        }
        else
        {
            msg.Fields["PutCVNO"] = dt.SegmentDest;
            msg.Fields["ForkPutRow"] = "";
            msg.Fields["ForkPutColumn"] = "";
            msg.Fields["ForkPutLevel"] = "";
        }

        return msg.Serialize();
    }

    public static CraneReport ParseReport(PlcMessage msg)
    {
        return new CraneReport
        {
            DeviceNo = msg.GetField("DeviceNo"),
            EquipmentTaskId = msg.GetField("EquipmentTaskId"),
            TaskState = msg.GetField("TaskState"),
            DeviceState = msg.GetField("DeviceState"),
            IsLoaded = msg.GetField("IsLoaded") == "1",
            XColumn = msg.GetField("XColumn"),
            YLevel = msg.GetField("YLevel"),
            ZRow = msg.GetField("ZRow")
        };
    }

    private static LocationParts ParseLocation(string location)
    {
        var parts = location.Split('-');
        if (parts.Length == 3)
        {
            return new LocationParts
            {
                IsShelfLocation = true,
                Row = parts[0],
                Column = parts[1],
                Level = parts[2]
            };
        }
        return new LocationParts { IsShelfLocation = false };
    }

    private class LocationParts
    {
        public bool IsShelfLocation { get; set; }
        public string Row { get; set; } = "";
        public string Column { get; set; } = "";
        public string Level { get; set; } = "";
    }
}

public class CraneReport
{
    public string DeviceNo { get; set; } = "";
    public string EquipmentTaskId { get; set; } = "";
    public string TaskState { get; set; } = "";
    public string DeviceState { get; set; } = "";
    public bool IsLoaded { get; set; }
    public string XColumn { get; set; } = "";
    public string YLevel { get; set; } = "";
    public string ZRow { get; set; } = "";
}
