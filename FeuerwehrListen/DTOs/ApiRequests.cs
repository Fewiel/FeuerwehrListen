using FeuerwehrListen.Models;

namespace FeuerwehrListen.DTOs;

public class CreateAttendanceListRequest
{
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? UnitNumber { get; set; }
}

public class CreateOperationListRequest
{
    public string OperationNumber { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public DateTime AlertTime { get; set; }
    public string? Address { get; set; }
}

public class AddAttendanceEntryRequest
{
    public string MemberNumberOrName { get; set; } = string.Empty;
    public bool IsExcused { get; set; } = false;
}

public class AddOperationEntryRequest
{
    public string MemberNumberOrName { get; set; } = string.Empty;
    public string Vehicle { get; set; } = string.Empty;
    public OperationFunction Function { get; set; }
    public bool WithBreathingApparatus { get; set; }
}

public class CreateKeywordRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateFireSafetyWatchRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime EventDateTime { get; set; }
    public List<CreateFireSafetyWatchRequirementRequest>? Requirements { get; set; }
}

public class CreateFireSafetyWatchRequirementRequest
{
    public int FunctionDefId { get; set; }
    public int Amount { get; set; }
    public int? VehicleId { get; set; }
}

public class AddFireSafetyWatchEntryRequest
{
    public string MemberNumberOrName { get; set; } = string.Empty;
    public int RequirementId { get; set; }
}

public class CreateDefectRequest
{
    public string Description { get; set; } = string.Empty;
    public int? VehicleId { get; set; }
    public string? CustomVehicle { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
}

public class UpdateDefectStatusRequest
{
    public string NewStatus { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public class CreateScheduledListRequest
{
    public ScheduledListType Type { get; set; }
    public string? Title { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public string? OperationNumber { get; set; }
    public string? Keyword { get; set; }
    public int? UnitNumber { get; set; }
    public DateTime ScheduledEventTime { get; set; }
    public int MinutesBeforeEvent { get; set; } = 30;
}


