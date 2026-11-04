# Documentación · Sistema de Matrículas
**Escuela Básica Escritora Marcela Paz**

Índice de la documentación del proyecto. **Primero leer [reunión 1](reunion-01-2026-10-21.md):** son observaciones preliminares del 21-10-2026, pendientes de contrastar con el acta de constitución. El SRS y los mockups previos contienen hipótesis de alcance que no deben tratarse como aprobadas. Los documentos marcados 🏗 son estructuras a completar con el equipo.

## Levantamiento y producto
| Documento | Estado | Contenido |
|---|---|---|
| [reunion-01-2026-10-21.md](reunion-01-2026-10-21.md) | 🏗 Observaciones por validar | Contexto, alcance inicial, propuesta de acceso, riesgos y preguntas para el acta |
| [SRS_Escuela_Marcela_Paz.md](../../SRS_Escuela_Marcela_Paz.md) | ⚠️ Borrador anterior | Requisitos históricos; revisar diferencias con reunión 1 antes de implementar |
| [systema-matricula-tasks.md](systema-matricula-tasks.md) | ⚠️ Plan anterior | Repriorizar tras el acta |

## Diseño visual
| Documento | Estado | Contenido |
|---|---|---|
| [MOCKUPS.md](MOCKUPS.md) | ✅ Actualizado | v2 (22-10-2026): 5 pantallas en `mockups/figma-svg-v2/` según reunión 1; v1 queda como referencia histórica |
| [design-system.md](design-system.md) | ✅ Completo | Tokens de color, tipografía, componentes y estados visuales |
| [user-flows.md](user-flows.md) | 🏗 Esqueleto revisado | Flujo preliminar para apoderados y personal; validar permisos y pasos con la escuela |

## Requisitos y Arquitectura (futuro)
| Documento | Estado | Contenido esperado |
|---|---|---|
| `requirements.md` | ⬜ Pendiente | Historias de usuario, criterios de aceptación de cada pantalla |
| `api-contract.md` | ⬜ Pendiente | Contratos REST de Estudiantes, Apoderados, Matrículas y Reportes |
| `data-model.md` | ⬜ Pendiente | DER con tablas Estudiantes, Apoderados, EstudianteApoderado, Cursos, Matriculas, AuditoriaLogs |
| `security.md` | ⬜ Pendiente | RBAC, Ley 19.628, políticas de contraseñas |
| `deployment.md` | ⬜ Pendiente | Publicación, respaldos (pg_dump), disaster recovery |

## Guías operativas
| Documento | Estado |
|---|---|
| [LOCATION-GUIDE.md](LOCATION-GUIDE.md) | ✅ Completo — convención de carpetas del repo |
| `../scripts/` | Scripts de setup y automatización |

## Orden de lectura sugerido
1. [Reunión 1](reunion-01-2026-10-21.md) → contexto actualizado, riesgos y decisiones abiertas
2. [Flujos](user-flows.md) → hipótesis de interacción aún por validar
3. [Mockups](MOCKUPS.md) y [sistema de diseño](design-system.md) → antecedentes visuales, no alcance comprometido
4. [Plan inicial](systema-matricula-tasks.md) y [SRS anterior](../../SRS_Escuela_Marcela_Paz.md) → revisar antes de reutilizar
