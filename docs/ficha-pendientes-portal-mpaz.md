# Ficha de pendientes — Portal de Matrícula MPAZ

**Corte: 27-11-2026**, con las definiciones del colegio del 26-11 ya aplicadas.
Consolida lo que queda abierto después de la corrida completa del plan
(75 de 89 casos ejecutados: 72 P, 3 B, 0 F).

Fuentes: `docs/checklist-portal-matricula-modulo-3.md` (§0, §16–§21),
`docs/decisiones-pendientes-colegio.md`, `docs/roles-permisos-actuales.md`,
`docs/revision-ley-21719.md` y `docs/plan-pruebas-portal-mpaz.xlsx`.

---

## 1. Tablero

| Frente | Abiertos | Quién desbloquea |
| --- | --- | --- |
| Pruebas por ejecutar | 14 casos (11 de 04 + TRX-11/12/13) | José |
| Decisiones del colegio | 7 | Dirección / sostenedor |
| Datos de alumnos | 3 (reinicio y validación el día de matrícula) | José + colegio |
| Producción e infraestructura | 6 | Coordinación + autorización |
| Deuda técnica y entrega | 7 | José |
| Resguardo inmediato de seguridad | 1 | José |

---

## 2. Cerrado en esta corrección (no vuelve a la lista)

| Tema | Definición del colegio, 26-11-2026 |
| --- | --- |
| **Quién crea y formaliza la matrícula** | **Secretaría.** Es quien crea y maneja casi todo lo de la matrícula |
| **Permisos sobre la matrícula** | Secretaría: leer, crear, editar, exportar · Dirección: leer, validar, exportar · Inspectoría: leer, editar · PIE: leer · UTP: leer, exportar · Convivencia: leer · TENS: leer · Docente: leer |
| **Autorización de paracetamol** | Va en la ficha de matrícula; **el apoderado** marca ahí si se le puede dar paracetamol al estudiante. No es un documento aparte. Ya implementado: paso 6 del asistente (`AutorizaParacetamol`, Sí/No) y bloque impreso de la ficha |
| **Formato del certificado de alumno regular** | Correcto, ya revisado. No se toca |
| **Documentos exigidos / obligatorios** | Se retira del alcance: se están definiendo caso a caso, cada dato como documento puntual |
| **Conservación y eliminación de datos** | Por ley no se pueden borrar: se conservan. No se planifica eliminación |

La matriz confirmada para la matrícula coincide con la que ya está cargada en
desarrollo (`GENERAL`: Secretaría L C E X · Dirección L V X · Inspectoría L E ·
PIE L · UTP L X · Convivencia L · TENS L · Docente L), así que **no requiere
cambios de código**; sólo cargarla en la base del colegio.

### 2.1 Corregido en la corrida de pruebas

| Tema | Qué pasó | Estado |
| --- | --- | --- |
| Auditoría de la emisión de certificados | El `CHECK` de `registros_acceso` no admitía `EMITIR`: la emisión no dejaba rastro y sólo quedaba un aviso en el log | Corregido (3 DDL + `pasos/28-accion-emitir-en-registros-acceso.sql`) y verificado con una emisión real; caso **CER-07** |
| Caso ALR-02 mal definido | Pedía marcar la alerta a mano; la revisión es automática (job cada 15 min) | Caso reescrito en el plan |

---

## 3. Pruebas: 14 casos sin ejecutar

Ejecutados: **75 de 89** (72 P, 3 B, 0 F) al 27-11-2026. Cerradas las hojas 01
Acceso, 02 Asistente, 03 Estudiantes, 05 Alertas y reportes, 06 Fichas médicas,
07 Funcionarios, 08 Certificados, 09 Configuración y 10 Transversal.

| Hoja | Casos | Qué falta |
| --- | --- | --- |
| 04 Medicación TENS | 11 | Prescripción vigente, registro de dosis, autorización, bloqueo sin orden, auditoría |
| 10 Transversal | 3 | TRX-11 CSRF, TRX-12 no-store, TRX-13 entrega de PDF (casos agregados el 27-11) |

Bloqueados (B):

- **ACC-08** — expiración de sesión a las 8 h: la cookie es de sesión y la política es absoluta; hay que esperar la ventana o simular reloj.
- **ALR-02** — el caso pedía marcar la alerta a mano; la revisión es automática (`AlertasProgramadas`, cada 15 min). Caso reescrito, falta ver correr el ciclo.
- **TRX-09** — publicar al servidor y construir la imagen Docker.

**La hoja 04 no está bloqueada por datos:** el mock trae el caso de asma con
prescripción VIGENTE validada y autorización OTORGADA, y `Medicacion:Habilitada`
se enciende por configuración. Falta ejecutarla; para producción sigue pendiente
el protocolo TENS escrito.

**Requisito para cerrar el plan:** publicar (TRX-09) y construir la imagen Docker
de `app`; el resto se puede hacer en local.

---

## 4. Decisiones del colegio que siguen abiertas

