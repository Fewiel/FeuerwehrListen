using FluentMigrator;

namespace FeuerwehrListen.Migrations
{
    [Migration(11)]
    public class Migration_011_AddSettings : Migration
    {
        public override void Up()
        {
            Create.Table("app_settings")
                .WithColumn("Key").AsString(255).PrimaryKey().NotNullable()
                .WithColumn("Value").AsString(1024).NotNullable();

            Insert.IntoTable("app_settings").Row(new { Key = "ModuleVisibility.Attendance", Value = "true" });
            Insert.IntoTable("app_settings").Row(new { Key = "ModuleVisibility.Operations", Value = "true" });
            Insert.IntoTable("app_settings").Row(new { Key = "ModuleVisibility.FireSafetyWatch", Value = "true" });
            Insert.IntoTable("app_settings").Row(new { Key = "AutoClose.AttendanceMinutes", Value = "0" });
            Insert.IntoTable("app_settings").Row(new { Key = "AutoClose.OperationMinutes", Value = "0" });
            Insert.IntoTable("app_settings").Row(new { Key = "AutoClose.FireSafetyWatchMinutes", Value = "0" });
        }

        public override void Down()
        {
            Delete.Table("app_settings");
        }
    }
}
