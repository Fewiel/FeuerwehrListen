using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(7)]
public class Migration_007_AddAttendanceIsExcused : Migration
{
    public override void Up()
    {
        Alter.Table("AttendanceEntry")
            .AddColumn("IsExcused").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("IsExcused").FromTable("AttendanceEntry");
    }
}

