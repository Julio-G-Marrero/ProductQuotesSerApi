namespace ProductQuotes;

/// <summary>
/// Se lanza cuando falla la consulta al proveedor de cotizaciones (red, autenticación,
/// respuesta inválida, etc.). Se distingue deliberadamente de "cero resultados", que es
/// un caso válido y se representa como una lista vacía, no como una excepción.
/// </summary>
public sealed class ProductQuoteProviderException : Exception
{
    public ProductQuoteProviderException(string message) : base(message)
    {
    }

    public ProductQuoteProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
