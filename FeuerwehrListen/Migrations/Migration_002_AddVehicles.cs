using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(2)]
public class Migration_002_AddVehicles : Migration
{
    public override void Up()
    {
        Create.Table("Vehicle")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("CallSign").AsString(50).NotNullable()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Vehicle");
    }
}




