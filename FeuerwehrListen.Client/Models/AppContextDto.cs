namespace FeuerwehrListen.Client.Models;

public record AppContextDto(string AppName, string? LogoUrl, ModuleFlags Modules);

public record ModuleFlags(bool Attendance, bool Operations, bool FireSafety, bool Defects);

public record FeedbackOperationDto(int Id, string Keyword, string Address, string Number, DateTime Time);
