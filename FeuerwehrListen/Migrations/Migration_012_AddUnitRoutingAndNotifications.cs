using FluentMigrator;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Migrations;

[Migration(12)]
public class Migration_012_AddUnitRoutingAndNotifications : Migration
{
    public override void Up()
    {
        Alter.Table("AttendanceList")
            .AddColumn("UnitNumber").AsInt32().Nullable();

        for (var unit = 1; unit <= 9; unit++)
        {
            Insert.IntoTable("app_settings").Row(new
            {
                Key = SettingKeys.GetAttendanceRecipientsKey(unit),
                Value = string.Empty
            });
        }

        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.NotificationOperationRecipients, Value = string.Empty });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.NotificationFireSafetyWatchRecipients, Value = string.Empty });

        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpHost, Value = string.Empty });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpPort, Value = "587" });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpUsername, Value = string.Empty });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpPassword, Value = string.Empty });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpFromAddress, Value = string.Empty });
        Insert.IntoTable("app_settings").Row(new { Key = SettingKeys.SmtpUseSsl, Value = "true" });
    }

    public override void Down()
    {
        Delete.Column("UnitNumber").FromTable("AttendanceList");

        for (var unit = 1; unit <= 9; unit++)
        {
            Delete.FromTable("app_settings").Row(new { Key = SettingKeys.GetAttendanceRecipientsKey(unit) });
        }

        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.NotificationOperationRecipients });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.NotificationFireSafetyWatchRecipients });

        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpHost });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpPort });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpUsername });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpPassword });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpFromAddress });
        Delete.FromTable("app_settings").Row(new { Key = SettingKeys.SmtpUseSsl });
    }
}
