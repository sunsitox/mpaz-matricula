# Decisiones pendientes del colegio · Portal de Matrícula MPAZ

**19-11-2026 · Documento de trabajo (actualizado).** Contiene *sólo* lo que
queda pendiente o inconcluso. Todo lo ya resuelto —UTP y Convivencia como
perfiles separados; firmante del certificado (subrogante, cargo «Director»);
documentos obligatorios que no bloquean (dec. 11); conservación sin borrar
expediente ni auditoría y poda de alertas a 24 meses (dec. 13); observaciones
de situación familiar (área `SITUACION_FAMILIAR`, dec. 19 por defecto); notas
de retiro (UTP y Convivencia escriben, Secretaría e Inspectoría leen);
Pre-kínder/Kínder sin letra— quedó aplicado en desarrollo y **no se repite
aquí**. Lo que sigue abajo es lo que falta confirmar o entregar.

| # | Qué falta | Propuesta por defecto (aplicada en desarrollo) | Pendiente del colegio |
| --- | --- | --- | --- |
| 1 | **Matriz de permisos en la base del colegio** | La matriz vigente en el repositorio (ver `docs/roles-permisos-actuales.md`) | Aprobarla y cargarla en la base del colegio, con respaldo previo. Sin ella, las pantallas responden 403 tras el login. |
| 2 | **Quién formaliza la matrícula** (crea, confirma, anula) | Secretaría crea/edita/confirma/anula; Dirección valida; el resto consulta | Confirmar o corregir el perfil que formaliza. |
| 3 | **Protocolo de medicación por TENS** (MAT-05), custodia y qué hacer si el TENS no está (dec. 3) | Medicación habilitada el 25-11-2026 por orden expresa (`Medicacion:Habilitada=true`) | Protocolo escrito aprobado (sigue pendiente). |
| 4 | **Textos legales de autorizaciones** de imagen y convivencia (dec. 4) | Texto vigente guardado como «Pendiente de aprobación (Dirección)», versión 1 | Aprobación con nombre de quien aprueba. |
| 5 | **Documentos obligatorios** (dec. 11, en detalle) | Requisitos actuales, «pendiente» que no bloquea la formalización | Lista formal de documentos obligatorios y cuáles bloquean el cierre. |
| 6 | **Conservación y eliminación** (dec. 13, en detalle) | No borrar expediente ni auditoría; poda de `alerta_revision` a 24 meses | Plazos formales de retención por tipo de documento y procedimiento de eliminación con el sostenedor. |
| 7 | **Observaciones de situación familiar** (dec. 19, en detalle) | Área propia `SITUACION_FAMILIAR`; UTP y Convivencia leen/crean/editan, Dirección lee; sin adjuntos | Decidir si llevarán adjuntos (`documento_id`) y quién los autoriza. |
| 8 | **Ficha médica del personal de 7 secciones** | Ficha médica general implementada en el apartado *Personal* (UTP + Dirección) | Entregar los campos por sección de la ficha oficial; al recibirla se implementa. |
| 9 | **Ley 21.719** (rige el 15-10-2027, Ley 21.855; hasta entonces Ley 19.628) | Revisión en `docs/revision-ley-21719.md` (7 brechas detectadas) | Aviso de privacidad, derechos de acceso/rectificación y plazos formales de conservación. |
| 10 | **Formato del certificado de alumno regular** | Folio, código de validación pública y 30 días; firmante subrogante con cargo «Director» | Validación institucional del formato (texto y plazo). |
