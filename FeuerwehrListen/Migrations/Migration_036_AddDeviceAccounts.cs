using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Geraetekonten (Rolle Device) mit dauerhaften Geraete-Sitzungen.</summary>
[Migration(36)]
public class Migration_036_AddDeviceAccounts : Migration
{
    public override void Up()
    {
        Alter.Table("User").AddColumn("AllowEnrollment").AsBoolean().NotNullable().WithDefaultValue(false);
        Create.Table("DeviceSession")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("TokenHash").AsString(64).NotNullable()
            .WithColumn("UserAgent").AsString(300).NotNullable().WithDefaultValue("")
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("LastSeenAt").AsDateTime().NotNullable()
            .WithColumn("LastIp").AsString(64).Nullable()
            .WithColumn("RevokedAt").AsDateTime().Nullable();
        Create.Index("IX_DeviceSession_TokenHash").OnTable("DeviceSession").OnColumn("TokenHash");
    }

    public override void Down()
    {
        Delete.Table("DeviceSession");
        Delete.Column("AllowEnrollment").FromTable("User");
    }
}
