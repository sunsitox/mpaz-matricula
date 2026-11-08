# Auditoría de UI/UX — Portal de Matrícula 2027

**Institución:** Escuela Escritora Marcela Paz
**Sistema:** `mpaz-matricula` (ASP.NET Core MVC, vistas Razor + CSS propio)
**Fecha del informe:** 07-11-2026
**Alcance:** asistente de matrícula de 7 pasos, login, panel administrativo (`Home`), listado y ficha de alumnos, administración de funcionarios, pantalla de envío y estilos globales.
**Método:** revisión estática de vistas Razor, `site.css`, `layout-admin.css`, `panel.css` y `ux.js`, contrastada con el HTML generado durante las pruebas E2E de la Etapa I.

> **Limitación importante:** no se ejecutaron Lighthouse ni axe, no se hicieron capturas renderizadas y no hubo pruebas con usuarios ni con lector de pantalla real. Los contrastes sí se calcularon numéricamente (fórmula de luminancia relativa de WCAG 2.2); el resto son hallazgos de código que deben confirmarse visualmente antes de cerrarlos.

---

## 1. Resumen ejecutivo

La interfaz está claramente por encima de un prototipo: hay un sistema de color y tipografía consistente, *focus visible* global, respeto a `prefers-reduced-motion`, un stepper con `aria-current`, notificaciones con `aria-live`, bloqueo de doble envío con `aria-busy`, búsqueda tolerante a tildes y un layout administrativo que se adapta a móvil. El trabajo de detalle se nota.

Los problemas se concentran en tres zonas:

1. **Textos que ya no son ciertos.** El asistente todavía dice «Estructura de prueba» y «no se persiste en la base de datos», cuando desde la Etapa I la ficha se guarda, genera folio y produce PDF. Es el hallazgo de mayor impacto real: erosiona la confianza en el dato y puede provocar doble digitación.
2. **Errores que no se anuncian.** Los resúmenes de validación son inconsistentes (unos con `role="alert"`, otros sin él) y algunos usan `ModelOnly`, así que los errores de campo no llegan al resumen. Ninguna pantalla mueve el foco al primer error. Para quien usa lector de pantalla o solo teclado, enviar un formulario largo puede terminar en silencio.
3. **Pantallas que se cierran solas.** La confirmación de guardado redirige a los 2,6 segundos, con un `role="dialog"` que no gestiona el foco. Quien lee despacio pierde el folio, que es el dato que necesita anotar.

A esto se suman tablas sin `scope` ni `caption` (y con scroll horizontal solo en una de las cuatro pantallas que las usan), un botón «×» que cierra un rol de forma irreversible sin confirmación, un contraste de 4,26:1 en el texto secundario —por debajo del mínimo AA— y texto a 0,7 rem en móvil.

| # | Hallazgo | Impacto |
|---|---|---|
| U-01 | Textos obsoletos: «Estructura de prueba» y «no se persiste» | **Alto** |
| U-02 | Errores de validación no anunciados ni enfocados de forma consistente | **Alto** |
| U-03 | Redirección automática a los 2,6 s y diálogo sin gestión de foco | **Alto** |
| U-04 | Tablas sin `caption`, `scope` ni `aria-sort`; scroll móvil en una sola pantalla | Medio |
| U-05 | Botón «×» destructivo, sin etiqueta accesible ni confirmación | Medio |
| U-06 | Contraste insuficiente en texto secundario (4,26:1) y placeholder (2,39:1) | Medio |
| U-07 | Texto a 0,7 rem (~11 px) en móvil | Medio |
| U-08 | CSS duplicado y en derivación (1979 líneas, bloques repetidos) | Medio |
| U-09 | Notificaciones que desaparecen a los 12 s sin pausa al enfocar | Bajo |
| U-10 | Estado de los pasos comunicado solo por color | Bajo |
| U-11 | Paso 6 sobrecargado: salud, PIE y tres autorizaciones en una pantalla | Bajo |

