# Revisión de protección de datos · Ley 21.719

**20-11-2026 · Revisión interna, sin cambios de código en esta entrega.**
La Ley 21.719 (nueva ley de protección de la vida privada y de datos
personales) rige desde el **01-12-2026**. El usuario lo mencionó como
“leyes 21.000 / seguridad y protección de datos”. Este documento deja
constancia de cómo está hoy el Portal de Matrícula MPAZ frente a esa
ley y qué falta para cumplirla. No sustituye asesoría legal.

## 1. Lo que el portal ya hace bien

| Exigencia práctica | Estado en el portal |
|---|---|
| Acceso solo con cuenta de funcionario, sin registro público | Sí: `Seguridad:ExigirInicioSesion=true`; no hay pestaña pública de registro (decisión 10 del documento de decisiones). |
| Acceso por rol y por área (mínimo necesario) | Sí: matriz de permisos `rol_permiso` por área/acción; cada bloque sensible de la ficha exige su permiso y, sin él, ni se consulta (S-02). |
| Auditoría de cada lectura y cambio | Sí: `registros_acceso` (lecturas) e `historial_cambios` (cambios, por trigger). Las fichas médicas del personal y las observaciones familiares también auditan. |
| Datos sensibles con protección reforzada | Sí: salud, PIE, judicial y medicación son áreas separadas; los archivos de documentos y fichas médicas se guardan fuera de `wwwroot`, con hash y descarga solo desde la ficha. |
| Sin cesión a terceros | Sí: no hay correo ni servicios externos de envío; el único canal externo es el web service de respaldo con token. |
| Conservación sin borrado indiscriminado | Sí, con la decisión 13 aplicada: expediente y auditoría no se borran; solo se poda `alerta_revision` a 24 meses (paso 22). |
| Documentos anteriores nunca se borran | Sí: un documento reemplazado queda inactivo, no se elimina. |
| Requisitos documentales no bloquean | Sí (decisión 11): `requisito_documental` con estado PENDIENTE no impide formalizar la matrícula (MAT-07). |

## 2. Brechas detectadas (a decidir con el colegio)

1. **Aviso de privacidad.** No hay un aviso visible que informe qué datos
   se tratan, con qué finalidad, por cuánto tiempo y ante quién se
   ejercen los derechos. Es el primer paso formal que exige la ley.
2. **Derechos de acceso, rectificación y oposición (ARCO).** No existe un
   flujo en el portal para que un apoderado o funcionario pida acceso,
   corrección o eliminación de sus datos; hoy todo pasa por Secretaría
   en papel. Hay que definir el procedimiento y los plazos.
3. **Plazos formales de conservación.** La decisión 13 define “no borrar
   nada” para expediente y auditoría, pero la ley pide plazos definidos
   por tipo de dato. Falta una tabla de retención aprobada por el
   sostenedor (p. ej. fichas de egresados, licencias del personal).
4. **Responsable de datos.** No está designado un encargado de protección
   de datos ni un canal de contacto para solicitudes. Debe nombrarlo el
   sostenedor, no el portal.
5. **Datos del personal (fichas médicas).** Son datos sensibles de
   trabajadores; hoy solo UTP los ve, lo cual es correcto, pero falta
   definir quién puede pedir su eliminación al terminar la relación
   laboral y en qué plazo.
6. **Evaluación de impacto de datos sensibles.** Salud, PIE y judicial
   son datos sensibles reforzados; la ley puede exigir una evaluación
   previa documentada. No existe aún.
7. **Seguridad técnica a verificar antes del 01-12-2026:** respaldo
   probado con restauración (existe respaldo Neon, falta prueba de
   restauración documentada), control de sesiones y cierre por
   inactividad, y revisión de contraseñas/SSO.

## 3. Propuesta de cierre (no implementada en esta entrega)

1. Aprobar el aviso de privacidad y publicarlo en el portal.
2. Definir el procedimiento ARCO con plazos y responsable.
3. Aprobar la tabla de retención por tipo de documento (decisión 13).
4. Nombrar al responsable de datos del sostenedor.
5. Documentar la evaluación de impacto de salud/PIE/judicial.
6. Prueba de restauración de respaldo y revisión de sesiones.

Cuando el colegio apruebe los puntos 1–4, se implementan en el portal;
mientras tanto, el tratamiento actual (acceso por rol, auditoría,
sin cesión y sin borrado) es la postura por defecto.
