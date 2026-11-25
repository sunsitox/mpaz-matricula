using MarcelaPaz.Web.Models;

namespace MarcelaPaz.Tests;

/// <summary>
/// Validación final de los bloques B (pasos 2 y 3: familia y apoderados) y C
/// (pasos 4 y 5: contactos de emergencia y personas autorizadas para el retiro).
/// Cubre lo que no se puede expresar con un atributo: valores que no existen en
/// el formulario, teléfonos sin dígitos suficientes y bloques que se contradicen
/// entre sí (RUN repetidos, contactos duplicados, el estudiante autorizándose a
/// sí mismo). Se suman a las pruebas de obligatoriedad de <c>BloqueCTests</c> y
/// de <c>FichaEtapa1Tests</c>.
/// </summary>
public class ValidacionPasos2a5Tests
{
    // RUN válidos usados en las pruebas
    private const string RutEstudiante = "12.345.678-5";
    private const string RutMadre = "6.532.187-4";
    private const string RutPadre = "22.222.222-2";

    private static FichaForm FormularioEstudiante() => new()
    {
        Tipo_Identificador = "RUN",
        Cedula_Identidad = RutEstudiante,
        Nombres = "Antonia",
        Apellido_Paterno = "Rojas",
    };

    private static FichaForm Paso2Valido() => new()
    {
        Tipo_Identificador = "RUN",
        Cedula_Identidad = RutEstudiante,
        Madre_Nombre_Apellido = "Ana Soto",
        Madre_Cedula_Identidad = RutMadre,
        Madre_Telefono = "+56 9 6123 4567",
        Madre_Escolaridad = "Educación media",
        Restriccion_Madre = "No",
    };

    private static FichaForm Paso3Valido() => new()
    {
        Principal_Nombre_Apellido = "Ana Soto",
        Principal_Cedula_Identidad = RutMadre,
        Principal_Telefono = "+56 9 6123 4567",
        Principal_Vinculo = "Madre",
    };

    private static FichaForm Paso4Valido() => new()
    {
        Contacto1_Identificacion = "María Rojas",
        Contacto1_Telefono = "+56 9 6123 4567",
        Contacto1_Vinculo = "Abuela",
    };

    private static FichaForm Paso5Valido() => new()
    {
        Tipo_Identificador = "RUN",
        Cedula_Identidad = RutEstudiante,
        SalidaSolo = "No",
        Retiro1_Rut = RutMadre,
        Retiro1_Vinculo = "Madre",
    };

    // ── Paso 2 · familia (bloque B) ──────────────────────────────────────────

    [Fact]
    public void Paso2_FormularioCompleto_NoDejaErrores()
    {
        Assert.Empty(ValidadorFicha.Validar(2, Paso2Valido()));
    }

