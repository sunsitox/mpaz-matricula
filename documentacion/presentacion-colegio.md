# Portal de Matrícula MPAZ — Documento de presentación

**Escuela Básica «Escritora Marcela Paz» · Dirección**
Versión 1.0 · 27-11-2026

---

## 1. Para qué sirve este documento

Reunir en un solo lugar **qué está construido**, **qué falta** y, sobre todo,
**qué necesitamos que Dirección decida** para cerrar el portal. La idea es que la
reunión termine con respuestas concretas y no con una nueva lista de dudas.

Al final de cada sección hay una marca:

- **[LISTO]** ya está implementado y probado.
- **[DECISIÓN]** requiere una respuesta del colegio.
- **[DATO]** requiere que el colegio entregue un documento o información.
- **[NUESTRO]** queda de nuestro lado, no depende del colegio.

---

## 2. Resumen ejecutivo

El portal está **funcionando y desplegado**: cubre el ciclo completo de matrícula,
desde el asistente de ocho pasos hasta el certificado de alumno regular, con
auditoría de cada acción y control de acceso por rol.

Lo que falta para cerrarlo no es desarrollo: son **8 decisiones del colegio** y
**5 documentos o datos** que solo Dirección puede aprobar o entregar. Con eso
resuelto quedan tres entregables nuestros: manual de uso, documentación de base de
datos y la integración con el login único del colegio.

**Lo que pedimos hoy:** revisar la sección 6 (decisiones) y la sección 7 (datos),
y salir de la reunión con una respuesta para cada punto.

---

## 3. Qué es el portal

Una aplicación web para que el colegio matricule sin papeles duplicados y con
trazabilidad: quién inscribió, quién corrigió, quién firmó, quién validó y cuándo.

| Módulo | Qué resuelve | Estado |
|---|---|---|
| 01 · Acceso y sesión | Login por correo institucional, bloqueo por intentos, cierre de sesión | **[LISTO]** |
| 02 · Ficha de matrícula | Asistente de 8 pasos con revisión final y folio | **[LISTO]** |
| 03 · Estudiantes y expediente | Búsqueda, ficha por pestañas, documentos, autorizaciones, firma, confirmación, corrección con historial y anulación | **[LISTO]** |
| 04 · Medicación (TENS) | Panel de medicación con base legal y control por rol | **[LISTO]**, operando con el protocolo pendiente |
| 05 · Alertas y reportes | Vencimientos, reporte de matrículas y faltantes con exportación CSV | **[LISTO]** |
| 06 · Fichas médicas del personal | Ficha de salud del personal, reservada a UTP y Dirección | **[LISTO]** (campos oficiales **[DATO]**) |
| 07 · Funcionarios y roles | Alta de cuentas, clave temporal, activar/desactivar, cierre de rol | **[LISTO]** |
| 08 · Certificados | Emisión con folio, QR y código de verificación pública | **[LISTO]** |
| 09 · Configuración | Año escolar de matrícula y su periodo | **[LISTO]** |
| 10 · Transversal | Auditoría, respaldos, exportaciones, integridad de datos | **[LISTO]** |

**Evidencia disponible para mostrar en la reunión:**

- Video del recorrido completo, 7 min 50 s, con los 10 módulos y 34 casos.
- 213 pruebas automatizadas en verde y un script que las corre y muestra el
  checklist por módulo (`scripts/pruebas-automatizadas.bat`).
- Documentación técnica con 15 diagramas (arquitectura 4+1, modelo de datos,
  estados, seguridad y operación).
- Plan de pruebas con 89 casos: 75 ejecutados, 14 pendientes (casi todos de
  medicación TENS).

---

## 4. Lo que ya está resuelto y no vuelve a la mesa

Para no repetir acuerdos ya tomados:

- La matrícula la **crea y formaliza Secretaría**; Dirección lee, valida y exporta.
- **UTP y Convivencia** son perfiles separados.
- El certificado lo firma **Dirección** (subrogante, cargo «Director») y su
  formato está aprobado.
