using System.Globalization;
using System.Net;
using System.Text.Json;
using ProductQuotes.Exceptions;
using ProductQuotes.Interfaces;
using ProductQuotes.Models;

namespace ProductQuotes.Services;

/// <summary>
/// Implementación de <see cref="IProductQuoteServices"/> respaldada por el sitio de
/// Optimo Autopartes (optimoautopartes.com.mx). A diferencia de Aldo, acepta texto libre
/// real: el endpoint de búsqueda del sitio combina descripción, marca y modelo en un solo
/// campo de texto, así que <c>productName</c> se envía tal cual (ej. "espejo aveo 2018").
/// </summary>
/// <remarks>
/// <para>
/// <b>Autenticación:</b> el catálogo público (sin sesión) expone precio de lista, pero
/// el costo real de proveedor solo aparece cuando la sesión está autenticada (mismo
/// endpoint, el campo pasa de <c>Precio</c> a <c>PrecioActual</c>). Investigado y
/// verificado: el descuento entre ambos precios NO es un porcentaje fijo (varía entre
/// ~41.6% y 43% según el producto, porque el precio público está redondeado a números
/// "bonitos"), así que no es viable calcularlo — esta implementación inicia sesión real
/// contra el sitio para obtener el precio exacto, en vez de estimarlo.
/// </para>
/// <para>
/// <b>Credenciales:</b> se leen de las variables de entorno <c>OPTIMO_USUARIO</c> y
/// <c>OPTIMO_PASSWORD</c> (nunca hardcodeadas — decisión explícita del equipo, distinta
/// a la de la API key de SerpApi, porque esto es usuario/contraseña de una cuenta de
/// cliente real). Si no están configuradas, lanza <see cref="ProductQuoteProviderException"/>.
/// </para>
/// <para>
/// <b>Sesión:</b> el login se hace una sola vez por proceso y se cachea (cookies en
/// memoria). El éxito del login NO se asume por el código de estado HTTP del POST — se
/// verifica leyendo <c>authData.Autentificado</c> en la respuesta real de búsqueda; si
/// sale <c>false</c> (sesión expirada o credenciales inválidas), reintenta el login una
/// vez y, si sigue fallando, lanza <see cref="ProductQuoteProviderException"/> en vez de
/// devolver precio público disfrazado de precio de proveedor.
/// </para>
/// <para>
/// <b>Url:</b> el sitio no tiene página de detalle de producto navegable (el botón
/// "ver detalle" del catálogo es un placeholder que no enlaza a nada) — se regresa la
/// URL del catálogo general.
/// </para>
/// </remarks>
public sealed class OptimoAutopartesProductQuoteService : IProductQuoteServices
{
    public const string Key = "optimo";

    private const string BaseUrl = "https://www.optimoautopartes.com.mx";
    private const string CatalogUrl = $"{BaseUrl}/productos_/";
    private const string Store = "Optimo Autopartes";

    private static readonly CookieContainer Cookies = new();
    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly SemaphoreSlim LoginLock = new(1, 1);
    private static bool _autenticado;

