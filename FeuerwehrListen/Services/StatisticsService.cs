using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class StatisticsService
{
    private readonly AppDbConnection _db;
    private readonly PersonalRequirementsService _requirementsService;
    private readonly MemberRepository _memberRepo;
    private readonly UnitAssignmentService _unitAssignment;

    public StatisticsService(AppDbConnection db, PersonalRequirementsService requirementsService,
        MemberRepository memberRepo, UnitAssignmentService unitAssignment)
    {
        _db = db;
        _requirementsService = requirementsService;
        _memberRepo = memberRepo;
        _unitAssignment = unitAssignment;
    }

    // ---------- Filter-Helfer ----------

    private async Task<List<OperationList>> FilteredOperationListsAsync(StatsFilter f)
    {
        var q = _db.OperationLists.AsQueryable();
        if (f.From.HasValue) q = q.Where(o => o.AlertTime >= f.From.Value);
        if (f.To.HasValue) q = q.Where(o => o.AlertTime <= f.To.Value);
        return await q.ToListAsync();
    }

    private async Task<List<AttendanceList>> FilteredAttendanceListsAsync(StatsFilter f)
    {
        var q = _db.AttendanceLists.AsQueryable();
        if (f.From.HasValue) q = q.Where(a => a.CreatedAt >= f.From.Value);
        if (f.To.HasValue) q = q.Where(a => a.CreatedAt <= f.To.Value);
        return await q.ToListAsync();
    }

    private async Task<List<FireSafetyWatch>> FilteredFswListsAsync(StatsFilter f)
    {
        var q = _db.FireSafetyWatches.AsQueryable();
        if (f.From.HasValue) q = q.Where(w => w.EventDateTime >= f.From.Value);
        if (f.To.HasValue) q = q.Where(w => w.EventDateTime <= f.To.Value);
        return await q.ToListAsync();
    }

    private async Task<List<int>> FilteredOperationIdsAsync(StatsFilter f)
        => (await FilteredOperationListsAsync(f)).Select(o => o.Id).ToList();

    /// <summary>Erlaubte Mitgliedsnummern für eine Einheit (Multi-Unit-fähig), oder null bei „alle".</summary>
    private async Task<HashSet<string>?> AllowedMemberNumbersAsync(int unit)
    {
        if (unit <= 0) return null;
        var members = await _memberRepo.GetAllAsync();
        var unitsByMember = await _memberRepo.GetUnitsForMembersAsync(members.Select(m => m.Id));
        var allowed = new HashSet<string>();
        foreach (var m in members)
        {
            unitsByMember.TryGetValue(m.Id, out var fromTable);
            var combined = _unitAssignment.CombineUnitNumbers(m, fromTable);
            if (combined.Contains(unit))
                allowed.Add(m.MemberNumber);
        }
        return allowed;
    }

    // ---------- Auswertungen ----------

    public async Task<ListStatistics> GetListStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };

        var ops = filter.IncludeOperations ? await FilteredOperationListsAsync(filter) : new List<OperationList>();
        var atts = filter.IncludeAttendance ? await FilteredAttendanceListsAsync(filter) : new List<AttendanceList>();
        var fsws = filter.IncludeFireSafetyWatch ? await FilteredFswListsAsync(filter) : new List<FireSafetyWatch>();

        var opIds = ops.Select(o => o.Id).ToList();
        var attIds = atts.Select(a => a.Id).ToList();
        var fswIds = fsws.Select(w => w.Id).ToList();

        var operationEntryCount = opIds.Count == 0 ? 0 : await _db.OperationEntries.CountAsync(e => opIds.Contains(e.OperationListId));
        var attendanceEntryCount = attIds.Count == 0 ? 0 : await _db.AttendanceEntries.CountAsync(e => attIds.Contains(e.AttendanceListId));
        var fswEntryCount = fswIds.Count == 0 ? 0 : await _db.FireSafetyWatchEntries.CountAsync(e => fswIds.Contains(e.FireSafetyWatchId));

        var allLists = ops.Count + atts.Count + fsws.Count;
        var totalParticipants = attendanceEntryCount + operationEntryCount + fswEntryCount;
        var averageParticipants = allLists > 0 ? (double)totalParticipants / allLists : 0;

        var lastCreated = new List<DateTime>();
        if (ops.Any()) lastCreated.Add(ops.Max(o => o.CreatedAt));
        if (atts.Any()) lastCreated.Add(atts.Max(a => a.CreatedAt));
        if (fsws.Any()) lastCreated.Add(fsws.Max(w => w.EventDateTime));

        return new ListStatistics
        {
            TotalLists = allLists,
            OpenLists = ops.Count(x => x.Status == ListStatus.Open) + atts.Count(x => x.Status == ListStatus.Open) + fsws.Count(x => x.Status == ListStatus.Open),
            ClosedLists = ops.Count(x => x.Status == ListStatus.Closed) + atts.Count(x => x.Status == ListStatus.Closed) + fsws.Count(x => x.Status == ListStatus.Closed),
            ArchivedLists = ops.Count(x => x.IsArchived) + atts.Count(x => x.IsArchived) + fsws.Count(x => x.IsArchived),
            AverageParticipants = Math.Round(averageParticipants, 2),
            TotalParticipants = totalParticipants,
            LastListCreated = lastCreated.Any() ? lastCreated.Max() : DateTime.MinValue
        };
    }

    public async Task<List<TopParticipant>> GetTopParticipantsAsync(StatsFilter? filter = null, int limit = 10)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };

        var opIds = filter.IncludeOperations ? await FilteredOperationIdsAsync(filter) : new List<int>();
        var attIds = filter.IncludeAttendance ? (await FilteredAttendanceListsAsync(filter)).Select(a => a.Id).ToList() : new List<int>();
        var fswIds = filter.IncludeFireSafetyWatch ? (await FilteredFswListsAsync(filter)).Select(w => w.Id).ToList() : new List<int>();

        var attendanceEntries = attIds.Count == 0 ? new List<AttendanceEntry>() : await _db.AttendanceEntries.Where(e => attIds.Contains(e.AttendanceListId)).ToListAsync();
        var operationEntries = opIds.Count == 0 ? new List<OperationEntry>() : await _db.OperationEntries.Where(e => opIds.Contains(e.OperationListId)).ToListAsync();
        var fswEntries = fswIds.Count == 0 ? new List<FireSafetyWatchEntry>() : await _db.FireSafetyWatchEntries.Where(e => fswIds.Contains(e.FireSafetyWatchId)).ToListAsync();

        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();
        var allowed = await AllowedMemberNumbersAsync(filter.Unit);

        var totalLists = opIds.Count + attIds.Count + fswIds.Count;
        var memberByNumber = members.ToDictionary(m => m.MemberNumber, m => m);
        var memberById = members.ToDictionary(m => m.Id, m => m);
        var memberParticipation = new Dictionary<int, int>();

        foreach (var entry in attendanceEntries)
        {
            var n = ExtractMemberNumber(entry.NameOrId);
            if (memberByNumber.TryGetValue(n, out var member)) { memberParticipation.TryGetValue(member.Id, out var c); memberParticipation[member.Id] = c + 1; }
        }
        foreach (var entry in operationEntries)
        {
            var n = ExtractMemberNumber(entry.NameOrId);
            if (memberByNumber.TryGetValue(n, out var member)) { memberParticipation.TryGetValue(member.Id, out var c); memberParticipation[member.Id] = c + 1; }
        }
        foreach (var entry in fswEntries)
        {
            if (memberById.ContainsKey(entry.MemberId)) { memberParticipation.TryGetValue(entry.MemberId, out var c); memberParticipation[entry.MemberId] = c + 1; }
        }

        IEnumerable<KeyValuePair<int, int>> query = memberParticipation;
        if (allowed != null)
            query = query.Where(kv => allowed.Contains(memberById[kv.Key].MemberNumber));

        return query
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .Select(x =>
            {
                var member = memberById[x.Key];
                return new TopParticipant
                {
                    MemberId = member.Id,
                    MemberName = $"{member.FirstName} {member.LastName}",
                    MemberNumber = member.MemberNumber,
                    ParticipationCount = x.Value,
                    Percentage = totalLists > 0 ? Math.Round((double)x.Value / totalLists * 100, 1) : 0
                };
            })
            .ToList();
    }

    public async Task<List<MemberStatistics>> GetMemberStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };

        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();
        var allowed = await AllowedMemberNumbersAsync(filter.Unit);
        if (allowed != null)
            members = members.Where(m => allowed.Contains(m.MemberNumber)).ToList();

        var attendanceLists = filter.IncludeAttendance ? await FilteredAttendanceListsAsync(filter) : new List<AttendanceList>();
        var operationLists = filter.IncludeOperations ? await FilteredOperationListsAsync(filter) : new List<OperationList>();
        var attIds = attendanceLists.Select(a => a.Id).ToList();
        var opIds = operationLists.Select(o => o.Id).ToList();
        var fswIds = filter.IncludeFireSafetyWatch ? (await FilteredFswListsAsync(filter)).Select(w => w.Id).ToList() : new List<int>();

        var attendanceEntries = attIds.Count == 0 ? new List<AttendanceEntry>() : await _db.AttendanceEntries.Where(e => attIds.Contains(e.AttendanceListId)).ToListAsync();
        var operationEntries = opIds.Count == 0 ? new List<OperationEntry>() : await _db.OperationEntries.Where(e => opIds.Contains(e.OperationListId)).ToListAsync();
        var fswEntries = fswIds.Count == 0 ? new List<FireSafetyWatchEntry>() : await _db.FireSafetyWatchEntries.Where(e => fswIds.Contains(e.FireSafetyWatchId)).ToListAsync();

        var attendanceByMember = attendanceEntries.GroupBy(e => ExtractMemberNumber(e.NameOrId)).ToDictionary(g => g.Key, g => g.ToList());
        var operationByMember = operationEntries.GroupBy(e => ExtractMemberNumber(e.NameOrId)).ToDictionary(g => g.Key, g => g.ToList());
        var fswByMember = fswEntries.GroupBy(e => e.MemberId).ToDictionary(g => g.Key, g => g.Count());

        var result = new List<MemberStatistics>();
        foreach (var member in members)
        {
            var memberAttendanceEntries = attendanceByMember.GetValueOrDefault(member.MemberNumber, new List<AttendanceEntry>());
            var memberOperationEntries = operationByMember.GetValueOrDefault(member.MemberNumber, new List<OperationEntry>());
            var memberFswCount = fswByMember.GetValueOrDefault(member.Id, 0);

            var memberAttendance = memberAttendanceEntries.Count;
            var memberOperations = memberOperationEntries.Count;
            var totalParticipations = memberAttendance + memberOperations + memberFswCount;

            var lastParticipation = DateTime.MinValue;
            var dates = new List<DateTime>();
            if (memberAttendanceEntries.Any()) dates.Add(memberAttendanceEntries.Max(e => e.EnteredAt));
            if (memberOperationEntries.Any()) dates.Add(memberOperationEntries.Max(e => e.EnteredAt));
            if (dates.Any()) lastParticipation = dates.Max();

            var monthlyData = CalculateMonthlyParticipation(memberAttendanceEntries, memberOperationEntries, attendanceLists, operationLists);

            result.Add(new MemberStatistics
            {
                MemberId = member.Id,
                MemberName = $"{member.FirstName} {member.LastName}",
                MemberNumber = member.MemberNumber,
                TotalAttendance = memberAttendance,
                TotalOperations = memberOperations,
                AttendancePercentage = totalParticipations,
                LastParticipation = lastParticipation,
                MonthlyData = monthlyData
            });
        }

        return result.OrderByDescending(x => x.TotalAttendance + x.TotalOperations).ToList();
    }

    public async Task<List<VehicleStatistics>> GetVehicleStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new List<VehicleStatistics>();

        var opIds = await FilteredOperationIdsAsync(filter);
        if (opIds.Count == 0) return new List<VehicleStatistics>();

        var operationEntries = await _db.OperationEntries
            .Where(e => opIds.Contains(e.OperationListId) && !string.IsNullOrWhiteSpace(e.Vehicle))
            .ToListAsync();
        if (!operationEntries.Any()) return new List<VehicleStatistics>();

        var vehicleStats = operationEntries
            .GroupBy(e => e.Vehicle!)
            .Select(g => new { Vehicle = g.Key, UsageCount = g.Select(e => e.OperationListId).Distinct().Count(), TotalCrew = g.Count() })
            .ToList();

        var totalVehicleUsages = vehicleStats.Sum(s => s.UsageCount);

        return vehicleStats
            .Select(v => new VehicleStatistics
            {
                VehicleName = v.Vehicle,
                UsageCount = v.UsageCount,
                UsagePercentage = totalVehicleUsages > 0 ? Math.Round((double)v.UsageCount / totalVehicleUsages * 100, 1) : 0,
                AverageCrew = v.UsageCount > 0 ? Math.Round((double)v.TotalCrew / v.UsageCount, 1) : 0
            })
            .OrderByDescending(x => x.UsageCount)
            .ToList();
    }

    public async Task<List<FunctionStatistics>> GetFunctionStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new List<FunctionStatistics>();

        var opIds = await FilteredOperationIdsAsync(filter);
        var operationEntries = opIds.Count == 0 ? new List<OperationEntry>() : await _db.OperationEntries.Where(e => opIds.Contains(e.OperationListId)).ToListAsync();
        var totalOperationParticipants = operationEntries.Count;
        if (totalOperationParticipants == 0) return new List<FunctionStatistics>();

        var entryIds = operationEntries.Select(e => e.Id).ToList();
        var functionLinks = await _db.OperationEntryFunctions.Where(f => entryIds.Contains(f.OperationEntryId)).ToListAsync();
        var functionDefs = await _db.OperationFunctionDefs.ToListAsync();

        var functionCounts = functionLinks.GroupBy(f => f.FunctionDefId).Select(g => new { FunctionId = g.Key, Count = g.Count() }).ToList();

        var result = new List<FunctionStatistics>();
        foreach (var func in functionCounts)
        {
            var functionDef = functionDefs.FirstOrDefault(d => d.Id == func.FunctionId);
            if (functionDef != null)
                result.Add(new FunctionStatistics { FunctionName = functionDef.Name, Count = func.Count, Percentage = Math.Round((double)func.Count / totalOperationParticipants * 100, 1) });
        }

        var entryIdsWithFunctions = functionLinks.Select(f => f.OperationEntryId).Distinct().ToHashSet();
        var truppCount = operationEntries.Count(e => !entryIdsWithFunctions.Contains(e.Id));
        if (truppCount > 0)
            result.Add(new FunctionStatistics { FunctionName = "Trupp", Count = truppCount, Percentage = Math.Round((double)truppCount / totalOperationParticipants * 100, 1) });

        return result.OrderByDescending(s => s.Count).ToList();
    }

    public async Task<BreathingApparatusStatistics> GetBreathingApparatusStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new BreathingApparatusStatistics();

        var opIds = await FilteredOperationIdsAsync(filter);
        var operationEntries = opIds.Count == 0 ? new List<OperationEntry>() : await _db.OperationEntries.Where(e => opIds.Contains(e.OperationListId)).ToListAsync();

        var atemschutzFunctions = await _db.OperationFunctionDefs.Where(f => f.Name.Contains("Atemschutz") || f.Name.Contains("AGT")).ToListAsync();

        int withApparatus = 0;
        if (atemschutzFunctions.Any() && operationEntries.Any())
        {
            var functionIds = atemschutzFunctions.Select(f => f.Id).ToList();
            var entryIds = operationEntries.Select(e => e.Id).ToList();
            withApparatus = await _db.OperationEntryFunctions.CountAsync(f => functionIds.Contains(f.FunctionDefId) && entryIds.Contains(f.OperationEntryId));
        }

        var totalParticipants = operationEntries.Count;
        var withoutApparatus = totalParticipants - withApparatus;

        return new BreathingApparatusStatistics
        {
            WithApparatus = withApparatus,
            WithoutApparatus = withoutApparatus,
            WithApparatusPercentage = totalParticipants > 0 ? Math.Round((double)withApparatus / totalParticipants * 100, 1) : 0
        };
    }

    public async Task<List<OperationComposition>> GetOperationCompositionAsync(StatsFilter? filter = null, int limit = 15)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new List<OperationComposition>();

        var q = _db.OperationLists.AsQueryable();
        if (filter.From.HasValue) q = q.Where(o => o.AlertTime >= filter.From.Value);
        if (filter.To.HasValue) q = q.Where(o => o.AlertTime <= filter.To.Value);
        var recentOperations = await q.OrderByDescending(o => o.AlertTime).Take(limit).ToListAsync();

        if (!recentOperations.Any()) return new List<OperationComposition>();

        var operationIds = recentOperations.Select(o => o.Id).ToList();
        var entries = await _db.OperationEntries.Where(e => operationIds.Contains(e.OperationListId)).ToListAsync();
        var entryIds = entries.Select(e => e.Id).ToList();
        var functionLinks = await _db.OperationEntryFunctions.Where(ef => entryIds.Contains(ef.OperationEntryId)).ToListAsync();

        var functionDefs = await _db.OperationFunctionDefs.ToListAsync();
        var functionDefMap = functionDefs.ToDictionary(f => f.Id, f => f.Name);

        var keywordIds = recentOperations.Where(o => o.KeywordId.HasValue).Select(o => o.KeywordId!.Value).Distinct().ToList();
        var allRequirements = new Dictionary<int, List<PersonalRequirement>>();
        foreach (var kwId in keywordIds)
            allRequirements[kwId] = await _requirementsService.GetRequirementsForKeywordAsync(kwId);

        var result = new List<OperationComposition>();

        foreach (var op in recentOperations.OrderBy(o => o.AlertTime))
        {
            var opEntries = entries.Where(e => e.OperationListId == op.Id).ToList();
            var totalParticipants = opEntries.Count;

            var composition = new OperationComposition
            {
                OperationNumber = op.OperationNumber,
                Keyword = op.Keyword ?? string.Empty,
                KeywordId = op.KeywordId,
                Address = op.Address ?? string.Empty,
                TotalParticipants = totalParticipants,
                FunctionCounts = functionDefs.ToDictionary(f => f.Name, f => 0),
                NoVehicleFunctionCounts = functionDefs.ToDictionary(f => f.Name, f => 0)
            };

            // Requirements NUR wenn für das Stichwort auch tatsächlich Requirements gesetzt sind.
            var hasRequirements = op.KeywordId.HasValue
                && allRequirements.TryGetValue(op.KeywordId.Value, out var reqsForOp) && reqsForOp.Any();
            if (hasRequirements)
            {
                var validationResult = await _requirementsService.ValidateRequirementsAsync(op.Id, op.KeywordId!.Value);
                composition.HasPersonalRequirements = true;
                composition.RequirementsFulfilled = validationResult.IsValid;

                var reqs = allRequirements[op.KeywordId.Value];
                var totalReqs = reqs.Count;
                var metReqs = totalReqs - validationResult.MissingRequirements.Count;
                composition.RequirementsFulfillmentRate = Math.Round((double)Math.Max(0, metReqs) / totalReqs * 100, 1);
            }

            var entriesWithVehicle = opEntries.Where(e => !string.IsNullOrWhiteSpace(e.Vehicle)).ToList();
            var entriesWithoutVehicle = opEntries.Where(e => string.IsNullOrWhiteSpace(e.Vehicle)).ToList();

            var entryIdsWithVehicle = entriesWithVehicle.Select(e => e.Id).ToHashSet();
            var entryIdsWithoutVehicle = entriesWithoutVehicle.Select(e => e.Id).ToHashSet();

            var functionsWithVehicle = functionLinks.Where(fl => entryIdsWithVehicle.Contains(fl.OperationEntryId));
            var functionsWithoutVehicle = functionLinks.Where(fl => entryIdsWithoutVehicle.Contains(fl.OperationEntryId));

            var countsWithVehicle = functionsWithVehicle.GroupBy(fl => fl.FunctionDefId).ToDictionary(g => g.Key, g => g.Count());
            var countsWithoutVehicle = functionsWithoutVehicle.GroupBy(fl => fl.FunctionDefId).ToDictionary(g => g.Key, g => g.Count());

            foreach (var (functionId, count) in countsWithVehicle)
                if (functionDefMap.TryGetValue(functionId, out var functionName)) composition.FunctionCounts[functionName] = count;
            foreach (var (functionId, count) in countsWithoutVehicle)
                if (functionDefMap.TryGetValue(functionId, out var functionName)) composition.NoVehicleFunctionCounts[functionName] = count;

            var entryIdsWithFunctionsWithVehicle = functionsWithVehicle.Select(f => f.OperationEntryId).Distinct().ToHashSet();
            var entryIdsWithFunctionsWithoutVehicle = functionsWithoutVehicle.Select(f => f.OperationEntryId).Distinct().ToHashSet();

            composition.WithVehicleTruppCount = entriesWithVehicle.Count(e => !entryIdsWithFunctionsWithVehicle.Contains(e.Id));
            composition.WithoutVehicleTruppCount = entriesWithoutVehicle.Count(e => !entryIdsWithFunctionsWithoutVehicle.Contains(e.Id));

            result.Add(composition);
        }

        return result;
    }

    public async Task<List<TrendData>> GetTrendDataAsync(int months = 12)
    {
        var endDate = DateTime.Now;
        var startDate = endDate.AddMonths(-months);

        var attendanceLists = await _db.AttendanceLists.Where(x => x.CreatedAt >= startDate).ToListAsync();
        var operationLists = await _db.OperationLists.Where(x => x.CreatedAt >= startDate).ToListAsync();

        var attendanceListIds = attendanceLists.Select(l => l.Id).ToHashSet();
        var operationListIds = operationLists.Select(l => l.Id).ToHashSet();

        var attendanceEntries = await _db.AttendanceEntries.Where(e => attendanceListIds.Contains(e.AttendanceListId)).ToListAsync();
        var operationEntries = await _db.OperationEntries.Where(e => operationListIds.Contains(e.OperationListId)).ToListAsync();

        var attendanceEntryCountByList = attendanceEntries.GroupBy(e => e.AttendanceListId).ToDictionary(g => g.Key, g => g.Count());
        var operationEntryCountByList = operationEntries.GroupBy(e => e.OperationListId).ToDictionary(g => g.Key, g => g.Count());

        var result = new List<TrendData>();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dayAttendanceLists = attendanceLists.Where(x => x.CreatedAt.Date == date).ToList();
            var dayOperationLists = operationLists.Where(x => x.CreatedAt.Date == date).ToList();

            var totalParticipants = 0;
            foreach (var list in dayAttendanceLists) totalParticipants += attendanceEntryCountByList.GetValueOrDefault(list.Id, 0);
            foreach (var list in dayOperationLists) totalParticipants += operationEntryCountByList.GetValueOrDefault(list.Id, 0);

            result.Add(new TrendData
            {
                Date = date,
                AttendanceCount = dayAttendanceLists.Count,
                OperationCount = dayOperationLists.Count,
                TotalParticipants = totalParticipants
            });
        }
        return result;
    }

    private static string ExtractMemberNumber(string nameOrId)
    {
        var match = System.Text.RegularExpressions.Regex.Match(nameOrId, @"\((\d+)\)");
        return match.Success ? match.Groups[1].Value : nameOrId;
    }

    private static List<MonthlyParticipation> CalculateMonthlyParticipation(
        List<AttendanceEntry> memberAttendanceEntries,
        List<OperationEntry> memberOperationEntries,
        List<AttendanceList> attendanceLists,
        List<OperationList> operationLists)
    {
        var result = new List<MonthlyParticipation>();
        var endDate = DateTime.Now;
        var startDate = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-12);

        var memberAttendanceListIds = memberAttendanceEntries.Select(e => e.AttendanceListId).ToHashSet();
        var memberOperationListIds = memberOperationEntries.Select(e => e.OperationListId).ToHashSet();

        for (var date = startDate; date <= endDate; date = date.AddMonths(1))
        {
            var year = date.Year;
            var month = date.Month;

            var monthAttendanceCount = attendanceLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month && memberAttendanceListIds.Contains(x.Id));
            var monthOperationCount = operationLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month && memberOperationListIds.Contains(x.Id));

            var totalListsInMonth = attendanceLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month)
                + operationLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month);

            var percentage = totalListsInMonth > 0 ? Math.Round((double)(monthAttendanceCount + monthOperationCount) / totalListsInMonth * 100, 2) : 0;

            result.Add(new MonthlyParticipation { Year = year, Month = month, AttendanceCount = monthAttendanceCount, OperationCount = monthOperationCount, Percentage = percentage });
        }

        return result;
    }

    public async Task<List<KeywordStatistics>> GetKeywordStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new List<KeywordStatistics>();

        var operationLists = await FilteredOperationListsAsync(filter);
        var keywords = await _db.Keywords.Where(k => k.IsActive).ToListAsync();

        var totalOperations = operationLists.Count;
        if (totalOperations == 0) return new List<KeywordStatistics>();

        var keywordUsage = operationLists.Where(o => o.KeywordId.HasValue).GroupBy(o => o.KeywordId!.Value).ToDictionary(g => g.Key, g => g.Count());

        var result = new List<KeywordStatistics>();
        foreach (var keyword in keywords)
        {
            var usageCount = keywordUsage.GetValueOrDefault(keyword.Id, 0);
            var operationsWithKeyword = operationLists.Where(o => o.KeywordId == keyword.Id).ToList();

            var definedRequirements = await _requirementsService.GetRequirementsForKeywordAsync(keyword.Id);
            var hasRequirements = definedRequirements.Any();

            var operationsWithRequirements = 0;
            var operationsFulfillingRequirements = 0;
            if (hasRequirements)
            {
                foreach (var operation in operationsWithKeyword)
                {
                    var validation = await _requirementsService.ValidateRequirementsAsync(operation.Id, keyword.Id);
                    operationsWithRequirements++;
                    if (validation.IsValid) operationsFulfillingRequirements++;
                }
            }

            var requirementsFulfillmentRate = operationsWithRequirements > 0 ? Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1) : 0;

            result.Add(new KeywordStatistics
            {
                KeywordName = keyword.Name,
                UsageCount = usageCount,
                UsagePercentage = Math.Round((double)usageCount / totalOperations * 100, 1),
                TotalOperations = operationsWithKeyword.Count,
                OperationsWithRequirements = operationsWithRequirements,
                OperationsFulfillingRequirements = operationsFulfillingRequirements,
                RequirementsFulfillmentRate = requirementsFulfillmentRate
            });
        }

        return result.OrderByDescending(k => k.UsageCount).ToList();
    }

    public async Task<PersonalRequirementsStatistics> GetPersonalRequirementsStatisticsAsync(StatsFilter? filter = null)
    {
        filter ??= new StatsFilter { ListType = StatListType.All };
        if (!filter.IncludeOperations) return new PersonalRequirementsStatistics();

        var operationLists = await FilteredOperationListsAsync(filter);
        var keywords = await _db.Keywords.Where(k => k.IsActive).ToListAsync();

        var operationsWithKeywords = operationLists.Count(o => o.KeywordId.HasValue);

        var operationsWithRequirements = 0;
        var operationsFulfillingRequirements = 0;
        var keywordSummaries = new List<KeywordRequirementsSummary>();

        foreach (var keyword in keywords)
        {
            var operationsWithKeyword = operationLists.Where(o => o.KeywordId == keyword.Id).ToList();
            var definedRequirements = await _requirementsService.GetRequirementsForKeywordAsync(keyword.Id);
            var hasRequirements = definedRequirements.Any();

            var keywordOperationsWithRequirements = 0;
            var keywordOperationsFulfillingRequirements = 0;
            var functionSummaries = new Dictionary<string, FunctionRequirementSummary>();

            if (hasRequirements)
            {
                foreach (var operation in operationsWithKeyword)
                {
                    var validation = await _requirementsService.ValidateRequirementsAsync(operation.Id, keyword.Id);
                    operationsWithRequirements++;
                    keywordOperationsWithRequirements++;
                    if (validation.IsValid) { operationsFulfillingRequirements++; keywordOperationsFulfillingRequirements++; }

                    foreach (var missing in validation.MissingRequirements)
                    {
                        if (!functionSummaries.ContainsKey(missing.FunctionName))
                        {
                            functionSummaries[missing.FunctionName] = new FunctionRequirementSummary
                            {
                                FunctionName = missing.FunctionName,
                                RequiredCount = missing.RequiredCount,
                                ActualCount = missing.CurrentCount,
                                MissingCount = missing.RequiredCount - missing.CurrentCount,
                                IsRequired = missing.IsRequired,
                                FulfillmentRate = 0
                            };
                        }
                        var summary = functionSummaries[missing.FunctionName];
                        summary.ActualCount = Math.Max(summary.ActualCount, missing.CurrentCount);
                        summary.MissingCount = Math.Max(summary.MissingCount, missing.RequiredCount - missing.CurrentCount);
                        summary.FulfillmentRate = summary.RequiredCount > 0 ? Math.Round((double)summary.ActualCount / summary.RequiredCount * 100, 1) : 0;
                    }
                }
            }

            // Nur Stichwörter mit tatsächlich gesetzten Requirements aufnehmen.
            if (keywordOperationsWithRequirements == 0) continue;

            var keywordFulfillmentRate = Math.Round((double)keywordOperationsFulfillingRequirements / keywordOperationsWithRequirements * 100, 1);

            keywordSummaries.Add(new KeywordRequirementsSummary
            {
                KeywordName = keyword.Name,
                OperationsCount = operationsWithKeyword.Count,
                RequirementsDefined = keywordOperationsWithRequirements,
                RequirementsFulfilled = keywordOperationsFulfillingRequirements,
                FulfillmentRate = keywordFulfillmentRate,
                FunctionSummaries = functionSummaries.Values.ToList()
            });
        }

        var overallFulfillmentRate = operationsWithRequirements > 0 ? Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1) : 0;

        return new PersonalRequirementsStatistics
        {
            // "nur Einsätze die ein Requirement gesetzt haben" fließen ein:
            TotalOperations = operationsWithRequirements,
            OperationsWithKeywords = operationsWithKeywords,
            OperationsWithRequirements = operationsWithRequirements,
            OperationsFulfillingRequirements = operationsFulfillingRequirements,
            RequirementsFulfillmentRate = overallFulfillmentRate,
            KeywordSummaries = keywordSummaries.OrderByDescending(k => k.OperationsCount).ToList()
        };
    }
}
