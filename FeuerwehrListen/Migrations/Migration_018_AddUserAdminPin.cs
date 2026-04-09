using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(18)]
public class Migration_018_AddUserAdminPin : Migration
{
    public override void Up()
    {
        Alter.Table("User").AddColumn("AdminPin").AsString(100).Nullable();
    }

    public override void Down()
    {
        Delete.Column("AdminPin").FromTable("User");
    }
}
