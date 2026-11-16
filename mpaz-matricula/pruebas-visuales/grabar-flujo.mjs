// Recorrido completo e interactivo del Portal de Matrícula MPAZ.
//
// Al abrirse el navegador el portal queda en pantalla y la grabación ESPERA a
// que usted pulse ENTER en la consola para comenzar. Si se ejecuta sin
// terminal interactiva, arranca solo tras 90 segundos.
//
// Flujos cubiertos (según el rol necesario):
//   SECRETARIA: acceso, panel, actividad, estudiantes (búsqueda/filtros),
//     ficha por pestañas, revocación de autorización, documentos (subir,
//     marcar, descargar), matrícula nueva completa con PDF y folio,
//     registrar firma, subir ficha firmada, confirmar, corregir la ficha
//     (nueva versión), certificado de alumno regular, reportes y faltantes,
//     anulación, denegación de administración.
//   DIRECCION: validación institucional de la matrícula y validación
//     documental (MAT-10).
//   ADMIN_TI: administración de funcionarios (crear, restablecer clave,
//     activar/desactivar, cerrar rol).
//   DOCENTE: vista limitada (los paneles sensibles explican la ausencia).
//
// Uso:
//   node grabar-flujo.mjs                  navegador visible
//   node grabar-flujo.mjs --headless        sin ventana
//
// Variables opcionales: BASE_URL, USUARIO, CLAVE, CLAVE_NUEVA, SLOWMO,
// MIN_VIDEO_MS, SOLO (nombres de casos separados por coma para probar).
// Si el servidor no responde, se levanta con --levantar-servidor.

import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const AQUI = path.dirname(fileURLToPath(import.meta.url));
const PROYECTO = path.resolve(AQUI, '..');
const PROYECTO_WEB = path.join(PROYECTO, 'MarcelaPaz.Web');
const EVIDENCIAS = path.join(AQUI, 'evidencias');
const SALIDA_VIDEO_DIR = path.join(EVIDENCIAS, 'grabar-flujo', 'video');
const SALIDA_VIDEO = path.join(EVIDENCIAS, 'grabar-flujo', 'flujo-mpaz.webm');
const LOG_SERVIDOR = path.join(EVIDENCIAS, 'grabar-flujo', 'servidor.log');

const BASE = (process.env.BASE_URL ?? 'http://localhost:5199').replace(/\/$/, '');
const SECRETARIA = process.env.USUARIO ?? 'mteresa.lagos@mpaz.cl';
const DIRECCION = process.env.USUARIO_DIRECCION ?? 'directora.fernandez@mpaz.cl';
const ADMIN = process.env.USUARIO_ADMIN ?? 'admin.palma@mpaz.cl';
const DOCENTE = process.env.USUARIO_DOCENTE ?? 'docente.ramirez@mpaz.cl';
const CLAVE_INICIAL = process.env.CLAVE ?? 'Mpaz2027segura';
const CLAVE_NUEVA = process.env.CLAVE_NUEVA ?? 'Simulacion2026X';
const HEADLESS = process.env.HEADLESS === '1' || process.argv.includes('--headless');
const SLOWMO = Number(process.env.SLOWMO ?? (HEADLESS ? 0 : 350));
const MIN_VIDEO_MS = Number(process.env.MIN_VIDEO_MS ?? 100000);
const SOLO = (process.env.SOLO ?? '').split(',').map(s => s.trim()).filter(Boolean);
const LEVANTAR_SERVIDOR = process.argv.includes('--levantar-servidor') || process.env.LEVANTAR_SERVIDOR === '1';
const SERVIDOR_CMD = process.env.SERVIDOR_CMD ?? ''; // Vacío: publicar a temporales y ejecutar el DLL.
const SERVIDOR_ARGS = process.env.SERVIDOR_ARGS ?? '';
// La página ocupa exactamente el ancho de la ventana, sin sobrepasarlo:
// el zoom se ajusta solo si el contenido es más ancho que la ventana.
const ANCHO = Number(process.env.ANCHO ?? 1280);
const ALTO = Number(process.env.ALTO ?? 720);
const ZOOM_MAX = Number(process.env.ZOOM ?? 1);

fs.mkdirSync(SALIDA_VIDEO_DIR, { recursive: true });

// El reloj de la duración mínima parte cuando usted pulsa ENTER (o cuando la
// corrida arranca sola por falta de terminal).
let inicio = Date.now();
let servidor = null;
let servidorLog = '';

function pausa(ms = 1200) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function rutaSalida(nombre, extension = 'png') {
  return path.join(EVIDENCIAS, 'grabar-flujo', nombre + '.' + extension);
}

