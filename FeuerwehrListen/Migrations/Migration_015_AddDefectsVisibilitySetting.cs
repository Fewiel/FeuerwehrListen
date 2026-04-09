using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(15)]
public class Migration_015_AddDefectsVisibilitySetting : Migration
{
    public override void Up()
    {
        IfDatabase("SQLite").Execute.Sql(
            "INSERT OR IGNORE INTO app_settings (Key, Value) VALUES ('ModuleVisibility.Defects', 'true')");
        IfDatabase("MySql").Execute.Sql(
            "INSERT IGNORE INTO app_settings (`Key`, `Value`) VALUES ('ModuleVisibility.Defects', 'true')");
    }

    public override void Down()
    {
        Delete.FromTable("app_settings").Row(new { Key = "ModuleVisibility.Defects" });
    }
}