- Los **documentos obligatorios** se retiraron del alcance: se definen caso a caso.
- La **conservación de datos** no se planifica: por ley se conservan.
- La autorización de **paracetamol** vive en la ficha y la responde el apoderado.
- **Pre-kínder y Kínder** no llevan letra.

---

## 5. Cómo se ve el cierre

```
   HOY                      DECISIONES           ENTREGA FINAL
    │                            │                     │
    ▼                            ▼                     ▼
 Portal funcionando  ──►  8 decisiones + 5 datos ──► Manual, BD y SSO
 (10 módulos, 34 casos)     (sección 6 y 7)          (sección 8)
```

Sin las decisiones de la sección 6 el portal **funciona igual**, pero queda con
textos marcados «pendiente de aprobación» y sin protocolo de medicación vigente.

---

## 6. Decisiones que necesitamos de Dirección

Cada fila trae una **propuesta por defecto** ya aplicada en desarrollo. Si el
colegio no responde, se queda esa propuesta; si responde otra cosa, se cambia.

| # | Decisión | Propuesta por defecto | Qué pasa si no se responde |
|---|---|---|---|
| 1 | **Matriz de permisos** que regirá en la base del colegio | La del repositorio (`docs/roles-permisos-actuales.md`) | Las pantallas responden «sin permiso» tras el login |
| 2 | **Protocolo de medicación TENS** (MAT-05) y qué hacer si no hay TENS | Medicación habilitada por orden expresa desde el 25-11-2026 | El registro de dosis queda sin respaldo normativo escrito |
| 3 | **Textos legales** de las autorizaciones de imagen y convivencia | Texto actual marcado «Pendiente de aprobación (Dirección)», versión 1 | Se firma un texto que no está aprobado formalmente |
| 4 | **Observaciones de situación familiar**: ¿llevan adjuntos? ¿quién los autoriza? | Área propia, sin adjuntos; UTP y Convivencia leen y editan | Queda sin definir el manejo de documentos sensibles |
| 5 | **Ficha médica del personal**: campos por sección de la ficha oficial | Ficha general ya implementada en «Personal» | La ficha del personal queda incompleta frente al formato del colegio |
| 6 | **Ley 21.719**: aviso de privacidad y derechos de acceso/rectificación | Revisión con 7 brechas detectadas (`docs/revision-ley-21719.md`) | Riesgo legal desde el 15-10-2027 |
| 7 | **Membrete del certificado**: qué dato identificatorio del colegio debe verse | Folio, código y 30 días ya presentes; falta destacar el dato del colegio | El certificado sale sin el identificador que exige el colegio |
| 8 | **Login único (SSO)**: ¿la web del colegio pasa a ser el login principal? | El portal queda como está y se integra después | Cada sistema mantiene su propia clave (ver sección 8.3) |

---

## 7. Datos y documentos que debe entregar el colegio

| # | Qué | Para qué | Quién |
|---|---|---|---|
| 1 | Protocolo TENS aprobado y firmado | Habilitar el registro de dosis con respaldo | Dirección |
| 2 | Ficha médica oficial del personal, por secciones | Completar el módulo 06 | Dirección / UTP |
| 3 | Textos definitivos de autorizaciones (imagen y convivencia) | Reemplazar el texto «pendiente de aprobación» | Dirección |
| 4 | Dato identificatorio del colegio para el membrete | Certificado de alumno regular | Dirección |
| 5 | Usuarios reales y sus roles (o el listado de funcionarios) | Cargar cuentas y permisos en la base del colegio | Dirección / Secretaría |

---

## 8. Lo que queda de nuestro lado

### 8.1 Entregables pendientes

| Entregable | Estado | Qué falta |
|---|---|---|
| Manual de uso (Secretaría y Dirección) | Pendiente | Paso a paso con capturas reales y errores frecuentes |
| Documentación de base de datos | Parcial | Esquema, restricciones e índices explicados |
| Despliegue y respaldo | Parcial | Prueba de restauración desde cero |
| Carga de alumnos reales | Pendiente | Ejecutar la limpieza y cargar el listado del colegio |