    public async Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country = "mx", string language = "es", int pageNumber = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("productName no puede estar vacío.", nameof(productName));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "pageNumber debe ser >= 1.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize debe ser >= 1.");

        var doc = await BuscarConSesionAsync(productName);

        var quotes = new List<ProductQuoteDto>();
        if (doc.RootElement.TryGetProperty("Productos", out var productos) && productos.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in productos.EnumerateArray())
            {
                var quote = TryParseProducto(item);
                if (quote is not null) quotes.Add(quote);
            }
        }

        return quotes.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

    private static ProductQuoteDto? TryParseProducto(JsonElement item)
    {
        var descripcion = GetString(item, "Descripcion");
        if (string.IsNullOrEmpty(descripcion)) return null;

        if (!TryGetPrecio(item, out var precio)) return null;

        var codigo = GetString(item, "Codigo");
        var imagen = GetString(item, "Imagen");
        var imageUrl = imagen == "1" && !string.IsNullOrEmpty(codigo)
            ? $"{BaseUrl}/imgcat/{codigo}tiny.jpg"
            : string.Empty;

        return new ProductQuoteDto(descripcion, Store, precio, CatalogUrl, imageUrl);
    }

    private static bool TryGetPrecio(JsonElement item, out decimal precio)
    {
        // Se llega aquí solo con una respuesta ya confirmada como autenticada
        // (ver BuscarConSesionAsync), así que el precio real de proveedor siempre
        // viene en "PrecioActual". Deliberadamente NO se hace fallback a "Precio"
        // (precio de lista público) — mezclar ambos en silencio fue un bug real,
        // detectado probando con credenciales inválidas.
        var texto = GetString(item, "PrecioActual");
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out precio);
    }

    private static string GetString(JsonElement item, string propiedad) =>
        item.TryGetProperty(propiedad, out var valor) ? valor.GetString() ?? string.Empty : string.Empty;

    private static bool EstaAutenticado(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("authData", out var authData) &&
        authData.TryGetProperty("Autentificado", out var autentificado) &&
        autentificado.ValueKind == JsonValueKind.True;

    private static async Task<JsonDocument> BuscarAsync(string texto)
    {
        var body = await PostAsync($"{BaseUrl}/productos_/datosProductosGlobal",
            new Dictionary<string, string> { ["Texto"] = texto });

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (Exception ex)
        {
            throw new ProductQuoteProviderException(
                $"Optimo Autopartes devolvió una respuesta inesperada para '{texto}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Garantiza una sesión autenticada y ejecuta la búsqueda, verificando el resultado
    /// real (<c>authData.Autentificado</c>) en vez de asumir que el login sirvió solo
    /// porque el POST no lanzó una excepción HTTP.
    /// </summary>
    private static async Task<JsonDocument> BuscarConSesionAsync(string texto)
    {
        await LoginLock.WaitAsync();
        try
        {
            if (!_autenticado) await LoginAsync();

            var doc = await BuscarAsync(texto);
            if (EstaAutenticado(doc))
            {
                _autenticado = true;
                return doc;
            }

            // Sesión ausente, expirada o credenciales inválidas: reintenta login una vez.
            _autenticado = false;
            await LoginAsync();
            doc = await BuscarAsync(texto);
            if (!EstaAutenticado(doc))
                throw new ProductQuoteProviderException(
                    "No se pudo iniciar sesión en Optimo Autopartes (verifica OPTIMO_USUARIO / OPTIMO_PASSWORD). " +
                    "Sin sesión autenticada no es posible obtener el precio real de proveedor.");

            _autenticado = true;
            return doc;
        }
        finally
        {
            LoginLock.Release();
        }
    }

    private static async Task LoginAsync()
    {
        var usuario = Environment.GetEnvironmentVariable("OPTIMO_USUARIO");
        var password = Environment.GetEnvironmentVariable("OPTIMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            throw new ProductQuoteProviderException(
                "Faltan las variables de entorno OPTIMO_USUARIO / OPTIMO_PASSWORD requeridas para autenticar con Optimo Autopartes.");

        await PostAsync($"{BaseUrl}/productos_/Login",
            new Dictionary<string, string> { ["claveusuario"] = usuario, ["password"] = password });
    }

    private static async Task<string> PostAsync(string url, Dictionary<string, string> form)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form)
            };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            using var response = await Http.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Redirect &&
                response.StatusCode != HttpStatusCode.SeeOther)
                throw new ProductQuoteProviderException($"Optimo Autopartes devolvió {(int)response.StatusCode}: {responseBody}");

            return responseBody;
        }
        catch (ProductQuoteProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProductQuoteProviderException($"Falló la consulta a Optimo Autopartes ({url}): {ex.Message}", ex);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            AllowAutoRedirect = false
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
        return client;
    }
}
