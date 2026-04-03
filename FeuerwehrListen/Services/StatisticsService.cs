using FeuerwehrListen.Data;
using FeuerwehrListen.Models;
using FeuerwehrListen.Repositories;
using LinqToDB;

namespace FeuerwehrListen.Services;

public class StatisticsService
{
    private readonly AppDbConnection _db;
    private readonly PersonalRequirementsService _requirementsService;

    public StatisticsService(AppDbConnection db, PersonalRequirementsService requirementsService)
    {
        _db = db;
        _requirementsService = requirementsService;
    }

    public async Task<ListStatistics> GetListStatisticsAsync()
    {
        // Use server-side counts instead of loading entire tables
        var attendanceListCount = await _db.AttendanceLists.CountAsync();
        var operationListCount = await _db.OperationLists.CountAsync();
        var fswListCount = await _db.FireSafetyWatches.CountAsync();

        var openAttendance = await _db.AttendanceLists.CountAsync(x => x.Status == ListStatus.Open);
        var openOperations = await _db.OperationLists.CountAsync(x => x.Status == ListStatus.Open);
        var openFsw = await _db.FireSafetyWatches.CountAsync(x => x.Status == ListStatus.Open);

        var closedAttendance = await _db.AttendanceLists.CountAsync(x => x.Status == ListStatus.Closed);
        var closedOperations = await _db.OperationLists.CountAsync(x => x.Status == ListStatus.Closed);
        var closedFsw = await _db.FireSafetyWatches.CountAsync(x => x.Status == ListStatus.Closed);

        var archivedAttendance = await _db.AttendanceLists.CountAsync(x => x.IsArchived);
        var archivedOperations = await _db.OperationLists.CountAsync(x => x.IsArchived);
        var archivedFsw = await _db.FireSafetyWatches.CountAsync(x => x.IsArchived);

        var attendanceEntryCount = await _db.AttendanceEntries.CountAsync();
        var operationEntryCount = await _db.OperationEntries.CountAsync();
        var fswEntryCount = await _db.FireSafetyWatchEntries.CountAsync();

        var allLists = attendanceListCount + operationListCount + fswListCount;
        var totalParticipants = attendanceEntryCount + operationEntryCount + fswEntryCount;
        var averageParticipants = allLists > 0 ? (double)totalParticipants / allLists : 0;

        var lastCreated = new List<DateTime>();
        if (attendanceListCount > 0)
            lastCreated.Add(await _db.AttendanceLists.MaxAsync(x => x.CreatedAt));
        if (operationListCount > 0)
            lastCreated.Add(await _db.OperationLists.MaxAsync(x => x.CreatedAt));
        if (fswListCount > 0)
            lastCreated.Add(await _db.FireSafetyWatches.MaxAsync(x => x.EventDateTime));

        return new ListStatistics
        {
            TotalLists = allLists,
            OpenLists = openAttendance + openOperations + openFsw,
            ClosedLists = closedAttendance + closedOperations + closedFsw,
            ArchivedLists = archivedAttendance + archivedOperations + archivedFsw,
            AverageParticipants = Math.Round(averageParticipants, 2),
            TotalParticipants = totalParticipants,
            LastListCreated = lastCreated.Any() ? lastCreated.Max() : DateTime.MinValue
        };
    }

