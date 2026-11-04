# Mockups · Sistema de Matrículas
**Escuela Básica Escritora Marcela Paz**

**Versión actual: v2 (22-10-2026)** — `mockups/figma-svg-v2/` según la [reunión 1](reunion-01-2026-10-21.md). La versión 1 sigue en `mockups/figma-svg/` como referencia histórica.

## v2 · 5 pantallas según el alcance preliminar

| # | Archivo | Pantalla | Rol | Contenido clave |
|---|---------|----------|-----|-----------------|
| 1 | `01-acceso-apoderado.svg` | Acceso del apoderado | Apoderado | RUT del estudiante + RUT del apoderado + año de nacimiento; error genérico que no revela qué falló; ayuda presencial en secretaría; sin OAuth ni correo |
| 2 | `02-ficha-pasos-apoderado.svg` | Ficha en pasos (actualización) | Apoderado | 4 pasos, guardado por etapas (aviso persistente), copia de teléfono/contacto/domicilio; declaración de veracidad al final, pendiente de confirmar institucionalmente |
| 3 | `03-ficha-estudiante-secre.svg` | Ficha del estudiante (Secretaría) | Secretaría | Ficha basada en el padrón importado del proceso externo, apoderados autorizados (titular/suplente) y actividad reciente para revisión |
| 4 | `04-acceso-personal.svg` | Acceso del personal | Dirección / UTP / Secretaría | Login RUN + contraseña separado del portal de apoderados; políticas de bloqueo/recuperación por acordar |
| 5 | `05-bandeja-revisiones.svg` | Bandeja de revisiones | Secretaría | Fichas enviadas por apoderados en estados Revisión / Validada / Observada; sin gráficos ni exportaciones por confirmar |

**Excluido de v2 por alcance:** dashboard con gráficos, gestión de cupos, exportaciones Excel, QR, comprobante PDF; se revalidan cuando llegue el acta de constitución.

## Fuente de verdad visual (Figma)
- **Archivo:** `Untitled` — https://www.figma.com/proto/ah9ugPGlAcRetDr0eSogCj/Untitled?node-id=2-380&page-id=0:1 (vista prototipo)
- **File key:** `ah9ugPGlAcRetDr0eSogCj`
- **Nota v2:** el archivo todavía muestra las 6 pantallas de la versión 1; hay que **importar los SVG de `figma-svg-v2/` a Figma** para tener la versión al día.
- **Organización:** 3 secciones en una página
  - `01 · Acceso y administración` (section nodo `3:76`)
  - `02 · Gestión de matrículas y estudiantes` (section `3:77`)
  - `03 · Consulta y documentación` (section `3:78`)

## Pantallas e IDs de Figma

| # | Pantalla | Nodo Figma | Rol principal | Propósito |
|---|----------|-----------|---------------|-----------|
| 1 | **01 · Acceso institucional** | `1:2` | Admin / Secretaría | Login con branding institucional, campos RUN + contraseña, alerta de credenciales, enlace a consulta de apoderado |
| 2 | **02 · Panel administrativo** | `2:46` | Admin / Secretaría | Dashboard: indicadores (matriculados, cupos, pendientes), ocupación por curso, matrícula por nivel, últimas matrículas |
| 3 | **03 · Formulario de matrícula** | `2:177` | Secretaría (diseño anterior) | Wizard de 4 pasos para alta; revisar frente al nuevo flujo de datos de estudiantes ya inscritos y guardado parcial |
| 4 | **04 · Búsqueda y listado de estudiantes** | `2:264` | Secretaría | Búsqueda por RUN/nombre/apellido con filtros por curso, letra y estado; tabla con chips de estado y exportación Excel |
| 5 | **05 · Portal del apoderado** | `2:380` | Apoderado | Ficha en solo lectura en el diseño anterior; la posible edición por etapas y el acceso propuesto aún no están diseñados |
| 6 | **06 · Comprobante de matrícula** | `2:452` | Secretaría / Apoderado (diseño anterior) | Documento ilustrativo; emisión, contenido y código de verificación pendientes de confirmación para fase futura |

> Para ver el detalle de cada pantalla en Figma, abrir el archivo y usar el nodo: `figma.com/design/ah9ugPGlAcRetDr0eSogCj/?node-id=<ID>` (reemplazar `:` por `-` en el ID).

## Assets en este repo

| Carpeta | Contenido |
|---------|-----------|
| `mockups/figma-svg-v2/` | **Versión 2 (actual):** 5 pantallas alineadas a reunión 1, importables a Figma |
| `mockups/figma-svg/` | SVG v1 de alta fidelidad — referencia histórica |
| `mockups/render/` | Renders PNG oficiales **exportados desde el archivo Figma** (fuente de referencia para specs) |
| `mockups/excalidraw/` | Wireframe board de baja fidelidad con el flujo por rol (para revisión rápida) |
| `mockups/render/contact-sheet.html` | Hoja de contacto con los renders para revisar sin abrir Figma |

## Convención de nombres
- SVG/PNG: `NN-nombre-pantalla.ext` (NN = número de pantalla)
- Frames en Figma: `NN · Nombre de pantalla`
- Grupos dentro de frame: nombre descriptivo de la región, ej. `Navegación lateral`, `Cabecera · …`, `Tabla · …`

## Cambios aplicados en Figma respecto a los SVG del repo
El archivo de Figma es la versión consolidada tras revisión:
- Pantallas renombradas y agrupadas en 3 secciones temáticas
- Fondos convertidos a imagen (`#f6f8fb`) y estructura en grupos por región
- El prototipo está armado desde `05 · Portal del apoderado` (nodo `2:380`) — pendiente extender los links del prototipo a las pantallas 1→2→3→4→6
