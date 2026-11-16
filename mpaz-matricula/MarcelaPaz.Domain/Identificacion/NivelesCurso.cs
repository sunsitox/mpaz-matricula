namespace MarcelaPaz.Domain.Identificacion;

/// <summary>
/// Niveles que imparte el colegio: de pre-kínder a 8º básico. El catálogo vive
/// en un solo lugar para que el formulario, la propuesta del curso anterior, la
/// búsqueda del curso en la base y el orden del panel no se contradigan.
/// </summary>
public static class NivelesCurso
{
    public const string PreKinder = "Pre-kínder";
    public const string Kinder = "Kínder";

    /// <summary>De menor a mayor: el orden en que se muestran y se recorren.</summary>
    public static readonly IReadOnlyList<string> Todos = new[]
    {
        PreKinder,
        Kinder,
        "1º básico",
        "2º básico",
        "3º básico",
        "4º básico",
        "5º básico",
        "6º básico",
        "7º básico",
        "8º básico",
    };

    /// <summary>
    /// Clave corta que usa el nombre del curso en la base: «PK A», «K A»,
    /// «1° A» … «8° B». Devuelve null si el nivel no pertenece al colegio.
    /// </summary>
    public static string? Token(string? nivel)
    {
        var texto = SinTildes(nivel).ToLowerInvariant().Replace(".", string.Empty).Trim();

        // Enseñanza media no pertenece al colegio: «1º medio» no es 1º básico.
        if (texto.Contains("medio", StringComparison.Ordinal) || texto.Contains("media", StringComparison.Ordinal))
        {
            return null;
        }

        switch (texto)
        {
            case "pre-kinder" or "pre kinder" or "prekinder" or "nt1":
                return "PK";
            case "kinder" or "nt2":
                return "K";
        }

        var numero = Digitos(texto);

        return numero.Length == 1 && numero[0] >= '1' && numero[0] <= '8' ? numero : null;
    }

    /// <summary>
    /// Nivel a partir del nombre del curso: «PK A» → «Pre-kínder», «K B» →
    /// «Kínder», «1° A» → «1º básico». Sirve para proponer el curso anterior.
    /// </summary>
    public static string? DesdeNombre(string? curso)
    {
        if (string.IsNullOrWhiteSpace(curso))
        {
            return null;
        }

        var palabra = curso
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];

        var clave = SinTildes(palabra).ToLowerInvariant().Replace(".", string.Empty);

        if (clave.Contains("medio", StringComparison.Ordinal) || clave.Contains("media", StringComparison.Ordinal))
        {
            return null;
        }

        switch (clave)
        {
            case "pk" or "pre-kinder" or "prekinder" or "nt1":
                return PreKinder;
            case "k" or "kinder" or "nt2":
                return Kinder;
        }

        var numero = Digitos(clave);

        return numero.Length == 1 && numero[0] >= '1' && numero[0] <= '8'
            ? $"{numero[0]}º básico"
            : null;
    }

    /// <summary>
    /// Posición del curso en el recorrido del colegio, para listar pre-kínder
    /// antes que 1º básico y no por orden alfabético. Un curso desconocido queda
    /// al final en vez de intercalarse.
    /// </summary>
    /// <summary>Último nivel del colegio: 8º básico. Quien lo termina egresa;
    /// no existe un curso superior al que renovar.
    /// </summary>
    public static bool EsUltimo(string? nivel) => nivel == Todos[^1];

    public static int Orden(string? curso)
    {
        var nivel = DesdeNombre(curso);

        if (nivel is null)
        {
            return Todos.Count;
        }

        for (var i = 0; i < Todos.Count; i += 1)
        {
            if (Todos[i] == nivel)
            {
                return i;
            }
        }

        return Todos.Count;
    }

    private static string Digitos(string texto) =>
        new(texto.Where(char.IsDigit).ToArray());

    private static string SinTildes(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var limpio = new System.Text.StringBuilder(descompuesto.Length);

        foreach (var letra in descompuesto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(letra)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(letra);
            }
        }

        return limpio.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
