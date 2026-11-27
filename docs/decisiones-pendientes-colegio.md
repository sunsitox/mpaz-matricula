# Decisiones pendientes del colegio · Portal de Matrícula MPAZ

**19-11-2026 · Documento de trabajo (actualizado 26-11-2026).** Contiene *sólo* lo que
queda pendiente o inconcluso. Todo lo ya resuelto —UTP y Convivencia como
perfiles separados; firmante del certificado (subrogante, cargo «Director»);
documentos obligatorios retirados del alcance (26-11-2026); conservación sin borrar
expediente ni auditoría (por ley se conservan) y poda de alertas a 24 meses (dec. 13); observaciones
de situación familiar (área `SITUACION_FAMILIAR`, dec. 19 por defecto); notas
de retiro (UTP y Convivencia escriben, Secretaría e Inspectoría leen);
Pre-kínder/Kínder sin letra— quedó aplicado en desarrollo y **no se repite
aquí**. Lo que sigue abajo es lo que falta confirmar o entregar.

**Confirmado por el colegio el 26-11-2026 y ya fuera de la lista:** la matrícula la
crea y formaliza **Secretaría** (leer, crear, editar, exportar); Dirección lee,
valida y exporta; Inspectoría lee y edita; PIE lee; UTP lee y exporta; Convivencia,
TENS y Docente leen. La autorización de paracetamol vive en la ficha de matrícula y
la responde el apoderado. El formato del certificado de alumno regular está aprobado.
Los documentos obligatorios se retiran del alcance (se definen caso a caso) y la
conservación de datos no se planifica: por ley se conservan.

| # | Qué falta | Propuesta por defecto (aplicada en desarrollo) | Pendiente del colegio |
| --- | --- | --- | --- |
| 1 | **Matriz de permisos en la base del colegio** | La matriz vigente en el repositorio (ver `docs/roles-permisos-actuales.md`) | Aprobarla y cargarla en la base del colegio, con respaldo previo. Sin ella, las pantallas responden 403 tras el login. |
| 2 | **Protocolo de medicación por TENS** (MAT-05), custodia y qué hacer si el TENS no está (dec. 3) | Medicación habilitada el 25-11-2026 por orden expresa (`Medicacion:Habilitada=true`) | Protocolo escrito aprobado (sigue pendiente). |
| 3 | **Textos legales de autorizaciones** de imagen y convivencia (dec. 4) | Texto vigente guardado como «Pendiente de aprobación (Dirección)», versión 1 | Aprobación con nombre de quien aprueba. |
| 5 | **Observaciones de situación familiar** (dec. 19, en detalle) | Área propia `SITUACION_FAMILIAR`; UTP y Convivencia leen/crean/editan, Dirección lee; sin adjuntos | Decidir si llevarán adjuntos (`documento_id`) y quién los autoriza. |
| 6 | **Ficha médica del personal de 7 secciones** | Ficha médica general implementada en el apartado *Personal* (UTP + Dirección) | Entregar los campos por sección de la ficha oficial; al recibirla se implementa. |
| 7 | **Ley 21.719** (rige el 15-10-2027, Ley 21.855; hasta entonces Ley 19.628) | Revisión en `docs/revision-ley-21719.md` (7 brechas detectadas) | Aviso de privacidad y derechos de acceso/rectificación. La conservación no se discute: por ley los datos se conservan (confirmado 26-11-2026). |
| 8 | **Membrete del certificado de alumno regular** | Folio, código de validación pública y 30 días; firmante subrogante con cargo «Director» | El formato está aprobado (26-11-2026). Falta precisar el dato identificatorio del colegio que debe aparecer y hacerlo más visible. |
