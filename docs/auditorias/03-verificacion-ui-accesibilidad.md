# Verificación complementaria de UI y accesibilidad

Fecha: 07-11-2026.

## Entorno y aislamiento

- Edge headless, Lighthouse 12.8.2 y axe-core 4.14.0.
- Servidor de prueba: http://127.0.0.1:5196, autenticación exigida y login real de funcionario.
- Copia temporal de la base local: `mpaz_a11y_verificacion`. El recorrido escribió únicamente en esta copia; no se cerraron matrículas en la base original.
- Compilación en directorio independiente: correcta, 0 errores y 6 advertencias existentes (NU1510, CS0114 y CS8601). La primera compilación en el directorio habitual falló por ejecutable bloqueado por una instancia de Visual Studio; no se detuvo esa instancia.
- Resultados estructurados, sin cookies ni contraseñas: `03-resultados-verificacion.json`.

## Resultados

| Página | Lighthouse accesibilidad | axe |
|---|---:|---:|
| Alumnos | 100/100 | Cubierta en la ronda anterior |
| Detalle de alumno | 100/100 | Cubierta en la ronda anterior |
| Panel Home | 100/100 | No ejecutado en esta ronda |
| Paso 1 | 100/100 | 0 violaciones |
| Paso 2 | 100/100 | 0 violaciones |
| Paso 3 | 100/100 | 0 violaciones |
| Paso 4 | 100/100 | 0 violaciones |
| Paso 5 | 100/100 | 0 violaciones |
| Paso 6 | 100/100 | 0 violaciones |
| Paso 7 | 100/100 | 0 violaciones |
| Confirmación con folio | 100/100 tras corrección | 0 violaciones tras corrección |

Las URL finales de Lighthouse coinciden con las páginas internas solicitadas: los resultados no corresponden a redirecciones al login.

## Recorrido funcional completado

1. Login real de funcionario.
2. Apertura del borrador de renovación de un estudiante en la copia de prueba.
3. Envío de los siete pasos mediante sus botones de formulario; avance comprobado en cada paso.
4. En el paso 6 se completaron los campos faltantes del expediente de prueba: alergias, contacto sanitario y autorizaciones de imagen y convivencia. Un intento previo confirmó que su ausencia produce errores de validación; no se omitió esa validación.
5. Confirmación explícita de revisión y generación de ficha con folio.
6. Foco inicial comprobado en `h1#envio-titulo`.
7. Botón «Copiar el folio» activado mediante Enter: contenido real del portapapeles igual al folio y anuncio «Folio copiado al portapapeles.» en la región de estado.
8. Permanencia en la confirmación comprobada después de 3,2 segundos: sin la antigua redirección automática.

El recorrido utilizó datos precargados y completó campos faltantes; no representa una prueba de todas las combinaciones ni una matrícula nueva desde cero.

## Defecto encontrado y corregido

`.envio__detalle` tenía `color: #6B7A90`, insuficiente para texto pequeño. Se cambió a `#5A6B80` en `wwwroot/css/envio.css`.

Antes: axe `color-contrast` (serious) y Lighthouse 96/100. Después: axe sin violaciones y Lighthouse 100/100. Se repitió únicamente la auditoría afectada después de modificar el CSS.

Las primeras mediciones del verificador durante las animaciones produjeron contrastes transitorios; se corrigió el verificador para esperar la transición antes de medir. La evidencia final corresponde al estado estable.

## Consola de Visual Studio: diagnóstico

`Microsoft.EntityFrameworkCore.Database.Command[20101]` son mensajes informativos de consultas ejecutadas. `@p='?'` indica valores de parámetros ocultos; listar `PasswordHash` en el SELECT no imprime los hashes de las filas. «0 creadas, 7 enlazadas» indica la preparación normal de cuentas al arrancar.

El fallo real fue `AddressInUseException` / SocketException 10048: dos procesos intentaban escuchar en el puerto 5195. Se identificó y detuvo el PID 2784, instancia que había dejado la auditoría anterior. Visual Studio abrió posteriormente su propia instancia; no se la detuvo. Las pruebas adicionales usaron el 5196, sin cambiar `launchSettings.json` ni silenciar errores.

Si vuelve a ocurrir: detener la ejecución anterior con Shift+F5 antes de iniciar otra; evitar ejecutar simultáneamente Visual Studio y otro servidor en el mismo puerto. El fallo en Program.cs al llamar a `app.Run()` no señala un error en las consultas SQL.

## Limpieza y límites

Servidor de prueba detenido, copia temporal de base eliminada y herramientas/dump de prueba retirados al finalizar. No se dejó un segundo servidor del agente ocupando el puerto 5195.

No se hicieron pruebas con NVDA/VoiceOver, recorrido completo exclusivamente con teclado, envíos inválidos en cada paso, revisión visual responsive ni certificación formal WCAG/ISO. Estas tareas más amplias siguen pendientes de la auditoría original. Un 100 de Lighthouse y cero violaciones de axe no demuestran conformidad total. No se midió rendimiento ni se ejecutaron categorías Lighthouse distintas de accesibilidad.
