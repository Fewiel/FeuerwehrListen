using FluentMigrator;

namespace FeuerwehrListen.Migrations;

[Migration(20)]
public class Migration_020_AddOperationReport : Migration
{
    public override void Up()
    {
        Create.Table("OperationReport")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OperationListId").AsInt32().NotNullable()

            .WithColumn("Einsatzleiter").AsString(200).Nullable()
            .WithColumn("OrtOrtsteil").AsString(200).Nullable()
            .WithColumn("Strasse").AsString(200).Nullable()

            .WithColumn("IsBrandeinsatz").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BrandKleinbrandA").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BrandKleinbrandB").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BrandMittelbrand").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BrandGrossbrand").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("BrandArtObjekt").AsString(300).Nullable()
            .WithColumn("BrandAusbruchstelle").AsString(300).Nullable()
            .WithColumn("BrandUrsache").AsString(300).Nullable()

            .WithColumn("IsThAbcOel").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThVuMitEingekl").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThVuOhneEingekl").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThZugunfall").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThFlugzeugunfall").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThExplosion").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThGasgeruch").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThSonstigerAbc").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThWasserEisrettung").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThMenschenNotlage").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThTiereNotlage").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThVerkehrshindernis").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThWasserSturmschaden").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThGefahrFallenderGegenstand").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThAuslaufendeGuelle").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThOeleinsatz").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("ThSonstigeHilfeleistung").AsBoolean().NotNullable().WithDefaultValue(false)

            .WithColumn("IsFehlalarm").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("FehlGutenGlauben").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("FehlBoeswillig").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("FehlBrandmeldeanlage").AsBoolean().NotNullable().WithDefaultValue(false)

            .WithColumn("Lagebericht").AsString(int.MaxValue).Nullable()

            .WithColumn("KostenpflichtStatus").AsInt32().NotNullable().WithDefaultValue(0)

            .WithColumn("VerursacherName").AsString(200).Nullable()
            .WithColumn("VerursacherAnschrift").AsString(300).Nullable()
            .WithColumn("VerursacherGeburtsdatum").AsString(50).Nullable()
            .WithColumn("VerursacherKfz").AsString(50).Nullable()
            .WithColumn("VerursacherFahrer").AsString(200).Nullable()

            .WithColumn("GeschaedigterName").AsString(200).Nullable()
            .WithColumn("GeschaedigterAnschrift").AsString(300).Nullable()
            .WithColumn("GeschaedigterGeburtsdatum").AsString(50).Nullable()
            .WithColumn("GeschaedigterKfz").AsString(50).Nullable()
            .WithColumn("GeschaedigterFahrer").AsString(200).Nullable()

            .WithColumn("WeitereFwLz").AsString(int.MaxValue).Nullable()
            .WithColumn("AnzahlKtw").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnzahlRtw").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnzahlNa").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnzahlRth").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("OrgLRd").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Lna").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DrkErsthelfer").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("SonstEinheiten").AsString(300).Nullable()
            .WithColumn("PolizeiKripo").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("StadtBillerbeck").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Kbm").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Schornsteinfeger").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("UntereWasserbehoerde").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Veterinaer").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Thw").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("Objektbetreiber").AsBoolean().NotNullable().WithDefaultValue(false)

            .WithColumn("AnzahlVerletzte").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnzahlTote").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AnzahlVerletzteFm").AsInt32().NotNullable().WithDefaultValue(0)

            .WithColumn("SchadenSachschaden").AsString(100).Nullable()
            .WithColumn("SchadenErhalteneWerte").AsString(100).Nullable()

            .WithColumn("UnterschriftIuk").AsString(200).Nullable()
            .WithColumn("UnterschriftEinsatzleiter").AsString(200).Nullable()

            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Create.Index("IX_OperationReport_OperationListId")
            .OnTable("OperationReport")
            .OnColumn("OperationListId").Unique();
    }

    public override void Down()
    {
        Delete.Table("OperationReport");
    }
}
