using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(4)]
public class Migration_004_AddOperationFunctionDef : Migration
{
    public override void Up()
    {
        Create.Table("OperationFunctionDef")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false);

        // Seed defaults
        Insert.IntoTable("OperationFunctionDef").Row(new { Name = "Atemschutzgeräteträger", IsDefault = true });
        Insert.IntoTable("OperationFunctionDef").Row(new { Name = "Gruppenführer", IsDefault = true });
        Insert.IntoTable("OperationFunctionDef").Row(new { Name = "Maschinist", IsDefault = true });
    }

    public override void Down()
    {
        Delete.Table("OperationFunctionDef");
    }
}


