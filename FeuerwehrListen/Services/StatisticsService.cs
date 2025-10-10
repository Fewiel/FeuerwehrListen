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
        var attendanceLists = await _db.AttendanceLists.CountAsync();
        var operationLists = await _db.OperationLists.CountAsync();

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

        var totalLists = attendanceLists + operationLists;

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
            // Die prozentuale Gesamt-Teilnahmequote wird entfernt, da sie irreführend ist.
            // Stattdessen fokussieren wir uns auf absolute Zahlen und monatliche Quoten.
            var totalParticipations = memberAttendance + memberOperations;
            
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
                // Veraltetes, irreführendes Feld. Wird nicht mehr berechnet.
                AttendancePercentage = totalParticipations, // Zeigt jetzt die Gesamtzahl an, nicht einen Prozentsatz
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
        var operationEntries = await _db.OperationEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Vehicle))
            .ToListAsync();

        if (!operationEntries.Any())
        {
            return new List<VehicleStatistics>();
        }

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
        {
            return new List<FunctionStatistics>();
        }

        var functionCounts = functionLinks
            .GroupBy(f => f.FunctionDefId)
            .Select(g => new
            {
                FunctionId = g.Key,
                Count = g.Count()
            })
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
        {
            return new List<OperationComposition>();
        }

        var operationIds = recentOperations.Select(o => o.Id).ToList();

        var entries = await _db.OperationEntries
            .Where(e => operationIds.Contains(e.OperationListId))
            .ToListAsync();

        var functionLinks = await _db.OperationEntryFunctions
            .Where(ef => entries.Select(e => e.Id).Contains(ef.OperationEntryId))
            .ToListAsync();

        var functionDefs = await _db.OperationFunctionDefs.ToListAsync();
        var functionDefMap = functionDefs.ToDictionary(f => f.Id, f => f.Name);

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

            // Personal Requirements Validierung
            if (op.KeywordId.HasValue)
            {
                var validationResult = await _requirementsService.ValidateRequirementsAsync(op.Id, op.KeywordId.Value);
                composition.HasPersonalRequirements = true;
                composition.RequirementsFulfillmentRate = validationResult.IsValid ? 100.0 : 
                    Math.Max(0, 100.0 - (validationResult.MissingRequirements.Count * 20.0)); // Vereinfachte Berechnung
                composition.RequirementsFulfilled = validationResult.IsValid;
            }
            else
            {
                composition.HasPersonalRequirements = false;
                composition.RequirementsFulfillmentRate = 0;
                composition.RequirementsFulfilled = false;
            }

            var entriesWithVehicle = opEntries.Where(e => !string.IsNullOrWhiteSpace(e.Vehicle)).ToList();
            var entriesWithoutVehicle = opEntries.Where(e => string.IsNullOrWhiteSpace(e.Vehicle)).ToList();

            var entryIdsWithVehicle = entriesWithVehicle.Select(e => e.Id).ToList();
            var entryIdsWithoutVehicle = entriesWithoutVehicle.Select(e => e.Id).ToList();

            var functionsWithVehicle = functionLinks
                .Where(fl => entryIdsWithVehicle.Contains(fl.OperationEntryId));
            var functionsWithoutVehicle = functionLinks
                .Where(fl => entryIdsWithoutVehicle.Contains(fl.OperationEntryId));

            var countsWithVehicle = functionsWithVehicle
                .GroupBy(fl => fl.FunctionDefId)
                .ToDictionary(g => g.Key, g => g.Count());
            var countsWithoutVehicle = functionsWithoutVehicle
                .GroupBy(fl => fl.FunctionDefId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var (functionId, count) in countsWithVehicle)
            {
                if (functionDefMap.TryGetValue(functionId, out var functionName))
                {
                    composition.FunctionCounts[functionName] = count;
                }
            }

            foreach (var (functionId, count) in countsWithoutVehicle)
            {
                if (functionDefMap.TryGetValue(functionId, out var functionName))
                {
                    composition.NoVehicleFunctionCounts[functionName] = count;
                }
            }

            var entryIdsWithFunctionsWithVehicle = functionsWithVehicle
                .Select(f => f.OperationEntryId)
                .Distinct()
                .ToHashSet();
            var entryIdsWithFunctionsWithoutVehicle = functionsWithoutVehicle
                .Select(f => f.OperationEntryId)
                .Distinct()
                .ToHashSet();

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

    public async Task<List<KeywordStatistics>> GetKeywordStatisticsAsync()
    {
        var operationLists = await _db.OperationLists.ToListAsync();
        var keywords = await _db.Keywords.Where(k => k.IsActive).ToListAsync();
        
        var totalOperations = operationLists.Count;
        if (totalOperations == 0)
        {
            return new List<KeywordStatistics>();
        }

        var keywordUsage = operationLists
            .Where(o => o.KeywordId.HasValue)
            .GroupBy(o => o.KeywordId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<KeywordStatistics>();
        
        foreach (var keyword in keywords)
        {
            var usageCount = keywordUsage.GetValueOrDefault(keyword.Id, 0);
            var operationsWithKeyword = operationLists.Where(o => o.KeywordId == keyword.Id).ToList();
            
            var operationsWithRequirements = 0;
            var operationsFulfillingRequirements = 0;
            
            foreach (var operation in operationsWithKeyword)
            {
                var validation = await _requirementsService.ValidateRequirementsAsync(operation.Id, keyword.Id);
                if (validation.MissingRequirements.Any())
                {
                    operationsWithRequirements++;
                    if (validation.IsValid)
                    {
                        operationsFulfillingRequirements++;
                    }
                }
            }
            
            var requirementsFulfillmentRate = operationsWithRequirements > 0 ? 
                Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1) : 0;

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
            var keywordOperationsWithRequirements = 0;
            var keywordOperationsFulfillingRequirements = 0;
            var functionSummaries = new Dictionary<string, FunctionRequirementSummary>();
            
            foreach (var operation in operationsWithKeyword)
            {
                var validation = await _requirementsService.ValidateRequirementsAsync(operation.Id, keyword.Id);
                if (validation.MissingRequirements.Any())
                {
                    operationsWithRequirements++;
                    keywordOperationsWithRequirements++;
                    
                    if (validation.IsValid)
                    {
                        operationsFulfillingRequirements++;
                        keywordOperationsFulfillingRequirements++;
                    }
                    
                    // Sammle Function-Statistiken
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
                        summary.FulfillmentRate = summary.RequiredCount > 0 ? 
                            Math.Round((double)summary.ActualCount / summary.RequiredCount * 100, 1) : 0;
                    }
                }
            }
            
            var keywordFulfillmentRate = keywordOperationsWithRequirements > 0 ? 
                Math.Round((double)keywordOperationsFulfillingRequirements / keywordOperationsWithRequirements * 100, 1) : 0;
            
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
        
        var overallFulfillmentRate = operationsWithRequirements > 0 ? 
            Math.Round((double)operationsFulfillingRequirements / operationsWithRequirements * 100, 1) : 0;
        
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

