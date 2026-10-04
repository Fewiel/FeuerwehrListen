using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(31)]
public class Migration_031_AddSsoProvider : Migration
{
    public override void Up()
    {
        // Bestandsnutzer behalten den Listen-Zugriff: DefaultValue(true) fuellt vorhandene Zeilen.
        Alter.Table("User")
            .AddColumn("HasListAccess").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.Table("PermissionKey")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable().WithDefaultValue("")
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Table("UserPermissionKey")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
            .WithColumn("PermissionKeyId").AsInt32().NotNullable();

        Create.Table("SsoClient")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ClientId").AsString(100).NotNullable()
            .WithColumn("ClientSecretHash").AsString(200).NotNullable()
            .WithColumn("Name").AsString(200).NotNullable().WithDefaultValue("")
            .WithColumn("RedirectUris").AsString(2000).NotNullable().WithDefaultValue("")
            .WithColumn("RequiredKeys").AsString(1000).NotNullable().WithDefaultValue("")
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("SsoClient");
        Delete.Table("UserPermissionKey");
        Delete.Table("PermissionKey");
        Delete.Column("HasListAccess").FromTable("User");
    }
}
