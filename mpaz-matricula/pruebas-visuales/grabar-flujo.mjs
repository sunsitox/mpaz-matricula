// Recorrido completo e interactivo del Portal de Matrícula MPAZ.
//
// Al abrirse el navegador el portal queda en pantalla y la grabación ESPERA a
// que usted pulse ENTER en la consola para comenzar. Si se ejecuta sin
// terminal interactiva, arranca solo tras 90 segundos.
//
// El recorrido está dividido por módulo en flujos/ (uno por módulo del plan de
// pruebas). Este archivo solo orquesta: servidor, navegador, video y resumen.
//
//   flujos/comun.mjs             configuración, sesiones y utilidades
//   flujos/01-acceso.mjs         login, panel, actividad, denegación
//   flujos/02-ficha.mjs          asistente de matrícula (8 pasos)
//   flujos/03-estudiantes.mjs    listado, ficha, documentos, firma, validación
//   flujos/04-medicacion.mjs     panel TENS y protocolo pendiente (MAT-05)
//   flujos/05-alertas-reportes.mjs  alertas, reportes y CSV
//   flujos/06-fichas-medicas.mjs ficha de salud del personal
//   flujos/07-funcionarios.mjs   alta, clave temporal, estado y roles
//   flujos/08-certificados.mjs   emisión, historial y verificación pública
//   flujos/09-configuracion.mjs  año escolar de matrícula
//   flujos/10-cierre.mjs         cierre de sesión y plano final
//
// Uso:
//   node grabar-flujo.mjs                   navegador visible
//   node grabar-flujo.mjs --headless        sin ventana
//   node grabar-flujo.mjs --levantar-servidor
//
// Variables opcionales: BASE_URL, USUARIO, USUARIO_DIRECCION, USUARIO_ADMIN,
// USUARIO_DOCENTE, CLAVE, CLAVE_NUEVA, SLOWMO, MIN_VIDEO_MS, SOLO (prefijos de
// casos separados por coma), CERT_CODIGO (para validar un certificado concreto).

import { chromium } from 'playwright';
import fs from 'node:fs';

import {
  ALTO,
  ANCHO,
  BASE,
  HEADLESS,
  SALIDA_VIDEO,
  SALIDA_VIDEO_DIR,
  SLOWMO,
  ajustarAlAncho,
  asegurarServidor,
  detenerServidor,
  esperarInicioManual,
  estado,
  guardarLogServidor,
  pag,
  pausa,
} from './flujos/comun.mjs';

import * as acceso from './flujos/01-acceso.mjs';
import * as ficha from './flujos/02-ficha.mjs';
import * as estudiantes from './flujos/03-estudiantes.mjs';
import * as medicacion from './flujos/04-medicacion.mjs';
import * as alertas from './flujos/05-alertas-reportes.mjs';
import * as fichasMedicas from './flujos/06-fichas-medicas.mjs';
import * as funcionarios from './flujos/07-funcionarios.mjs';
import * as certificados from './flujos/08-certificados.mjs';
import * as configuracion from './flujos/09-configuracion.mjs';
import * as cierre from './flujos/10-cierre.mjs';

const MODULOS = [
  acceso,
  ficha,
  estudiantes,
  medicacion,
  alertas,
  fichasMedicas,
  funcionarios,
  certificados,
  configuracion,
  cierre,
];

function separador(texto) {
  console.log('');
  console.log('--------------------------------------------------------------');
  console.log(texto);
  console.log('--------------------------------------------------------------');
}

