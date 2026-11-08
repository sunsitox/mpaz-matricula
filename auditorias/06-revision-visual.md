# Revisión visual responsive — Portal de Matrícula 2027

**Institución:** Escuela Escritora Marcela Paz
**Sistema:** `mpaz-matricula` (ASP.NET Core MVC, Razor + CSS propio)
**Fecha:** 07-11-2026
**Alcance:** ocho pantallas del portal en cuatro anchos, con medición automática del navegador e inspección de capturas: `Login`, `Home/Index` (panel), `Alumnos/Index`, `Alumnos/Detalle`, `Ficha/Paso/1`, `Ficha/Paso/6`, `Ficha/Paso/7` y `Ficha/Enviada`.
**Anchos:** 360 px (móvil), 640 px (móvil grande), 768 px (tableta) y 1280 px (escritorio).
**Método:** copia de la base en `mpaz_visual_verificacion`, compilación aparte en `artifacts/visual-temporal/build` (sin tocar la instancia de Visual Studio del puerto 5195), servidor en `http://127.0.0.1:5196` y Edge controlado con Playwright. En cada combinación se midió desborde horizontal del documento, elementos fuera del viewport, objetivos táctiles menores de 24 px, texto menor de 12 px y solapamiento de la barra fija de pasos. Después se revisaron las capturas a tamaño completo y se ejecutó axe sobre las pantallas principales.

---

## 1. Resumen

En las 32 combinaciones (8 pantallas × 4 anchos) no queda **ningún desborde horizontal del documento**: `scrollWidth` iguala a `clientWidth` en todas. Se encontraron y corrigieron **nueve defectos visuales**, de los cuales dos eran roturas reales en móvil (tarjetas cortadas en el panel y la ficha del alumno, y dos botones de la barra del paso 7 fuera de pantalla) y siete eran de legibilidad o de detalle (objetivos táctiles de 18 px, texto por debajo de 12 px, RUN y fechas partidos en dos y cuatro líneas, y el recuadro de foco del navegador sobre el título de la confirmación).

axe no reporta violaciones en `Login`, `Panel`, `Alumnos`, `Detalle` y `Paso 7` (0 en las cinco).

| # | Pantalla | Defecto | Estado |
|---|---|---|---|
| V-01 | Panel, Detalle | Tarjetas cortadas a la derecha a 360 px (desborde de 92 px y 86 px) | Corregido |
| V-02 | Paso 7 | Barra fija con cuatro acciones: «Abrir en otra pestaña» y «Guardar y finalizar» con coordenadas negativas | Corregido |
| V-03 | Alumnos | Enlaces de ordenamiento por debajo de 24 px de alto | Corregido |
| V-04 | Asistente | Etiqueta de las opciones de radio por debajo de 24 px de alto | Corregido |
| V-05 | Alumnos, Detalle | «Ver» y «Volver al listado» de 18 px de alto | Corregido |
| V-06 | Panel | Etiquetas de KPI y del menú a 11,68 px y 11,52 px | Corregido |
| V-07 | Alumnos | RUN partido en dos líneas (`25.889.900-` / `8`) | Corregido |
| V-08 | Panel | Fecha de «Últimos accesos» partida en cuatro líneas | Corregido |
| V-09 | Enviada | Recuadro negro de foco del navegador sobre el título «Ficha guardada» | Corregido |

---

## 2. Defectos corregidos

### V-01 · Alto — El panel y la ficha del alumno se salían de la pantalla

A 360 px el documento medía 452 px de ancho en `Home/Index` (92 px de desborde) y 446 px en `Alumnos/Detalle` (86 px): las tarjetas se veían cortadas por la derecha y aparecía desplazamiento horizontal en toda la página.

**Causa.** Las tarjetas son elementos de rejilla/flex sin `min-width` explícito; el ancho mínimo automático de una tarjeta lo fija su contenido más ancho (una tabla), así que la tarjeta crecía más allá del contenedor aunque el contenedor tuviera el ancho correcto.

**Corrección** — `admin.css`:

```css
.admin__tarjeta,
.admin__contenedor {
    min-width: 0;
}
```

**Verificación.** Desborde 0 px a 360, 640, 768 y 1280 en panel y detalle; el contenido interior (tablas) se desplaza dentro de su propia envoltura sin arrastrar la página.

### V-02 · Alto — Dos acciones del paso 7 quedaban fuera de la pantalla

