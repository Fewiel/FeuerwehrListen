using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(35)]
public class Migration_035_AddMemberBirthDate : Migration
{
    public override void Up() => Alter.Table("Member").AddColumn("BirthDate").AsDateTime().Nullable();
    public override void Down() => Delete.Column("BirthDate").FromTable("Member");
}