    public async Task<List<TopParticipant>> GetTopParticipantsAsync(int limit = 10)
    {
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var fswEntries = await _db.FireSafetyWatchEntries.ToListAsync();
        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();

        var totalLists = await _db.AttendanceLists.CountAsync()
                       + await _db.OperationLists.CountAsync()
                       + await _db.FireSafetyWatches.CountAsync();

        // Pre-build member number lookup
        var memberByNumber = members.ToDictionary(m => m.MemberNumber, m => m);
        var memberById = members.ToDictionary(m => m.Id, m => m);

        var memberParticipation = new Dictionary<int, int>();

        // Count attendance + operation entries (by member number extraction)
        foreach (var entry in attendanceEntries)
        {
            var memberNumber = ExtractMemberNumber(entry.NameOrId);
            if (memberByNumber.TryGetValue(memberNumber, out var member))
            {
                memberParticipation.TryGetValue(member.Id, out var count);
                memberParticipation[member.Id] = count + 1;
            }
        }

        foreach (var entry in operationEntries)
        {
            var memberNumber = ExtractMemberNumber(entry.NameOrId);
            if (memberByNumber.TryGetValue(memberNumber, out var member))
            {
                memberParticipation.TryGetValue(member.Id, out var count);
                memberParticipation[member.Id] = count + 1;
            }
        }

        // Count FSW entries (by MemberId directly)
        foreach (var entry in fswEntries)
        {
            if (memberById.ContainsKey(entry.MemberId))
            {
                memberParticipation.TryGetValue(entry.MemberId, out var count);
                memberParticipation[entry.MemberId] = count + 1;
            }
        }

        return memberParticipation
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

    public async Task<List<MemberStatistics>> GetMemberStatisticsAsync()
    {
        var members = await _db.Members.Where(x => x.IsActive).ToListAsync();
        var attendanceEntries = await _db.AttendanceEntries.ToListAsync();
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var fswEntries = await _db.FireSafetyWatchEntries.ToListAsync();
        var attendanceLists = await _db.AttendanceLists.ToListAsync();
        var operationLists = await _db.OperationLists.ToListAsync();

        // Pre-group entries by member number for O(1) lookups
        var attendanceByMember = attendanceEntries
            .GroupBy(e => ExtractMemberNumber(e.NameOrId))
            .ToDictionary(g => g.Key, g => g.ToList());
        var operationByMember = operationEntries
            .GroupBy(e => ExtractMemberNumber(e.NameOrId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Pre-group FSW entries by MemberId
        var fswByMember = fswEntries
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<MemberStatistics>();

        foreach (var member in members)
        {
            var memberAttendanceEntries = attendanceByMember.GetValueOrDefault(member.MemberNumber, new List<AttendanceEntry>());
            var memberOperationEntries = operationByMember.GetValueOrDefault(member.MemberNumber, new List<OperationEntry>());
            var memberFswCount = fswByMember.GetValueOrDefault(member.Id, 0);

            var memberAttendance = memberAttendanceEntries.Count;
            var memberOperations = memberOperationEntries.Count;
            var totalParticipations = memberAttendance + memberOperations + memberFswCount;

            // Determine last participation
            var lastParticipation = DateTime.MinValue;
            var dates = new List<DateTime>();
            if (memberAttendanceEntries.Any())
                dates.Add(memberAttendanceEntries.Max(e => e.EnteredAt));
            if (memberOperationEntries.Any())
                dates.Add(memberOperationEntries.Max(e => e.EnteredAt));
            if (dates.Any())
                lastParticipation = dates.Max();

            var monthlyData = CalculateMonthlyParticipation(
                memberAttendanceEntries, memberOperationEntries, attendanceLists, operationLists);

            result.Add(new MemberStatistics
            {
                MemberId = member.Id,
                MemberName = $"{member.FirstName} {member.LastName}",
                MemberNumber = member.MemberNumber,
                TotalAttendance = memberAttendance,
                TotalOperations = memberOperations,
                AttendancePercentage = totalParticipations, // Total participation count (legacy field name)
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

        // Only load entries for relevant lists (not entire tables)
        var attendanceListIds = attendanceLists.Select(l => l.Id).ToHashSet();
        var operationListIds = operationLists.Select(l => l.Id).ToHashSet();

        var attendanceEntries = await _db.AttendanceEntries
            .Where(e => attendanceListIds.Contains(e.AttendanceListId))
            .ToListAsync();
        var operationEntries = await _db.OperationEntries
            .Where(e => operationListIds.Contains(e.OperationListId))
            .ToListAsync();

        // Pre-group entries by list ID for O(1) lookups
        var attendanceEntryCountByList = attendanceEntries
            .GroupBy(e => e.AttendanceListId)
            .ToDictionary(g => g.Key, g => g.Count());
        var operationEntryCountByList = operationEntries
            .GroupBy(e => e.OperationListId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<TrendData>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var dayAttendanceLists = attendanceLists.Where(x => x.CreatedAt.Date == date).ToList();
            var dayOperationLists = operationLists.Where(x => x.CreatedAt.Date == date).ToList();

            var totalParticipants = 0;
            foreach (var list in dayAttendanceLists)
                totalParticipants += attendanceEntryCountByList.GetValueOrDefault(list.Id, 0);
            foreach (var list in dayOperationLists)
                totalParticipants += operationEntryCountByList.GetValueOrDefault(list.Id, 0);

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

    public async Task<List<VehicleStatistics>> GetVehicleStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Vehicle))
            .ToListAsync();

        if (!operationEntries.Any())
            return new List<VehicleStatistics>();

        var vehicleStats = operationEntries
            .GroupBy(e => e.Vehicle!)
            .Select(g => new
            {
                Vehicle = g.Key,
                UsageCount = g.Select(e => e.OperationListId).Distinct().Count(),
                TotalCrew = g.Count()
            })
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

    public async Task<List<FunctionStatistics>> GetFunctionStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries.ToListAsync();
        var functionLinks = await _db.OperationEntryFunctions.ToListAsync();
        var functionDefs = await _db.OperationFunctionDefs.ToListAsync();

        var totalOperationParticipants = operationEntries.Count;
        if (totalOperationParticipants == 0)
            return new List<FunctionStatistics>();

        var functionCounts = functionLinks
            .GroupBy(f => f.FunctionDefId)
            .Select(g => new { FunctionId = g.Key, Count = g.Count() })
            .ToList();

        var result = new List<FunctionStatistics>();
        foreach (var func in functionCounts)
        {
            var functionDef = functionDefs.FirstOrDefault(d => d.Id == func.FunctionId);
            if (functionDef != null)
            {
                result.Add(new FunctionStatistics
                {
                    FunctionName = functionDef.Name,
                    Count = func.Count,
                    Percentage = Math.Round((double)func.Count / totalOperationParticipants * 100, 1)
                });
            }
        }

        var entryIdsWithFunctions = functionLinks.Select(f => f.OperationEntryId).Distinct().ToHashSet();
        var truppCount = operationEntries.Count(e => !entryIdsWithFunctions.Contains(e.Id));

        if (truppCount > 0)
        {
            result.Add(new FunctionStatistics
            {
                FunctionName = "Trupp",
                Count = truppCount,
                Percentage = Math.Round((double)truppCount / totalOperationParticipants * 100, 1)
            });
        }

        return result.OrderByDescending(s => s.Count).ToList();
    }

    public async Task<BreathingApparatusStatistics> GetBreathingApparatusStatisticsAsync()
    {
        var operationEntries = await _db.OperationEntries.ToListAsync();

        var atemschutzFunctions = await _db.OperationFunctionDefs
            .Where(f => f.Name.Contains("Atemschutz") || f.Name.Contains("AGT"))
            .ToListAsync();

        int withApparatus = 0;
        if (atemschutzFunctions.Any())
        {
            var functionIds = atemschutzFunctions.Select(f => f.Id).ToList();
            withApparatus = await _db.OperationEntryFunctions
                .CountAsync(f => functionIds.Contains(f.FunctionDefId));
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

    public async Task<List<OperationComposition>> GetOperationCompositionAsync(int limit = 15)
    {
        var recentOperations = await _db.OperationLists
            .OrderByDescending(o => o.AlertTime)
            .Take(limit)
            .ToListAsync();

        if (!recentOperations.Any())
            return new List<OperationComposition>();

        var operationIds = recentOperations.Select(o => o.Id).ToList();

        // Bulk-load entries and functions to avoid N+1 queries
        var entries = await _db.OperationEntries
            .Where(e => operationIds.Contains(e.OperationListId))
            .ToListAsync();

        var entryIds = entries.Select(e => e.Id).ToList();
        var functionLinks = await _db.OperationEntryFunctions
            .Where(ef => entryIds.Contains(ef.OperationEntryId))
            .ToListAsync();

        var functionDefs = await _db.OperationFunctionDefs.ToListAsync();
        var functionDefMap = functionDefs.ToDictionary(f => f.Id, f => f.Name);

        // Bulk-load requirements for all keywords at once
        var keywordIds = recentOperations
            .Where(o => o.KeywordId.HasValue)
            .Select(o => o.KeywordId!.Value)
            .Distinct()
            .ToList();
        var allRequirements = new Dictionary<int, List<PersonalRequirement>>();
        foreach (var kwId in keywordIds)
        {
            allRequirements[kwId] = await _requirementsService.GetRequirementsForKeywordAsync(kwId);
        }

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

            // Requirements validation using in-memory data
            if (op.KeywordId.HasValue)
            {
                var validationResult = await _requirementsService.ValidateRequirementsAsync(op.Id, op.KeywordId.Value);
                composition.HasPersonalRequirements = true;
                composition.RequirementsFulfilled = validationResult.IsValid;

                // Calculate actual fulfillment rate based on requirement counts
                if (allRequirements.TryGetValue(op.KeywordId.Value, out var reqs) && reqs.Any())
                {
                    var totalReqs = reqs.Count;
                    var metReqs = totalReqs - validationResult.MissingRequirements.Count;
                    composition.RequirementsFulfillmentRate = Math.Round((double)Math.Max(0, metReqs) / totalReqs * 100, 1);
                }
                else
                {
                    composition.RequirementsFulfillmentRate = validationResult.IsValid ? 100.0 : 0;
                }
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
                if (functionDefMap.TryGetValue(functionId, out var functionName))
                    composition.FunctionCounts[functionName] = count;

            foreach (var (functionId, count) in countsWithoutVehicle)
                if (functionDefMap.TryGetValue(functionId, out var functionName))
                    composition.NoVehicleFunctionCounts[functionName] = count;

            var entryIdsWithFunctionsWithVehicle = functionsWithVehicle.Select(f => f.OperationEntryId).Distinct().ToHashSet();
            var entryIdsWithFunctionsWithoutVehicle = functionsWithoutVehicle.Select(f => f.OperationEntryId).Distinct().ToHashSet();

            composition.WithVehicleTruppCount = entriesWithVehicle.Count(e => !entryIdsWithFunctionsWithVehicle.Contains(e.Id));
            composition.WithoutVehicleTruppCount = entriesWithoutVehicle.Count(e => !entryIdsWithFunctionsWithoutVehicle.Contains(e.Id));

            result.Add(composition);
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
        // Use first-of-month to ensure consistent month boundaries
        var startDate = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(-12);

        // Pre-group member entries by list ID for O(1) lookups
        var memberAttendanceListIds = memberAttendanceEntries
            .Select(e => e.AttendanceListId).ToHashSet();
        var memberOperationListIds = memberOperationEntries
            .Select(e => e.OperationListId).ToHashSet();

        for (var date = startDate; date <= endDate; date = date.AddMonths(1))
        {
            var year = date.Year;
            var month = date.Month;

            var monthAttendanceCount = attendanceLists
                .Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month
                    && memberAttendanceListIds.Contains(x.Id));
            var monthOperationCount = operationLists
                .Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month
                    && memberOperationListIds.Contains(x.Id));

            var totalListsInMonth = attendanceLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month)
                + operationLists.Count(x => x.CreatedAt.Year == year && x.CreatedAt.Month == month);

            var percentage = totalListsInMonth > 0
                ? Math.Round((double)(monthAttendanceCount + monthOperationCount) / totalListsInMonth * 100, 2)
                : 0;

            result.Add(new MonthlyParticipation
            {
                Year = year,
                Month = month,
                AttendanceCount = monthAttendanceCount,
                OperationCount = monthOperationCount,
                Percentage = percentage
            });
        }

        return result;
    }

    public async Task<List<KeywordStatistics>> GetKeywordStatisticsAsync()
    {
        var operationLists = await _db.OperationLists.ToListAsync();
        var keywords = await _db.Keywords.Where(k => k.IsActive).ToListAsync();

        var totalOperations = operationLists.Count;
        if (totalOperations == 0)
            return new List<KeywordStatistics>();

        var keywordUsage = operationLists
            .Where(o => o.KeywordId.HasValue)
            .GroupBy(o => o.KeywordId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<KeywordStatistics>();

        foreach (var keyword in keywords)
        {
            var usageCount = keywordUsage.GetValueOrDefault(keyword.Id, 0);
            var operationsWithKeyword = operationLists.Where(o => o.KeywordId == keyword.Id).ToList();

            // Check if this keyword has requirements defined at all
            var definedRequirements = await _requirementsService.GetRequirementsForKeywordAsync(keyword.Id);
            var hasRequirements = definedRequirements.Any();

            var operationsWithRequirements = 0;
            var operationsFulfillingRequirements = 0;

            if (hasRequirements)
            {
                foreach (var operation in operationsWithKeyword)
                {
                    var validation = await _requirementsService.ValidateRequirementsAsync(operation.Id, keyword.Id);
                    // Count ALL operations that have requirements defined (not just those missing some)
                    operationsWithRequirements++;
                    if (validation.IsValid)
                        operationsFulfillingRequirements++;
                }
            }

            var requirementsFulfillmentRate = operationsWithRequirements > 0
                ? Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1)
                : 0;

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

    public async Task<PersonalRequirementsStatistics> GetPersonalRequirementsStatisticsAsync()
    {
        var operationLists = await _db.OperationLists.ToListAsync();
        var keywords = await _db.Keywords.Where(k => k.IsActive).ToListAsync();

        var totalOperations = operationLists.Count;
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

                    // Count ALL operations with defined requirements
                    operationsWithRequirements++;
                    keywordOperationsWithRequirements++;

                    if (validation.IsValid)
                    {
                        operationsFulfillingRequirements++;
                        keywordOperationsFulfillingRequirements++;
                    }

                    // Collect function statistics from missing requirements
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
                        summary.FulfillmentRate = summary.RequiredCount > 0
                            ? Math.Round((double)summary.ActualCount / summary.RequiredCount * 100, 1)
                            : 0;
                    }
                }
            }

            var keywordFulfillmentRate = keywordOperationsWithRequirements > 0
                ? Math.Round((double)keywordOperationsFulfillingRequirements / keywordOperationsWithRequirements * 100, 1)
                : 0;

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

        var overallFulfillmentRate = operationsWithRequirements > 0
            ? Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1)
            : 0;

        return new PersonalRequirementsStatistics
        {
            TotalOperations = totalOperations,
            OperationsWithKeywords = operationsWithKeywords,
            OperationsWithRequirements = operationsWithRequirements,
            OperationsFulfillingRequirements = operationsFulfillingRequirements,
            RequirementsFulfillmentRate = overallFulfillmentRate,
            KeywordSummaries = keywordSummaries.OrderByDescending(k => k.OperationsCount).ToList()
        };
    }
}
