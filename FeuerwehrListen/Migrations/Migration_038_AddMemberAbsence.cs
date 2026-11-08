using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(38)]
public class Migration_038_AddMemberAbsence : Migration
{
    public override void Up() => Create.Table("MemberAbsence")
        .WithColumn("Id").AsInt32().PrimaryKey().Identity()
        .WithColumn("MemberId").AsInt32().NotNullable()
        .WithColumn("FromTime").AsDateTime().NotNullable()
        .WithColumn("ToTime").AsDateTime().NotNullable()
        .WithColumn("Note").AsString(500).NotNullable().WithDefaultValue("")
        .WithColumn("CreatedBy").AsString(200).NotNullable().WithDefaultValue("")
        .WithColumn("CreatedAt").AsDateTime().NotNullable();

    public override void Down() => Delete.Table("MemberAbsence");
}
