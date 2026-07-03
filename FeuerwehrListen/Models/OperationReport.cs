using LinqToDB.Mapping;

namespace FeuerwehrListen.Models;

/// <summary>
/// Digitaler Einsatzbericht (Seiten 1-3 des Papierformulars), 1:1 zu einer Einsatzliste.
/// Kopf-Daten (Nr., Stichwort, Alarmzeit) werden aus der verknüpften OperationList gezogen;
/// hier stehen nur die berichtsspezifischen Zusatzfelder.
/// </summary>
[Table("OperationReport")]
public class OperationReport
{
    [PrimaryKey, Identity]
    [Column("Id")]
    public int Id { get; set; }

    /// <summary>FK auf OperationList (eindeutig – ein Bericht pro Einsatz).</summary>
    [Column("OperationListId"), NotNull]
    public int OperationListId { get; set; }

    // --- Kopf (Ergänzungen zur Einsatzliste) ---
    [Column("Einsatzleiter")] public string? Einsatzleiter { get; set; }
    [Column("OrtOrtsteil")] public string? OrtOrtsteil { get; set; }
    [Column("Strasse")] public string? Strasse { get; set; }

    // --- Brandeinsatz ---
    [Column("IsBrandeinsatz")] public bool IsBrandeinsatz { get; set; }
    [Column("BrandKleinbrandA")] public bool BrandKleinbrandA { get; set; }
    [Column("BrandKleinbrandB")] public bool BrandKleinbrandB { get; set; }
    [Column("BrandMittelbrand")] public bool BrandMittelbrand { get; set; }
    [Column("BrandGrossbrand")] public bool BrandGrossbrand { get; set; }
    [Column("BrandArtObjekt")] public string? BrandArtObjekt { get; set; }
    [Column("BrandAusbruchstelle")] public string? BrandAusbruchstelle { get; set; }
    [Column("BrandUrsache")] public string? BrandUrsache { get; set; }

    // --- TH- / ABC- und Öleinsätze ---
    [Column("IsThAbcOel")] public bool IsThAbcOel { get; set; }
    [Column("ThVuMitEingekl")] public bool ThVuMitEingekl { get; set; }
    [Column("ThVuOhneEingekl")] public bool ThVuOhneEingekl { get; set; }
    [Column("ThZugunfall")] public bool ThZugunfall { get; set; }
    [Column("ThFlugzeugunfall")] public bool ThFlugzeugunfall { get; set; }
    [Column("ThExplosion")] public bool ThExplosion { get; set; }
    [Column("ThGasgeruch")] public bool ThGasgeruch { get; set; }
    [Column("ThSonstigerAbc")] public bool ThSonstigerAbc { get; set; }
    [Column("ThWasserEisrettung")] public bool ThWasserEisrettung { get; set; }
    [Column("ThMenschenNotlage")] public bool ThMenschenNotlage { get; set; }
    [Column("ThTiereNotlage")] public bool ThTiereNotlage { get; set; }
    [Column("ThVerkehrshindernis")] public bool ThVerkehrshindernis { get; set; }
    [Column("ThWasserSturmschaden")] public bool ThWasserSturmschaden { get; set; }
    [Column("ThGefahrFallenderGegenstand")] public bool ThGefahrFallenderGegenstand { get; set; }
    [Column("ThAuslaufendeGuelle")] public bool ThAuslaufendeGuelle { get; set; }
    [Column("ThOeleinsatz")] public bool ThOeleinsatz { get; set; }
    [Column("ThSonstigeHilfeleistung")] public bool ThSonstigeHilfeleistung { get; set; }

    // --- Fehlalarm ---
    [Column("IsFehlalarm")] public bool IsFehlalarm { get; set; }
    [Column("FehlGutenGlauben")] public bool FehlGutenGlauben { get; set; }
    [Column("FehlBoeswillig")] public bool FehlBoeswillig { get; set; }
    [Column("FehlBrandmeldeanlage")] public bool FehlBrandmeldeanlage { get; set; }

