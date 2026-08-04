using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(25)]
public class Migration_025_AddTruppFunction : Migration
{
    public override void Up()
    {
        // "Trupp" als Standard-Funktion (v.a. fuer Brandsicherheitswachen).
        // StrengthPosition 3 = Mannschaft.
        Insert.IntoTable("OperationFunctionDef")
            .Row(new { Name = "Trupp", IsDefault = true, StrengthPosition = 3 });
    }

    public override void Down()
    {
        Delete.FromTable("OperationFunctionDef").Row(new { Name = "Trupp" });
    }
}
