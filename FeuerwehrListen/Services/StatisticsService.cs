using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class StatisticsService
{
    private readonly AppDbConnection _db;

    public StatisticsService(AppDbConnection db)
    {
        _db = db;
    }

    public async Task<ListStatistics> GetListStatisticsAsync()
    {
        var attendanceLists = await _db.AttendanceLists.ToListAsync();
        var operationLists = await _db.OperationLists.ToListAsync();
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();

        var allLists = attendanceLists.Count + operationLists.Count;
        var openLists = attendanceLists.Count(x => x.Status == ListStatus.Open) + 
                       operationLists.Count(x => x.Status == ListStatus.Open);
        var closedLists = attendanceLists.Count(x => x.Status == ListStatus.Closed) + 
                         operationLists.Count(x => x.Status == ListStatus.Closed);
        var archivedLists = attendanceLists.Count(x => x.IsArchived) + 
                           operationLists.Count(x => x.IsArchived);

        var totalParticipants = attendanceEntries.Count + operationEntries.Count;
        var averageParticipants = allLists > 0 ? (double)totalParticipants / allLists : 0;

        var lastCreated = new List<DateTime>();
        if (attendanceLists.Any()) lastCreated.Add(attendanceLists.Max(x => x.CreatedAt));
        if (operationLists.Any()) lastCreated.Add(operationLists.Max(x => x.CreatedAt));

        return new ListStatistics
        {
            TotalLists = allLists,
            OpenLists = openLists,
            ClosedLists = closedLists,
            ArchivedLists = archivedLists,
            AverageParticipants = Math.Round(averageParticipants, 2),
            TotalParticipants = totalParticipants,
            LastListCreated = lastCreated.Any() ? lastCreated.Max() : DateTime.MinValue
        };
    }

    public async Task<List<TopParticipant>> GetTopParticipantsAsync(int limit = 10)
    {
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();

        var memberParticipation = new Dictionary<int, int>();

        // Zähle Teilnahmen pro Mitglied
        foreach (var entry in attendanceEntries)
        {
            var memberNumber = ExtractMemberNumber(entry.NameOrId);
            var member = members.FirstOrDefault(m => m.MemberNumber == memberNumber);
            if (member != null)
            {
                if (!memberParticipation.ContainsKey(member.Id))
                    memberParticipation[member.Id] = 0;
                memberParticipation[member.Id]++;
            }
        }

        foreach (var entry in operationEntries)
        {
            var memberNumber = ExtractMemberNumber(entry.NameOrId);
            var member = members.FirstOrDefault(m => m.MemberNumber == memberNumber);
            if (member != null)
            {
                if (!memberParticipation.ContainsKey(member.Id))
                    memberParticipation[member.Id] = 0;
                memberParticipation[member.Id]++;
            }
        }

        var totalParticipation = memberParticipation.Values.Sum();

        return memberParticipation
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .Select(x =>
            {
                var member = members.First(m => m.Id == x.Key);
                return new TopParticipant
                {
                    MemberId = member.Id,
                    MemberName = $"{member.FirstName} {member.LastName}",
                    MemberNumber = member.MemberNumber,
                    ParticipationCount = x.Value,
                    Percentage = Math.Round((double)x.Value / 20 * 100, 2)
                };
            })
            .ToList();
    }

    public async Task<List<MemberStatistics>> GetMemberStatisticsAsync()
    {
        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var attendanceLists = await _db.AttendanceLists.ToListAsync();
        var operationLists = await _db.OperationLists.ToListAsync();

        var result = new List<MemberStatistics>();

        foreach (var member in members)
        {
            var memberAttendance = attendanceEntries.Count(e => 
                ExtractMemberNumber(e.NameOrId) == member.MemberNumber);
            var memberOperations = operationEntries.Count(e => 
                ExtractMemberNumber(e.NameOrId) == member.MemberNumber);

            // Berechne Teilnahmequote basierend auf der Anzahl der Teilnahmen
            // Maximaler Referenzwert: 20 Teilnahmen = 100%
            var totalParticipations = memberAttendance + memberOperations;
            var attendancePercentage = Math.Round((double)totalParticipations / 20 * 100, 2);
            if (attendancePercentage > 100) attendancePercentage = 100;

            var lastParticipation = DateTime.MinValue;
            var memberEntries = attendanceEntries.Where(e => 
                ExtractMemberNumber(e.NameOrId) == member.MemberNumber)
                .Cast<object>()
                .Concat(operationEntries.Where(e => 
                    ExtractMemberNumber(e.NameOrId) == member.MemberNumber).Cast<object>());
            
            if (memberEntries.Any())
            {
                var attendanceEntriesForMember = attendanceEntries.Where(e => 
                    ExtractMemberNumber(e.NameOrId) == member.MemberNumber);
                var operationEntriesForMember = operationEntries.Where(e => 
                    ExtractMemberNumber(e.NameOrId) == member.MemberNumber);
                
                var allEntries = attendanceEntriesForMember.Cast<object>()
                    .Concat(operationEntriesForMember.Cast<object>());
                
                lastParticipation = allEntries.Max(e => 
                    e is AttendanceEntry ae ? ae.EnteredAt : 
                    e is OperationEntry oe ? oe.EnteredAt : DateTime.MinValue);
            }

            var monthlyData = CalculateMonthlyParticipation(member.MemberNumber, 
                attendanceEntries, operationEntries, attendanceLists, operationLists);

            result.Add(new MemberStatistics
            {
                MemberId = member.Id,
                MemberName = $"{member.FirstName} {member.LastName}",
                MemberNumber = member.MemberNumber,
                TotalAttendance = memberAttendance,
                TotalOperations = memberOperations,
                AttendancePercentage = attendancePercentage,
                LastParticipation = lastParticipation,
                MonthlyData = monthlyData
            });
        }

        return result.OrderByDescending(x => x.TotalAttendance + x.TotalOperations).ToList();
    }

    public async Task<List<TrendData>> GetTrendDataAsync(int months = 12)
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-months);

        var attendanceLists = await _db.AttendanceLists
            .Where(x => x.CreatedAt >= startDate)
            .ToListAsync();
        var operationLists = await _db.OperationLists
            .Where(x => x.CreatedAt >= startDate)
            .ToListAsync();
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();

        var result = new List<TrendData>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dayAttendanceLists = attendanceLists.Where(x => x.CreatedAt.Date == date).ToList();
            var dayOperationLists = operationLists.Where(x => x.CreatedAt.Date == date).ToList();

            var attendanceCount = dayAttendanceLists.Count;
            var operationCount = dayOperationLists.Count;

            var totalParticipants = 0;
            foreach (var list in dayAttendanceLists)
            {
                totalParticipants += attendanceEntries.Count(e => e.AttendanceListId == list.Id);
            }
            foreach (var list in dayOperationLists)
            {
                totalParticipants += operationEntries.Count(e => e.OperationListId == list.Id);
            }

            result.Add(new TrendData
            {
                Date = date,
                AttendanceCount = attendanceCount,
                OperationCount = operationCount,
                TotalParticipants = totalParticipants
            });
        }

        return result;
    }

    public async Task<List<VehicleStatistics>> GetVehicleStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var vehicles = await _db.Vehicles.Where(x => x.IsActive).ToListAsync();

        // Unterscheide Einsätze per Fahrzeug und berechne durchschnittliche Besatzung
        var vehicleStats = operationEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Vehicle))
            .GroupBy(e => e.Vehicle!)
            .Select(g => new
            {
                Vehicle = g.Key,
                DistinctOperations = g.Select(e => e.OperationListId).Distinct().ToList(),
                CrewPerOperation = g.GroupBy(e => e.OperationListId).Select(og => og.Count()).ToList()
            })
            .ToList();

        var totalOperations = operationEntries.Select(e => e.OperationListId).Distinct().Count();

        return vehicleStats
            .Select(v => new VehicleStatistics
            {
                VehicleName = v.Vehicle,
                UsageCount = v.DistinctOperations.Count,
                UsagePercentage = totalOperations > 0 ? Math.Round((double)v.DistinctOperations.Count / totalOperations * 100, 2) : 0,
                // Wir speichern den Durchschnitt vorerst im Feld TotalCrewMembers als Zahl (bestehendes DTO)
                TotalCrewMembers = v.CrewPerOperation.Count > 0 ? (int)Math.Round(v.CrewPerOperation.Average(), MidpointRounding.AwayFromZero) : 0
            })
            .OrderByDescending(x => x.UsageCount)
            .ToList();
    }

    public async Task<List<FunctionStatistics>> GetFunctionStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries.ToListAsync();

        var functionCounts = operationEntries
            .GroupBy(e => e.Function)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalCount = functionCounts.Values.Sum();

        return functionCounts
            .Select(kvp => new FunctionStatistics
            {
                FunctionName = kvp.Key.ToString(),
                Count = kvp.Value,
                Percentage = Math.Round((double)kvp.Value / 15 * 100, 2)
            })
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    public async Task<BreathingApparatusStatistics> GetBreathingApparatusStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries.ToListAsync();

        var withApparatus = operationEntries.Count(e => e.WithBreathingApparatus);
        var withoutApparatus = operationEntries.Count(e => !e.WithBreathingApparatus);
        var total = operationEntries.Count;

        return new BreathingApparatusStatistics
        {
            WithApparatus = withApparatus,
            WithoutApparatus = withoutApparatus,
            WithApparatusPercentage = Math.Round((double)withApparatus / 25 * 100, 2)
        };
    }

    private static string ExtractMemberNumber(string nameOrId)
    {
        // Extrahiert Mitgliedsnummer aus "Max Mustermann (1234)"
        var match = System.Text.RegularExpressions.Regex.Match(nameOrId, @"\((\d+)\)");
        return match.Success ? match.Groups[1].Value : nameOrId;
    }

    private static List<MonthlyParticipation> CalculateMonthlyParticipation(
        string memberNumber,
        List<AttendanceEntry> attendanceEntries,
        List<OperationEntry> operationEntries,
        List<AttendanceList> attendanceLists,
        List<OperationList> operationLists)
    {
        var result = new List<MonthlyParticipation>();
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-12);

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddMonths(1))
        {
            var year = date.Year;
            var month = date.Month;

            var monthAttendanceLists = attendanceLists
                .Where(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month)
                .ToList();
            var monthOperationLists = operationLists
                .Where(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month)
                .ToList();

            var attendanceCount = 0;
            var operationCount = 0;

            foreach (var list in monthAttendanceLists)
            {
                if (attendanceEntries.Any(e => e.AttendanceListId == list.Id && 
                    ExtractMemberNumber(e.NameOrId) == memberNumber))
                {
                    attendanceCount++;
                }
            }

            foreach (var list in monthOperationLists)
            {
                if (operationEntries.Any(e => e.OperationListId == list.Id && 
                    ExtractMemberNumber(e.NameOrId) == memberNumber))
                {
                    operationCount++;
                }
            }

            var totalLists = monthAttendanceLists.Count + monthOperationLists.Count;
            var percentage = totalLists > 0 ? 
                Math.Round((double)(attendanceCount + operationCount) / totalLists * 100, 2) : 0;

            result.Add(new MonthlyParticipation
            {
                Year = year,
                Month = month,
                AttendanceCount = attendanceCount,
                OperationCount = operationCount,
                Percentage = percentage
            });
        }

        return result;
    }
}

