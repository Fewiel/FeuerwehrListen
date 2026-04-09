using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(16)]
public class Migration_016_AddMemberUnitNumber : Migration
{
    public override void Up()
    {
        Alter.Table("Member").AddColumn("UnitNumber").AsInt32().Nullable();
    }

    public override void Down()
    {
        Delete.Column("UnitNumber").FromTable("Member");
    }
}
