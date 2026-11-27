// Módulo 04 · Medicación (TENS).
// El registro de dosis está deshabilitado mientras el colegio no apruebe el
// protocolo TENS (MAT-05): la pantalla existe, explica la base legal y el
// sistema rechaza el intento. Además muestra la vista limitada del docente.

import {
  DOCENTE,
  TENS,
  abrirFichaPorRun,
  abrirPestana,
  asegurarSesion,
  caso,
  estado,
  mirar,
  pag,
  pausa,
  recorrerPagina,
} from './comun.mjs';

export const titulo = '04 · Medicación (TENS)';

function run() {
  if (!estado.runNuevo) throw new Error('falta la matrícula del módulo 02');
  return estado.runNuevo;
}

export async function ejecutar() {
  await caso('01-panel-medicacion', async () => {
    await asegurarSesion(TENS);
    await abrirFichaPorRun(run());
    await abrirPestana('salud');
    const panel = pag().locator('#panel-medicacion').first();
    await panel.waitFor({ timeout: 10000 });
    await panel.scrollIntoViewIfNeeded();
    await pausa(1500);
    const texto = await panel.innerText();
    await recorrerPagina(4);
    await mirar(2);
    if (/sin permiso|no tiene permiso/i.test(texto)) {
      return 'el rol TENS no ve el panel: la ficha explica la ausencia';
    }
    return 'prescripciones, órdenes y autorizaciones vigentes';
  });

  await caso('02-intento-registro-bloqueado', async () => {
    const registrar = pag().getByRole('button', { name: /registrar dosis|administrar/i }).first();
    const hayBoton = (await registrar.count()) > 0;
    if (!hayBoton) {
      return 'sin botón de registro: el protocolo TENS no está aprobado';
    }
    await registrar.click();
    await pag().waitForLoadState('domcontentloaded');
    await pausa(2500);
    const aviso = await pag().locator('.acceso__alerta, [role="alert"]').first().innerText().catch(() => '');
    return aviso ? aviso.slice(0, 160) : 'el sistema devolvió el formulario sin registrar la dosis';
  });

  await caso('03-vista-limitada-docente', async () => {
    await asegurarSesion(DOCENTE);
    await abrirFichaPorRun(run());
    for (const pestana of ['autorizaciones', 'documentos', 'salud', 'pie']) {
      await abrirPestana(pestana);
      await recorrerPagina(3);
    }
    await mirar(2);
    return 'DOCENTE sin permisos de área: los paneles explican la ausencia';
  });

  return 'medicación y control de acceso por rol';
}
