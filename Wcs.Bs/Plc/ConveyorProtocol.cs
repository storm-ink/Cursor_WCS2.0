using Wcs.Bs.Domain;

namespace Wcs.Bs.Plc;

public static class ConveyorProtocol
{
    public static string BuildTaskCommand(DeviceTaskEntity dt, string palletCode, int handshake = 1)
    {
        var tuid = palletCode.PadRight(20);
        var msg = new PlcMessage();
        msg.Fields["CMD"] = "CV_TASK";
        msg.Fields["HandShake"] = handshake.ToString();
        msg.Fields["TaskNo"] = $"{dt.TaskCode}-{dt.StepOrder}";
        msg.Fields["TUID"] = tuid;
        msg.Fields["RotingNo"] = dt.RoutingNo ?? "1";
        msg.Fields["From"] = dt.SegmentSource;
        msg.Fields["To"] = dt.SegmentDest;
        return msg.Serialize();
    }

    public static CvReport ParseReport(PlcMessage msg, int taskReportCount, int locationCount)
    {
        var report = new CvReport();

        for (int i = 1; i <= taskReportCount; i++)
        {
            var prefix = $"Task{i}_";
            var hs = msg.GetField($"{prefix}HandShake");
            if (string.IsNullOrEmpty(hs)) continue;

            report.Tasks.Add(new CvTaskReport
            {
                Index = i,
                HandShake = hs,
                TaskNo = msg.GetField($"{prefix}TaskNo"),
                TUID = msg.GetField($"{prefix}TUID"),
                RotingNo = msg.GetField($"{prefix}RotingNo"),
                From = msg.GetField($"{prefix}From"),
                To = msg.GetField($"{prefix}To")
            });
        }

        for (int i = 1; i <= locationCount; i++)
        {
            var prefix = $"Loc{i}_";
            var posNo = msg.GetField($"{prefix}PosNo");
            if (string.IsNullOrEmpty(posNo)) continue;

            report.Locations.Add(new CvLocationReport
            {
                Index = i,
                PosNo = posNo,
                TaskNo = msg.GetField($"{prefix}TaskNo"),
                HaveGoods = msg.GetField($"{prefix}HaveGoods") == "1",
                Alarms = msg.GetField($"{prefix}Alarms"),
                State = msg.GetField($"{prefix}State")
            });
        }

        return report;
    }
}

public class CvReport
{
    public List<CvTaskReport> Tasks { get; set; } = new();
    public List<CvLocationReport> Locations { get; set; } = new();
}

public class CvTaskReport
{
    public int Index { get; set; }
    public string HandShake { get; set; } = "";
    public string TaskNo { get; set; } = "";
    public string TUID { get; set; } = "";
    public string RotingNo { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public class CvLocationReport
{
    public int Index { get; set; }
    public string PosNo { get; set; } = "";
    public string TaskNo { get; set; } = "";
    public bool HaveGoods { get; set; }
    public string Alarms { get; set; } = "";
    public string State { get; set; } = "";
}
