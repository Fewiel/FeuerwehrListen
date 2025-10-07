using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(6)]
public class Migration_006_AddOperationAddress : Migration
{
    public override void Up()
    {
        Alter.Table("OperationList")
            .AddColumn("Address").AsString(500).Nullable()
            .AddColumn("Latitude").AsDouble().Nullable()
            .AddColumn("Longitude").AsDouble().Nullable();
    }

    public override void Down()
    {
        Delete.Column("Address").FromTable("OperationList");
        Delete.Column("Latitude").FromTable("OperationList");
        Delete.Column("Longitude").FromTable("OperationList");
    }
}


