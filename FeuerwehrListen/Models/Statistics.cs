namespace FeuerwehrListen.Models;

public class MemberStatistics
{
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberNumber { get; set; } = string.Empty;
    public int TotalAttendance { get; set; }
    public int TotalOperations { get; set; }
    public double AttendancePercentage { get; set; }
    public DateTime LastParticipation { get; set; }
    public List<MonthlyParticipation> MonthlyData { get; set; } = new();
}

public class MonthlyParticipation
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int AttendanceCount { get; set; }
    public int OperationCount { get; set; }
    public double Percentage { get; set; }
}

public class ListStatistics
{
    public int TotalLists { get; set; }
    public int OpenLists { get; set; }
    public int ClosedLists { get; set; }
    public int ArchivedLists { get; set; }
    public double AverageParticipants { get; set; }
    public int TotalParticipants { get; set; }
    public DateTime LastListCreated { get; set; }
}

public class TopParticipant
{
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberNumber { get; set; } = string.Empty;
    public int ParticipationCount { get; set; }
    public double Percentage { get; set; }
}

public class TrendData
{
    public DateTime Date { get; set; }
    public int AttendanceCount { get; set; }
    public int OperationCount { get; set; }
    public int TotalParticipants { get; set; }
}

public class VehicleStatistics
{
    public string VehicleName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public double UsagePercentage { get; set; }
    public double AverageCrew { get; set; }
}

public class FunctionStatistics
{
    public string FunctionName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class BreathingApparatusStatistics
{
    public int WithApparatus { get; set; }
    public int WithoutApparatus { get; set; }
    public double WithApparatusPercentage { get; set; }
}

public class OperationComposition
{
    public string OperationNumber { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int TotalParticipants { get; set; }
    public Dictionary<string, int> FunctionCounts { get; set; } = new();
    public Dictionary<string, int> NoVehicleFunctionCounts { get; set; } = new();
    public int WithVehicleTruppCount { get; set; }
    public int WithoutVehicleTruppCount { get; set; }
}


