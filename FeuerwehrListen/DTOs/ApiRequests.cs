using FeuerwehrListen.Models;

namespace FeuerwehrListen.DTOs;

public class CreateAttendanceListRequest
{
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CreateOperationListRequest
{
    public string OperationNumber { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public DateTime AlertTime { get; set; }
}

public class AddAttendanceEntryRequest
{
    public string MemberNumberOrName { get; set; } = string.Empty;
}

public class AddOperationEntryRequest
{
    public string MemberNumberOrName { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;
    public OperationFunction Function { get; set; }
    public bool WithBreathingApparatus { get; set; }
}

public class CreateScheduledListRequest
{
    public ScheduledListType Type { get; set; }
    public string? Title { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? OperationNumber { get; set; }
    public string? Keyword { get; set; }
    public DateTime ScheduledEventTime { get; set; }
    public int MinutesBeforeEvent { get; set; } = 30;
}