---

## 2. Hallazgos

### U-01 · Alto — El asistente dice que no guarda, pero guarda

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Shared/_WizardLayout.cshtml:25`
- `mpaz-matricula/MarcelaPaz.Web/Views/Shared/_WizardLayout.cshtml:71`

**Evidencia**

```html
<div class="wizard__subtitulo">Estructura de prueba · los campos se definirán con el colegio</div>
...
<span class="wizard__nota-guardado-detalle">(Por ahora es solo estructura: no se persiste en la base de datos.)</span>
```

Ambos textos contradicen el comportamiento actual: `FichaController.Guardar` escribe en las tablas del DDL en cada paso (`RegistroFicha.AplicarAsync`) y el paso 7 cierra la ficha con folio y snapshot. La nota aparece justo debajo del botón «Guardar y continuar», en el punto donde el usuario decide si confía en lo que escribió.

**Impacto.** Es el mensaje más leído del asistente y el más equivocado. Un apoderado o funcionario que lo crea puede duplicar la digitación en papel, abandonar a mitad de camino o desconfiar del folio. También contradice el mensaje de la pantalla de cierre, que celebra haber guardado: dos pantallas del mismo flujo dicen cosas opuestas.

**Recomendación.** Reemplazar por texto veraz y orientado a la acción: «Cada paso se guarda al continuar. Puede retomarlo más tarde desde la ficha del alumno». Eliminar la palabra «prueba» de toda la interfaz visible al usuario. Si en algún momento se necesita una advertencia de entorno, derivarla de configuración y mostrarla como banda de entorno, no como subtítulo permanente.

---

### U-02 · Alto — Los errores de validación no se anuncian ni reciben foco

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Cuenta/Login.cshtml:50`
- `mpaz-matricula/MarcelaPaz.Web/Views/Funcionarios/Crear.cshtml:16`
- `mpaz-matricula/MarcelaPaz.Web/Views/Ficha/Paso1.cshtml:13`
- `mpaz-matricula/MarcelaPaz.Web/Views/Ficha/Paso7.cshtml:31`
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/js/ux.js` (sin gestión de errores)

**Evidencia**

Los resúmenes no siguen un patrón común:

```html
<!-- Login.cshtml:50 — sin role -->
<div asp-validation-summary="ModelOnly" class="acceso__alerta"></div>

<!-- Funcionarios/Crear.cshtml:16 — sin role -->
<div asp-validation-summary="ModelOnly" class="acceso__alerta"></div>

<!-- Paso1.cshtml:13 — con role, pero solo ModelOnly -->
<div asp-validation-summary="ModelOnly" class="acceso__alerta" role="alert"></div>

<!-- Paso7.cshtml:31 — con role, pero solo ModelOnly -->
<div asp-validation-summary="ModelOnly" class="acceso__alerta" role="alert"></div>
```

Los pasos 2 a 6 usan `All` con `role="alert"`, así que el comportamiento cambia según el paso. Con `ModelOnly`, los errores añadidos a un campo concreto (`ModelState.AddModelError(nameof(...))`, que es la mayoría en `FichaController.ValidarPaso`) no aparecen en el resumen y quedan solo junto al campo, fuera del foco.

`ux.js` mejora la navegación y el envío (barra de progreso, `aria-busy`, bloqueo de doble envío) pero no busca `.field-validation-error` ni mueve el foco tras un POST fallido.

**Impacto.** En un formulario de siete pasos, con decenas de campos, el usuario de teclado o lector de pantalla recibe la misma página y no sabe por qué no avanzó. En `Login` y `Crear funcionario` ni siquiera hay anuncio. Incumple WCAG 2.2: 3.3.1 (identificación de errores), 3.3.3 (sugerencia) y 4.1.3 (mensajes de estado).

**Recomendación**
1. Unificar un único parcial de resumen con `role="alert"` y `tabindex="-1"`, usado con `asp-validation-summary="All"` en todos los formularios.
2. En `ux.js`, tras cargar una página con errores: enfocar el resumen (o el primer campo inválido) y añadir `aria-invalid="true"` y `aria-describedby` al campo.
3. Mantener `role="alert"` en los mensajes de campo, o `aria-live="polite"` si se prefiere no interrumpir.
4. Añadir una prueba manual con teclado: enviar el paso 6 vacío y verificar que el foco aterriza en un mensaje legible.

---

### U-03 · Alto — La confirmación se cierra sola y el diálogo no toma el foco

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Ficha/Enviada.cshtml:10`, `:41`, `:49-51`

