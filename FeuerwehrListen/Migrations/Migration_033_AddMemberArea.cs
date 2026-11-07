using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Mitgliederbereich: Login, Bereiche, Fuehrungskreis-Stufe, Lichtbild, Dienstausweis.</summary>
[Migration(33)]
public class Migration_033_AddMemberArea : Migration
{
    public override void Up()
    {
        Alter.Table("Member")
            .AddColumn("PasswordHash").AsString(300).Nullable()
            .AddColumn("PasswordSetAt").AsDateTime().Nullable()
            .AddColumn("PasswordSetByUserId").AsInt32().Nullable()
            .AddColumn("ModuleAccess").AsString(500).NotNullable().WithDefaultValue("")
            .AddColumn("LeadershipCircle").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("Rank").AsString(100).Nullable()
            .AddColumn("Email").AsString(200).Nullable()
            .AddColumn("PhotoFile").AsString(200).Nullable()
            .AddColumn("PhotoUpdatedAt").AsDateTime().Nullable()
            .AddColumn("CardPublicId").AsString(64).Nullable()
            .AddColumn("CardValidUntil").AsDateTime().Nullable()
            .AddColumn("CardRevokedAt").AsDateTime().Nullable()
            .AddColumn("FailedLogins").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("LockedUntil").AsDateTime().Nullable();
    }

    public override void Down()
    {
        foreach (var c in new[] { "PasswordHash", "PasswordSetAt", "PasswordSetByUserId", "ModuleAccess", "LeadershipCircle",
                     "Rank", "Email", "PhotoFile", "PhotoUpdatedAt", "CardPublicId", "CardValidUntil", "CardRevokedAt",
                     "FailedLogins", "LockedUntil" })
            Delete.Column(c).FromTable("Member");
    }
}
