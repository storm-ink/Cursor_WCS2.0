namespace Wcs.Bs.Domain;

public enum TaskSource
{
    Manual = 0,
    Wms = 1
}

public enum TaskType
{
    Inbound = 1,
    Outbound = 2,
    Transfer = 3
}

public enum TaskStatus
{
    Created = 1,
    SendingToPlc = 2,
    Running = 3,
    Finished = 4,
    Error = 5,
    Cancelled = 6
}

public enum DeviceTaskStatus
{
    Waiting = 1,
    SendingToPlc = 2,
    Running = 3,
    Finished = 4,
    Error = 5
}

public enum DeviceType
{
    Conveyor = 1,
    Crane = 2
}

public enum LogCategory
{
    Api,
    Plc,
    Task,
    Device,
    System
}