    [Fact]
    public void Paso2_SinMadreNiPadre_ExigeAlMenosUnBloque()
    {
        var errores = ValidadorFicha.Validar(2, FormularioEstudiante());

        Assert.Contains(errores, error => error.Campo is null
            && error.Mensaje.Contains("madre", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Tal vez")]
    [InlineData("NS/NR")]
    public void Paso2_RestriccionConValorQueNoOfreceElFormulario_SeRechaza(string respuesta)
    {
        var form = Paso2Valido();
        form.Restriccion_Madre = respuesta;

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Restriccion_Madre));
    }

    [Fact]
    public void Paso2_RestriccionTipoFueraDeLaLista_SeRechaza()
    {
        var form = Paso2Valido();
        form.Restriccion_Madre = "Si";
        form.Restriccion_Madre_Tipo = "Administrativa";
        form.Restriccion_Madre_Detalle = "Resolución interna";

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Tipo));
        Assert.DoesNotContain(errores, error => error.Campo == nameof(FichaForm.Restriccion_Madre_Detalle));
    }

    [Theory]
    [InlineData("Judicial")]
    [InlineData("Familiar")]
    public void Paso2_RestriccionTipoDeLaLista_SeAcepta(string tipo)
    {
        var form = Paso2Valido();
        form.Restriccion_Madre = "Si";
        form.Restriccion_Madre_Tipo = tipo;
        form.Restriccion_Madre_Detalle = "Orden vigente";

        var errores = ValidadorFicha.Validar(2, form);

        Assert.DoesNotContain(errores, error => error.Campo is not null);
    }

    [Fact]
    public void Paso2_MadreYPadreConElMismoRun_SeRechaza()
    {
        var form = Paso2Valido();
        form.Padre_Nombre_Apellido = "Ana Soto";
        form.Padre_Cedula_Identidad = RutMadre;
        form.Padre_Telefono = "+56 9 2233 4455";
        form.Restriccion_Padre = "No";

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Padre_Cedula_Identidad));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("+56 9")]
    [InlineData("sin número")]
    public void Paso2_TelefonoSinDigitosSuficientes_SeRechaza(string telefono)
    {
        var form = Paso2Valido();
        form.Madre_Telefono = telefono;

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Madre_Telefono));
    }

    [Fact]
    public void Paso2_EscolaridadFueraDeLaLista_SeRechaza()
    {
        var form = Paso2Valido();
        form.Madre_Escolaridad = "Doctorado honoris causa";

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Madre_Escolaridad));
    }

    [Fact]
    public void Paso2_RestriccionDelPadreSinDatosDelPadre_SeRechaza()
    {
        var form = Paso2Valido();
        form.Restriccion_Padre = "Si";

        var errores = ValidadorFicha.Validar(2, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Restriccion_Padre));
    }

    // ── Paso 3 · apoderados (bloque B) ───────────────────────────────────────

    [Fact]
    public void Paso3_PrincipalCompleto_NoDejaErrores()
    {
        Assert.Empty(ValidadorFicha.Validar(3, Paso3Valido()));
    }

    [Fact]
    public void Paso3_SinApoderadoPrincipal_ExigeSusDatos()
    {
        var errores = ValidadorFicha.Validar(3, new FichaForm());

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Principal_Nombre_Apellido));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Principal_Cedula_Identidad));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Principal_Telefono));
        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Principal_Vinculo));
    }

    [Fact]
    public void Paso3_SecundarioConElRunDelPrincipal_SeRechaza()
    {
        var form = Paso3Valido();
        form.Secundario_Nombre_Apellido = "Ana Soto";
        form.Secundario_Cedula_Identidad = RutMadre;
        form.Secundario_Telefono = "+56 9 2233 4455";
        form.Secundario_Vinculo = "Madre";

        var errores = ValidadorFicha.Validar(3, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Secundario_Cedula_Identidad));
    }

    [Fact]
    public void Paso3_SecundarioDistinto_SeAcepta()
    {
        var form = Paso3Valido();
        form.Secundario_Nombre_Apellido = "Luis Rojas";
        form.Secundario_Cedula_Identidad = RutPadre;
        form.Secundario_Telefono = "+56 9 2233 4455";
        form.Secundario_Vinculo = "Padre";

        Assert.Empty(ValidadorFicha.Validar(3, form));
    }

    [Fact]
    public void Paso3_TelefonoDelPrincipalSinDigitos_SeRechaza()
    {
        var form = Paso3Valido();
        form.Principal_Telefono = "—";

        var errores = ValidadorFicha.Validar(3, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Principal_Telefono));
    }

    // ── Paso 4 · contactos de emergencia (bloque C) ──────────────────────────

    [Fact]
    public void Paso4_ContactoCompleto_NoDejaErrores()
    {
        Assert.Empty(ValidadorFicha.Validar(4, Paso4Valido()));
    }

    [Fact]
    public void Paso4_DosContactosConElMismoTelefono_SeRechaza()
    {
        var form = Paso4Valido();
        form.Contacto2_Identificacion = "María Rojas";
        form.Contacto2_Telefono = "56961234567";
        form.Contacto2_Vinculo = "Abuela";

        var errores = ValidadorFicha.Validar(4, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto2_Telefono));
    }

    [Fact]
    public void Paso4_DosContactosDistintos_SeAceptan()
    {
        var form = Paso4Valido();
        form.Contacto2_Identificacion = "Pedro Díaz";
        form.Contacto2_Telefono = "+56 9 2233 4455";
        form.Contacto2_Vinculo = "Vecino";

        Assert.Empty(ValidadorFicha.Validar(4, form));
    }

    [Fact]
    public void Paso4_TelefonoDelContactoInvalido_SeRechaza()
    {
        var form = Paso4Valido();
        form.Contacto1_Telefono = "12";

        var errores = ValidadorFicha.Validar(4, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Contacto1_Telefono));
    }

    // ── Paso 5 · retiro (bloque C) ───────────────────────────────────────────

    [Fact]
    public void Paso5_UnAutorizadoCompleto_NoDejaErrores()
    {
        Assert.Empty(ValidadorFicha.Validar(5, Paso5Valido()));
    }

    [Theory]
    [InlineData("A veces")]
    [InlineData("Depende")]
    public void Paso5_SalidaSoloConValorQueNoOfreceElFormulario_SeRechaza(string respuesta)
    {
        var form = Paso5Valido();
        form.SalidaSolo = respuesta;

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.SalidaSolo));
    }

    [Fact]
    public void Paso5_DosAutorizadosConElMismoRun_SeRechaza()
    {
        var form = Paso5Valido();
        form.Retiro2_Rut = RutMadre;
        form.Retiro2_Vinculo = "Madre";

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Retiro2_Rut));
    }

    [Fact]
    public void Paso5_TresAutorizadosDistintos_SeAceptan()
    {
        var form = Paso5Valido();
        form.Retiro2_Rut = RutPadre;
        form.Retiro2_Vinculo = "Padre";
        form.Retiro3_Rut = "11.111.111-1";
        form.Retiro3_Vinculo = "Tía";

        Assert.Empty(ValidadorFicha.Validar(5, form));
    }

    [Fact]
    public void Paso5_ElEstudianteAutorizadoASiMismo_SeRechaza()
    {
        var form = Paso5Valido();
        form.Retiro1_Rut = RutEstudiante;
        form.Retiro1_Vinculo = "Yo";

        var errores = ValidadorFicha.Validar(5, form);

        Assert.Contains(errores, error => error.Campo == nameof(FichaForm.Retiro1_Rut));
    }

    [Fact]
    public void Paso5_ConIpeNoSeComparaConRun()
    {
        // Con IPE el documento no es un RUN: la comparación con las personas
        // autorizadas no aplica y no debe producir un falso error.
        var form = Paso5Valido();
        form.Tipo_Identificador = "IPE";
        form.Cedula_Identidad = "IPE-2027-001";

        Assert.Empty(ValidadorFicha.Validar(5, form));
    }

    // ── Integración de los bloques B y C ─────────────────────────────────────

    [Fact]
    public void BloquesBYC_CompletosYCoherentes_PasanLosCuatroPasos()
    {
        var form = FormularioEstudiante();
        form.Madre_Nombre_Apellido = "Ana Soto";
        form.Madre_Cedula_Identidad = RutMadre;
        form.Madre_Telefono = "+56 9 6123 4567";
        form.Madre_Escolaridad = "Educación universitaria";
        form.Restriccion_Madre = "No";

        form.Padre_Nombre_Apellido = "Luis Rojas";
        form.Padre_Cedula_Identidad = RutPadre;
        form.Padre_Telefono = "+56 9 2233 4455";
        form.Restriccion_Padre = "No";

        form.Principal_Nombre_Apellido = "Ana Soto";
        form.Principal_Cedula_Identidad = RutMadre;
        form.Principal_Telefono = "+56 9 6123 4567";
        form.Principal_Vinculo = "Madre";
        form.Secundario_Nombre_Apellido = "Luis Rojas";
        form.Secundario_Cedula_Identidad = RutPadre;
        form.Secundario_Telefono = "+56 9 2233 4455";
        form.Secundario_Vinculo = "Padre";

        form.Contacto1_Identificacion = "María Rojas";
        form.Contacto1_Telefono = "+56 9 6123 4567";
        form.Contacto1_Vinculo = "Abuela";
        form.Contacto2_Identificacion = "Pedro Díaz";
        form.Contacto2_Telefono = "+56 9 3344 5566";
        form.Contacto2_Vinculo = "Vecino";

        form.SalidaSolo = "No";
        form.Retiro1_Rut = RutMadre;
        form.Retiro1_Vinculo = "Madre";
        form.Retiro2_Rut = RutPadre;
        form.Retiro2_Vinculo = "Padre";

        for (var paso = 2; paso <= 5; paso++)
        {
            Assert.Empty(ValidadorFicha.Validar(paso, form));
        }
    }

    [Fact]
    public void BloquesBYC_ConErroresEnCadaPaso_SeDetectanAntesDeGuardar()
    {
        var form = new FichaForm { SalidaSolo = "Si" };

        Assert.NotEmpty(ValidadorFicha.Validar(2, form));
        Assert.NotEmpty(ValidadorFicha.Validar(3, form));
        Assert.NotEmpty(ValidadorFicha.Validar(4, form));
    }
}
