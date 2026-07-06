using System.Text;
using FeuerwehrListen.Models;
using QRCoder;

namespace FeuerwehrListen.Services;

/// <summary>
/// Baut die 2D-Vorschau (SVG) eines Feuerwehr-Tags: blaue Tag-Flaeche (Body) + rote
/// Texte/QR (Inlay) an den exakten SCAD-Positionen. Reines C#, guenstig - laeuft auf dem
/// Server ohne Last.
///
/// Das eigentliche STL-Rendering (OpenSCAD) laeuft NICHT hier, sondern client-seitig ueber
/// den lokalen Helfer (tools/fwtag-helper) auf dem PC des Admins - die kleine VM soll nicht
/// mit CGAL-Rendering belastet werden.
/// </summary>
public class TagRenderService
{
    // Name-Format wie in der SCAD-Pipeline (export_all.sh): "V. Nachname".
    // Robust fuer unvollstaendige Daten: nur-Nachname -> Nachname; nur-Vorname -> ganzer
    // Vorname (statt einzelner Initiale); gar kein Name -> leer (Datenpflege noetig).
    public static string DisplayName(Member m)
    {
        var first = (m.FirstName ?? string.Empty).Trim();
        var last = (m.LastName ?? string.Empty).Trim();
        if (first.Length > 0 && last.Length > 0) return $"{first[..1]}. {last}";
        if (last.Length > 0) return last;
        if (first.Length > 0) return first;
        return string.Empty;
    }

    // ---- Tag-Geometrie (muss mit feuerwehr_tag.scad uebereinstimmen) ----
    private const double TagWidth = 40, TagHeight = 60, CornerR = 5;
    private const double SlotWidth = 15, SlotHeight = 3.5, SlotCornerR = 1.75, SlotYOffset = 4.5;
    private const double QrSize = 27;
    // Layout-Y (aus der Mitte, positiv = oben)
    private const double Line1Y = 20, Line2Y = 15, NameY = 8, NumberY = 3, QrY = -14;
    private const double SizeLine = 3.8, SizeName = 3.5, SizeNumber = 5.0;
    private const string Line1 = "FEUERWEHR", Line2 = "BILLERBECK";

    // ============================================================
    // Vorschau-SVG (Draufsicht, Body blau + Inlay rot an SCAD-Positionen)
    // ============================================================
    public string BuildPreviewSvg(Member member)
    {
        var name = DisplayName(member);
        var number = (member.MemberNumber ?? string.Empty).Trim();

        const string blue = "#1657b0"; // Body
        const string red = "#e4002b";  // Inlay

        // SCAD (Mitte-Ursprung, +y oben) -> SVG (oben-links, +y unten)
        static double X(double x) => TagWidth / 2 + x;
        static double Y(double y) => TagHeight / 2 - y;

        var sb = new StringBuilder();
        var slotX = X(0) - SlotWidth / 2;
        var slotY = Y(TagHeight / 2 - SlotYOffset) - SlotHeight / 2;

        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"-4 -4 {TagWidth + 8} {TagHeight + 8}\" font-family=\"Arial, Liberation Sans, sans-serif\">");
        // Maske: Tag-Flaeche minus Schluesselring-Schlitz
        sb.Append("<defs><mask id=\"tagmask\">");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{TagWidth}\" height=\"{TagHeight}\" rx=\"{CornerR}\" fill=\"white\"/>");
        sb.Append($"<rect x=\"{F(slotX)}\" y=\"{F(slotY)}\" width=\"{SlotWidth}\" height=\"{SlotHeight}\" rx=\"{SlotCornerR}\" fill=\"black\"/>");
        sb.Append("</mask></defs>");
        // Body (blau)
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{TagWidth}\" height=\"{TagHeight}\" rx=\"{CornerR}\" fill=\"{blue}\" mask=\"url(#tagmask)\"/>");

        // Rote Texte (Inlay)
        AppendText(sb, Line1, X(0), Y(Line1Y), SizeLine, red);
        AppendText(sb, Line2, X(0), Y(Line2Y), SizeLine, red);
        AppendText(sb, name, X(0), Y(NameY), SizeName, red);
        AppendText(sb, number, X(0), Y(NumberY), SizeNumber, red);

        // QR (Inlay, rot) - echter QR mit gleichem Inhalt wie im STL
        AppendQr(sb, number, X(0), Y(QrY), QrSize, red);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void AppendText(StringBuilder sb, string text, double cx, double cy, double size, string fill)
    {
        sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy)}\" font-size=\"{F(size)}\" font-weight=\"bold\" fill=\"{fill}\" " +
                  $"text-anchor=\"middle\" dominant-baseline=\"central\">{Xml(text)}</text>");
    }

    private static void AppendQr(StringBuilder sb, string content, double cx, double cy, double size, string fill)
    {
        var data = new QRCodeGenerator().CreateQrCode(
            string.IsNullOrWhiteSpace(content) ? " " : content, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        int n = matrix.Count;
        if (n == 0) return;
        double module = size / n;
        double x0 = cx - size / 2, y0 = cy - size / 2;
        sb.Append("<g>");
        for (int r = 0; r < n; r++)
        {
            var row = matrix[r];
            for (int c = 0; c < n; c++)
            {
                if (!row[c]) continue;
                sb.Append($"<rect x=\"{F(x0 + c * module)}\" y=\"{F(y0 + r * module)}\" width=\"{F(module + 0.02)}\" height=\"{F(module + 0.02)}\" fill=\"{fill}\"/>");
            }
        }
        sb.Append("</g>");
    }

    private static string F(double d) => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Xml(string s) => (s ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
