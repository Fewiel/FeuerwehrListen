namespace FeuerwehrListen.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

public class ApiError
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class ListResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EntryResponse
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string NameOrId { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; }
    public bool IsExcused { get; set; }
}

public class OperationEntryResponse : EntryResponse
{
    public string Vehicle { get; set; } = string.Empty;
    public string Function { get; set; } = string.Empty;
    public bool WithBreathingApparatus { get; set; }
}

public class MemberResponse
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
}