| # | Qué falta | Por qué bloquea |
| --- | --- | --- |
| 1 | Aprobar y **cargar la matriz de permisos en la base del colegio** | Sin ella las pantallas responden 403 tras el login. El contenido ya está confirmado (§2) |
| 2 | **Protocolo escrito de medicación TENS** | La medicación está habilitada sin respaldo de custodia ni qué hacer si el TENS no está. Las 12 preguntas están en `docs/protocolo-medicacion-preguntas-TENS-direccion.docx` |
| 3 | **Textos legales de autorizaciones** de imagen y convivencia | Hoy se guardan como «Pendiente de aprobación (Dirección)» con nombre de quien aprueba |
| 4 | **Situación familiar: ¿llevará adjuntos?** | El área existe sin archivos; hay que decidir si los tendrá y quién los autoriza |
| 5 | **Ficha médica del personal: campos por sección** | Implementada en general (UTP + Dirección); falta el detalle de las 7 secciones de la ficha oficial |
| 6 | **Ley 21.719**: aviso de privacidad y derechos de acceso/rectificación | Rige el 15-10-2027 (Ley 21.855); hasta entonces Ley 19.628. Brechas en `docs/revision-ley-21719.md` |
| 7 | **Membrete del certificado**: identificador del colegio | El dato que aparece no sería el correcto y debería verse más. Por precisar cuál es el dato y cómo debe mostrarse |

---

## 5. Datos de alumnos

No es un proyecto de importación. Lo que hay es **un intento incompleto del
inspector general** por tener una base propia, con pocos registros. La decisión
del colegio es:

- [ ] **Reiniciar la base de datos** de trabajo antes de matrícula (hoy tiene datos
      parciales y de demostración).
- [ ] **Validar a todos los estudiantes el día de la matrícula**, no antes: ahí se
      corrigen RUN, apoderados, curso y datos clínicos con el apoderado delante.
- [ ] Quitar las **5 matrículas de demostración** (`MP-2027-00001` a `00005`) y las
      **9 cuentas `prueba.*`** al reiniciar.

Por eso quedan sin efecto el perfilado de la fuente, la normalización masiva de RUN,
la cuarentena y la carga inicial: no se migran esos datos.

---

## 6. Producción e infraestructura

- [ ] Aplicar en el servidor lo que falte del paquete SQL: `12-alertas-vencimiento.sql`,
      `13-alertas-revision.sql`, `28-accion-emitir-en-registros-acceso.sql` (auditoría de
      certificados), migración `AgregarSso` y la guarda de `04-privilegios-aplicacion.sql`.
- [ ] Cargar la matriz de permisos en la base del colegio (§4.1).
- [ ] Construir y probar la **imagen Docker** del servicio `app` y el arranque completo.
- [ ] Respaldos programados y copia externa.
- [ ] Dominio propio con Cloudflare Access.
- [ ] Rotación de credenciales que estuvieron visibles.

Ya hecho: servidor con `Medicacion:Habilitada=true` (`20261125T061732Z`), tablas sin
recarga (`20261125T063936Z`) y ficha de salud del personal con trigger de auditoría en Neon.

---

## 7. Deuda técnica y cierre de entrega

- [ ] Automatizar el recorrido completo del asistente (hoy se validó con un script externo).
- [ ] Verificar que el seed mock y `datos-prueba/` no entren en la imagen Docker.
- [ ] Commitear `docs/plan-pruebas-portal-mpaz.xlsx` y `docs/generar_plan_pruebas.py`.
- [ ] Firmar el plan: los resultados ya están cargados (75/89); el guion conserva los
      resultados al regenerar el Excel.
- [ ] **No hay pantalla para crear o validar órdenes médicas**: hoy se cargan por SQL.
      Sin eso, el colegio no puede ingresar una receta nueva sin ayuda técnica.
- [ ] El historial de certificados no permite reimprimir ni anular: si se pierde el PDF
      hay que emitir otro. El modelo tiene `AnuladoEn`, falta la acción y su caso.
- [ ] Decidir el destino de `SUPERADMIN` y `superadmin.test@mpaz.cl` (solo en la base local de prueba).

---

## 8. Resguardo inmediato de seguridad

- [ ] Dejar en cero el contador de intentos fallidos de `admin.palma@mpaz.cl` en
      producción (`AccessFailedCount` +1, sin bloqueo), por el desvío de
      `ConnectionStrings:Auth` durante las pruebas.

Las cuentas temporales ya están eliminadas: la base `mpaz_pruebas`, el servidor local
en 5102, el PostgreSQL de 5433 y `mpaz-test.env` no existen. No quedó ninguna cuenta
creada en producción.

---

## 9. Orden recomendado

1. **Cargar la matriz en la base del colegio** — el contenido ya está definido, sólo
   falta el respaldo previo y la ejecución.
2. Ejecutar la **hoja 04** con el caso de asma del mock y los tres casos nuevos de
   la hoja 10 (TRX-11, TRX-12, TRX-13).
3. Construir la **imagen Docker** de `app` y publicar: cierra TRX-09.
4. Reiniciar la base y dejar el portal limpio para la matrícula; la validación de
   estudiantes se hace ese día.
5. Con el protocolo TENS en mano: habilitar la medicación en producción y hacer la
   prueba de aceptación con una orden real.
6. Decidir la carga de órdenes médicas (pantalla propia o carga asistida).

---

## 10. Riesgos abiertos

| Riesgo | Impacto |
| --- | --- |
| Medicación habilitada sin protocolo escrito de TENS | Se registran dosis sin respaldo institucional de custodia |
| Matriz de permisos sin cargar en la base del colegio | Tras el login, las pantallas responden 403 |
| 9 cuentas `prueba.*` y 5 matrículas demo en desarrollo | Podrían confundirse con datos reales si no se reinicia |
| Textos de autorizaciones sin aprobación formal | La ficha se firma con texto marcado como pendiente |
| Ley 21.719 sin aviso de privacidad | Incumplimiento al 15-10-2027 |
| Órdenes médicas que sólo se cargan por SQL | Una receta nueva requiere acceso técnico a la base |