**Evidencia**

```html
<div class="envio" role="dialog" aria-modal="true" aria-labelledby="envio-titulo">
...
<script>
    window.setTimeout(function () { ... }, 1100);
    window.setTimeout(function () {
        window.location.replace('@destino');
    }, 2600);
</script>
```

El contenedor se declara `aria-modal="true"` —lo que debería hacer que un lector de pantalla restrinja la navegación al diálogo— pero no recibe el foco al cargar, no atrapa el foco y no se puede cerrar con `Escape`. La página se reemplaza a los 2,6 segundos, sin cuenta atrás visible ni control para detenerla.

**Impacto.** El folio (`MP-2027-00002` y siguientes) es el dato que el apoderado debe anotar o con el que Secretaría verifica la matrícula. A 2,6 segundos, un usuario que lee despacio, que tiene baja visión o que usa ampliador pierde la pantalla antes de terminar la frase. Para un lector de pantalla, el diálogo modal puede quedar «colgado» hasta la navegación. Afecta a WCAG 2.2.1 (límite de tiempo) y 2.4.3 (orden del foco).

**Recomendación**
1. Eliminar la redirección automática, o elevarla a 10–15 segundos con una cuenta atrás visible y un botón «Quedarme aquí».
2. Enfocar el `h1` del diálogo al cargar (`tabindex="-1"`, `.focus()`) y devolver el foco al salir.
3. Hacer que el folio sea seleccionable y añadir un botón «Copiar folio»; es la acción real que el usuario necesita.
4. Si se mantiene como diálogo modal, implementar trampa de foco y `Escape`; si es una página completa, quitar `role="dialog"` y `aria-modal`, que hoy no se cumplen.

---

### U-04 · Medio — Tablas sin semántica completa y scroll móvil desigual

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Alumnos/Index.cshtml:94-128`
- `mpaz-matricula/MarcelaPaz.Web/Views/Alumnos/Detalle.cshtml:135`, `:169`, `:211`, `:242`, `:279`
- `mpaz-matricula/MarcelaPaz.Web/Views/Funcionarios/Index.cshtml:23-30`
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/layout-admin.css:166-167`

**Evidencia**

Ninguna tabla del proyecto usa `<caption>` ni `scope="col"`. En el listado, los encabezados son enlaces de ordenamiento sin `aria-sort`, y la flecha de dirección vive en un `span` decorativo:

```html
<th>
    <a class="@claseOrden("nombre")" href="@enlaceOrden("nombre")">
        Alumno <span class="alumnos__flecha">@flecha("nombre")</span>
    </a>
</th>
```

El único contenedor con desplazamiento horizontal es el del listado:

```css
.alumnos__tabla-envoltura {   /* layout-admin.css:166 */
    overflow-x: auto;         /* :167 */
}
```

`Alumnos/Detalle` tiene cinco tablas y `Funcionarios/Index` una, ninguna envuelta: en un teléfono se desbordan sobre el layout.

**Impacto.** Un lector de pantalla no puede asociar celdas con encabezados en tablas de seis columnas, y no sabe por qué columna está ordenado el listado. En móvil, la ficha del alumno —la pantalla que más se consulta en terreno— se vuelve ilegible.

