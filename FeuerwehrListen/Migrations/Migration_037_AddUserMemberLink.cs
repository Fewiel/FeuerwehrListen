using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Benutzer mit Mitglied verknuepfen (Login mit Mitgliedsnummer erhaelt Benutzer-Rechte).</summary>
[Migration(37)]
public class Migration_037_AddUserMemberLink : Migration
{
    public override void Up() => Alter.Table("User").AddColumn("MemberId").AsInt32().Nullable();
    public override void Down() => Delete.Column("MemberId").FromTable("User");
}
