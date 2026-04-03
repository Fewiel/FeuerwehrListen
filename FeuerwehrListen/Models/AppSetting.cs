using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

[Table("app_settings")]
public class AppSetting
{
    [PrimaryKey]
    [Column("Key")]
    public string Key { get; set; } = string.Empty;

    [Column("Value")]
    public string Value { get; set; } = string.Empty;
}