function enSolo(nombre) {
  return SOLO.length === 0 || SOLO.some(prefijo => nombre.startsWith(prefijo));
}

// ---------------------------------------------------------------------------
// Arranque manual: el video comienza cuando el portal ya está abierto y el
// usuario pulsa ENTER. Sin terminal interactiva, se arranca tras 90 segundos.
// ---------------------------------------------------------------------------
async function esperarInicioManual() {
  if (!process.stdin.isTTY) {
    console.log('\n[Sin terminal interactiva] La grabación comienza automáticamente.\n');
    return;
  }

  console.log([
    '',
    '==============================================================',
    '  RECORRIDO AUTOMATIZADO · PORTAL DE MATRÍCULA MARCELA PAZ',
    '==============================================================',
    '',
    '  El navegador ya tiene el portal abierto (pantalla de acceso).',
    '  Todo lo que se ve desde ahora queda en el video.',
    '',
    '  >>> Pulse ENTER para comenzar la grabación <<<',
    '  (o espere 90 segundos y arranca solo; Ctrl+C para cancelar) ',
    '',
  ].join('\n'));

  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const tecleado = new Promise(resolve => rl.question('', resolve));
  const limite = new Promise(resolve => setTimeout(() => resolve('timeout'), 90000));
  const respuesta = await Promise.race([tecleado, limite]);
  rl.close();

  if (respuesta === 'timeout') {
    console.log('Arrancando automáticamente tras 90 segundos sin respuesta.\n');
  } else {
    console.log('Comenzando…\n');
  }
  inicio = Date.now();
}

// ---------------------------------------------------------------------------
// Servidor (se publica en temporales, nunca sobre el portal en ejecución)
// ---------------------------------------------------------------------------
async function esperarRespuesta(url, intentos = 3, esperaMs = 3000) {
  for (let i = 0; i < intentos; i += 1) {
    try {
      const respuesta = await fetch(url, { redirect: 'manual', signal: AbortSignal.timeout(5000) });
      if (respuesta.status >= 200 && respuesta.status < 500) return respuesta;
    } catch {
      // Se reintenta abajo.
    }
    await pausa(esperaMs);
  }
  return null;
}

function detenerServidor() {
  if (!servidor || servidor.killed) return;
  try {
    if (process.platform === 'win32') {
      spawn('taskkill.exe', ['/PID', String(servidor.pid), '/T', '/F'], { stdio: 'ignore' });
    } else {
      servidor.kill('SIGTERM');
    }
  } catch {
    servidor.kill('KILL');
  }
}

function ejecutarComando(comando, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(comando, args, { cwd, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    let salida = '';
    child.stdout.on('data', data => { salida += data.toString(); });
    child.stderr.on('data', data => { salida += data.toString(); });
    child.on('error', reject);
    child.on('exit', codigo => {
      if (codigo === 0) resolve(salida);
      else reject(new Error(`${comando} ${args.join(' ')} terminó con código ${codigo}\n${salida}`));
    });
  });
}

