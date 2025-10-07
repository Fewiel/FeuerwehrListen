using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FeuerwehrListen.Services;

public class GeocodingService
{
    private readonly HttpClient _httpClient;

    public GeocodingService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FeuerwehrListen", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(double? lat, double? lon)> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return (null, null);

        var url = $"search?format=jsonv2&q={Uri.EscapeDataString(address)}&limit=1";
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<List<NominatimResult>>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);
            var first = result?.FirstOrDefault();
            if (first == null) return (null, null);
            if (double.TryParse(first.Lat, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(first.Lon, System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                return (lat, lon);
            }
            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    private sealed class NominatimResult
    {
        public string Lat { get; set; } = string.Empty;
        public string Lon { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}


