using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(13)]
public class Migration_013_AddScheduledListUnitNumber : Migration
{
    public override void Up()
    {
        Alter.Table("ScheduledList")
            .AddColumn("UnitNumber").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("UnitNumber").FromTable("ScheduledList");
    }
}
