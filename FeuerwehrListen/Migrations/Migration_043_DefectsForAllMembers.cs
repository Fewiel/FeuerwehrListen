using FluentMigrator;

namespace FeuerwehrListen.Migrations;

/// <summary>Einmalig: bei allen Mitgliedern den Bereich "Maengel" freigeben, falls noch nicht gesetzt.</summary>
[Migration(43)]
public class Migration_043_DefectsForAllMembers : Migration
{
    public override void Up()
    {
        // In C# statt SQL: ModuleAccess ist eine kommagetrennte Liste, das Anhaengen ist so fuer
        // SQLite und MySQL gleich und doppelte Eintraege sind ausgeschlossen.
        Execute.WithConnection((conn, tx) =>
        {
            var rows = new List<(int Id, string Access)>();
            using (var select = conn.CreateCommand())
            {
                select.Transaction = tx;
                select.CommandText = "SELECT Id, ModuleAccess FROM Member";
                using var reader = select.ExecuteReader();
                while (reader.Read()) rows.Add((Convert.ToInt32(reader.GetValue(0)), reader.IsDBNull(1) ? "" : reader.GetString(1)));
            }
            foreach (var (id, access) in rows)
            {
                var parts = access.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (parts.Contains("defects", StringComparer.OrdinalIgnoreCase)) continue;
                parts.Add("defects");
                using var update = conn.CreateCommand();
                update.Transaction = tx;
                update.CommandText = "UPDATE Member SET ModuleAccess = @a WHERE Id = @id";
                var a = update.CreateParameter(); a.ParameterName = "@a"; a.Value = string.Join(",", parts); update.Parameters.Add(a);
                var i = update.CreateParameter(); i.ParameterName = "@id"; i.Value = id; update.Parameters.Add(i);
                update.ExecuteNonQuery();
            }
        });
    }

    public override void Down() { }
}
