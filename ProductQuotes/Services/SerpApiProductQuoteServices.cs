using ProductQuotes.Exceptions;
using ProductQuotes.Interfaces;
using ProductQuotes.Models;
using System.Text.Json;

namespace ProductQuotes.Services;

/// <summary>
/// Implementación de <see cref="IProductQuoteServices"/> respaldada por Google Shopping vía SerpApi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Caveat de paginación (documentado por SerpApi):</b> para el "new layout" de Google Shopping
/// (el que usa esta cuenta — se identifica porque las respuestas traen
/// <c>immersive_product_page_token</c>), SerpApi advierte explícitamente que el parámetro
/// <c>start</c> "no es recomendado" y que lo correcto es seguir el link secuencial
/// <c>serpapi_pagination.next</c>. Esta implementación de todos modos calcula
/// <c>start = (pageNumber - 1) * pageSize</c> para cumplir el contrato de la interfaz tal como
/// fue especificado. Verificado empíricamente (2026-07-09): con <c>pageNumber == 1</c> los
/// resultados son confiables; a partir de <c>pageNumber == 2</c> se observó ~83% de resultados
/// duplicados respecto a la página anterior. Quien consuma esta librería debe asumir que solo
/// la primera página tiene cobertura completa y sin solapamiento garantizados.
/// </para>
/// <para>
/// <b>API key:</b> por decisión explícita del equipo, queda hardcodeada por ahora. Pendiente
/// evaluar migración a variable de entorno o inyección de dependencias.
/// </para>
/// </remarks>
public sealed class SerpApiProductQuoteService : IProductQuoteServices
{
    // TODO: migrar a variable de entorno o inyección de dependencias (decisión pendiente del equipo).
    private const string ApiKey = "15a263cf2f80126761cc6c7ca8e77b7a1cbe21c6b1223c16fcb44105d29557cf";
    private const string BaseUrl = "https://serpapi.com/search.json";

    private static readonly HttpClient Http = new();

    public async Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country = "mx", string language = "es", int pageNumber = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("productName no puede estar vacío.", nameof(productName));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "pageNumber debe ser >= 1.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize debe ser >= 1.");

        var start = (pageNumber - 1) * pageSize;
        var url = $"{BaseUrl}?engine=google_shopping" +
                  $"&q={Uri.EscapeDataString(productName)}" +
                  $"&gl={Uri.EscapeDataString(country)}" +
                  $"&hl={Uri.EscapeDataString(language)}" +
                  $"&start={start}" +
                  $"&api_key={ApiKey}";

        JsonDocument doc;
        try
        {
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ProductQuoteProviderException(
                    $"SerpApi devolvió {(int)response.StatusCode} {response.StatusCode}: {body}");

            doc = JsonDocument.Parse(body);
        }
        catch (ProductQuoteProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductQuoteProviderException(
                $"Falló la consulta a SerpApi para '{productName}': {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp))
                throw new ProductQuoteProviderException($"SerpApi devolvió un error: {errorProp.GetString()}");

            var quotes = new List<ProductQuoteDto>();
            if (root.TryGetProperty("shopping_results", out var results))
            {
                foreach (var item in results.EnumerateArray())
                {
                    // "Raw" pero con un mínimo de calidad: sin precio numérico parseable, no es una
                    // cotización utilizable (Price es decimal, no nullable) — se excluye.
                    if (!item.TryGetProperty("extracted_price", out var priceProp) ||
                        priceProp.ValueKind != JsonValueKind.Number)
                        continue;

                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    var source = item.TryGetProperty("source", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                    var link = item.TryGetProperty("product_link", out var l) ? l.GetString() ?? string.Empty : string.Empty;

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(link))
                        continue;

                    quotes.Add(new ProductQuoteDto(title, source, priceProp.GetDecimal(), link));
                }
            }

            return quotes.Take(pageSize).ToList();
        }
    }
}