La barra fija del paso 7 tiene cuatro acciones (Volver, Descargar PDF, Abrir en otra pestaña, Guardar y finalizar). A 360 px los dos últimos botones aparecían en coordenadas negativas (−147 → −85), es decir, recortados por la izquierda y sin posibilidad de alcanzarlos con el dedo.

**Corrección** — clase `wizard__botones--paso7` en `Views/Ficha/Paso7.cshtml` y, dentro del media query de 699 px de `formulario.css`:

```css
.wizard__botones--paso7 {
    flex-wrap: wrap;
    row-gap: 0.45rem;
}

.wizard__botones--paso7 .acceso__boton,
.wizard__botones--paso7 .acceso__boton-secundario {
    flex: 1 1 auto;
}

.wizard__cuerpo:has(.wizard__botones--paso7) {
    padding-bottom: 8.5rem;
}
```

**Verificación a 360 px con borrador lleno.** La barra pasa a dos filas de 115 px de alto; los cuatro botones quedan dentro del ancho útil (14 → 346 px) y miden 44 px de alto. Al llegar al final del documento, el último bloque de contenido queda 140 px por encima de la barra: no hay solapamiento. Con el borrador vacío (dos acciones, barra de 64 px) el hueco es de 2 px. A 768 px la barra reparte las acciones en dos filas y el contenido sigue visible.

### V-03 a V-05 · Medio — Objetivos táctiles de 18 px

WCAG 2.2 · 2.5.8 (tamaño del objetivo) pide 24 × 24 px como mínimo para los controles. Se midieron en su caja real, tomando la etiqueta contenedora cuando el control visible es el `input` (radios y casillas):

| Elemento | Antes | Ahora | Archivo |
|---|---|---|---|
| `.alumnos__orden` (encabezados de la tabla) | bajo 24 px | 24 px | `layout-admin.css` |
| `.acceso__radio` (etiqueta de opción) | bajo 24 px | 24 px | `wizard.css` |
| `.alumnos__ver` («Ver» en cada fila) | 18 px | 24 px | `layout-admin.css` |
| `.alumnos__volver` («Volver al listado») | 18 px | 24 px | `layout-admin.css` |

Los enlaces de nombre de la tabla (`.alumnos__nombre`, 20 px de alto) se dejan como están: son enlaces de texto dentro de una celda y la excepción de «objetivo en línea» de 2.5.8 los cubre.

### V-06 · Bajo — Texto por debajo de 12 px

Los encabezados de la tabla de alumnos estaban a 11,52 px (0,72 rem), las etiquetas del menú lateral a 11,52 px y las de los KPI del panel a 11,68 px. Se subieron a 12 px (0,75 rem). Después del cambio, el único texto menor de 12 px que queda en el recorrido es la flecha de ordenamiento de la tabla (10,4 px), decorativa y con `aria-hidden="true"`; no queda texto de lectura por debajo de ese tamaño.

### V-07 · Medio — El RUN se partía en dos líneas

A 360 px la columna RUN mostraba `25.889.900-` y `8` en líneas distintas: un identificador que se lee mal y se transcribe mal. La tabla ya se desplaza en horizontal dentro de `.alumnos__tabla-envoltura`, así que el dato puede ocupar su ancho natural:

```css
.alumnos__dato {
    white-space: nowrap;
}
```

Se aplica a RUN, curso, estado, tipo y comuna; los nombres siguen partiéndose en varias líneas, que es lo deseable.

### V-08 · Bajo — La fecha de accesos en cuatro líneas

En «Últimos accesos» del panel la fecha `07-11-2026 02:04` se repartía en cuatro líneas dentro de una columna estrecha. Se añadió la clase `panel__fecha` a esa celda y `white-space: nowrap` en `panel.css`.

### V-09 · Bajo — Recuadro negro sobre el título de la confirmación

`ux.js` mueve el foco al título de «Ficha guardada» (`data-ux-foco`) para que el lector de pantalla anuncie la confirmación. El navegador dibujaba su contorno por defecto —un rectángulo negro de 1 px a todo el ancho de la tarjeta— que se lee como un campo de formulario. Se sustituyó por un contorno nulo, documentado en `envio.css`, porque el elemento es un encabezado y no un control: no se llega a él con Tab y el foco sigue activo para las tecnologías de asistencia.

---

## 3. Breakpoints

