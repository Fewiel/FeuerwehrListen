using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(15)]
public class Migration_015_AddDefectsVisibilitySetting : Migration
{
    public override void Up()
    {
        Insert.IntoTable("app_settings").Row(new { Key = "ModuleVisibility.Defects", Value = "true" });
    }

    public override void Down()
    {
        Delete.FromTable("app_settings").Row(new { Key = "ModuleVisibility.Defects" });
    }
}
