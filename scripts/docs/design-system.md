# Sistema de Diseño · Sistema de Matrículas
**Versión:** 1.1 (tokens institucionales oficiales: azul marino `#0C255B`, dorado `#D7A62C` — confirmados en el logo `logo-mpaz.svg`)

Tokens que gobiernan el look & feel de las 6 pantallas. Al implementar el frontend (.NET Razor/Blazor o SPA), estos valores deben vivir como variables CSS.

## Paleta de color

| Token | Hex | Rol de uso |
|-------|-----|------------|
| `--color-primary` | `#0C255B` | Azul marino institucional: sidebar, panel de login, header de documento |
| `--color-primary-strong` | `#081A44` | Azul marino más oscuro: banda inferior del panel de login |
| `--color-action` | `#1A3D85` | Azul marino intermedio: botones primarios, enlaces, barras de gráfico "en uso" |
| `--color-surface-active` | `#23407C` | Item de navegación activo, hover de sidebar |
| `--color-accent` | `#D7A62C` | Dorado de marca: emblema, indicadores de atención |
| `--color-success` | `#2E9E6B` | Estado "Matriculado", validaciones OK, chips verdes |
| `--color-warning-bg` | `#FDF3DC` | Fondo de alertas amarillas (pendientes) |
| `--color-warning-text` | `#7A5610` | Texto sobre alertas amarillas |
| `--color-danger` | `#B23B3B` | Estado "Retirado", errores de credenciales |
| `--color-danger-bg` | `#FBEAEA` | Fondo de alertas de error |
| `--color-bg` | `#F6F8FB` | Fondo general de la aplicación |
| `--color-surface` | `#FFFFFF` | Tarjetas, tablas, formularios |
| `--color-border` | `#E4EAF2` | Bordes de tarjetas y tablas |
| `--color-input-border` | `#C9D4E2` | Bordes de campos de formulario |
| `--color-text` | `#1C2B3A` | Texto principal y datos |
| `--color-text-secondary` | `#6B7C92` | Etiquetas y metadatos |
| `--color-text-muted` | `#ADBACB` | Placeholders y agregados de eje |
| `--color-text-inverted` | `#C7D5E8` | Texto secundario sobre azul marino institucional |

## Tipografía

| Uso | Valor |
|-----|-------|
| Familia base | `Segoe UI, Helvetica, Arial, sans-serif` (reemplazable por la tipografía institucional final) |
| Título de pantalla | 24px · Bold · `--color-text` |
| Título de panel (login) | 44px · Bold · blanco |
| Título de tarjetas/secciones | 16px · Bold |
| Body / datos de tabla | 13–14px · Regular |
| Etiquetas de formulario | 13–14px · SemiBold |
| Chip/badge | 11px · SemiBold |
| Metadatos legales | 10–12px · Regular |

Longitud de línea de párrafos: máximo ~80 caracteres.

## Componentes y estados

### Botones
- **Primario:** fondo `--color-action`, texto blanco, radio 8px, altura 44–48px, hover `--color-primary`
- **Secundario:** fondo blanco, borde `--color-input-border`, texto `--color-text`
- **Enlace:** `--color-action`, SemiBold

### Campos de formulario
- Altura 42–44px, radio 8px, fondo blanco, borde `--color-input-border`
- **Foco:** contorno `--color-action` (2px)
- **Foco válido:** borde `--color-success` (2px) + helper text verde con ✓
- **Error:** borde `--color-danger` + fondo `--color-danger-bg`
- Con selector: flecha `▾` en `--color-text-muted` a la derecha

### Chips / badges de estado
- Radio completo (pill), altura 20px, padding horizontal 16px
- Estados: `Matriculado` (verde `#E6F5ED`), `Pendiente` (dorado `#FDF3DC`), `Retirado` (rojo `#FFE9E9`), `Titular`/`Suplente` (verde/azul claro)

### Stepper del wizard
- Paso pendiente: círculo con borde `--color-input-border` y texto `--color-text-muted`
- Paso hecho: círculo dorado `--color-accent` con texto `--color-primary`
- Paso activo: círculo azul marino `--color-primary` con texto blanco y anillo dorado translúcido
- Encabezado del wizard: banda `--color-primary` con filete dorado de 4px inferior, título blanco y subtítulo `--color-panel-soft`
- Título de cada paso: regla dorada (56px × 4px)  debajo, similar a la del panel de login

### Tablas
- Encabezado: fondo `#EFF3F9`, texto 12px SemiBold
- Filas alternas: blanco / `--color-bg`
- Fila con alerta: badge adicional (ej. "prioritária" en rojo claro)

### Indicadores del dashboard
- Tarjeta blanca, borde `--color-border`, radio 10px, padding 24px
- Número grande: 34px Bold; tendencia: 12px con ▲ verde o texto dorado

## Gráficos
- Barras verticales de ocupación: gris `#C9D4E2` (cupo total), azul `--color-action` (en uso), dorado (por liberar), verde (con cupos)
- Barras horizontales por nivel: pista gris + relleno azul, etiqueta numérica a la derecha

## Iconografía
Los SVG usan glifos tipográficos (▦ ✎ ▾ ⌕ ⇩); en la implementación reemplazar por una librería de iconos coherente (Material Icons o Feather) manteniendo tamaño 16–20px y tono (`--color-text-secondary` en reposo).

## Accesibilidad mínima
- Contraste texto/aye: los pares texto-secundario/fondo y chip/fondo cumplen AA en los tonos elegidos; verificar con el auditor WCAG al implementar
- Campos con `label` siempre visible (nunca solo placeholder)
- Estados de foco visibles en todos los controles interactivos
