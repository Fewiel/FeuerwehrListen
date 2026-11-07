using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(32)]
public class Migration_032_AddLoginGateAndModuleAccess : Migration
{
    public override void Up()
    {
        // Bestandsnutzer behalten Vollzugang: DefaultValue("*") fuellt vorhandene Zeilen.
        Alter.Table("User")
            .AddColumn("ModuleAccess").AsString(500).NotNullable().WithDefaultValue("*");

        // Direktlink fuer Brandsicherheitswachen (/wache/{PublicId}).
        Alter.Table("fire_safety_watches")
            .AddColumn("PublicId").AsString(64).Nullable();

        // Altdaten mit zufaelligen GUIDs fuellen. Bewusst in C# statt per SQL: UUID() in MySQL
        // ist zeitbasiert (erratbar) und RANDOM_BYTES fehlt in aelteren MariaDB-Versionen.
        Execute.WithConnection((conn, tx) =>
        {
            var ids = new List<int>();
            using (var select = conn.CreateCommand())
            {
                select.Transaction = tx;
                select.CommandText = "SELECT Id FROM fire_safety_watches WHERE PublicId IS NULL";
                using var reader = select.ExecuteReader();
                while (reader.Read()) ids.Add(Convert.ToInt32(reader.GetValue(0)));
            }
            foreach (var id in ids)
            {
                using var update = conn.CreateCommand();
                update.Transaction = tx;
                update.CommandText = "UPDATE fire_safety_watches SET PublicId = @p WHERE Id = @id";
                var p = update.CreateParameter(); p.ParameterName = "@p"; p.Value = Guid.NewGuid().ToString("N"); update.Parameters.Add(p);
                var i = update.CreateParameter(); i.ParameterName = "@id"; i.Value = id; update.Parameters.Add(i);
                update.ExecuteNonQuery();
            }
        });
    }

    public override void Down()
    {
        Delete.Column("PublicId").FromTable("fire_safety_watches");
        Delete.Column("ModuleAccess").FromTable("User");
    }
}