### 8.2 Cobertura de pruebas

- 213 pruebas automatizadas y 34 casos visuales cubren los módulos 02, 03, 05,
  06, 08 y 10.
- **Sin prueba automatizada:** 01 (Identity), 04 (triggers PL/pgSQL), 07
  (`UserManager`) y 09 (SQL crudo). Se probaron a mano y en el video; falta
  automatizarlos.
- **Plan manual:** 14 casos pendientes, 11 de ellos de medicación TENS.

### 8.3 Integración con el login del colegio (SSO)

Revisión hecha el 27-11-2026 sobre la base de Neon, **sin modificar nada**:

| Esquema | Qué contiene | Observación |
|---|---|---|
| `mpaz_web` | Web del colegio (Django): `core_usuario` con 23 usuarios y `auth_user` con 6 | Dos padrones que solo coinciden en 3 correos |
| `mpaz_auth` | Servidor OpenID Connect (OpenIddict) | Solo el cliente de prueba del prototipo |
| `mpaz_matricula` | Portal de matrícula | 10 funcionarios, todos con `sso_sujeto` (UUID) |

Hallazgos que condicionan la integración:

- **10 de 23** contraseñas de `core_usuario` están en texto plano o son muy
  cortas: hay que sanearlas antes de un login único.
- De nuestros 10 funcionarios, **8 existen en `core_usuario`** por correo y
  **ninguno** en `auth_user`.
- El portal ya emite tokens OIDC (`EmisorSso`), así que la pieza existe; falta
  invertir el sentido: que la web autentique y el portal consuma.
- **Falta decidir:** ¿`core_usuario` o `auth_user` es el padrón oficial?

**Propuesta:** login único por OpenID Connect, con la web como fuente de
identidad y el portal como cliente; los permisos del portal siguen siendo
locales. Antes hay que definir el padrón, sanear las claves y crear los 2
funcionarios que faltan.

---

## 9. Marco legal y riesgos

- Hoy rige la **Ley 19.628**; la **Ley 21.719** entra en vigencia el **15-10-2027**
  (fecha fijada por la Ley 21.855). El portal ya trata datos de niños, niñas y
  adolescentes, que son categoría especial.
- El portal **no expone datos reales de alumnos** en videos, pruebas ni
  documentación: todo lo que se muestra es ficticio.
- Riesgo principal si no se aprueba el aviso de privacidad: incumplimiento
  normativo desde la fecha de vigencia, no un problema técnico.

---

## 10. Plan de cierre propuesto

| Fase | Contenido | Depende de |
|---|---|---|
| 1 | Cerrar decisiones 1 a 8 y recibir los 5 datos | Dirección |
| 2 | Manual de uso y documentación de base de datos | Nosotros |
| 3 | Carga de alumnos reales y prueba de restauración | Colegio + nosotros |
| 4 | Login único con la web del colegio | Decisión 8 y saneamiento de claves |

---

## 11. Anexos

- `documentacion/modulo-matricula/documentacion-tecnica.md` — arquitectura
  completa con diagramas.
- `mpaz-matricula/pruebas-visuales/evidencias/grabar-flujo/flujo-mpaz.webm` —
  video del recorrido.
- `docs/plan-pruebas-portal-mpaz.xlsx` — plan de pruebas con su estado.
- `docs/decisiones-pendientes-colegio.md` — detalle de cada decisión.
- `docs/roles-permisos-actuales.md` — matriz de permisos vigente.
- `docs/revision-ley-21719.md` — brechas de protección de datos.

---

## 12. Cómo usar este documento en la reunión

1. Mostrar el video (7 min) o los módulos 02, 03 y 08 en vivo.
2. Recorrer la tabla de la sección 6 y anotar la respuesta de cada fila.
3. Confirmar qué documentos de la sección 7 se entregan y cuándo.
4. Cerrar con las fechas de la sección 10.
