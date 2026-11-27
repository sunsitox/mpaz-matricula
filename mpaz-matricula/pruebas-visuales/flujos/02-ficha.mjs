// Módulo 02 · Ficha de matrícula (asistente de 8 pasos).
// Crea una matrícula nueva completa y deja su RUN en estado.runNuevo para que
// los módulos siguientes sigan el ciclo de vida del expediente.

import {
  SECRETARIA,
  abrirDetalles,
  asegurarSesion,
  caso,
  estado,
  guardarYSiguiente,
  irA,
  llenar,
  pag,
  pantallaFinal,
  pausa,
  radio,
  runUnico,
  seleccionar,
} from './comun.mjs';

export const titulo = '02 · Ficha de matrícula (8 pasos)';

async function wizardCompleto(run) {
  const page = pag();
  await asegurarSesion(SECRETARIA);
  await irA('/', 1000);
  await page.getByRole('button', { name: /matrícula nueva/i }).first().click();
  await page.waitForLoadState('domcontentloaded');
  await page.getByRole('heading', { name: /paso 1/i }).waitFor({ timeout: 10000 });
  await abrirDetalles();

  // Paso 1 · identificación
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

  // Paso 2 · padres y apoderados
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

  // Paso 3 · apoderado principal
  await abrirDetalles();
  await llenar('#Principal_Nombre_Apellido', 'María SIMULACIÓN');
  await llenar('#Principal_Cedula_Identidad', runUnico(2));
  await llenar('#Principal_Telefono', '+56912345678');
  await llenar('#Principal_Vinculo', 'Madre');
  await llenar('#Principal_Correo', 'apoderado.simulacion@example.cl');
  await guardarYSiguiente(4);

  // Paso 4 · contactos de emergencia
  await abrirDetalles();
  await llenar('#Contacto1_Identificacion', 'Juana Pérez');
  await llenar('#Contacto1_Telefono', '+56911223344');
  await llenar('#Contacto1_Vinculo', 'Abuela');
  await guardarYSiguiente(5);

  // Paso 5 · personas autorizadas a retirar
  await abrirDetalles();
  await radio('SalidaSolo', 'No');
  await llenar('#Retiro1_Rut', runUnico(3));
  await llenar('#Retiro1_Vinculo', 'Tía');
  await guardarYSiguiente(6);

  // Paso 6 · salud y PIE
  await abrirDetalles();
  await radio('ParticipaPie', 'No');
  await radio('AutorizaParacetamol', 'Si');
  await radio('TieneSituacionMedica', 'No');
  await llenar('#Contacto_Sanitario_Nombre', 'Consultorio de prueba');
  await llenar('#Contacto_Sanitario_Telefono', '+56911223344');
  await radio('SalidaBano', 'No');
  await guardarYSiguiente(7);

  // Paso 7 · autorizaciones de imagen y convivencia
  await abrirDetalles();
  await radio('AutorizaImagen', 'Si');
  await radio('AutorizaConvivencia', 'Si');
  await guardarYSiguiente(8);

  // Paso 8 · revisión y confirmación de la ficha
  await page.locator('.wizard__revision').waitFor({ timeout: 10000 });
  await page.waitForTimeout(4000);
  await page.mouse.wheel(0, 500);
  await page.locator('#Confirmo_Revision').check({ force: true });
  await guardarYSiguiente('guardada');
  await page.getByRole('heading', { name: /ficha guardada/i }).waitFor({ timeout: 10000 });
  await pantallaFinal();
  return `RUN ${run}`;
}

export async function ejecutar() {
  const run = runUnico();
  await caso('01-wizard-matricula-nueva', () => wizardCompleto(run), { critico: true });
  estado.runNuevo = run;
  await pausa(500);
  return `matrícula ${run} generada`;
}
