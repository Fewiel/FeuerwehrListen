using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(19)]
public class Migration_019_AddMemberUnitsTable : Migration
{
    public override void Up()
    {
        Create.Table("MemberUnit")
            .WithColumn("MemberId").AsInt32().NotNullable()
            .WithColumn("UnitNumber").AsInt32().NotNullable();

        Create.PrimaryKey("PK_MemberUnit")
            .OnTable("MemberUnit")
            .Columns("MemberId", "UnitNumber");

        Create.Index("IX_MemberUnit_UnitNumber")
            .OnTable("MemberUnit")
            .OnColumn("UnitNumber").Ascending();

        // Bestehende Einheits-Zuordnungen (Member.UnitNumber) in die Join-Tabelle migrieren,
        // damit Multi-Unit-Resolver direkt funktionieren.
        Execute.Sql(@"
            INSERT INTO MemberUnit (MemberId, UnitNumber)
            SELECT Id, UnitNumber FROM Member
            WHERE UnitNumber IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM MemberUnit mu
                WHERE mu.MemberId = Member.Id AND mu.UnitNumber = Member.UnitNumber
              );
        ");
    }

    public override void Down()
    {
        Delete.Table("MemberUnit");
    }
}