    // --- Seite 2: Lagebericht ---
    [Column("Lagebericht")] public string? Lagebericht { get; set; }

    // --- Kostenpflichtiger Einsatz ---
    [Column("KostenpflichtStatus")] public KostenpflichtStatus KostenpflichtStatus { get; set; } = KostenpflichtStatus.Unbekannt;

    // Verursacher / Fahrzeughalter
    [Column("VerursacherName")] public string? VerursacherName { get; set; }
    [Column("VerursacherAnschrift")] public string? VerursacherAnschrift { get; set; }
    [Column("VerursacherGeburtsdatum")] public string? VerursacherGeburtsdatum { get; set; }
    [Column("VerursacherKfz")] public string? VerursacherKfz { get; set; }
    [Column("VerursacherFahrer")] public string? VerursacherFahrer { get; set; }

    // Geschädigter / Fahrzeughalter
    [Column("GeschaedigterName")] public string? GeschaedigterName { get; set; }
    [Column("GeschaedigterAnschrift")] public string? GeschaedigterAnschrift { get; set; }
    [Column("GeschaedigterGeburtsdatum")] public string? GeschaedigterGeburtsdatum { get; set; }
    [Column("GeschaedigterKfz")] public string? GeschaedigterKfz { get; set; }
    [Column("GeschaedigterFahrer")] public string? GeschaedigterFahrer { get; set; }

    // --- Dienststellen / Funktionen vor Ort ---
    [Column("WeitereFwLz")] public string? WeitereFwLz { get; set; }
    [Column("AnzahlKtw")] public int AnzahlKtw { get; set; }
    [Column("AnzahlRtw")] public int AnzahlRtw { get; set; }
    [Column("AnzahlNa")] public int AnzahlNa { get; set; }
    [Column("AnzahlRth")] public int AnzahlRth { get; set; }
    [Column("OrgLRd")] public bool OrgLRd { get; set; }
    [Column("Lna")] public bool Lna { get; set; }
    [Column("DrkErsthelfer")] public bool DrkErsthelfer { get; set; }
    [Column("SonstEinheiten")] public string? SonstEinheiten { get; set; }
    [Column("PolizeiKripo")] public bool PolizeiKripo { get; set; }
    [Column("StadtBillerbeck")] public bool StadtBillerbeck { get; set; }
    [Column("Kbm")] public bool Kbm { get; set; }
    [Column("Schornsteinfeger")] public bool Schornsteinfeger { get; set; }
    [Column("UntereWasserbehoerde")] public bool UntereWasserbehoerde { get; set; }
    [Column("Veterinaer")] public bool Veterinaer { get; set; }
    [Column("Thw")] public bool Thw { get; set; }
    [Column("Objektbetreiber")] public bool Objektbetreiber { get; set; }

    // --- Seite 3: Personenschäden ---
    [Column("AnzahlVerletzte")] public int AnzahlVerletzte { get; set; }
    [Column("AnzahlTote")] public int AnzahlTote { get; set; }
    [Column("AnzahlVerletzteFm")] public int AnzahlVerletzteFm { get; set; }

    // --- Schadenshöhe (freitext, um Locale/Komma-Probleme zu vermeiden) ---
    [Column("SchadenSachschaden")] public string? SchadenSachschaden { get; set; }
    [Column("SchadenErhalteneWerte")] public string? SchadenErhalteneWerte { get; set; }

    // --- Unterschriften ---
    [Column("UnterschriftIuk")] public string? UnterschriftIuk { get; set; }
    [Column("UnterschriftEinsatzleiter")] public string? UnterschriftEinsatzleiter { get; set; }
    // Digitale Unterschrift als PNG-Data-URL (optional).
    [Column("UnterschriftIukImage")] public string? UnterschriftIukImage { get; set; }
    [Column("UnterschriftEinsatzleiterImage")] public string? UnterschriftEinsatzleiterImage { get; set; }

    [Column("CreatedAt")] public DateTime CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }
}
