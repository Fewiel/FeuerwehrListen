using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(24)]
public class Migration_024_AddMittelDauer : Migration
{
    public override void Up()
    {
        Alter.Table("OperationReportMittel")
            .AddColumn("Dauer").AsString(100).Nullable();
    }

    public override void Down()
    {
        Delete.Column("Dauer").FromTable("OperationReportMittel");
    }
}
