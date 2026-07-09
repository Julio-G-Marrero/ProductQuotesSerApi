# ProductQuotesSerApi

Biblioteca de clases en .NET 10 que expone un contrato genérico para obtener cotizaciones de
producto desde una fuente externa (implementación actual: Google Shopping vía SerpApi).

## Contrato

```csharp
public interface IProductQuoteProvider
{
    Task<List<ProductQuoteDto>> GetProductQuotes(
        string productName, string country, string language, int pageNumber, int pageSize);
}

public sealed record ProductQuoteDto(string ProductName, string Store, decimal Price, string Url);
```

## Uso

```csharp
services.AddProductQuotes(); // registra IProductQuoteProvider -> SerpApiProductQuoteProvider
```

## Notas conocidas

- Devuelve datos crudos, sin filtrado de outliers (queda a cargo del consumidor).
- Solo cotiza en pesos mexicanos (MXN) por ahora.
- `pageNumber == 1` es confiable. Para `pageNumber > 1`, SerpApi documenta que el parámetro
  `start` no es recomendado para el "new layout" de Google Shopping — puede haber resultados
  solapados. Ver comentario XML en `SerpApiProductQuoteProvider.cs`.
- La API key de SerpApi está hardcodeada en `SerpApiProductQuoteProvider.cs` — pendiente
  decisión del equipo sobre migrar a variable de entorno o configuración inyectada.
