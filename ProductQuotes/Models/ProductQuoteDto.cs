namespace ProductQuotes.Models;

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
/// <param name="ImageUrl">URL de la miniatura del producto. Vacío si el proveedor no la tiene.</param>
/// <param name="ImmersiveProductPageToken">
/// Token de Google (solo lo produce el proveedor SerpApi/Google Shopping) que permite resolver el
/// link directo del vendedor y más detalle del producto vía el motor <c>google_immersive_product</c>
/// — investigado pero todavía no implementado como resolución automática por el costo en créditos
/// (1 llamada extra por producto). Se devuelve crudo para que el consumidor decida cuándo vale la
/// pena resolverlo. Vacío si el proveedor no lo soporta.
/// </param>
public sealed record ProductQuoteDto(
    string ProductName,
    string Store,
    decimal Price,
    string Url,
    string ImageUrl = "",
    string ImmersiveProductPageToken = "");
