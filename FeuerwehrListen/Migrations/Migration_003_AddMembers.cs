using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(3)]
public class Migration_003_AddMembers : Migration
{
    public override void Up()
    {
        Create.Table("Member")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("MemberNumber").AsString(50).NotNullable()
            .WithColumn("FirstName").AsString(100).NotNullable()
            .WithColumn("LastName").AsString(100).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_Member_MemberNumber")
            .OnTable("Member")
            .OnColumn("MemberNumber")
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("Member");
    }
}


