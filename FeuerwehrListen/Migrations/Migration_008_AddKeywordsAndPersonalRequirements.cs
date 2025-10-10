using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(8)]
public class Migration_008_AddKeywordsAndPersonalRequirements : Migration
{
    public override void Up()
    {
        // Create Keywords table
        Create.Table("Keyword")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

                // Create PersonalRequirements table
                Create.Table("PersonalRequirement")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("KeywordId").AsInt32().NotNullable()
                    .WithColumn("FunctionDefId").AsInt32().NotNullable()
                    .WithColumn("MinimumCount").AsInt32().NotNullable().WithDefaultValue(1)
                    .WithColumn("IsRequired").AsBoolean().NotNullable().WithDefaultValue(true)
                    .WithColumn("CreatedAt").AsDateTime().NotNullable();

                // Add KeywordId column to OperationList table
                Alter.Table("OperationList")
                    .AddColumn("KeywordId").AsInt32().Nullable();

                // Insert default keywords
                // Brandbekämpfung
                Insert.IntoTable("Keyword").Row(new { Name = "F1", Description = "Brandbekämpfung - Kleinbrand", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "F2", Description = "Brandbekämpfung - Mittelbrand", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "F3", Description = "Brandbekämpfung - Großbrand", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "F3_Gebäude", Description = "Brandbekämpfung - Großbrand Gebäude", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "F3_MiG", Description = "Brandbekämpfung - Großbrand mit Menschen in Gefahr", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "F4", Description = "Brandbekämpfung - Schadensfeuer", IsActive = true, CreatedAt = DateTime.Now });
                
                // Technische Hilfeleistung
                Insert.IntoTable("Keyword").Row(new { Name = "TH1", Description = "Technische Hilfeleistung - Klein", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "TH1_VU", Description = "Technische Hilfeleistung - Klein Verkehrsunfall", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "TH2", Description = "Technische Hilfeleistung - Mittel", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "TH3_VU", Description = "Technische Hilfeleistung - Groß Verkehrsunfall", IsActive = true, CreatedAt = DateTime.Now });
                
                // ABC-Einsätze
                Insert.IntoTable("Keyword").Row(new { Name = "ABC1", Description = "ABC-Einsatz - Klein", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "ABC2", Description = "ABC-Einsatz - Mittel", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "ABC3", Description = "ABC-Einsatz - Groß", IsActive = true, CreatedAt = DateTime.Now });
                
                // Ölspur
                Insert.IntoTable("Keyword").Row(new { Name = "Öl1", Description = "Ölspur - Klein", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "Öl2", Description = "Ölspur - Mittel", IsActive = true, CreatedAt = DateTime.Now });
                Insert.IntoTable("Keyword").Row(new { Name = "Öl3", Description = "Ölspur - Groß", IsActive = true, CreatedAt = DateTime.Now });

        // Create indexes for better performance
        Create.Index("IX_Keyword_Name").OnTable("Keyword").OnColumn("Name");
        Create.Index("IX_PersonalRequirement_KeywordId").OnTable("PersonalRequirement").OnColumn("KeywordId");
        Create.Index("IX_PersonalRequirement_FunctionDefId").OnTable("PersonalRequirement").OnColumn("FunctionDefId");
        Create.Index("IX_OperationList_KeywordId").OnTable("OperationList").OnColumn("KeywordId");
    }

    public override void Down()
    {
        Delete.Table("PersonalRequirement");
        Delete.Table("Keyword");
        Delete.Column("KeywordId").FromTable("OperationList");
    }
}
