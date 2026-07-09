namespace ProductQuotes;

/// <summary>
/// Cotización de un producto en una tienda específica.
/// </summary>
/// <param name="ProductName">Título de la publicación/listing encontrado (no el término de búsqueda original).</param>
/// <param name="Store">Nombre de la tienda/vendedor.</param>
/// <param name="Price">Precio en pesos mexicanos (MXN). La librería solo cotiza en MXN por ahora.</param>
/// <param name="Url">
/// Link a la publicación. En el caso del proveedor de Google Shopping, este es un link intermedio
/// de Google (no el link directo del sitio del vendedor) — Google no expone el link directo en
/// los resultados de shopping_results.
/// </param>
public sealed record ProductQuoteDto(string ProductName, string Store, decimal Price, string Url);
