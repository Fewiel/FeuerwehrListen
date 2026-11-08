using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Protokoll-Abschnitte: Informationen (vorab) / Tagesordnung / Aus den Bereichen.</summary>
[Migration(40)]
public class Migration_040_LeadershipItemSection : Migration
{
    public override void Up() => Alter.Table("LeadershipItem").AddColumn("Section").AsInt32().NotNullable().WithDefaultValue(0);
    public override void Down() => Delete.Column("Section").FromTable("LeadershipItem");
}
