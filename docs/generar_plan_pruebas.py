#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Plan de pruebas completo del Portal de Matrícula MPAZ.
Genera docs/plan-pruebas-portal-mpaz.xlsx (portada + 1 hoja por módulo).
"""
import os

import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

OUT = "docs/plan-pruebas-portal-mpaz.xlsx"

# Los resultados ya cargados en el Excel mandan: regenerar el plan no borra la
# ejecución, que es el dato que no se puede reconstruir desde este guion.
EXISTENTE = {}
if os.path.exists(OUT):
    try:
        _previo = openpyxl.load_workbook(OUT)
        for _hoja in _previo.worksheets:
            for _fila in range(5, _hoja.max_row + 1):
                _caso = _hoja.cell(_fila, 1).value
                _res = _hoja.cell(_fila, 9).value
                if _caso and _res:
                    EXISTENTE[_caso] = (_res, _hoja.cell(_fila, 10).value or "")
    except Exception as _e:  # un Excel dañado no debe impedir regenerar el plan
        print("Aviso: no se pudieron leer los resultados previos:", _e)

HEADER_FILL = PatternFill("solid", fgColor="1F4E79")
HEADER_FONT = Font(name="Calibri", bold=True, color="FFFFFF")
TITLE_FONT = Font(name="Calibri", bold=True, size=14, color="1F4E79")
SUB_FONT = Font(name="Calibri", bold=True, size=11, color="1F4E79")
THIN = Side(style="thin", color="B0B0B0")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
WRAP = Alignment(wrap_text=True, vertical="top")

COLUMNS = ["ID", "Módulo / Página", "Escenario", "Tipo", "Precondición / Datos",
           "Pasos", "Resultado esperado", "Prioridad", "Resultado", "Evidencia / Observaciones"]
WIDTHS = [12, 30, 32, 16, 36, 44, 44, 11, 11, 32]

def add_sheet(wb, name, title, subtitle, cases):
    ws = wb.create_sheet(name)
    ws.sheet_properties.tabColor = "1F4E79"
    ws.merge_cells("A1:J1")
    c = ws["A1"]; c.value = title; c.font = TITLE_FONT
    ws.merge_cells("A2:J2")
    c = ws["A2"]; c.value = subtitle; c.font = Font(name="Calibri", italic=True, size=10, color="404040")
    ws.merge_cells("A3:J3")
    c = ws["A3"]; c.value = ("Cómo usar: ejecute cada caso en el ambiente indicado, marque Resultado (P=pass, F=fail, B=bloqueado) "
                             "y deje evidencia (captura o log). Prioridad: Alta = bloquea publicación; Media = debe corregirse; Baja = mejora.")
    c.font = Font(name="Calibri", italic=True, size=9, color="404040"); c.alignment = Alignment(wrap_text=True, vertical="top")
    ws.row_dimensions[3].height = 28
    for i, h in enumerate(COLUMNS, 1):
        cell = ws.cell(row=4, column=i, value=h)
        cell.font = HEADER_FONT; cell.fill = HEADER_FILL; cell.alignment = Alignment(wrap_text=True, vertical="center")
        cell.border = BORDER
    ws.row_dimensions[4].height = 32
    for r, case in enumerate(cases, 5):
        fila = list(case)
        if not fila[8] and fila[0] in EXISTENTE:
            fila[8], fila[9] = EXISTENTE[fila[0]]
        for i, v in enumerate(fila, 1):
            cell = ws.cell(row=r, column=i, value=v)
            cell.font = Font(name="Calibri", size=10); cell.alignment = WRAP; cell.border = BORDER
        ws.row_dimensions[r].height = 62
    for i, w in enumerate(WIDTHS, 1):
        ws.column_dimensions[get_column_letter(i)].width = w
    ws.freeze_panes = "C5"
    ws.auto_filter.ref = f"A4:J{4 + len(cases)}"
    ws.sheet_properties.pageSetUpPr.fitToPage = True
    ws.page_setup.orientation = "landscape"; ws.page_setup.paperSize = ws.PAPERSIZE_A3
    ws.page_setup.fitToWidth = 1; ws.page_setup.fitToHeight = 0

def C(id_, mod, esc, tipo, pre, pasos, esp, pri="Alta", res="", ev=""):
    return [id_, mod, esc, tipo, pre, pasos, esp, pri, res, ev]

wb = openpyxl.Workbook()
wb.remove(wb.active)

# ---------- PORTADA ----------
ws = wb.create_sheet("PORTADA")
ws.sheet_properties.tabColor = "548235"
ws.column_dimensions["A"].width = 28
ws.column_dimensions["B"].width = 110
ws.merge_cells("A1:B1")
ws["A1"] = "PLAN DE PRUEBAS COMPLETO — Portal de Matrícula MPAZ (Escuela Escritora Marcela Paz)"
ws["A1"].font = TITLE_FONT
rows = [
 ("Fecha / versión", "27-11-2026 · Plan v1.1. Ambiente de referencia: https://matricula.empaz.cl (solo con cuentas de prueba; nunca con datos reales, Ley 19.628)."),
 ("Estado de ejecución", "75 de 89 casos ejecutados al 27-11-2026: 72 P, 3 B (ACC-08, ALR-02, TRX-09), 0 F. Cerradas las hojas 01, 02, 03, 05, 06, 07, 08, 09 y 10. Pendientes: los 11 de 04 Medicación TENS y TRX-11 a TRX-13."),
 ("Dependencias", "04 Medicación TENS: ejecutable en local con el caso de asma del mock (prescripción VIGENTE validada + autorización OTORGADA); habilitarla en producción exige el protocolo TENS escrito. TRX-09: publicar y construir la imagen Docker. El portal no tiene pantalla para crear o validar órdenes médicas: se cargan por SQL."),
 ("Objetivo", "Verificar que todas las páginas y módulos del portal cumplen su función, respetan permisos por rol/área, validan datos (RUN/IPE/IPA) y dejan auditoría, antes de cada despliegue a producción."),
 ("Alcance", "Acceso y seguridad; asistente de ficha (Pasos 1-8) y borrador; Estudiantes y expediente; Medicación TENS (MAT-05); Alertas y Reportes; Fichas médicas del personal; Funcionarios y roles; Certificados alumnos; Configuración de año escolar; Transversal (UX sin recarga, datos sensibles, respaldo)."),
 ("Fuera de alcance", "Integraciones futuras (Etapa IV), boletín de notas/asistencia, envío de correos, y cualquier validación jurídica del colegio (protocolos, avisos de privacidad). La medicación en producción exige protocolo TENS escrito aprobado."),
 ("Estrategia", "1) Pruebas automatizadas existentes: suite xUnit (213 tests) en cada commit. 2) Este plan: ejecución manual guiada por módulo en ambiente de desarrollo/producción con cuentas prueba.*@mpaz.cl. 3) Revisión de seguridad y permisos por rol (9 roles). 4) Verificación sin JavaScript (degradación elegante)."),
 ("Ambientes y datos", "Desarrollo local (127.0.0.1) y producción (matricula.empaz.cl). Datos ficticios de prueba; prohibido cargar datos reales de estudiantes en este plan. Base: Neon (rama de prueba para casos destructivos)."),
 ("Cuentas de prueba a usar", "Una por rol: SECRETARIA, DIRECCION, INSPECTORIA, EQUIPO_PIE, UTP, CONVIVENCIA, DOCENTE, TENS, ADMIN_TI (+ admin.palma@mpaz.cl). Sin estas cuentas no se puede probar seguridad: se marcan casos como Bloqueado."),
 ("Criterio de salida", "0 defectos de prioridad Alta abiertos; suites 213/213; casos P del plan ≥ 95%; sin regresiones en permisos ni datos sensibles."),
 ("Cómo registrar", "En cada hoja: Resultado P/F/B + Evidencia (captura, log o número de ticket). Los fallos se anexan a la checklist con ID del caso (ej. FICHA-03)."),
 ("Hojas del Excel", "01_AccesoSeguridad (10) · 02_AsistenteFicha (13) · 03_EstudiantesExpediente (11) · 04_MedicacionTENS (11) · 05_AlertasReportes (8) · 06_FichasMedicas (5) · 07_FuncionariosRoles (6) · 08_Certificados (7) · 09_Configuracion (5) · 10_Transversal (13). Total: 89 casos."),
]
r = 3
for k, v in rows:
    ws.cell(row=r, column=1, value=k).font = SUB_FONT
    c = ws.cell(row=r, column=2, value=v); c.alignment = WRAP; c.font = Font(name="Calibri", size=10)
    ws.row_dimensions[r].height = 44
    r += 1

# ---------- 01 Acceso ----------
add_sheet(wb, "01_AccesoSeguridad", "01 · Acceso y seguridad", "Login, salida, cambio de contraseña, sin permiso, bloqueo y expiración de sesión.", [
 C("ACC-01","Cuenta/Login","Ingreso válido de funcionario","Funcional","Cuenta vigente de prueba","Escribir correo y contraseña; Enter","Ingresa al Home con su año activo; auditoría de acceso creada","Alta"),
 C("ACC-02","Cuenta/Login","Credenciales incorrectas","Negativo","Cuenta de prueba","Ingresar con contraseña errónea 1 vez","Muestra error, no ingresa, no revela si el correo existe","Alta"),
 C("ACC-03","Cuenta/Login","Bloqueo tras intentos fallidos","Seguridad","Cuenta de prueba","Fallar el login 5 veces seguidas","Cuenta bloqueada 15 minutos; mensaje sin datos sensibles","Alta"),
 C("ACC-04","Cuenta/Salida","Cerrar sesión","Funcional","Sesión iniciada","Usar Salir; intentar volver con atrás del navegador","Sesión cerrada; las rutas protegidas redirigen a Login","Alta"),
 C("ACC-05","Cuenta/CambiarContrasena","Cambio obligatorio en primer ingreso","Funcional","Cuenta con contraseña inicial","Ingresar; cambiar con clave nueva válida","Permite continuar solo tras cambiar; la clave anterior deja de funcionar","Alta"),
 C("ACC-06","Cuenta/CambiarContrasena","Claves débiles o reutilización","Negativo","Cuenta en cambio obligatorio","Intentar clave corta, sin mayúscula/número y reutilizar la inicial","Rechaza con mensajes por cada regra (≥10, mayúscula, minúscula, número)","Media"),
 C("ACC-07","Cuenta/SinPermiso","Acceso a módulo sin permiso","Seguridad","Cuenta de rol sin el permiso (ej. DOCENTE → Medicación)","Escribir la URL directamente","Redirige a SinPermiso / 403; no expone datos del bloque","Alta"),
 C("ACC-08","Sesión","Expiración e inactividad","Seguridad","Sesión iniciada","Esperar al tiempo configurado y actuar","Pide ingresar de nuevo; no conserva datos sensibles en memoria","Media"),
 C("ACC-09","Acceso (sin JS)","Página sin JavaScript","Accesibilidad/UX","Navegador sin JS","Cargar Login y un módulo protegido","El login funciona; los bloques protegidos explican la ausencia de permiso","Media"),
 C("ACC-10","Home/Actividad","Panel inicial según rol","Funcional","Usuarios por rol","Ingresar con cada rol y revisar Home/Actividad","Cada rol ve solo sus indicadores y enlaces permitidos","Media"),
])

# ---------- 02 Asistente ----------
add_sheet(wb, "02_AsistenteFicha", "02 · Asistente de ficha de matrícula (Pasos 1-8)", "Flujo completo de captura, validaciones RUN/IPE/IPA, borrador, PDF, corrección y renovación.", [
 C("FICHA-01","Ficha/Buscar","Buscar estudiante por RUN/IPE","Funcional","Estudiantes de prueba existentes","Buscar por RUN con puntos/guion y por IPE","Muestra el resultado correcto; formato legible sin alterar el dato","Alta"),
 C("FICHA-02","Ficha/Paso 1-2","Datos estudiante y curso obligatorios","Negativo","Borrador nuevo","Avanzar sin datos obligatorios","Bloquea con mensaje por campo; no crea ficha parcial","Alta"),
 C("FICHA-03","Ficha/Paso 2","RUN estudiante con dígito verificador inválido","Negativo","Borrador nuevo","Ingresar RUN con DV incorrecto","Rechaza y explica; el registro queda en borrador","Alta"),
 C("FICHA-04","Ficha/Paso 2","IPE en estudiante y RUN/IPA en apoderado; rechazo de IPA en estudiante e IPE en adulto","Negativo","Borrador nuevo","Combinaciones IPE/IPA en cada rol","Acepta solo RUN/IPE en estudiantes y RUN/IPA en adultos; rechaza el resto","Alta"),
 C("FICHA-05","Ficha/Paso 3-5","Apoderados, contactos de emergencia y retiros","Funcional","Borrador con apoderados","Completar los 3 bloques y avanzar","Guarda y recuerda al volver atrás; sin pérdida entre pasos","Alta"),
 C("FICHA-06","Ficha/Paso 6","Antecedentes médicos, alergias y PIE (bloques condicionales)","Funcional","Borrador","Responder No en situación médica y PIE; revisar que el detalle se oculta","El detalle no se imprime ni se exige cuando la respuesta es No","Media"),
 C("FICHA-07","Ficha/Paso 6","Texto de autorización de paracetamol","Funcional","Borrador Paso 6","Marcar Sí/No y revisar el texto impreso","El texto exige indicación médica + protocolo TENS aprobado + supervigilancia; no es orden médica","Alta"),
 C("FICHA-08","Ficha/Guardar","Guardar y confirmar ficha (PDF + folio)","Funcional","Asistente completo","Confirmar y descargar","Genera PDF fiel con folio y versión; queda FICHA_GENERADA","Alta"),
 C("FICHA-09","Ficha/Corregir","Corrección de ficha generada","Funcional","Ficha VIGENTE","Corregir con motivo y cerrar","Crea versión nueva; la anterior queda Reemplazada, no se sobrescribe; auditoría completa","Alta"),
 C("FICHA-10","Ficha/Renovar","Renovación al año siguiente con datos precargados","Funcional","Estudiante con matrícula anterior","Renovar en año nuevo","Precarga datos sin duplicar estudiante; el RUN/IPE se mantiene","Alta"),
 C("FICHA-11","Ficha/Egresado","Egresado de 8º básico","Funcional","Alumno en último nivel sin renovación","Intentar nueva matrícula","Flujo de ConfirmarEgreso correcto; no permite matrícula nueva indebida","Media"),
 C("FICHA-12","Ficha (sin JS)","Asistente sin JavaScript","Accesibilidad/UX","Sin JS","Recorrer los pasos","Todo el flujo funciona sin JS (mejora progresiva)","Media"),
 C("FICHA-13","Ficha/Comprobante","Comprobante de envío / FormularioAlumno","Funcional","Matrícula con ficha","Descargar comprobante y formulario","PDF generados y descargables; folio consistente con la base","Media"),
])

# ---------- 03 Estudiantes ----------
add_sheet(wb, "03_Estudiantes", "03 · Estudiantes y expediente", "Listado, detalle por pestañas, matrícula (confirmar/validar/anular), documentos, retiros y autorizaciones.", [
 C("EST-01","Alumnos/Index","Búsqueda y filtros (q, curso, estado, pendientes)","Funcional","Padrón de prueba con variedad","Filtrar por cada criterio y combinación","Resultados correctos; paginación 25/50/100; búsqueda audita (hash, no el RUN)","Alta"),
 C("EST-02","Alumnos/Index","Tablas sin recarga (refresco parcial)","UX","Padrón con datos","Filtrar y paginar con JS activado","La tabla se actualiza sin recargar la página; sin cambio de scroll; funciona también sin JS","Alta"),
 C("EST-03","Alumnos/Detalle","Pestañas por permiso (General/Autorizaciones/Documentos/Salud/PIE)","Seguridad","Roles distintos","Abrir detalle con cada rol","Cada pestaña muestra solo lo permitido; sin permiso explica la ausencia (S-02)","Alta"),
 C("EST-04","Alumnos/Detalle","Confirmar matrícula","Funcional","Matrícula FIRMADA con ficha","Confirmar","Pasa a CONFIRMADA con responsable y fecha; auditoría","Alta"),
 C("EST-05","Alumnos/Detalle","Anular con motivo y reabrir nueva","Funcional","Matrícula en estado anulable","Anular con motivo; crear nueva","Anulación exige motivo; la anulada libera el año (MAT-04)","Alta"),
 C("EST-06","Alumnos/Detalle","Validación institucional de Dirección","Funcional","Matrícula FIRMADA/CONFIRMADA sin validar","Validar como Dirección y como otro rol","Solo Dirección valida; queda fecha y responsable; otro rol no puede","Alta"),
 C("EST-07","Alumnos/Documentos","Subir/reemplazar requisito documental (PDF/JPG/PNG, 15 MB)","Funcional/Negativo","Matrícula con requisitos","Subir válido; luego reemplazado; luego archivo >15 MB y tipo inválido","Acepta y reemplaza sin borrar (inactivo); rechaza tipo/tamaño con mensaje. La lista de documentos obligatorios quedó fuera de alcance (definición 26-11): se prueba la carga, no la exigibilidad","Alta"),
 C("EST-08","Alumnos/Documentos","Validación documental y ubicación del original","Funcional","Ficha VIGENTE","Validar y registrar ubicación","Guarda estado (Pendiente/Validada/Observada) y observación; Validada exige firma+escaneo","Media"),
 C("EST-09","Alumnos/Retiros","Personas autorizadas a retirar y notas de retiro","Funcional","Estudiante con retiros y notas","Agregar nota de cada tipo (orden judicial, autorizado, no autorizado)","Notas visibles con vigencia; permisos: UTP/Convivencia escriben, resto lee","Alta"),
 C("EST-10","Alumnos/Autorizaciones","Autorizaciones: estado, vigencia y revocación","Funcional","Autorizaciones con vencimientos","Revocar una autorizada con motivo","La revocación bloquea la difusión desde ese momento; conserva firma y fecha originales","Alta"),
 C("EST-11","Alumnos/Detalle","Datos personales visibles solo con permiso de área","Seguridad","Rol sin FAMILIA/SALUD/PIE","Abrir detalle","No se consultan ni se muestran esos bloques; la auditoría solo lista áreas efectivamente leídas","Alta"),
])

# ---------- 04 TENS ----------
add_sheet(wb, "04_MedicacionTENS", "04 · Medicación TENS (MAT-05)", "Prescripciones, registro de dosis, incidencias y seguridad clínica (Código Sanitario, DS 90). El portal no crea ni valida órdenes médicas: se cargan por SQL. El mock trae el caso de asma (prescripción VIGENTE validada + autorización OTORGADA) para probar el registro.", [
 C("TENS-01","Medicación","Panel visible solo con área MEDICACION y flag habilitado","Seguridad","TENS con MEDICACION:LEER/CREAR vs. roles sin el área; Medicacion:Habilitada=true","Abrir Detalle con cada rol","Solo MEDICACION:LEER/CREAR ve el panel; el resto ve explicación de ausencia","Alta"),
 C("TENS-02","Medicación","Sin prescripciones VIGENTES no hay formulario de dosis","Funcional","Estudiante sin prescripciones VIGENTES (el del caso de asma sí tiene una: usar otro alumno)","Abrir panel de dosis","No se muestra el formulario (regla 679554d); mensaje de protocolo TENS aprobado","Alta"),
 C("TENS-03","Medicación","Registrar dosis con orden VIGENTE + autorización otorgada","Funcional","Prescripción VIGENTE validada + autorización MEDICACION OTORGADA (caso de asma) + TENS activo + flag habilitado","Registrar dosis con fecha/hora e incidencias","Guarda fecha, hora, dosis, TENS e incidencias; solo-inserción (no se edita ni borra)","Alta"),
 C("TENS-04","Medicación","Registro sin orden VIGENTE (PENDIENTE/VENCIDA/SUSPENDIDA)","Negativo","Cambiar el estado de la prescripción por SQL (no hay pantalla que lo haga)","Intentar registrar dosis","El trigger lo rechaza; mensaje claro; no crea registro","Alta"),
 C("TENS-05","Medicación","Registro sin autorización otorgada","Negativo","Orden VIGENTE cuya autorización se revocó (desde Autorizaciones o por SQL)","Intentar registrar dosis","El trigger lo rechaza; no crea registro","Alta"),
 C("TENS-06","Medicación","Registro por funcionario sin rol TENS activo","Negativo","Usuario con MEDICACION:CREAR y rol TENS cerrado (Hasta = ayer)","Intentar registrar dosis","El trigger rechaza: 'Solo un funcionario con rol TENS activo...'","Alta"),
 C("TENS-07","Medicación","Flag Medicacion:Habilitada en false","Seguridad","Flag apagado (configuración)","Intentar registrar dosis por POST directo","Bloqueado en servidor (no solo en vista); mensaje de falta de protocolo","Alta"),
 C("TENS-08","Medicación","Auditoría del registro de dosis","Seguridad","Dosis registrada en TENS-03","Revisar registros_acceso e historial_cambios","Evento CREAR area=MEDICACION con responsable, estudiante y prescripción","Media"),
 C("TENS-09","Medicación","Texto de paracetamol en ficha (no habilita sin orden)","Legal","Ficha impresa Paso 6","Revisar texto y aprobarlo","Exige indicación médica + protocolo aprobado + supervigilancia; no es orden médica","Alta"),
 C("TENS-10","Medicación","Acceso a diagnósticos: Docentes/Inspectoría no los ven","Seguridad","DOCENTE e INSPECTORIA sobre el alumno del caso de asma","Abrir Detalle de estudiante con prescripción","No ven órdenes ni dosis; el TENS sí ve contactos/salud pertinente","Media"),
 C("TENS-11","Medicación","Vencimiento de prescripción/autorización (aviso 30 días)","Funcional","Prescripción con vigente_hasta en ≤30 días (ajustar por SQL)","Revisar Alertas","Avisa al TENS 30 días antes; la dosis se bloquea al vencer","Media"),
])

# ---------- 05 Alertas/Reportes ----------
add_sheet(wb, "05_AlertasReportes", "05 · Alertas y Reportes", "Alertas de revisión, reporte de matrículas, faltantes, exportación CSV y tablas sin recarga.", [
 C("ALR-01","Alertas/Index","Listado de alertas por año","Funcional","Alertas de prueba","Abrir y cambiar de año","Muestra solo las del año; sin recarga al paginar","Media"),
 C("ALR-02","Alertas/Revisión","La revisión de alertas es automática (MAT-17)","Funcional","Servicio AlertasProgramadas activo (ciclo cada 15 min)","Dejar correr el ciclo y revisar alerta_revision","Escribe una fila de resumen en alerta_revision con fecha y conteos; la alerta sale del listado activo. No existe acción manual de 'marcar revisada'","Media","B","El caso original pedía marcar la alerta a mano: esa acción no existe. La revisión es automática (AlertasProgramadas, cada 15 min, decisión MAT-17) y escribe en alerta_revision; verificado en código, no se ejecutó el ciclo en la corrida local. Caso reescrito el 27-11."),
 C("ALR-03","Reportes/Matrículas","Reporte de matrículas por año","Funcional","Matrículas de prueba","Generar reporte","Datos consistentes con el padrón; filtros por curso/estado","Alta"),
 C("ALR-04","Reportes/Matrículas","Exportar CSV de matrículas","Funcional","Reporte generado","Descargar CSV","Archivo con encabezados, codificación correcta y mismo contenido que la vista","Media"),
 C("ALR-05","Reportes/Faltantes","Reporte de faltantes (sin ficha/pendientes)","Funcional","Matrículas pendientes","Generar reporte","Coincide con el filtro 'pendientes' de Estudiantes","Media"),
 C("ALR-06","Reportes/Faltantes","Exportar CSV de faltantes","Funcional","Reporte de faltantes","Descargar CSV","Contenido igual a la vista; sin datos de roles no autorizados","Media"),
 C("ALR-07","Reportes (permisos)","Reportes con rol sin permiso","Seguridad","Rol sin permiso de reportes","Acceder por URL directa","403/SinPermiso; no descarga CSV","Alta"),
 C("ALR-08","Reportes (UX)","Filtro sin recarga en reportes","UX","Reportes con datos","Filtrar y exportar","El filtro actualiza la tabla sin recarga; el CSV usa el filtro aplicado","Media"),
])

# ---------- 06 Fichas médicas ----------
add_sheet(wb, "06_FichasMedicas", "06 · Fichas médicas del personal", "Ficha de salud del funcionario (7 secciones), CRUD, permisos UTP/Dirección y confidencialidad.", [
 C("FM-01","FichasMedicas/Index","Listado con búsqueda por funcionario","Funcional","Fichas de prueba","Buscar y paginar","Resultados correctos; sin recarga al filtrar","Media"),
 C("FM-02","FichasMedicas/Nuevo","Crear ficha con las 7 secciones","Funcional","Funcionario de prueba","Crear y guardar","Guarda completa; validación por campo","Alta"),
 C("FM-03","FichasMedicas/Editar","Editar y ver ficha","Funcional","Ficha existente","Editar un dato y ver","Actualiza sin duplicar; cambios auditados","Media"),
 C("FM-04","FichasMedicas (permisos)","Apertura solo para UTP y Dirección","Seguridad","Roles distintos","Acceder por URL directa con cada rol","Solo UTP/Dirección ven o editan; el resto recibe 403/SinPermiso","Alta"),
 C("FM-05","FichasMedicas (datos)","Confidencialidad de datos sensibles del personal","Seguridad","Ficha con datos de salud laboral","Revisar Index y Detalle","No se exponen a roles sin acceso; archivos fuera de wwwroot","Alta"),
])

# ---------- 07 Funcionarios ----------
add_sheet(wb, "07_FuncionariosRoles", "07 · Funcionarios y roles", "ABM de funcionarios, asignación de roles (9 roles), permisos por área/acción y bloqueo de cuentas.", [
 C("FUN-01","Funcionarios/Index","Listado y búsqueda de funcionarios","Funcional","Funcionarios de prueba","Buscar y ordenar","Resultados correctos y auditados","Media"),
 C("FUN-02","Funcionarios/Crear","Crear funcionario con rol y cuenta","Funcional","Sin funcionario duplicado","Crear con correo institucional","Cuenta creada con correo único; rol asignado; auditoría","Alta"),
 C("FUN-03","Funcionarios/Crear","Correo duplicado o formato inválido","Negativo","Correo ya existente","Intentar crear duplicado","Rechaza sin duplicar; mensaje claro","Media"),
 C("FUN-04","Funcionarios/Editar","Cambiar roles y datos; desactivar cuenta","Funcional","Funcionario activo","Cambiar rol y desactivar","Los cambios rigen al próximo ingreso; historial conservado","Alta"),
 C("FUN-05","Funcionarios (roles)","Matriz de permisos: los 9 roles por área/acción","Seguridad","Los 9 roles verificados en base","Probar una acción representativa por rol/área","Cada rol puede solo lo de la matriz (ej. ADMIN_TI sin alumnos; TENS sin VALIDAR/EDITAR/EXPORTAR)","Alta"),
 C("FUN-06","Funcionarios (seguridad)","Bloqueo, contraseña inicial y SSO","Seguridad","Cuenta de prueba","Revisar bloqueo 15 min y flujo de contraseña inicial","Bloqueo y cambio obligatorio funcionan (ver ACC-03/05)","Media"),
])

# ---------- 08 Certificados ----------
add_sheet(wb, "08_Certificados", "08 · Certificados de alumno regular", "Emisión con folio/código/QR, firmante, vigencia y validación pública de certificados.", [
 C("CER-01","Certificados/Index","Listado de certificados emitidos","Funcional","Certificados de prueba","Listar y buscar por folio","Muestra folio, código, alumno y estado","Media"),
 C("CER-02","Certificados/Emitir","Emitir certificado de alumno regular","Funcional","Matrícula FIRMADA o CONFIRMADA/validada","Emitir con permiso DOCUMENTOS","Genera PDF ministerial con RBD, folio, QR y firmante por cargo; minimiza datos (RUN identifica)","Alta"),
 C("CER-03","Certificados/Emitir","Matrícula no habilitada para certificado","Negativo","Matrícula en borrador/anulada o sin validar","Intentar emitir","No emite; explica el requisito faltante","Alta"),
 C("CER-04","Certificados/Emitir","Quién genera y quién firma","Seguridad","Secretaría (PERMISO CREAR/EDITAR) vs. Dirección","Emitir como cada rol","Genera Secretaría; firma Dirección institucional (cargo, subrogante)","Alta"),
 C("CER-05","Certificados/Validar-Verificar","Validación pública por folio+código y por código (QR)","Funcional","Certificado emitido","Validar con datos correctos e incorrectos","Con datos correctos confirma vigencia; con incorrectos no revela datos del alumno","Alta"),
 C("CER-06","Certificados","Vigencia impresa de 30 días","Funcional","Certificado emitido","Revisar PDF y validar vencido","El PDF declara vigencia; vencido se informa como tal","Media"),
 C("CER-07","Certificados/Auditoría","La emisión deja registro EMITIR con el folio","Seguridad","Matrícula FIRMADA + Secretaría; CHECK de registros_acceso con EMITIR (paso 28)","Emitir el certificado y consultar registros_acceso por recurso certificado_emitido","Fila accion=EMITIR area=DOCUMENTOS recurso=certificado_emitido con recurso_id, estudiante, funcionario y folio en detalle; sin 'No se pudo registrar el acceso' en el log","Alta","P","Defecto corregido el 26-11: el CHECK no admitía EMITIR y la emisión quedaba sin rastro. Con EMITIR en los 3 DDL y el paso 28, la emisión deja accion=EMITIR area=DOCUMENTOS recurso=certificado_emitido recurso_id=1 estudiante=5 funcionario=1 detalle={folio CAR-2027-000001}; 0 errores en el log."),
])

# ---------- 09 Configuración ----------
add_sheet(wb, "09_Configuracion", "09 · Configuración de año escolar", "Alta, edición, activación y eliminación de años; activo único; efecto en todo el portal.", [
 C("CFG-01","Configuracion","Agregar año escolar con apertura/cierre","Funcional","Administrador del portal","Agregar año nuevo","Queda creado con fechas; no se activa solo","Media"),
 C("CFG-02","Configuracion","Activar un año","Funcional","Dos años creados","Activar uno","Solo uno activo; el resto del portal usa ese año; auditoría","Alta"),
 C("CFG-03","Configuracion","Editar año no activo","Funcional","Año no activo","Editar fechas","Guarda; el año activo no cambia","Media"),
 C("CFG-04","Configuracion","Eliminar año con matrículas","Negativo","Año con matrículas","Intentar eliminar","Rechaza o exige resolver las matrículas; nunca borra en silencio","Alta"),
 C("CFG-05","Configuracion (permisos)","Acceso solo a rol autorizado","Seguridad","Rol sin permiso de configuración","Acceder por URL directa","403/SinPermiso","Alta"),
])

# ---------- 10 Transversal ----------
add_sheet(wb, "10_Transversal", "10 · Transversal (UX, datos y operación)", "Tablas sin recarga, validación de identidad, auditoría, respaldo, accesibilidad y despliegue.", [
 C("TRX-01","UX/Tablas","Refresco parcial en Estudiantes, Alertas y Reportes","UX","JS activado","Filtrar, ordenar y paginar cada tabla","La región se actualiza sin recarga; sin JS todo sigue funcionando (navegación normal)","Alta"),
 C("TRX-02","UX/Tablas","Estado ocupado y carga","UX","Tabla con datos","Filtrar con red lenta/red completa","Indicador [aria-busy] visible; sin saltos de layout","Baja"),
 C("TRX-03","Datos/Identidad","RUN/IPE alumno y RUN/IPA apoderado en todo el portal","Funcional","Datos mixtos RUN/IPE","Crear, buscar y listar con cada tipo","Regla consistente: alumnos RUN o IPE; adultos RUN o IPA; el IPE sin DV no se formatea como RUN","Alta"),
 C("TRX-04","Datos/Sensibles","Datos de salud/PIE/judicial no se filtran por rol","Seguridad","Roles sin áreas sensibles","Abrir listados y detalles","Sin permisos ni se consultan; auditoría registra áreas leídas (MAT-13)","Alta"),
 C("TRX-05","Auditoría","Lecturas y cambios con responsable y fecha","Seguridad","Sesión activa","Hacer una lectura y un cambio, revisar auditoría","Ambos eventos registrados; los documentos anteriores quedan inactivos, no se borran","Alta"),
 C("TRX-06","Datos/Retención","Conservación: expediente y auditoría no se eliminan","Seguridad","Reporte de retención","Revisar reglas de poda","El expediente, la ficha y la auditoría se conservan (por ley no se eliminan, definición 26-11); solo se poda el resumen técnico alerta_revision a 24 meses","Media"),
 C("TRX-07","Accesibilidad","Teclado, foco y lectores en tablas y formularios","Accesibilidad","Navegación con teclado","Recorrer tablas y formularios solo con teclado","Foco visible; etiquetas y roles correctos; errores anunciados","Media"),
 C("TRX-08","Operación","Respaldo y restauración probada","Operación","Respaldo Neon","Respaldar y restaurar en ambiente de prueba","Restauración documentada y exitosa","Alta"),
 C("TRX-09","Operación","Despliegue: build, tests, publicar y verificar","Operación","Release preparada","Build + tests 213/213 + publicar + HTTP 302 + Docker/portal OK","Publicación verificada y portal respondiendo; sin regresiones","Alta"),
 C("TRX-10","Datos/borrador","Borradores no filtran ni se exponen sin permiso","Seguridad","Borrador de otro funcionario","Intentar acceder por URL","No expone datos; solo el responsable y roles autorizados","Media"),
 C("TRX-11","Seguridad/CSRF","POST sin token antiforgery","Seguridad","Sesión válida","Enviar un POST a /Certificados/Emitir o /Alumnos/SubirDocumento sin __RequestVerificationToken","Responde 400 y no ejecuta la acción; con el token tomado de una página GET sí funciona","Alta"),
 C("TRX-12","Privacidad/Caché","Páginas autenticadas sin caché","Seguridad","Sesión iniciada","Pedir /Alumnos y el detalle de un alumno; revisar cabeceras","Cache-Control: no-store, no-cache, must-revalidate y Pragma: no-cache; el login sin sesión no las lleva","Media"),
 C("TRX-13","Documentos/PDF","Entrega de PDF (ficha, certificado, ficha médica)","Funcional","Documentos generados","Descargar cada PDF y revisar cabeceras y contenido","application/pdf con nombre de archivo y tamaño razonable; abre sin errores y no expone rutas internas ni datos de otros alumnos","Media"),
])

wb.save(OUT)
print("OK", OUT, "hojas:", len(wb.sheetnames))