**Recomendación**
1. `<caption>` (visible o `sr-only`) en cada tabla y `scope="col"` en los `th`.
2. `aria-sort="ascending|descending|none"` en el `th` de la columna activa, y `aria-hidden="true"` en la flecha.
3. Aplicar `overflow-x: auto` (con `-webkit-overflow-scrolling: touch`) a todas las tablas mediante una clase común, en lugar de una sola envoltura.
4. En móvil, evaluar convertir la ficha en tarjetas etiquetadas: cinco tablas de cuatro a seis columnas no se resuelven con scroll.

---

### U-05 · Medio — El botón «×» cierra un rol sin confirmación

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Funcionarios/Index.cshtml:46-53` (botón en la línea 51)

**Evidencia**

```html
<span class="admin__chip">
    @rol.Codigo
    <form asp-action="CerrarRol" method="post">
        <input type="hidden" name="funcionarioId" value="@funcionario.Id" />
        <input type="hidden" name="rolId" value="@rol.RolId" />
        <button type="submit" title="Cerrar este rol hoy">×</button>
    </form>
</span>
```

El botón se dibuja como una «×» dentro de una etiqueta de rol, el patrón visual de «quitar esta etiqueta», no de «cerrar un rol con fecha de hoy». No hay diálogo de confirmación ni deshacer, y `CerrarRol` fija `Hasta = hoy` de inmediato.

**Impacto.** Es una acción prácticamente irreversible desde la interfaz (habría que recrear la relación) disparada por un clic en un control de 12 píxeles pegado a texto. Un lector de pantalla anuncia «times», sin contexto. El `title` no es accesible en táctil ni con teclado.

**Recomendación**
1. Reemplazar por un botón con texto «Cerrar rol» o un menú de acciones por fila.
2. Pedir confirmación explícita indicando el nombre del funcionario y el rol.
3. Añadir `aria-label` completo («Cerrar el rol TENS de María Teresa Lagos»).
4. Ofrecer deshacer durante unos segundos, o una acción «reabrir rol» en la misma pantalla.

---

### U-06 · Medio — Contraste insuficiente en texto secundario y placeholder

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/site.css:21` (`--color-text-secondary: #6B7C92`)
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/site.css:15` (`--color-placeholder: #9AA9BC`)

**Evidencia (cálculo WCAG 2.2 sobre fondo blanco `#FFFFFF`)**

| Token | Color | Contraste | AA texto normal (4,5:1) |
|---|---|---|---|
| `--color-text-secondary` | `#6B7C92` | **4,26:1** | No cumple |
| `--color-placeholder` | `#9AA9BC` | **2,39:1** | No cumple |

Cálculo por luminancia relativa: `#6B7C92` → L ≈ 0,196; `(1,0 + 0,05) / (0,196 + 0,05) ≈ 4,26`. `#9AA9BC` → L ≈ 0,389; `1,05 / 0,439 ≈ 2,39`.

`#6B7C92` se usa para texto secundario de apoyo, ayudas y metadatos. A 14–16 px no alcanza el mínimo AA de 4,5:1; a 18,66 px en negrita o 24 px sí lo alcanzaría (umbral 3:1).

**Impacto.** Baja legibilidad para personas con baja visión o pantallas de bajo brillo, en un formulario que se llena una vez al año y donde el texto de ayuda es la única explicación de algunos campos.

**Recomendación**
1. Oscurecer el token secundario hasta al menos 4,5:1 (por ejemplo `#5A6B80` ≈ 5,2:1; verificar con el medidor antes de fijarlo).
2. Subir el placeholder a ≥ 4,5:1 o no usarlo como única fuente de información: debe haber `<label>` visible, que ya existe en la mayoría de los campos.
3. Revisar los pares de color sobre fondos no blancos (chips, bandas de estado, avisos), que no se cubrieron en este cálculo.

---

