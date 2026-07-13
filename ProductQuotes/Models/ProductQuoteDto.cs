namespace ProductQuotes.Models;

/// <summary>
/// Cotización de un producto en una tienda específica.
/// </summary>
/// <param name="ProductName">Título de la publicación/listing encontrado (no el término de búsqueda original).</param>
/// <param name="Store">Nombre de la tienda/vendedor.</param>
/// <param name="Price">Precio en pesos mexicanos (MXN). La librería solo cotiza en MXN por ahora.</param>
/// <param name="Url">Link a la publicación.
/// <param name="ImageUrl">URL de la miniatura del producto. Vacío si el proveedor no la tiene.</param>
public sealed record ProductQuoteDto(
    string ProductName,
    string Store,
    decimal Price,
    string Url,
    string ImageUrl = "");
