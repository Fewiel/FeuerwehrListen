using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Fuehrungskreis: Sitzungen (Kalendertyp 5), Protokolle, Tagesordnungspunkte, Aufgaben.</summary>
[Migration(34)]
public class Migration_034_AddLeadership : Migration
{
    public override void Up()
    {
        Alter.Table("CalendarEvent").AddColumn("LeadershipCircle").AsInt32().Nullable();
        Alter.Table("AttendanceList").AddColumn("LeadershipProtocolId").AsInt32().Nullable();

        Create.Table("LeadershipProtocol")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CalendarEventId").AsInt32().NotNullable()
            .WithColumn("Circle").AsInt32().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("AttendanceListId").AsInt32().Nullable()
            .WithColumn("StartedAt").AsDateTime().Nullable()
            .WithColumn("ClosedAt").AsDateTime().Nullable()
            .WithColumn("ClosedBy").AsString(200).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("LeadershipItem")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ProtocolId").AsInt32().Nullable()
            .WithColumn("Circle").AsInt32().NotNullable()
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Title").AsString(500).NotNullable()
            .WithColumn("OwnerMemberId").AsInt32().Nullable()
            .WithColumn("Notes").AsString(int.MaxValue).NotNullable().WithDefaultValue("")
            .WithColumn("Decision").AsString(2000).NotNullable().WithDefaultValue("")
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("SourceTaskId").AsInt32().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("LeadershipTask")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Circle").AsInt32().NotNullable()
            .WithColumn("Title").AsString(500).NotNullable()
            .WithColumn("Description").AsString(4000).NotNullable().WithDefaultValue("")
            .WithColumn("AssigneeMemberId").AsInt32().Nullable()
            .WithColumn("DueDate").AsDateTime().Nullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("SourceItemId").AsInt32().Nullable()
            .WithColumn("CreatedBy").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("CompletedAt").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("LeadershipTask");
        Delete.Table("LeadershipItem");
        Delete.Table("LeadershipProtocol");
        Delete.Column("LeadershipProtocolId").FromTable("AttendanceList");
        Delete.Column("LeadershipCircle").FromTable("CalendarEvent");
    }
}
