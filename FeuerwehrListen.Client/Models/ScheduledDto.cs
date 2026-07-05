namespace FeuerwehrListen.Client.Models;

public record ScheduledDto(
    int Id, string Type, string Title, string Unit, int? UnitNumber,
    string OperationNumber, string Keyword, DateTime EventTime, int MinutesBefore,
    DateTime OpenTime, bool IsProcessed);