### U-07 · Medio — Texto a 0,7 rem en pantallas pequeñas

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/site.css:634`, `:675`

**Evidencia.** Dentro del bloque `@media (max-width: 699px)` que comienza en la línea 634, hay una regla con `font-size: 0.7rem` (línea 675). A 16 px de base son ~11,2 px, por debajo del tamaño cómodo de lectura y del mínimo práctico de 12 px; con el zoom del navegador al 200 % el texto escala, pero el punto de partida es bajo.

**Impacto.** En el dispositivo donde más se usa el asistente (teléfono del apoderado), el texto se vuelve difícil de leer sin hacer zoom, y el zoom rompe la barra de acciones fija.

**Recomendación.** No bajar de 0,875 rem (~14 px) para texto de contenido; reservar tamaños menores solo para metadatos no esenciales. Verificar qué elemento afecta la regla y, si es una etiqueta de paso, acortar el texto antes que reducirlo.

---

### U-08 · Medio — CSS duplicado que ya empieza a divergir

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/site.css` (1979 líneas)
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/css/layout-admin.css`, `panel.css`

**Evidencia.** Hay bloques repetidos de estilos del asistente y del panel administrativo, y breakpoints distintos conviviendo: `699px` en `site.css:634, 1528, 1581, 1708, 1830`; `1024px` en `site.css:1014`; `780px` en `layout-admin.css:57`; `900px` en `layout-admin.css:355` y `panel.css:74`. No hay capa de tokens centralizada más allá de las variables iniciales.

**Impacto.** Cada ajuste visual debe hacerse en dos o tres lugares, y ya hay señales de deriva: el listado de alumnos tiene scroll móvil y las demás tablas no (U-04); el resumen de validación se comporta distinto según el paso (U-02). Para el colegio, esto se traduce en pantallas que se ven distintas sin que nadie lo haya decidido.

**Recomendación.** Consolidar en un archivo con orden explícito (tokens → base → componentes → layout → utilidades), eliminar duplicados y unificar los breakpoints en dos o tres valores documentados. No es una reescritura: es deduplicación con verificación visual pantalla por pantalla.

---

### U-09 · Bajo — Las notificaciones desaparecen a los 12 s sin pausa

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/wwwroot/js/ux.js` (temporizador de cierre del *toast*)
- `mpaz-matricula/MarcelaPaz.Web/Views/Shared/_Notificacion.cshtml:5`

**Evidencia.** El *toast* tiene `role="status" aria-live="polite"` (correcto) y se retira automáticamente a los 12 segundos. No se pausa al pasar el mouse ni al enfocar con teclado.

**Impacto.** Un mensaje de error con instrucciones puede desaparecer mientras el usuario lo lee o antes de que llegue a él con el lector de pantalla. WCAG 2.2.1 pide que el contenido con tiempo límite se pueda pausar o prorrogar.

**Recomendación.** Pausar el temporizador en `mouseenter` y `focusin`, reanudar al salir, y no cerrar automáticamente los mensajes de error (solo los de éxito).

---

### U-10 · Bajo — El estado del paso se comunica solo por color

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Views/Shared/_WizardLayout.cshtml:52-60`

**Evidencia**

```html
<div class="wizard__paso @clase" @(i == pasoActual ? "aria-current=\"step\"" : "")>
    <span class="wizard__paso-numero" aria-hidden="true">@i</span>
    <span class="wizard__paso-texto">@nombresPasos[i - 1]</span>