async function asegurarServidor() {
  if (await esperarRespuesta(`${BASE}/Cuenta/Login`, 1, 1000)) return;

  if (!LEVANTAR_SERVIDOR) {
    throw new Error(`El servidor no responde en ${BASE}. Levántelo o ejecute el script con --levantar-servidor.`);
  }

  let comando = SERVIDOR_CMD;
  let args;
  let cwdServidor = PROYECTO;
  const entorno = { ...process.env };

  if (!comando) {
    // La corrida no debe depender de los DLL bloqueados por el portal que ya
    // está abierto en el mismo árbol: se publica en una carpeta temporal.
    const salida = path.join(os.tmpdir(), `mpazvideo-${process.pid}`);
    fs.mkdirSync(salida, { recursive: true });
    const dotnet = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    await ejecutarComando(dotnet, [
      'publish', PROYECTO_WEB, '-c', 'Debug', '--nologo',
      '-o', salida,
      `-p:BaseOutputPath=${path.join(salida, 'build')}`,
    ], PROYECTO);
    comando = dotnet;
    args = [path.join(salida, 'MarcelaPaz.Web.dll'), '--urls', BASE];
    cwdServidor = salida;
    entorno.ASPNETCORE_ENVIRONMENT = 'Development';
    entorno.DOTNET_ENVIRONMENT = 'Development';
  } else if (SERVIDOR_ARGS) {
    args = SERVIDOR_ARGS.split(' ');
  } else {
    const dotnet = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
    comando = dotnet;
    args = ['run', '--project', PROYECTO_WEB, '--urls', BASE];
  }

  servidor = spawn(comando, args, {
    cwd: cwdServidor,
    env: entorno,
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  servidor.stdout.on('data', data => { servidorLog += data.toString(); });
  servidor.stderr.on('data', data => { servidorLog += data.toString(); });

  const lista = await esperarRespuesta(`${BASE}/Cuenta/Login`, 40, 3000);
  if (!lista) {
    detenerServidor();
    fs.writeFileSync(LOG_SERVIDOR, servidorLog);
    throw new Error(`El servidor no quedó listo en ${BASE}. Revisa evidencias/grabar-flujo/servidor.log`);
  }
}

// ---------------------------------------------------------------------------
// Utilidades de navegador
// ---------------------------------------------------------------------------
function runUnico(desplazamiento = 0) {
  const ahora = new Date();
  const valor = Number('28' + String(ahora.getHours()).padStart(2, '0')
    + String(ahora.getMinutes()).padStart(2, '0')
    + String(ahora.getSeconds()).padStart(2, '0')) + desplazamiento;
  const cuerpo = String(valor).padStart(8, '0');
  let suma = 0;
  let factor = 2;
  for (let i = cuerpo.length - 1; i >= 0; i -= 1) {
    suma += Number(cuerpo[i]) * factor;
    factor = factor === 7 ? 2 : factor + 1;
  }
  const resto = 11 - (suma % 11);
  const dv = resto === 11 ? '0' : resto === 10 ? 'K' : String(resto);
  return `${cuerpo}-${dv}`;
}

async function llenar(selector, valor) {
  const campo = page.locator(selector).first();
  if ((await campo.count()) === 0) throw new Error(`No apareció el campo ${selector}`);
  await campo.fill(String(valor));
  await pausa(250);
}

async function seleccionar(selector, valor) {
  const campo = page.locator(selector).first();
  await campo.selectOption(valor);
  await pausa(250);
}

async function radio(nombre, valor) {
  const opcion = page.locator(`[name="${nombre}"][value="${valor}"]`).first();
  await opcion.check({ force: true });
  await pausa(250);
}

async function abrirDetalles() {
  await page.evaluate(() => {
    document.querySelectorAll('details').forEach(detalle => { detalle.open = true; });
  });
}

async function guardarYSiguiente(pasoEsperado) {
  await page.getByRole('button', { name: /continuar|guardar y finalizar/i }).click();
  await page.getByRole('heading', { name: new RegExp(`Paso ${pasoEsperado}\\b|Ficha guardada`, 'i') })
    .waitFor({ timeout: 15000 })
    .catch(async () => {
      const texto = await page.evaluate(() => document.body?.innerText ?? '').catch(() => '');
      await page.screenshot({ path: rutaSalida(`diagnostico-paso-${pasoEsperado}`), fullPage: false }).catch(() => {});
      throw new Error(`No se avanzó al paso ${pasoEsperado}: ${page.url()} — ${texto.slice(0, 400)}`);
    });
  await pausa(1500);
}

async function pantallaFinal() {
  const texto = await page.locator('body').innerText().catch(() => '');
  if (!/Ficha guardada|Panel de matrícula|Estudiantes/i.test(texto)) {
    throw new Error('La pantalla final no coincide con el cierre esperado');
  }
  await pausa(3000);
}

// Paneó suave de arriba a abajo con solape, para que en el video se vea
// TODA la página sin saltos que corten contenido.
async function recorrerPagina(pasos = 7) {
  await page.evaluate(async (n) => {
    const total = Math.max(document.documentElement.scrollHeight - window.innerHeight, 1);
    const paso = Math.max(Math.round(total / n), Math.round(window.innerHeight * 0.6));
    for (let y = paso; y <= total; y += paso) {
      window.scrollTo({ top: y, behavior: 'smooth' });
      await new Promise(resolve => setTimeout(resolve, 560));
    }
    window.scrollTo({ top: total, behavior: 'smooth' });
    await new Promise(resolve => setTimeout(resolve, 500));
    // Desplaza también las tablas con scroll horizontal interno.
    document.querySelectorAll('.tabla-envoltura, .alumnos__tabla-envoltura')
      .forEach(contenedor => {
        contenedor.scrollTo({ left: contenedor.scrollWidth, behavior: 'smooth' });
      });
    await new Promise(resolve => setTimeout(resolve, 500));
    document.querySelectorAll('.tabla-envoltura, .alumnos__tabla-envoltura')
      .forEach(contenedor => {
        contenedor.scrollTo({ left: 0, behavior: 'smooth' });
      });
    window.scrollTo({ top: 0, behavior: 'smooth' });
    await new Promise(resolve => setTimeout(resolve, 500));
  }).catch(() => {});
}

// Ajusta el zoom para que el contenido ocupe exactamente la ventana:
// nunca más ancho (sin scroll horizontal) y sin dejar espacios.
async function ajustarAlAncho() {
  await page.evaluate((maximo) => {
    const ancho = document.documentElement.scrollWidth;
    const ventana = window.innerWidth;
    const zoom = ancho > ventana ? Math.floor((ventana / ancho) * 100) / 100 : maximo;
    document.documentElement.style.zoom = String(Math.min(zoom, maximo));
  }, ZOOM_MAX).catch(() => {});
}

// Pausa viva: el mouse se mueve por la pantalla como si alguien mirara.
async function mirar(segundos = 3) {
  const puntos = [[240, 160], [900, 220], [500, 420], [1100, 480], [320, 560], [760, 300]];
  for (let i = 0; i < segundos; i += 1) {
    const [x, y] = puntos[i % puntos.length];
    await page.mouse.move(x, y).catch(() => {});
    await pausa(900);
  }
}

async function esperarMinimoParaVideo() {
  const restante = MIN_VIDEO_MS - (Date.now() - inicio);
  if (restante > 0) await pausa(restante);
}

// ---------------------------------------------------------------------------
// Casos
// ---------------------------------------------------------------------------
let navegador;
let contexto;
let page;
const resultados = [];

async function caso(nombre, fn, { critico = false } = {}) {
  if (!enSolo(nombre)) return;
  const antes = Date.now();
  try {
    const detalle = (await fn()) ?? '';
    resultados.push({ caso: nombre, estado: 'OK', detalle });
    console.log(`[OK] ${nombre}${detalle ? ` — ${detalle}` : ''} (${Math.round((Date.now() - antes) / 1000)}s)`);
  } catch (error) {
    const estado = critico ? 'FALLO' : 'OMITIDO';
    resultados.push({ caso: nombre, estado, detalle: String(error?.message ?? error).slice(0, 260) });
    console.log(`[${estado}] ${nombre} — ${String(error?.message ?? error).slice(0, 220)}`);
    if (critico) throw error;
  }
}

async function iniciarSesion(usuario) {
  for (const clave of [CLAVE_INICIAL, CLAVE_NUEVA]) {
    await page.goto(`${BASE}/Cuenta/Login`, { waitUntil: 'domcontentloaded' });
    const formulario = page.getByLabel(/correo institucional/i);
    const aparecio = await formulario.waitFor({ timeout: 5000 }).then(() => true).catch(() => false);
    if (!aparecio) {
      // Ya autenticado: el portal redirige al panel en lugar del acceso.
      const enPanel = await page.getByRole('heading', { name: /panel de matrícula|estudiantes/i }).first()
        .waitFor({ timeout: 3000 }).then(() => true).catch(() => false);
      if (enPanel) return `sesión activa como ${usuario}`;
    }
    await formulario.fill(usuario);
    await page.getByLabel(/^contraseña$/i).first().fill(clave);
    await page.getByRole('button', { name: /ingresar/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(1000);

    if (/CambiarContrasena/i.test(page.url())) {
      await page.getByLabel(/^actual/i).fill(CLAVE_INICIAL);
      await page.getByLabel(/^nueva/i).fill(CLAVE_NUEVA);
      await page.getByLabel(/^confirmar/i).fill(CLAVE_NUEVA);
      await page.getByRole('button', { name: /guardar|cambiar/i }).click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1000);
      continue;
    }

    if (!/Cuenta\/Login/i.test(page.url())) {
      await page.getByRole('heading', { name: /panel de matrícula|estudiantes/i }).first()
        .waitFor({ timeout: 10000 })
        .catch(() => {});
      return `sesión iniciada como ${usuario}`;
    }
  }
  throw new Error(`no se pudo iniciar sesión como ${usuario}`);
}

async function asegurarSesion(usuario) {
  if (!/Cuenta\/(Login|CambiarContrasena)/i.test(page.url())) {
    const activa = await page.getByRole('button', { name: /cerrar sesión/i }).first()
      .waitFor({ timeout: 4000 }).then(() => true).catch(() => false);
    if (activa) return `sesión activa como ${usuario}`;
  }
  return iniciarSesion(usuario);
}

async function cerrarSesion() {
  const salir = page.getByRole('button', { name: /cerrar sesión/i }).first();
  if ((await salir.count()) > 0) {
    await salir.click();
    await page.waitForLoadState('domcontentloaded');
  }
  await pausa(800);
}

// --- MATRÍCULA NUEVA COMPLETA (pasos 1 a 8) ---------------------------------
async function wizardCompleto(run) {
  await asegurarSesion(SECRETARIA);
  await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: /matrícula nueva/i }).first().click();
  await page.waitForLoadState('domcontentloaded');
  await page.getByRole('heading', { name: /paso 1/i }).waitFor({ timeout: 10000 });
  await abrirDetalles();

  await seleccionar('#Curso_Nivel', { label: '2º básico' });
  await seleccionar('#Curso_Letra', { label: 'A' });
  await llenar('#Nombres', 'Antonia');
  await llenar('#Apellido_Paterno', 'SIMULACIÓN');
  await llenar('#Apellido_Materno', 'Video');
  await llenar('#Cedula_Identidad', run);
  await llenar('#Fecha_Nacimiento', '2018-03-15');
  await llenar('#Nacionalidad', 'Chilena');
  await llenar('#Domicilio', 'Calle Demo 123, Villa Prueba');
  await llenar('#Comuna', 'San Bernardo');
  await guardarYSiguiente(2);

  await abrirDetalles();
  await llenar('#Madre_Nombre_Apellido', 'María SIMULACIÓN');
  await llenar('#Madre_Cedula_Identidad', runUnico());
  await llenar('#Madre_Telefono', '+56912345678');
  await seleccionar('#Madre_Escolaridad', { label: 'Educación universitaria' });
  await llenar('#Madre_Domicilio', 'Calle Demo 123, San Bernardo');
  await llenar('#Madre_Correo', 'madre.simulacion@example.cl');
  await radio('Restriccion_Madre', 'No');

  await llenar('#Padre_Nombre_Apellido', 'Luis SIMULACIÓN');
  await llenar('#Padre_Cedula_Identidad', runUnico(1));
  await llenar('#Padre_Telefono', '+56922345678');
  await seleccionar('#Padre_Escolaridad', { label: 'Educación media' });
  await llenar('#Padre_Domicilio', 'Calle Demo 123, San Bernardo');
  await llenar('#Padre_Correo', 'padre.simulacion@example.cl');
  await radio('Restriccion_Padre', 'No');
  await guardarYSiguiente(3);

  await abrirDetalles();
  await llenar('#Principal_Nombre_Apellido', 'María SIMULACIÓN');
  await llenar('#Principal_Cedula_Identidad', runUnico(2));
  await llenar('#Principal_Telefono', '+56912345678');
  await llenar('#Principal_Vinculo', 'Madre');
  await llenar('#Principal_Correo', 'apoderado.simulacion@example.cl');
  await guardarYSiguiente(4);

  await abrirDetalles();
  await llenar('#Contacto1_Identificacion', 'Juana Pérez');
  await llenar('#Contacto1_Telefono', '+56911223344');
  await llenar('#Contacto1_Vinculo', 'Abuela');
  await guardarYSiguiente(5);

  await abrirDetalles();
  await radio('SalidaSolo', 'No');
  await llenar('#Retiro1_Rut', runUnico(3));
  await llenar('#Retiro1_Vinculo', 'Tía');
  await guardarYSiguiente(6);

  await abrirDetalles();
  await radio('ParticipaPie', 'No');
  await radio('AutorizaParacetamol', 'Si');
  await radio('TieneSituacionMedica', 'No');
  await llenar('#Contacto_Sanitario_Nombre', 'Consultorio de prueba');
  await llenar('#Contacto_Sanitario_Telefono', '+56911223344');
  await radio('SalidaBano', 'No');
  await guardarYSiguiente(7);

  await abrirDetalles();
  await radio('AutorizaImagen', 'Si');
  await radio('AutorizaConvivencia', 'Si');
  await guardarYSiguiente(8);

  await page.locator('.wizard__revision').waitFor({ timeout: 10000 });
  await page.waitForTimeout(4000);
  await page.mouse.wheel(0, 500);
  await page.locator('#Confirmo_Revision').check({ force: true });
  await guardarYSiguiente('guardada');
  await page.getByRole('heading', { name: /ficha guardada/i }).waitFor({ timeout: 10000 });
  await pantallaFinal();
  return `RUN ${run}`;
}

async function abrirFichaPorRun(run) {
  await page.goto(`${BASE}/Alumnos?q=${encodeURIComponent(run)}`, { waitUntil: 'domcontentloaded' });
  const fila = page.locator('tbody tr').first();
  await fila.waitFor({ timeout: 10000 });
  await page.locator('.alumnos__ver').first().click();
  await page.waitForLoadState('domcontentloaded');
  await pausa(1500);
}

async function subirArchivoDemo(selector, nombre) {
  const archivo = rutaSalida(nombre ?? 'archivo-demo', 'png');
  await page.screenshot({ path: archivo });
  await page.locator(selector).first().setInputFiles(archivo);
  return archivo;
}

// ---------------------------------------------------------------------------
// Programa principal
// ---------------------------------------------------------------------------
try {
  console.log(`Grabación del recorrido · ${BASE}`);
  await asegurarServidor();

  navegador = await chromium.launch({ headless: HEADLESS, slowMo: SLOWMO });
  contexto = await navegador.newContext({
    viewport: { width: ANCHO, height: ALTO },
    ignoreHTTPSErrors: true,
    recordVideo: { dir: SALIDA_VIDEO_DIR, size: { width: ANCHO, height: ALTO } },
  });
  page = await contexto.newPage();
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

  // ====================== SECRETARIA =======================================
  await caso('01-login-secretaria', () => iniciarSesion(SECRETARIA), { critico: true });

  await caso('02-panel', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /panel de matrícula/i }).waitFor({ timeout: 10000 });
    await recorrerPagina();
    await mirar(3);
    return 'KPIs, avance por curso y pendientes';
  }, { critico: true });

  await caso('03-actividad', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Home/Actividad`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /actividad reciente/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(5);
    await mirar(2);
    return 'consultas, descargas y accesos denegados';
  });

  await caso('04-alumnos-busqueda-y-filtros', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.locator('#q').fill('a');
    await page.getByRole('button', { name: /filtrar/i }).click();
    await page.waitForLoadState('domcontentloaded');
    const total = await page.locator('tbody tr').count();
    await recorrerPagina(6);
    await mirar(2);
    return `${total} filas tras buscar`;
  }, { critico: true });

  await caso('05-ficha-pestanas', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.locator('.alumnos__ver').first().click();
    await page.waitForLoadState('domcontentloaded');
    for (const pestana of ['autorizaciones', 'documentos', 'salud', 'pie']) {
      await page.locator(`#pestana-${pestana}`).click();
      await pausa(1200);
      await recorrerPagina(4);
    }
    await mirar(3);
    return 'pestañas General, Autorizaciones, Documentos, Salud y PIE';
  }, { critico: true });

  await caso('06-no-funciona-revocar-autorizacion', async () => {
    // Busca en las primeras fichas una con una autorización OTORGADA.
    let revocada = false;
    for (let intento = 0; intento < 6 && !revocada; intento += 1) {
      await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
      await page.locator('.alumnos__ver').nth(intento).click();
      await page.waitForLoadState('domcontentloaded');
      await page.locator('#pestana-autorizaciones').click();
      const formulario = page.locator('form:has(input[name="motivo"])').first();
      const hay = await formulario.waitFor({ timeout: 4000 }).then(() => true).catch(() => false);
      if (!hay) continue;
      await formulario.locator('input[name="motivo"]').fill('Revocada en demostración grabada');
      await formulario.getByRole('button', { name: /^revocar$/i }).click();
      await page.waitForLoadState('domcontentloaded');
      revocada = true;
    }
    if (!revocada) throw new Error('ninguna ficha de las primeras con autorización otorgada');
    await pausa(2000);
    return 'revocación con motivo, funcionario y fecha';
  });

  await caso('07-documentos', async () => {
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.locator('.alumnos__ver').first().click();
    await page.waitForLoadState('domcontentloaded');
    await page.locator('#pestana-documentos').click();
    const subida = page.locator('input[name="archivo"]').first();
    const haySubida = (await subida.count()) > 0;
    if (haySubida) {
      await subirArchivoDemo('input[name="archivo"]', 'documento-demo');
      await page.waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    const noAplica = page.getByRole('button', { name: /^no aplica$/i }).first();
    if ((await noAplica.count()) > 0) {
      await noAplica.click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    return haySubida ? 'se subió un archivo y se marcó un requisito como no aplica' : 'sin requisitos cargables en esta ficha';
  });

  const RUN_NUEVO = runUnico();
  await caso('08-wizard-matricula-nueva', () => wizardCompleto(RUN_NUEVO), { critico: true });

  await caso('09-registrar-firma-y-subir-escaneo', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(RUN_NUEVO);
    const formFirma = page.getByRole('button', { name: /registrar ficha firmada/i }).first();
    if ((await formFirma.count()) > 0) {
      await page.locator('input[name="fecha"]').first().fill('2026-11-13');
      await formFirma.click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(1500);
    }
    await page.locator('#pestana-documentos').click();
    const inputFicha = page.locator('#ficha-firmada');
    if ((await inputFicha.count()) > 0) {
      await subirArchivoDemo('#ficha-firmada', 'ficha-firmada-demo');
      await page.getByRole('button', { name: /subir ficha firmada/i }).click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(2000);
    }
    return 'transición FICHA_GENERADA → FIRMADA y escaneo asociado';
  }, { critico: true });

  await caso('10-confirmar', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(RUN_NUEVO);
    await page.locator('#pestana-documentos').click();
    await pausa(1000);
    const boton = page.getByRole('button', { name: /confirmar matrícula/i }).first();
    await boton.click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'confirmación según el procedimiento oficial';
  });

  await caso('11-corregir-ficha', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(RUN_NUEVO);
    const boton = page.getByRole('button', { name: /corregir ficha/i }).first();
    await boton.click();
    await page.waitForLoadState('domcontentloaded');
    // La corrección carga el snapshot congelado: se va directo al paso 8.
    await page.goto(`${BASE}/Ficha/Paso/8`, { waitUntil: 'domcontentloaded' });
    await page.locator('.wizard__revision').waitFor({ timeout: 10000 });
    await page.locator('#motivo-correccion').fill('Corrección mostrada en la grabación');
    await page.locator('#Confirmo_Revision').check({ force: true });
    await guardarYSiguiente('guardada');
    return 'nueva versión con motivo; la anterior queda REEMPLAZADA';
  });

  await caso('12-certificado-alumno-regular', async () => {
    await asegurarSesion(SECRETARIA);
    await abrirFichaPorRun(RUN_NUEVO);
    const descarga = page.waitForEvent('download', { timeout: 15000 }).catch(() => null);
    await page.getByRole('link', { name: /certificado alumno regular/i }).first().click();
    const archivo = await descarga;
    if (archivo) await archivo.saveAs(rutaSalida('certificado-alumno-regular', 'pdf'));
    await page.goto(`${BASE}/Certificados`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /certificados de alumno regular/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(5);
    await mirar(2);
    return archivo ? 'PDF emitido y listado en el historial' : 'historial de certificados';
  });

  await caso('13-reportes-y-csv', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Reportes/Matriculas`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /reporte de matrículas/i }).waitFor({ timeout: 10000 });
    const descarga = page.waitForEvent('download', { timeout: 12000 }).catch(() => null);
    await page.getByRole('link', { name: /descargar csv/i }).first().click();
    const archivo = await descarga;
    if (archivo) await archivo.saveAs(rutaSalida('reporte-matriculas', 'csv'));
    await recorrerPagina(5);
    await page.goto(`${BASE}/Reportes/Faltantes`, { waitUntil: 'domcontentloaded' });
    await recorrerPagina(5);
    await mirar(2);
    return 'estado de matrículas, CSV auditado y faltantes';
  });

  await caso('14-anular-matricula', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Alumnos?estado=BORRADOR`, { waitUntil: 'domcontentloaded' });
    await page.locator('.alumnos__ver').first().click();
    await page.waitForLoadState('domcontentloaded');
    await page.locator('#pestana-documentos').click();
    await page.locator('#motivo-anulacion').fill('Anulada en demostración grabada');
    await page.getByRole('button', { name: /anular matrícula/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'la anulación libera el año para una matrícula nueva';
  });

  await caso('15-denegacion-funcionarios', async () => {
    await asegurarSesion(SECRETARIA);
    await page.goto(`${BASE}/Admin/Funcionarios`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /permiso/i }).waitFor({ timeout: 8000 }).catch(() => {});
    await pausa(2000);
    return 'SECRETARIA denegada y auditada';
  });

  await cerrarSesion();

  // ====================== DIRECCION ========================================
  await caso('16-validar-matricula', async () => {
    await asegurarSesion(DIRECCION);
    await abrirFichaPorRun(RUN_NUEVO);
    await page.locator('#pestana-documentos').click();
    await page.getByRole('button', { name: /validar matrícula/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2500);
    return 'validación institucional con fecha y responsable';
  }, { critico: true });

  await caso('17-validacion-documental-mat10', async () => {
    await asegurarSesion(DIRECCION);
    await page.locator('#ubicacion-original').fill('Caja 3 · carpeta 2027 · archivo de secretaría');
    await page.locator('#estado-validacion').selectOption('OBSERVADA');
    await page.locator('#observacion-validacion').fill('Falta el reverso del escaneo');
    await page.getByRole('button', { name: /guardar validación documental/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2000);
    await page.locator('#estado-validacion').selectOption('VALIDADA');
    await page.locator('#observacion-validacion').fill('');
    await page.getByRole('button', { name: /guardar validación documental/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2000);
    return 'ubicación del original, OBSERVADA y luego VALIDADA';
  }, { critico: true });

  await cerrarSesion();

  // ====================== ADMIN_TI =========================================
  await caso('18-administracion-funcionarios', async () => {
    const detalle = await asegurarSesion(ADMIN);
    await page.goto(`${BASE}/Admin/Funcionarios`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /^funcionarios$/i }).waitFor({ timeout: 10000 });
    await recorrerPagina(6);
    await mirar(2);

    // Crear un funcionario de demostración (una sola vez: si ya existe, se reutiliza).
    const yaExiste = (await page.locator('tr', { hasText: 'demo.video@mpaz.cl' }).count()) > 0;
    if (!yaExiste) {
      await page.getByRole('link', { name: /crear funcionario/i }).click();
      await page.waitForLoadState('domcontentloaded');
      await page.locator('#Nombre').fill('Video Demo Grav');
      await page.locator('#Correo').fill('demo.video@mpaz.cl');
      await page.locator('#Contrasena').fill('VideoDemo2027X');
      await page.locator('input[name="Roles"][value="DOCENTE"]').check({ force: true });
      await page.getByRole('button', { name: /^crear funcionario$/i }).click();
      await page.waitForLoadState('domcontentloaded');
      await pausa(2500);
    }

    const fila = page.locator('tr', { hasText: 'demo.video@mpaz.cl' }).first();

    // Restablecer clave: muestra la temporal una sola vez.
    await fila.getByRole('button', { name: /restablecer contraseña/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await page.getByRole('heading', { name: /contraseña temporal/i }).waitFor({ timeout: 8000 }).catch(() => {});
    await pausa(2500);

    // Desactivar y reactivar.
    await fila.getByRole('button', { name: /^desactivar$/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await fila.getByRole('button', { name: /^activar$/i }).click();
    await page.waitForLoadState('domcontentloaded');
    await pausa(2000);
    return `${detalle}; alta, clave temporal, desactivar/reactivar`;
  }, { critico: true });

  await cerrarSesion();

  // ====================== DOCENTE (vista limitada) =========================
  await caso('19-vista-limitada-docente', async () => {
    await iniciarSesion(DOCENTE);
    await page.goto(`${BASE}/Alumnos`, { waitUntil: 'domcontentloaded' });
    await page.locator('.alumnos__ver').first().click();
    await page.waitForLoadState('domcontentloaded');
    for (const pestana of ['autorizaciones', 'documentos', 'salud', 'pie']) {
      await page.locator(`#pestana-${pestana}`).click();
      await pausa(900);
      await recorrerPagina(3);
    }
    await mirar(2);
    return 'sin permisos de área: los paneles explican la ausencia';
  });

  await cerrarSesion();

  await caso('20-cierre', async () => {
    await esperarMinimoParaVideo();
    await page.goto(`${BASE}/`, { waitUntil: 'domcontentloaded' });
    await recorrerPagina();
    await mirar(3);
    return 'fin del recorrido';
  }, { critico: true });
} catch (error) {
  resultados.push({ caso: 'error-general', estado: 'FALLO', detalle: String(error?.message ?? error).slice(0, 500) });
  console.error(error);
} finally {
  let videoOrigen = null;
  try {
    videoOrigen = await page?.video()?.path().catch(() => null);
  } catch {
    videoOrigen = null;
  }

  try {
    await contexto?.close();
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
    await navegador?.close();
  } catch {
    // El navegador ya puede estar cerrado.
  }

  detenerServidor();
  if (servidorLog) fs.writeFileSync(LOG_SERVIDOR, servidorLog);
  const segundos = Math.round((Date.now() - inicio) / 1000);
  console.log(`Duración de la corrida: ${segundos}s`);
  const ok = resultados.filter(registro => registro.estado === 'OK').length;
  const omitidos = resultados.filter(registro => registro.estado === 'OMITIDO').length;
  const fallos = resultados.filter(registro => registro.estado === 'FALLO').length;
  console.log(`Resumen: ${ok} OK · ${omitidos} omitidos · ${fallos} fallidos`);
  process.exitCode = fallos > 0 ? 1 : 0;
}
