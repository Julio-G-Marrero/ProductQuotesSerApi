using System.Globalization;
using HtmlAgilityPack;
using ProductQuotes.Exceptions;
using ProductQuotes.Interfaces;
using ProductQuotes.Models;

namespace ProductQuotes.Services;

/// <summary>
/// Implementación de <see cref="IProductQuoteServices"/> respaldada por el sitio de
/// Aldo Autopartes (aldoautopartes.com). A diferencia de SerpApi, este proveedor no acepta
/// texto libre real: busca por vehículo (marca/modelo/año), no por descripción de producto
/// (esa búsqueda es un substring literal contra el catálogo interno y no sirve para texto
/// arbitrario — investigado y descartado).
/// </summary>
/// <remarks>
/// <para>
/// <b>Formato esperado de <c>productName</c>:</b> <c>"Marca|Modelo|Año|Parte"</c>, separado por
/// pipes (ej. <c>"Chevrolet|Aveo|2018|Cofre"</c>). Es responsabilidad del llamador armar el
/// string en ese formato — pensado para un caller que ya tiene marca/modelo/año estructurados
/// (ej. desde una orden de trabajo con el vehículo ya identificado), no para un buscador de
/// texto libre. "Parte" es opcional: si viene vacío, devuelve todas las refacciones del
/// vehículo sin filtrar.
/// </para>
/// <para>
/// <b>Precio:</b> Aldo publica "precio sin IVA" y "precio con IVA" (el segundo es el primero
/// + 16% de IVA). El costo real del negocio es <c>precio_sin_iva * (1 - 0.375)</c> — Aldo
/// otorga 37.5% de descuento sobre el precio sin IVA. <see cref="ProductQuoteDto.Price"/>
/// devuelve ese costo ya calculado, no el precio publicado.
/// </para>
/// <para>
/// <b>country</b>/<b>language</b> no aplican (el sitio es México/español únicamente) — se
/// ignoran, se aceptan solo para cumplir el contrato de la interfaz.
/// </para>
/// </remarks>
public sealed class AldoAutopartesProductQuoteService : IProductQuoteServices
{
    public const string Key = "aldo";

    private const string BaseUrl = "http://www.aldoautopartes.com";
    private const decimal DescuentoAldo = 0.375m;
    private const string Store = "Aldo Autopartes";

    private static readonly HttpClient Http = CreateHttpClient();

    // Cache en memoria: nombre de marca (normalizado) -> id_marca. Se puebla una sola vez.
    private static readonly SemaphoreSlim MarcasLock = new(1, 1);
    private static Dictionary<string, string>? _marcasCache;

    // Cache en memoria: id_marca -> (nombre de modelo normalizado -> id_modelo). Por marca, bajo demanda.
    private static readonly SemaphoreSlim ModelosLock = new(1, 1);
    private static readonly Dictionary<string, Dictionary<string, string>> _modelosCache = [];