</div>
```

`aria-current="step"` está bien puesto y el nombre del paso es texto real, así que la navegación es comprensible. Pero «hecho» y «pendiente» se distinguen únicamente por la clase visual (`wizard__paso--hecho` / `--pendiente`); el número va `aria-hidden`.

**Impacto.** Quien no distingue colores no sabe qué pasos completó, y como el número está oculto tampoco recibe la posición relativa.

**Recomendación.** Añadir un texto para lectores de pantalla («completado», «en curso», «pendiente») y un icono con forma distinta, no solo color. Mantener `aria-current`.

---

### U-11 · Bajo — El paso 6 concentra demasiadas decisiones

**Ubicación**
- `mpaz-matricula/MarcelaPaz.Web/Controllers/FichaController.cs` (`ValidarPaso`, caso 6)
- `mpaz-matricula/MarcelaPaz.Web/Views/Ficha/Paso6.cshtml`

**Evidencia.** El paso 6 reúne, en una sola pantalla: situación médica (hasta tres), alergias, contacto sanitario, salida al baño, autorización de paracetamol, participación y diagnóstico PIE, apoyos, autorización de imagen y autorización de convivencia. Son diez decisiones, tres de ellas consentimientos con efecto legal.

**Impacto.** Es el paso con más probabilidad de abandono y de respuesta apresurada, y mezcla datos clínicos con consentimientos que el apoderado debe leer. Un consentimiento marcado por inercia es un problema jurídico, no solo de usabilidad.

**Recomendación.** Dividir en tres subsecciones con encabezados claros (Salud · PIE · Autorizaciones), cada una con su propio texto explicativo. Idealmente, separar las autorizaciones en un paso propio con el texto completo a la vista, no un checkbox suelto.

---

## 3. Controles positivos verificados

| Control | Evidencia |
|---|---|
| *Focus visible* global y consistente | `site.css:1066-1071` |
| Respeto a `prefers-reduced-motion` | `site.css:1326`; `ux.js:5` (desactiva la barra de progreso) |
| Stepper con `aria-current="step"` y etiqueta textual | `_WizardLayout.cshtml:52-60` |
| Notificaciones con `role="status"` y `aria-live="polite"` | `_Notificacion.cshtml:5` |
| Bloqueo de doble envío con `aria-busy` y `aria-disabled` | `ux.js` (manejo de `submit`) |
| Velo de carga con `role="status"` | `ux.js` |
| Búsqueda tolerante a tildes y a formato de RUN | `AlumnosController.SinTildes`, `Index` |
| Ordenamiento validado contra lista blanca de columnas | `AlumnosController.Index` |
| Avisos con `role="status"` en encabezado y modo | `_WizardLayout.cshtml:34-49` |
| Layout administrativo adaptable (menú lateral → barra superior) | `layout-admin.css:57`; `panel.css:74` |
| Scroll horizontal en el listado de alumnos | `layout-admin.css:166-167` |
| Vista previa de PDF con estado de carga | `ux.js` (`.wizard__pdf-marco`) |
| Mensajes de error de negocio en lenguaje claro y con paso siguiente | `FichaController` (`TempData["Aviso"]`) |
| Botones de funciones no implementadas visiblemente deshabilitados | `AlumnosController` (comentario de extracción pendiente) |

---

## 4. Plan de acción sugerido

| Orden | Acción | Hallazgo | Esfuerzo |
|---|---|---|---|
| 1 | Corregir los textos «Estructura de prueba» / «no se persiste» | U-01 | Muy bajo |
| 2 | Quitar o alargar la redirección automática y enfocar el diálogo | U-03 | Bajo |
| 3 | Unificar resumen de validación con `role="alert"` y foco al primer error | U-02 | Bajo |
| 4 | `caption`, `scope`, `aria-sort` y scroll móvil en todas las tablas | U-04 | Bajo |
| 5 | Rediseñar el cierre de rol: texto, confirmación, `aria-label` | U-05 | Bajo |
| 6 | Ajustar contrastes a ≥ 4,5:1 y subir el mínimo de 0,7 rem | U-06, U-07 | Bajo |
| 7 | Dividir el paso 6 en subsecciones | U-11 | Medio |
| 8 | Deduplicar CSS y unificar breakpoints | U-08 | Medio |
| 9 | Pausar *toasts* y añadir texto de estado al stepper | U-09, U-10 | Bajo |

---

## 5. Verificación pendiente

**Actualización 07-11-2026:** se ejecutaron Lighthouse autenticado en 11 páginas internas, axe en los siete pasos y la confirmación con folio, y el recorrido de renovación hasta generar/copiar el folio. Se corrigió el contraste de `.envio__detalle` y se repitió su auditoría: 100/100 y 0 violaciones. Evidencia, metodología y límites en `03-verificacion-ui-accesibilidad.md` y `03-resultados-verificacion.json`.

**Actualización 07-11-2026 (ronda 2):** se verificó la navegación solo con teclado en los siete pasos, los errores de cada paso y la unificación de breakpoints, y se corrigieron cinco defectos encontrados así (errores sin vínculo al campo, grupos de radios parcialmente marcados, desplegables cerrados con errores, paso 7 sin resumen y redacción «de el»). Detalle en `04-verificacion-teclado-errores.md` y `04-resultados-teclado.json`.

**Actualización 07-11-2026 (ronda 3):** se ejecutó la prueba con NVDA 2025.3.2 portable sobre login, panel, paso 1 con y sin errores, paso 6 y la confirmación con folio, con anuncios capturados desde el registro de voz. Se corrigió además que `.acceso__alerta` no tenía estilos. Detalle en `05-verificacion-nvda.md` y `05-evidencias-nvda.json`.

**Actualización 07-11-2026 (ronda 4):** se ejecutó la revisión visual responsive en 360, 640, 768 y 1280 px sobre ocho pantallas, midiendo desborde horizontal, objetivos táctiles y tamaño de texto. Se corrigieron nueve defectos: el desborde de 92 px y 86 px de las tarjetas del panel y de la ficha del alumno a 360 px, los dos botones del paso 7 que quedaban fuera de pantalla, cuatro objetivos táctiles bajo 24 px (WCAG 2.5.8), tres tamaños de letra bajo 12 px, el RUN y la fecha de accesos partidos en varias líneas y el contorno de foco del navegador sobre el título de la confirmación. axe: 0 violaciones en login, panel, alumnos, detalle y paso 7. Detalle, mediciones y capturas en `06-revision-visual.md`, `06-resultados-visuales.json` y `auditorias/evidencias-visuales/`.

La lista siguiente corresponde al alcance original; el zoom al 200 % y el alto contraste siguen sin cubrirse:

1. **axe DevTools o Lighthouse** sobre `Login`, `Home`, `Ficha/Paso1`, `Ficha/Paso6`, `Alumnos/Index`, `Alumnos/Detalle` y `Funcionarios/Index`, con la aplicación levantada.
2. **Recorrido solo con teclado** de los siete pasos, incluyendo un envío con errores en cada paso.
3. **Prueba con lector de pantalla** (NVDA o VoiceOver) en el asistente y en la pantalla de folio.
4. **Revisión visual en 360 px y 768 px** de la ficha del alumno, que es donde se concentran las tablas sin envoltura.
5. **Medición de contraste** de los pares sobre fondos no blancos (chips de rol, estados, bandas de aviso).

---

## 6. Limitaciones

- La auditoría original fue estática. La verificación automática posterior está documentada en el informe 03; no se realizaron pruebas con usuarios ni se certificó conformidad WCAG/ISO.
- Los contrastes de U-06 se calcularon sobre fondo blanco; los pares sobre fondos tintados no se midieron.
- No se evaluó el contenido de los textos legales de las autorizaciones (imagen, convivencia, medicación): su redacción depende de Dirección y del sostenedor, y sigue pendiente según el checklist del proyecto.
- No se revisó el rendimiento de carga ni el tamaño de los PDF generados.
- La auditoría de seguridad asociada está en `auditorias/01-auditoria-seguridad.md`; varios hallazgos de UI (U-01, U-02) se relacionan con el modo revisión descrito allí.

---

*Informe generado a partir del código en `mpaz-matricula/` al 07-11-2026. Las líneas citadas corresponden a ese estado; si el archivo cambia, deben revalidarse.*
