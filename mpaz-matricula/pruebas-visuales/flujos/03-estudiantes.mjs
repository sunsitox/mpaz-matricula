// Módulo 03 · Estudiantes y expediente.
// Listado y filtros, ficha por pestañas, documentos, firma, confirmación,
// validación institucional y documental, revocación de autorizaciones,
// corrección con nueva versión y anulación, sobre la matrícula del módulo 02.
// El orden sigue las reglas del portal: las autorizaciones existen una vez
// validada la matrícula y la validación documental (MAT-10) exige el folio.

import {
  DIRECCION,
  SECRETARIA,
  abrirFichaPorRun,
  abrirPestana,
  asegurarSesion,
  caso,
  cerrarAviso,
  estado,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
  subirArchivoDemo,
} from './comun.mjs';

export const titulo = '03 · Estudiantes y expediente';

function run() {
  if (!estado.runNuevo) throw new Error('falta la matrícula del módulo 02');
  return estado.runNuevo;
}

// Envía el formulario que contiene el campo indicado sin pasar por la capa de
// confirmación del navegador (conserva el token antiforgery del formulario).
async function enviarFormulario(selector) {
  await Promise.all([
    pag().waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 20000 }).catch(() => {}),
    pag().evaluate(campo => {
      const formulario = document.querySelector(campo)?.closest('form');
      if (formulario) formulario.submit();
    }, selector),
  ]);
  await pausa(1200);
}