Se fusionaron los dos bloques `max-width: 699px` contiguos de `formulario.css` en uno solo, sin cambiar ninguna declaración. Los cortes 700 px, 780 px, 900 px y 1024 px se mantuvieron: cada uno controla componentes distintos (rejilla del asistente, barra de acciones, menú administrativo y tablas), y unificarlos obligaría a rehacer esos componentes sin ganancia visual. La medición en los cuatro anchos no muestra ninguna transición rota: 699/700 y 767/768 fueron los puntos revisados.

---

## 4. Resultados por pantalla

| Pantalla | 360 | 640 | 768 | 1280 |
|---|---|---|---|---|
| Login | Sin desborde | Sin desborde | Sin desborde | Sin desborde |
| Panel | Sin desborde | Sin desborde | Sin desborde | Sin desborde |
| Alumnos | Tabla con scroll interno | Tabla con scroll interno | Sin desborde | Enlaces en línea de 20 px (exentos) |
| Detalle | Sin desborde | Sin desborde | Sin desborde | Sin desborde |
| Paso 1 | Sin desborde | Sin desborde | Sin desborde | Sin desborde |
| Paso 6 | Sin desborde | Sin desborde | Sin desborde | Sin desborde |
| Paso 7 | Barra en dos filas | Barra en dos filas | Barra en dos filas | Una fila |
| Enviada | Sin desborde | Sin desborde | Sin desborde | Sin desborde |

«Tabla con scroll interno» significa que la tabla es más ancha que la tarjeta y se desplaza dentro de `.alumnos__tabla-envoltura` (`tabindex="0"`); el documento no se desplaza. Es el comportamiento admitido por WCAG 1.4.10 para tablas de datos.

La confirmación de guardado se revisó en dos momentos: al cargar (rueda girando) y tras 0,9 s, cuando `ux.js` añade `.envio--listo` y la rueda se convierte en el check verde. La captura inicial parecía un indicador de carga infinito; se comprobó que es una transición y que termina.

---

## 5. Verificación automática

- **32/32 combinaciones** sin desborde horizontal del documento. Datos completos en `06-resultados-visuales.json`.
- **axe 0 violaciones** en `Login`, `Panel`, `Alumnos`, `Detalle` y `Paso 7` a 360 px, con `color-contrast` incluido.
- **Nota sobre los falsos positivos de contraste.** Medido al instante de cargar, axe marca seis fallos de contraste en el login (etiquetas 2,95:1, botón 3,38:1). No son reales: `.acceso__card` entra con la animación `ux-entrada` y en ese momento está al 60 % de opacidad, así que axe compone los colores sobre el fondo y calcula un contraste degradado. Con 1,5 s de espera —o con `prefers-reduced-motion`, donde la animación no corre— el resultado es 0 violaciones. Cualquier medición futura de contraste en este portal debe esperar a que termine la animación o desactivarla.

---

## 6. Evidencia

Capturas en `auditorias/evidencias-visuales/`:

| Archivo | Qué muestra |
|---|---|
| `panel-360.png` | Panel a 360 px, tarjetas completas y fecha de accesos en una línea |
| `detalle-360.png` | Ficha del alumno a 360 px, sin desborde de página |
| `alumnos-360.png` | Listado a 360 px: RUN íntegro, tabla con scroll interno |
| `paso7-360.png` | Paso 7 a 360 px con la barra en dos filas |
| `paso7-768.png` | Paso 7 a 768 px |
| `enviada-360.png` | Confirmación con folio, check verde y título sin recuadro |

---

## 7. Pendiente y límites

- **Textos legales de las autorizaciones** (imagen, convivencia, medicación): siguen pendientes por decisión del proyecto; no se tocaron.
- **Zoom al 200 % y alto contraste:** no se revisaron en esta ronda. La medición se hizo a densidad 1 y sin modo de contraste forzado.
- **Navegadores:** solo Edge/Chromium. `:has()` —usado en la reserva de espacio del paso 7— está soportado en las versiones actuales de Chrome, Edge, Safari y Firefox, pero no se probó en ellos.
- **Lectores de pantalla:** esta ronda no incluyó NVDA; los cambios son de presentación y no alteran nombres, roles ni orden de lectura. La evidencia con NVDA sigue en `05-verificacion-nvda.md`.
- **Rendimiento:** no se midió el costo de las nuevas reglas; son declaraciones de una línea sobre selectores existentes.
- Los datos de prueba (folio `MP-2027-00001`, alumno 11) provienen de la copia de la base; la base `mpaz_visual_verificacion` y el directorio de trabajo `artifacts/visual-temporal` se eliminaron al cerrar la ronda.

---

*Informe generado a partir del código en `mpaz-matricula/` al 07-11-2026.*
