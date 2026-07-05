namespace FeuerwehrListen.Client.Models;

// Passt zum JSON des Fast-Endpoints GET /client-api/open-lists (camelCase, web defaults).
public record OpenListsResponse(
    DateTime ServerTime,
    List<ListItem> Operations,
    List<ListItem> Attendance,
    List<ListItem> Watches);

public record ListItem(int Id, string Title, string? Sub, DateTime Time, string Href);
