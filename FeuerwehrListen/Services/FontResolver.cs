using PdfSharp.Fonts;
using System.Collections.Generic;

namespace FeuerwehrListen.Services;

public class FontResolver : IFontResolver
{
    private readonly Dictionary<string, FontResolverInfo> _fonts = new();
    private readonly Dictionary<string, byte[]> _fontData = new();

    public FontResolver()
    {
        LoadFont("CreatoDisplay-Regular", "CreatoDisplay-Regular.otf");
        LoadFont("CreatoDisplay-Bold", "CreatoDisplay-Bold.otf");
        LoadFont("CreatoDisplay-Medium", "CreatoDisplay-Medium.otf");
        LoadFont("CreatoDisplay-Light", "CreatoDisplay-Light.otf");
        
        _fonts["CreatoDisplay"] = new FontResolverInfo("CreatoDisplay-Regular", false, false);
        _fonts["CreatoDisplay-Bold"] = new FontResolverInfo("CreatoDisplay-Bold", true, false);
        _fonts["CreatoDisplay-Medium"] = new FontResolverInfo("CreatoDisplay-Medium", false, false);
        _fonts["CreatoDisplay-Light"] = new FontResolverInfo("CreatoDisplay-Light", false, false);
        
        _fonts["Arial"] = new FontResolverInfo("Arial", false, false);
        _fonts["Arial-Bold"] = new FontResolverInfo("Arial", true, false);
    }

    private void LoadFont(string fontName, string fileName)
    {
        try
        {
            var possiblePaths = new[]
            {
                Path.Combine("wwwroot", "fonts", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "fonts", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", fileName),
                Path.Combine(Environment.CurrentDirectory, "wwwroot", "fonts", fileName)
            };

            string? foundPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }

            if (foundPath != null)
            {
                _fontData[fontName] = File.ReadAllBytes(foundPath);
            }
            else
            {
                foreach (var path in possiblePaths)
                {
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    public byte[] GetFont(string faceName)
    {
        if (_fontData.TryGetValue(faceName, out var fontBytes))
        {
            return fontBytes;
        }
        
        return new byte[0];
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        try
        {
            if (familyName == "CreatoDisplay")
            {
                if (isBold)
                {
                    return new FontResolverInfo("CreatoDisplay-Bold", true, false);
                }
                else
                {
                    return new FontResolverInfo("CreatoDisplay-Regular", false, false);
                }
            }
            
            if (isBold)
            {
                return new FontResolverInfo("Arial", true, false);
            }
            else
            {
                return new FontResolverInfo("Arial", false, false);
            }
        }
        catch (Exception ex)
        {
            return new FontResolverInfo("Arial", false, false);
        }
    }
}
