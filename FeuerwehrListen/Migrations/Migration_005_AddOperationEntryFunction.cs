using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(5)]
public class Migration_005_AddOperationEntryFunction : Migration
{
    public override void Up()
    {
        Create.Table("OperationEntryFunction")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationEntryId").AsInt32().NotNullable()
            .WithColumn("FunctionDefId").AsInt32().NotNullable();

        // Optional: Indizes für schnelle Abfragen
        Create.Index().OnTable("OperationEntryFunction").OnColumn("OperationEntryId");
        Create.Index().OnTable("OperationEntryFunction").OnColumn("FunctionDefId");
    }

    public override void Down()
    {
        Delete.Table("OperationEntryFunction");
    }
}


