namespace MarcelaPaz.Domain.Identificacion;

/// <summary>
/// RUN chileno. Se puede escribir con o sin puntos; la base lo guarda sin puntos
/// («11111111-K») y los documentos y pantallas lo muestran con puntos
/// («11.111.111-K»).
/// </summary>
public static class RutChileno
{
    /// <summary>«11.111.111-k» → «11111111-K». Deja el RUN como lo guarda la base.</summary>
    public static string Limpiar(string? rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
        {
            return string.Empty;
        }

        var limpio = new string(rut.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        return limpio.Length < 2
            ? limpio
            : $"{limpio[..^1]}-{limpio[^1]}";
    }

    /// <summary>«11111111-K» → «11.111.111-K». Si el texto no es un RUN, lo devuelve tal cual.</summary>
    public static string Formatear(string? rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
        {
            return string.Empty;
        }

        var limpio = Limpiar(rut);

        if (!EsValido(limpio))
        {
            return rut.Trim();
        }

        var cuerpo = limpio[..^2];
        var digito = limpio[^1];
        var conPuntos = string.Join(".", PartirEnTres(cuerpo));

        return $"{conPuntos}-{digito}";
    }

    /// <summary>Valida el dígito verificador con el módulo 11.</summary>
    public static bool EsValido(string? rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
        {
            return false;
        }

        var partes = Limpiar(rut).Split('-');

        if (partes.Length != 2 || partes[0].Length == 0 || !partes[0].All(char.IsDigit))
        {
            return false;
        }

        if (!int.TryParse(partes[0], out var cuerpo) || cuerpo <= 0)
        {
            return false;
        }

        var suma = 0;
        var factor = 2;

        foreach (var digito in partes[0].Reverse())
        {
            suma += (digito - '0') * factor;
            factor = factor == 7 ? 2 : factor + 1;
        }

        var resto = 11 - (suma % 11);
        var esperado = resto switch
        {
            11 => "0",
            10 => "K",
            _ => resto.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return string.Equals(partes[1], esperado, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> PartirEnTres(string cuerpo)
    {
        var grupos = new List<string>();

        for (var fin = cuerpo.Length; fin > 0; fin -= 3)
        {
            var inicio = Math.Max(0, fin - 3);
            grupos.Insert(0, cuerpo[inicio..fin]);
        }

        return grupos;
    }
}
