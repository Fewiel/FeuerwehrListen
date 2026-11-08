using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Web-Push-Abos fuer Benachrichtigungen im Mitgliederbereich.</summary>
[Migration(42)]
public class Migration_042_AddPushSubscriptions : Migration
{
    public override void Up() => Create.Table("PushSubscription")
        .WithColumn("Id").AsInt32().PrimaryKey().Identity()
        .WithColumn("MemberId").AsInt32().NotNullable()
        .WithColumn("Endpoint").AsString(1000).NotNullable()
        .WithColumn("P256dh").AsString(200).NotNullable()
        .WithColumn("Auth").AsString(200).NotNullable()
        .WithColumn("CreatedAt").AsDateTime().NotNullable();

    public override void Down() => Delete.Table("PushSubscription");
}
