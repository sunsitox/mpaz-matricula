# Flujos de usuario · Sistema de matrículas

**Estado:** estructura preliminar, no aprobada. Basada en las [observaciones de reunión 1](reunion-01-2026-10-21.md), pendientes de acta de constitución y validación con la escuela. Los [mockups](MOCKUPS.md) son antecedentes, no especificación de comportamiento.

## Actores y límites por definir

- **Apoderado titular o suplente autorizado:** accede solo a los estudiantes con vínculo validado en servidor; permisos de lectura y edición pendientes de definir.
- **Personal administrativo / Secretaría:** acceso institucional separado; validar roles reales y permisos.
- **Dirección:** supervisión o aprobación, según se acuerde. El dashboard del mockup no constituye un requisito confirmado.
- **Estudiante ya inscrito:** su registro debe existir previamente en el padrón institucional; definir quién lo importa, corrige y valida.

## Flujo A · Identificar expediente y verificar apoderado (propuesta)

- [ ] Ingresar RUT del estudiante, RUT del apoderado y año de nacimiento del estudiante.
- [ ] Validar formatos y consultar el vínculo titular/suplente en servidor, sin revelar cuál dato falló ni si existe el alumno.
- [ ] Definir verificación de identidad suficiente **antes** de permitir datos sensibles, edición o descargas: esos tres datos pueden conocerse por terceros.
- [ ] Limitar intentos por IP e identificador sin bloquear indiscriminadamente una red compartida; definir duración de sesión y reingreso.
- [ ] Resolver más de un estudiante asociado al mismo apoderado sin exponer fichas ajenas.
- [ ] Definir atención presencial u otro mecanismo para usuarios que no logren ingresar; no depender de OAuth o correo de recuperación.

## Flujo B · Completar ficha en etapas (propuesta inicial)

- [ ] Confirmar que el estudiante ya figura en el padrón procedente del proceso externo; evitar altas/admisiones por el portal sin autorización institucional.
- [ ] Definir bloques del stepper con Dirección y Secretaría: datos del estudiante, apoderados, antecedentes requeridos, revisión y confirmación (orden provisional).
- [ ] Guardar de forma persistente cada etapa en PostgreSQL; al reanudar, cargar solo el borrador autorizado del expediente correspondiente.
- [ ] Diferenciar campos obligatorios de opcionales y mostrar errores específicos sin perder lo ingresado.
- [ ] Definir en qué momento se exige «Declaro que los datos ingresados son fidedignos»; propuesta: al finalizar, para no impedir avances con guardado parcial. Validar texto y valor institucional.
- [ ] Determinar si la finalización confirma matrícula o solo envía datos para revisión del personal.

## Flujo C · Personal institucional (acceso diferenciado)

- [ ] Definir credenciales y recuperación de acceso del personal separadas del portal de apoderados.
- [ ] Confirmar qué puede buscar, editar y aprobar Secretaría, y qué operaciones requieren autorización de Dirección.
- [ ] Verificar acceso al padrón, manejo de duplicados, modificaciones y auditoría antes de implementarlo.

## Flujo D · Administración y documentos (proyección futura)

- [ ] Búsqueda y edición centralizada de registros por personal autorizado.
- [ ] Comprobante de matrícula y certificado de alumno regular en PDF con logotipo institucional; definir contenido, responsables, formato y etapa del proyecto.
- [ ] Revalidar dashboard, gestión de cupos, exportaciones, QR y reportes de los mockups/SRS anterior; no prometerlos para la primera entrega.

## Casos transversales pendientes

- [ ] Exposición del servidor Ubuntu: acceso local/remoto, HTTPS, disponibilidad y soporte a apoderados.
- [ ] Protección de datos de menores: permisos por vínculo, tratamiento de salud/PIE, retención, respaldo y auditoría; validar exigencias institucionales y legales.
- [ ] Usabilidad en móvil, legibilidad, ayudas claras y prueba con apoderados de distintos niveles de alfabetización digital.
- [ ] Definir criterios medibles de desempeño, disponibilidad y aceptación tras conocer volumen y alcance reales.
