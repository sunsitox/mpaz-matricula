# Verificación de teclado, errores por paso y unificación de breakpoints

Fecha: 07-11-2026.

## 1. Breakpoints del CSS

Inventario tras la segmentación: 15 declaraciones `@media`.

| Archivo | Regla | Componente que controla |
|---|---|---|
| `wizard.css` | `min-width: 700px` | rejilla de campos a dos columnas, separación del stepper |
| `wizard.css`, `formulario.css`, `envio.css` | `max-width: 699px` | móvil del asistente y de la confirmación |
| `layout-admin.css` | `max-width: 780px` | menú lateral pasa a barra horizontal |
| `wizard.css` | `min-width: 900px` | dos paneles del acceso |
| `layout-admin.css`, `panel.css` | `max-width: 900px` | paneles del área administrativa |
| `admin.css` | `max-width: 1024px` | rejilla de indicadores |
| `envio.css`, `ux.css` | `prefers-reduced-motion` | respeto al movimiento reducido |

**Unificado:** los dos bloques `max-width: 699px` contiguos de `formulario.css` (ajustes móviles del asistente y del paso 7) se fusionaron en uno solo. El punto de corte ya era idéntico y las reglas contiguas, así que el orden de cascada no cambia.

**No unificado, con motivo:**

- `699` frente a `700` es el mismo límite expresado como máximo y como mínimo; unificarlos exigiría reescribir media queries completas sin cambiar el comportamiento.
- `780` (menú lateral) y `900` (paneles) responden a componentes distintos. Igualarlos a 1024 o a 780 movería el cambio de diseño a un ancho donde hoy no ocurre.
- `1024` de `admin.css` depende de que quepan cuatro indicadores; depende del componente, no del ancho de página.
- Los bloques `prefers-reduced-motion` están repetidos por archivo de forma deliberada: cada archivo se mantiene autosuficiente.

**Comprobación de que no cambió el diseño:** se compararon los estilos calculados de todos los elementos del documento en el paso 7 a 360, 699, 700, 768, 780, 781, 899, 900, 901, 1024 y 1280 px, cargando el CSS fusionado y el CSS previo reconstruido. Las 11 comparaciones fueron idénticas propiedad por propiedad.

## 2. Navegación solo con teclado

Recorrido con Tab desde el inicio de cada paso hasta que el foco volvió a un elemento ya visitado.

| Paso | Tabulaciones hasta cerrar el ciclo | Ciclo completo | Botón de envío alcanzable | Foco en elemento invisible |
|---|---:|---|---|---|
| 1 | 10 | Sí | Sí | Ninguno |
| 2 | 14 | Sí | Sí | Ninguno |
| 3 | 12 | Sí | Sí | Ninguno |
| 4 | 10 | Sí | Sí | Ninguno |
| 5 | 11 | Sí | Sí | Ninguno |
| 6 | 22 | Sí | Sí | Ninguno |
| 7 | 6 | Sí | Sí | Ninguno |

- **Enlace «Saltar al contenido»**: es el primer elemento del orden de tabulación; al activarlo con Enter, el foco pasa a `#contenido-principal` sin cambiar de página.
- **Controles desplegables** (`<details>`/`<summary>`): se alcanzan con Tab y se abren y cierran con Enter.
- **Sin trampas de foco**: en los siete pasos el ciclo termina volviendo al inicio.

## 3. Errores de validación por paso

Se envió cada paso sin datos y se inspeccionó la respuesta.

| Paso | Resumen `role="alert"` | Foco en el resumen | Errores ligados a su campo | Grupos de radios marcados por completo |
|---|---|---|---|---|
| 1 | Sí | Sí | 9 de 9 | — |
| 2 | Sí | Sí | Regla de bloque, sin campo único | — |
| 3 | Sí | Sí | 4 de 4 | — |
| 4 | Sí | Sí | Regla de bloque, sin campo único | — |
| 5 | Sí | Sí | 1 de 1 | 2 de 2 opciones |
| 6 | Sí | Sí | 7 de 7 | 3 de 3, 2 de 2 y 2 de 2 |
| 7 | Sí | Sí | 1 de 1 | — |

