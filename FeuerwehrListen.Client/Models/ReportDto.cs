namespace FeuerwehrListen.Client.Models;

// Spiegelt FeuerwehrListen.Models.OperationReport (JSON-Round-Trip). Mutable für @bind.
public class ReportDto
{
    public int Id { get; set; }
    public int OperationListId { get; set; }
    public string? Einsatzleiter { get; set; }
    public string? OrtOrtsteil { get; set; }
    public string? Strasse { get; set; }

    public bool IsBrandeinsatz { get; set; }
    public bool BrandKleinbrandA { get; set; }
    public bool BrandKleinbrandB { get; set; }
    public bool BrandMittelbrand { get; set; }
    public bool BrandGrossbrand { get; set; }
    public string? BrandArtObjekt { get; set; }
    public string? BrandAusbruchstelle { get; set; }
    public string? BrandUrsache { get; set; }

    public bool IsThAbcOel { get; set; }
    public bool ThVuMitEingekl { get; set; }
    public bool ThVuOhneEingekl { get; set; }
    public bool ThZugunfall { get; set; }
    public bool ThFlugzeugunfall { get; set; }
    public bool ThExplosion { get; set; }
    public bool ThGasgeruch { get; set; }
    public bool ThSonstigerAbc { get; set; }
    public bool ThWasserEisrettung { get; set; }
    public bool ThMenschenNotlage { get; set; }
    public bool ThTiereNotlage { get; set; }
    public bool ThVerkehrshindernis { get; set; }
    public bool ThWasserSturmschaden { get; set; }
    public bool ThGefahrFallenderGegenstand { get; set; }
    public bool ThAuslaufendeGuelle { get; set; }
    public bool ThOeleinsatz { get; set; }
    public bool ThSonstigeHilfeleistung { get; set; }

    public bool IsFehlalarm { get; set; }
    public bool FehlGutenGlauben { get; set; }
    public bool FehlBoeswillig { get; set; }
    public bool FehlBrandmeldeanlage { get; set; }

    public string? Lagebericht { get; set; }
    public int KostenpflichtStatus { get; set; } // 0=Unbekannt,1=Ja,2=Nein

    public string? VerursacherName { get; set; }
    public string? VerursacherAnschrift { get; set; }
    public string? VerursacherGeburtsdatum { get; set; }
    public string? VerursacherKfz { get; set; }
    public string? VerursacherFahrer { get; set; }
    public string? GeschaedigterName { get; set; }
    public string? GeschaedigterAnschrift { get; set; }
    public string? GeschaedigterGeburtsdatum { get; set; }
    public string? GeschaedigterKfz { get; set; }
    public string? GeschaedigterFahrer { get; set; }

    public string? WeitereFwLz { get; set; }
    public int AnzahlKtw { get; set; }
    public int AnzahlRtw { get; set; }
    public int AnzahlNa { get; set; }
    public int AnzahlRth { get; set; }
    public bool OrgLRd { get; set; }
    public bool Lna { get; set; }
    public bool DrkErsthelfer { get; set; }
    public string? SonstEinheiten { get; set; }
    public bool PolizeiKripo { get; set; }
    public bool StadtBillerbeck { get; set; }
    public bool Kbm { get; set; }
    public bool Schornsteinfeger { get; set; }
    public bool UntereWasserbehoerde { get; set; }
    public bool Veterinaer { get; set; }
    public bool Thw { get; set; }
    public bool Objektbetreiber { get; set; }

    public bool HatMenschenrettung { get; set; }
    public string? MenschenrettungDauer { get; set; }
    public string? MenschenrettungPersonalaufwand { get; set; }
    public int AnzahlVerletzte { get; set; }
    public int AnzahlTote { get; set; }
    public int AnzahlVerletzteFm { get; set; }
    public string? SchadenSachschaden { get; set; }
    public string? SchadenErhalteneWerte { get; set; }

    public string? UnterschriftIuk { get; set; }
    public string? UnterschriftEinsatzleiter { get; set; }
    public string? UnterschriftIukImage { get; set; }
    public string? UnterschriftEinsatzleiterImage { get; set; }
}

public record ReportBundle(ReportDto Report, List<ExtForce> ExternalForces, List<Mittel> Mittel, List<VehStrength> VehicleStrengths, List<ReportEntry> Entries, string GesamtStaerke, NextcloudInfo Nextcloud);
public class ExtForce { public string? Rufname { get; set; } public string? Staerke { get; set; } }
public class Mittel { public string? Name { get; set; } public int Anzahl { get; set; } public string? Dauer { get; set; } public bool IsCustom { get; set; } }
public class VehStrength { public string? VehicleName { get; set; } public string? Staerke { get; set; } }
public record ReportEntry(string Name, string? Vehicle, string[] Functions, bool Breathing);
public record NextcloudInfo(bool Configured, string Folder);