try {
  console.log(`Grabación del recorrido · ${BASE}`);
  await asegurarServidor();

  estado.navegador = await chromium.launch({ headless: HEADLESS, slowMo: SLOWMO });
  estado.contexto = await estado.navegador.newContext({
    viewport: { width: ANCHO, height: ALTO },
    ignoreHTTPSErrors: true,
    recordVideo: { dir: SALIDA_VIDEO_DIR, size: { width: ANCHO, height: ALTO } },
  });
  estado.page = await estado.contexto.newPage();
  const page = pag();

  // Los formularios usan confirm() para acciones destructivas: se aceptan
  // para que la demostración siga su curso.
  page.on('dialog', dialogo => dialogo.accept().catch(() => {}));

  // Sin barras de scroll visibles: ocupaban un borde gris ancho y achicaban
  // la pantalla. El desplazamiento por código sigue funcionando.
  await page.addStyleTag({ content: [
    'html, body { scrollbar-width: none !important; }',
    '*, *::before, *::after { scrollbar-width: none !important; }',
    '*::-webkit-scrollbar { width: 0 !important; height: 0 !important; display: none !important; }',
  ].join('\n') }).catch(() => {});
  // Cada vez que se carga una pantalla, se ajusta al ancho exacto.
  page.on('framenavigated', async () => { await pausa(300); await ajustarAlAncho(); });

  // El portal queda abierto en pantalla y la grabación espera a que usted
  // pulse ENTER para comenzar (el video ya se está registrando).
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
  const esLogin = /Cuenta\/Login/i.test(page.url());
  await page.getByRole('heading', { name: esLogin ? /iniciar sesión/i : /panel de matrícula/i })
    .waitFor({ timeout: 15000 })
    .catch(() => {});
  await esperarInicioManual();

  for (const modulo of MODULOS) {
    estado.modulo = modulo.titulo;
    const antes = estado.resultados.length;
    separador(modulo.titulo);
    try {
      const detalle = await modulo.ejecutar();
      const propios = estado.resultados.slice(antes);
      const ok = propios.filter(r => r.estado === 'OK').length;
      const omitidos = propios.filter(r => r.estado === 'OMITIDO').length;
      console.log(`[módulo] ${ok} OK · ${omitidos} omitidos${detalle ? ` — ${detalle}` : ''}`);
    } catch (error) {
      console.error(`[módulo] detenido: ${error?.message ?? error}`);
      throw error;
    }
  }
} catch (error) {
  estado.resultados.push({ caso: 'error-general', estado: 'FALLO', detalle: String(error?.message ?? error).slice(0, 500), modulo: estado.modulo });
  console.error(error);
} finally {
  let videoOrigen = null;
  try {
    videoOrigen = await estado.page?.video()?.path().catch(() => null);
  } catch {
    videoOrigen = null;
  }

  try {
    await estado.contexto?.close();
  } catch {
    // El contexto ya puede estar cerrado.
  }

  try {
    if (videoOrigen && fs.existsSync(videoOrigen)) {
      fs.copyFileSync(videoOrigen, SALIDA_VIDEO);
      console.log(`Video: ${SALIDA_VIDEO}`);
    } else {
      console.log(`Video no encontrado en ${SALIDA_VIDEO_DIR}`);
    }
  } catch (error) {
    console.log(`No se pudo copiar el video: ${error.message}`);
  }

  try {
    await estado.navegador?.close();
  } catch {
    // El navegador ya puede estar cerrado.
  }

  detenerServidor();
  guardarLogServidor();

  const segundos = Math.round((Date.now() - estado.inicio) / 1000);
  console.log(`Duración de la corrida: ${segundos}s`);

  const porModulo = new Map();
  for (const registro of estado.resultados) {
    const clave = registro.modulo || 'general';
    const fila = porModulo.get(clave) ?? { ok: 0, omitidos: 0, fallos: 0 };
    if (registro.estado === 'OK') fila.ok += 1;
    else if (registro.estado === 'OMITIDO') fila.omitidos += 1;
    else fila.fallos += 1;
    porModulo.set(clave, fila);
  }

  console.log('');
  console.log('Checklist del recorrido');
  for (const [nombre, fila] of porModulo) {
    const marca = fila.fallos > 0 ? '[XX]' : fila.omitidos > 0 ? '[--]' : '[OK]';
    console.log(`  ${marca} ${nombre.padEnd(42, '.')} ${fila.ok} OK · ${fila.omitidos} omitidos · ${fila.fallos} fallos`);
  }

  const ok = estado.resultados.filter(r => r.estado === 'OK').length;
  const omitidos = estado.resultados.filter(r => r.estado === 'OMITIDO');
  const fallos = estado.resultados.filter(r => r.estado === 'FALLO').length;
  console.log('');
  for (const registro of omitidos) {
    console.log(`  [--] ${registro.caso} — ${registro.detalle}`);
  }
  console.log(`Resumen: ${ok} OK · ${omitidos.length} omitidos · ${fallos} fallidos`);
  process.exitCode = fallos > 0 ? 1 : 0;
}