Todos los `aria-describedby` añadidos apuntan a un elemento existente. Las reglas que no pertenecen a un campo («Complete al menos los datos de la madre o los del padre», «Registre al menos un contacto de emergencia») se anuncian en el resumen sin marcar campos, que es lo correcto.

## 4. Defectos encontrados y corregidos

1. **Errores sin vínculo al campo.** El resumen se construía con `asp-validation-summary`, que no expone qué campo originó cada mensaje. Los campos sin mensaje en línea (paso 1: nombres, apellidos, domicilio, comuna; paso 6: alergias y contacto sanitario) no reciban `aria-invalid` ni `aria-describedby`, y un lector de pantalla no podía relacionarlos. El resumen ahora emite cada mensaje con el nombre del campo y `ux.js` marca los controles, incluidas todas las opciones de un grupo de radios, no sólo la primera.
2. **Grupos de radios parcialmente marcados.** Con el vínculo anterior, sólo la primera opción de `SalidaSolo`, `ParticipaPie`, `TieneSituacionMedica`, `AutorizaImagen` y `AutorizaConvivencia` quedaba marcada. Ahora se marcan todas las opciones del grupo.
3. **Desplegables cerrados con errores dentro.** Si un error vive en una sección plegada, `ux.js` la abre para que el mensaje sea visible al llegar por el resumen.
4. **Paso 7 sin confirmación.** Antes redirigía al mismo paso con un aviso en `TempData`, sin resumen de errores ni foco. Ahora devuelve la vista con el resumen, el foco y la casilla marcada como inválida.
5. **Redacción de los mensajes.** «Indique el nombre de el apoderado principal» pasó a «Indique el nombre del apoderado principal», con la contracción correcta en los cuatro bloques de adulto.

Después de los cambios: compilación con 0 errores, los siete pasos con resumen y foco, y el recorrido de éxito verificado de nuevo (folio `MP-2027-00001`, foco en `h1#envio-titulo`), para descartar que la corrección del paso 7 rompiera la generación de la ficha.

## 5. NVDA: no ejecutado

**No se realizó la prueba con NVDA y no se presenta como aprobada.** En el equipo no había ningún proceso de NVDA en ejecución y la automatización disponible controla el navegador, no la salida hablada de un lector de pantalla. Simular esa prueba con consultas al árbol de accesibilidad no equivale a escuchar cómo se anuncia una página.

Queda pendiente, para ejecutarla con una persona frente al equipo:

1. Activar NVDA y recorrer los siete pasos con Tab, confirmando que se anuncian las etiquetas, el estado del stepper y los desplegables.
2. Enviar un paso vacío y comprobar que se anuncia el resumen completo y luego cada error al llegar al campo.
3. Comprobar que los grupos «¿En tratamiento?», PIE y autorizaciones se anuncian como grupo con su leyenda, y que la opción marcada se informa como seleccionada.
4. Llegar al folio y verificar el anuncio del título y del resultado de «Copiar el folio».

## 6. Límites

- La navegación se automatizó con pulsaciones reales de Tab, pero no sustituye a una persona que use lector de pantalla ni a la revisión del orden lógico percibido.
- No se probó el zoom al 200 %, el modo de alto contraste de Windows ni los estados de foco con puntero.
- No se probaron las variantes de error simultáneo en varios pasos ni la conservación de datos tras un error, salvo lo ya cubierto en el recorrido de éxito.
- La comparación de estilos calculados cubre el paso 7; los demás pasos comparten las mismas hojas y el mismo breakpoint, pero no se compararon uno por uno.
- Los textos legales de las autorizaciones siguen pendientes de Dirección, como se acordó.
- Los resultados crudos están en `04-resultados-teclado.json`.

## 7. Entorno

Servidor de prueba en `127.0.0.1:5196` sobre una copia temporal de la base (`mpaz_teclado_verificacion`), compilación en `artifacts/teclado-temporal/build`. No se tocó la base `mpaz-matricula` ni el puerto 5195, ocupado por Visual Studio. El servidor, la copia de la base y las herramientas se retiraron al terminar.
