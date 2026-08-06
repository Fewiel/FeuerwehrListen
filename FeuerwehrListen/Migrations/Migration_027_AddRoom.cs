using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>
/// Raeume als buchbare Ressource fuer den Kalender. Je Raum optional Freigabepflicht
/// mit einer oder mehreren (kommagetrennten) Zustaendigen-Adressen.
/// </summary>
[Migration(27)]
public class Migration_027_AddRoom : Migration
{
    public override void Up()
    {
        Create.Table("Room")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("Capacity").AsInt32().Nullable()
            .WithColumn("RequiresApproval").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ApproverEmails").AsString(500).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Room");
    }
}
