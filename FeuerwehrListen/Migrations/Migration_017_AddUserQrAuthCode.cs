using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(17)]
public class Migration_017_AddUserQrAuthCode : Migration
{
    public override void Up()
    {
        Alter.Table("User").AddColumn("QrAuthCode").AsString(500).Nullable();
        Create.Index("IX_User_QrAuthCode").OnTable("User").OnColumn("QrAuthCode").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_User_QrAuthCode").OnTable("User");
        Delete.Column("QrAuthCode").FromTable("User");
    }
}
