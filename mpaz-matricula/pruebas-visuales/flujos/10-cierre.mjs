// Módulo 10 · Cierre.
// Cierra la sesión, muestra la página pública de validación y vuelve al panel
// para el plano final del video.

import {
  asegurarSesion,
  caso,
  cerrarSesion,
  esperarMinimoParaVideo,
  irA,
  mirar,
  pag,
  pausa,
  recorrerPagina,
  SECRETARIA,
} from './comun.mjs';

export const titulo = '10 · Cierre del recorrido';

export async function ejecutar() {
  await caso('01-cerrar-sesion', async () => {
    await asegurarSesion(SECRETARIA);
    await cerrarSesion();
    await pausa(1200);
    return 'la cookie de sesión queda invalidada';
  });

  await caso('02-verificacion-publica', async () => {
    await irA('/Certificados/Validar', 1200);
    await pag().getByRole('heading', { name: /certificado de alumno regular/i }).waitFor({ timeout: 10000 });
    await pausa(2500);
    return 'la verificación no pide sesión: la abre cualquiera con el papel';
  });

  await caso('03-panel-final', async () => {
    await esperarMinimoParaVideo();
    await irA('/', 1200);
    await recorrerPagina();
    await mirar(3);
    return 'fin del recorrido';
  }, { critico: true });

  return 'cierre';
}