export async function ejecutar() {
  await caso('01-listado-busqueda-y-filtros', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Alumnos', 800);
    await pag().locator('#q').fill('a');
    await pag().getByRole('button', { name: /filtrar/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    const total = await pag().locator('tbody tr').count();
    await recorrerPagina(6);
    await mirar(2);
    return `${total} filas tras buscar`;
  }, { critico: true });

  await caso('02-ficha-por-pestanas', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    for (const pestana of ['autorizaciones', 'documentos', 'salud', 'pie']) {
      await abrirPestana(pestana);
      await recorrerPagina(4);
    }
    await mirar(3);
    return 'General, Autorizaciones, Documentos, Salud y PIE';
  }, { critico: true });

  await caso('03-documentos', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    await abrirPestana('documentos');
    const subida = pag().locator('input[name="archivo"]').first();
    const haySubida = (await subida.count()) > 0;
    if (haySubida) {
      await subirArchivoDemo('input[name="archivo"]', 'documento-demo');
      await pag().waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    const noAplica = pag().getByRole('button', { name: /^no aplica$/i }).first();
    if ((await noAplica.count()) > 0) {
      await noAplica.click();
      await pag().waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    return haySubida ? 'archivo subido y requisito marcado como no aplica' : 'sin requisitos cargables';
  });

  await caso('04-registrar-firma-y-escaneo', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    const formFirma = pag().getByRole('button', { name: /registrar ficha firmada/i }).first();
    if ((await formFirma.count()) > 0) {
      await pag().locator('input[name="fecha"]').first().fill('2026-11-27');
      await formFirma.click();
      await pag().waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    await abrirPestana('documentos');
    const inputFicha = pag().locator('#ficha-firmada');
    if ((await inputFicha.count()) > 0) {
      await subirArchivoDemo('#ficha-firmada', 'ficha-firmada-demo');
      await pag().getByRole('button', { name: /subir ficha firmada/i }).click();
      await pag().waitForLoadState('domcontentloaded');
      await pausa(2000);
    }
    return 'FICHA_GENERADA → FIRMADA con escaneo asociado';
  }, { critico: true });

  await caso('05-confirmar-matricula', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    await abrirPestana('documentos');
    await pag().getByRole('button', { name: /confirmar matrícula/i }).first().click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'estado CONFIRMADA según el procedimiento';
  }, { critico: true });

  await caso('06-validar-matricula', async () => {
    await asegurarSesion(DIRECCION);
    await abrirFichaPorRun(run());
    await abrirPestana('documentos');
    await pag().getByRole('button', { name: /validar matrícula/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2500);
    return 'validación institucional con fecha y responsable';
  }, { critico: true });

  await caso('07-validar-documentacion', async () => {
    await asegurarSesion(DIRECCION);
    await abrirFichaPorRun(run());
    await abrirPestana('documentos');
    const estadoValidacion = pag().locator('#estado-validacion');
    if (!await estadoValidacion.waitFor({ timeout: 8000 }).then(() => true).catch(() => false)) {
      throw new Error('la ficha no ofrece el formulario de validación documental (falta folio o permiso)');
    }
    await cerrarAviso();
    // Se completa el formulario y se envía por DOM: la capa de confirmación de
    // ux.js intercepta el clic y deja la pantalla bloqueada en la grabación.
    await pag().locator('#ubicacion-original').fill('Caja 3 · carpeta 2027 · archivo de secretaría', { force: true });
    await pag().locator('#estado-validacion').selectOption('OBSERVADA', { force: true });
    await pag().locator('#observacion-validacion').fill('Falta el reverso del escaneo', { force: true });
    await pausa(1500);
    await enviarFormulario('#estado-validacion');
    await pausa(2000);
    await cerrarAviso();
    await pag().locator('#estado-validacion').selectOption('VALIDADA', { force: true });
    await pag().locator('#observacion-validacion').fill('', { force: true });
    await pausa(1200);
    await enviarFormulario('#estado-validacion');
    await pausa(2000);
    return 'original en papel, OBSERVADA y luego VALIDADA (MAT-10)';
  });

  await caso('08-corregir-ficha', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    await cerrarAviso();
    await pag().getByRole('button', { name: /corregir ficha/i }).first().click();
    await pag().waitForLoadState('domcontentloaded');
    // La corrección carga el snapshot congelado: se va directo al paso 8.
    await irA('/Ficha/Paso/8', 800);
    await pag().locator('.wizard__revision').waitFor({ timeout: 10000 });
    await pag().locator('#motivo-correccion').fill('Corrección mostrada en la grabación');
    await pag().locator('#Confirmo_Revision').check({ force: true });
    await pag().getByRole('button', { name: /continuar|guardar y finalizar/i }).click();
    await pag().getByRole('heading', { name: /ficha guardada/i }).waitFor({ timeout: 15000 });
    return 'nueva versión con motivo; la anterior queda REEMPLAZADA';
  });

  await caso('09-revocar-autorizacion', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(run());
    await abrirPestana('autorizaciones');
    const formulario = pag().locator('form:has(input[name="motivo"])').first();
    // La pestaña ya se dibujó al abrirla: si no hay formularios, se pasa al
    // respaldo sin esperar de más.
    let hay = (await pag().locator('form:has(input[name="motivo"])').count()) > 0;
    if (!hay) {
      // Respaldo: la primera matrícula confirmada del listado, que sí tiene
      // autorizaciones otorgadas. Se abre la lista una sola vez y se vuelve
      // con el historial en vez de recargarla por cada fila.
      await irA('/Alumnos?estado=CONFIRMADA', 900);
      const filas = pag().locator('.alumnos__ver');
      const total = Math.min(await filas.count(), 2);
      for (let intento = 0; intento < total && !hay; intento += 1) {
        await filas.nth(intento).click();
        await pag().waitForLoadState('domcontentloaded');
        await abrirPestana('autorizaciones');
        hay = (await pag().locator('form:has(input[name="motivo"])').count()) > 0;
        if (!hay) {
          await pag().goBack({ waitUntil: 'domcontentloaded' });
          await pausa(400);
        }
      }
    }
    if (!hay) throw new Error('ninguna matrícula confirmada muestra autorizaciones revocables');
    await pag().locator('form:has(input[name="motivo"]) input[name="motivo"]').first()
      .fill('Revocada en demostración grabada', { force: true });
    await pausa(1500);
    await enviarFormulario('form:has(input[name="motivo"]) input[name="motivo"]');
    await pausa(2000);
    return 'revocación con motivo, funcionario y fecha';
  });


  await caso('10-anular-matricula', async () => {
    await asegurarSesion(SECRETARIA);
    await irA('/Alumnos?estado=BORRADOR', 800);
    await pag().locator('.alumnos__ver').first().click();
    await pag().waitForLoadState('domcontentloaded');
    await abrirPestana('documentos');
    await pag().locator('#motivo-anulacion').fill('Anulada en demostración grabada');
    await pag().getByRole('button', { name: /anular matrícula/i }).click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'la anulación libera el año para una matrícula nueva';
  });

  return 'expediente completo: firma, confirmación, validación y anulación';
}
