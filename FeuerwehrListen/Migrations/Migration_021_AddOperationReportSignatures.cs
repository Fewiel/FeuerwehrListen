using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(21)]
public class Migration_021_AddOperationReportSignatures : Migration
{
    public override void Up()
    {
        Alter.Table("OperationReport")
            .AddColumn("UnterschriftIukImage").AsString(int.MaxValue).Nullable()
            .AddColumn("UnterschriftEinsatzleiterImage").AsString(int.MaxValue).Nullable();
    }

    public override void Down()
    {
        Delete.Column("UnterschriftIukImage").FromTable("OperationReport");
        Delete.Column("UnterschriftEinsatzleiterImage").FromTable("OperationReport");
    }
}