    public async Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country = "mx", string language = "es", int pageNumber = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("productName no puede estar vacío.", nameof(productName));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "pageNumber debe ser >= 1.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize debe ser >= 1.");

        var (marcaTexto, modeloTexto, anioTexto, parteTexto) = ParseProductName(productName);

        var idMarca = await ResolveMarcaIdAsync(marcaTexto);
        var idModelo = await ResolveModeloIdAsync(idMarca, modeloTexto);

        var html = await PostAsync(
            $"{BaseUrl}/pi_resultados.jsp",
            new Dictionary<string, string> { ["_action"] = "1", ["id_marca"] = idMarca, ["id_modelo"] = idModelo, ["anio"] = anioTexto });

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var quotes = new List<ProductQuoteDto>();
        // El sitio alterna clases de fila para el efecto cebra: "tablegridcella" (impares) y
        // "tablegridcellb" (pares) — hay que capturar ambas, no solo la "a".
        var rows = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'tablegridcell')]");
        if (rows is not null)
        {
            foreach (var row in rows)
            {
                var quote = TryParseRow(row);
                if (quote is null) continue;

                if (!string.IsNullOrWhiteSpace(parteTexto) &&
                    !quote.ProductName.Contains(parteTexto, StringComparison.OrdinalIgnoreCase))
                    continue;

                quotes.Add(quote);
            }
        }

        return quotes.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

    private static (string Marca, string Modelo, string Anio, string Parte) ParseProductName(string productName)
    {
        var partes = productName.Split('|', StringSplitOptions.TrimEntries);
        if (partes.Length < 3)
            throw new ArgumentException(
                "Formato esperado para este proveedor: \"Marca|Modelo|Año|Parte\" (Parte es opcional). " +
                $"Ej: \"Chevrolet|Aveo|2018|Cofre\". Recibido: \"{productName}\".",
                nameof(productName));

        var parte = partes.Length >= 4 ? partes[3] : string.Empty;
        return (partes[0], partes[1], partes[2], parte);
    }

    private static ProductQuoteDto? TryParseRow(HtmlNode row)
    {
        var cells = row.SelectNodes("./td");
        if (cells is null || cells.Count < 5) return null;

        var descripcion = HtmlEntity.DeEntitize(cells[2].InnerText).Trim();
        if (string.IsNullOrEmpty(descripcion)) return null;

        if (!TryParsePrecio(cells[3].InnerText, out var precioSinIva)) return null;

        var link = cells[2].SelectSingleNode(".//a")?.GetAttributeValue("href", string.Empty) ?? string.Empty;
        var url = string.IsNullOrEmpty(link) ? string.Empty : $"{BaseUrl}/{link.TrimStart('/')}";

        var imageUrl = cells.Count > 1
            ? cells[1].SelectSingleNode(".//img")?.GetAttributeValue("src", string.Empty) ?? string.Empty
            : string.Empty;

        var costoReal = precioSinIva * (1 - DescuentoAldo);

        return new ProductQuoteDto(descripcion, Store, costoReal, url, imageUrl, string.Empty);
    }

    private static bool TryParsePrecio(string texto, out decimal valor)
    {
        var limpio = texto.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    private static async Task<string> ResolveMarcaIdAsync(string marcaTexto)
    {
        var marcas = await GetMarcasAsync();
        var clave = Normalizar(marcaTexto);

        if (marcas.TryGetValue(clave, out var id)) return id;

        throw new ProductQuoteProviderException(
            $"No se encontró la marca '{marcaTexto}' en el catálogo de Aldo Autopartes.");
    }

    private static async Task<string> ResolveModeloIdAsync(string idMarca, string modeloTexto)
    {
        var modelos = await GetModelosAsync(idMarca);
        var clave = Normalizar(modeloTexto);

        if (modelos.TryGetValue(clave, out var id)) return id;

        throw new ProductQuoteProviderException(
            $"No se encontró el modelo '{modeloTexto}' para esa marca en el catálogo de Aldo Autopartes.");
    }

    private static async Task<Dictionary<string, string>> GetMarcasAsync()
    {
        if (_marcasCache is not null) return _marcasCache;

        await MarcasLock.WaitAsync();
        try
        {
            if (_marcasCache is not null) return _marcasCache;

            var html = await GetAsync($"{BaseUrl}/pi_busqueda.jsp");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            _marcasCache = ExtraerOpciones(doc, "id_marca");
            return _marcasCache;
        }
        finally
        {
            MarcasLock.Release();
        }
    }

    private static async Task<Dictionary<string, string>> GetModelosAsync(string idMarca)
    {
        if (_modelosCache.TryGetValue(idMarca, out var cached)) return cached;

        await ModelosLock.WaitAsync();
        try
        {
            if (_modelosCache.TryGetValue(idMarca, out cached)) return cached;

            var html = await PostAsync(
                $"{BaseUrl}/pi_busqueda.jsp",
                new Dictionary<string, string> { ["_action"] = "1", ["id_marca"] = idMarca, ["id_modelo"] = string.Empty, ["anio"] = string.Empty });

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var modelos = ExtraerOpciones(doc, "id_modelo");
            _modelosCache[idMarca] = modelos;
            return modelos;
        }
        finally
        {
            ModelosLock.Release();
        }
    }

    private static Dictionary<string, string> ExtraerOpciones(HtmlDocument doc, string selectId)
    {
        var resultado = new Dictionary<string, string>();
        var select = doc.GetElementbyId(selectId);
        var opciones = select?.SelectNodes(".//option");
        if (opciones is null) return resultado;

        foreach (var opcion in opciones)
        {
            var valor = opcion.GetAttributeValue("value", string.Empty).Trim();
            var texto = Normalizar(HtmlEntity.DeEntitize(opcion.InnerText));
            if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(texto)) continue;

            resultado[texto] = valor;
        }
        return resultado;
    }

    private static string Normalizar(string texto) => texto.Trim().ToUpperInvariant();

    private static async Task<string> GetAsync(string url)
    {
        try
        {
            using var response = await Http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new ProductQuoteProviderException($"Aldo Autopartes devolvió {(int)response.StatusCode}: {body}");
            return body;
        }
        catch (ProductQuoteProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductQuoteProviderException($"Falló la consulta a Aldo Autopartes ({url}): {ex.Message}", ex);
        }
    }

    private static async Task<string> PostAsync(string url, Dictionary<string, string> form)
    {
        try
        {
            using var response = await Http.PostAsync(url, new FormUrlEncodedContent(form));
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new ProductQuoteProviderException($"Aldo Autopartes devolvió {(int)response.StatusCode}: {body}");
            return body;
        }
        catch (ProductQuoteProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductQuoteProviderException($"Falló la consulta a Aldo Autopartes ({url}): {ex.Message}", ex);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        return client;
    }
}
