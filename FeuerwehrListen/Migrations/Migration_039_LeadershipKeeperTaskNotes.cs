using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Fuehrungskreis: Protokollfuehrung je Sitzung, Notizen an Aufgaben.</summary>
[Migration(39)]
public class Migration_039_LeadershipKeeperTaskNotes : Migration
{
    public override void Up()
    {
        Alter.Table("LeadershipProtocol").AddColumn("KeeperMemberId").AsInt32().Nullable();
        Alter.Table("LeadershipTask").AddColumn("Notes").AsString(4000).NotNullable().WithDefaultValue("");
    }

    public override void Down()
    {
        Delete.Column("KeeperMemberId").FromTable("LeadershipProtocol");
        Delete.Column("Notes").FromTable("LeadershipTask");
    }
}
