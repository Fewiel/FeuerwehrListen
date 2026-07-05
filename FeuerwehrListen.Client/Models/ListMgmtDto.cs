namespace FeuerwehrListen.Client.Models;

public record ListMgmtDto(List<ListMgmtItem> Operations, List<ListMgmtItem> Attendance, List<ListMgmtItem> Watches);
public record ListMgmtItem(int Id, string Title, string? Sub, DateTime? ClosedAt, string Href);
