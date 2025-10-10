using System.Collections.Generic;

namespace FeuerwehrListen.Models;

public class PersonalRequirementsValidationResult
{
    public bool IsValid { get; set; }
    public List<MissingRequirement> MissingRequirements { get; set; } = new();
}

public class MissingRequirement
{
    public string FunctionName { get; set; } = string.Empty;
    public int CurrentCount { get; set; }
    public int RequiredCount { get; set; }
    public bool IsRequired { get; set; }
}
