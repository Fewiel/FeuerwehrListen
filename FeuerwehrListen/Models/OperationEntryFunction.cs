using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("OperationEntryFunction")]
public class OperationEntryFunction
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    [Column("OperationEntryId"), NotNull]
    public int OperationEntryId { get; set; }

    [Column("FunctionDefId"), NotNull]
    public int FunctionDefId { get; set; }
}


