# Verificación con NVDA y corrección del aviso de errores

Fecha: 07-11-2026.

## 1. Defecto que faltaba: el aviso de errores no tenía estilos

`.acceso__alerta` se usaba en el resumen de validación (U-02) y en los avisos de `Buscar` y `Panel`, pero **no existía ninguna regla para esa clase**. La única declaración relacionada era `.acceso__alerta--errores:focus`, añadida para el contorno del foco. En pantalla, el resumen de errores se veía como una lista de texto suelta sin fondo ni borde, perdiendo la jerarquía visual que sí tienen los avisos de advertencia.

Se añadió en `acceso.css`, con el mismo molde que `.acceso__aviso` y en tono de error:

- fondo `--color-danger-bg` (#FBEAEA), borde izquierdo de 4 px en `--color-danger`, texto `--color-text`;
- lista sin márgenes externos, sangría de 1,15 rem y separación entre elementos.

Comprobado en el navegador: fondo `rgb(251, 234, 234)`, borde `rgb(178, 59, 59)` de 4 px, lista de 9 elementos con viñetas. Contraste del texto sobre el fondo nuevo, muy por encima de 4,5:1. axe sobre la página con errores, el paso 1, el paso 6 y el login: **0 violaciones**.

## 2. Prueba con NVDA

### Cómo se hizo

NVDA no estaba instalado. Se descargó la versión **2025.3.2** y se creó una **copia portable** en una carpeta temporal, sin instalar nada en el sistema ni tocar el registro. Se ejecutó con `--log-level=12 --log-file=…`, que registra en `nvda.log` cada texto enviado al sintetizador (`Speaking [...]`), y con el diálogo de bienvenida y el de novedades desactivados.

Para que NVDA siguiera la ventana correcta hubo que traer el navegador a primer plano mediante `SetForegroundWindow`; sin eso seguía a las ventanas del usuario y no al portal. El recorrido se hizo con un navegador visible y pulsaciones reales de Tab, flechas y Enter, con pausas de 1,2 a 3 s.

### Lo que anunció NVDA

| Momento | Anuncio capturado |
|---|---|
| Login | «principal región», «formulario región», «Correo institucional, edición, en blanco», «Contraseña, edición, contraseña, en blanco», «Ingresar, botón» |
| Panel | «Saltar al contenido, enlace», «Logo Escuela Escritora Marcela Paz, gráfico», «Secciones del portal, navegación región» |
| Paso 1, primer Tab | «Saltar al contenido de la ficha, enlace» |
| Paso 1 con errores | «principal región, sección, lista, con 9 elementos», seguido de «• Ingrese la comuna del domicilio», «• Ingrese los nombres del estudiante», «• Ingrese el domicilio del estudiante», «• Seleccione la letra del curso»… |
| Paso 6, PIE | «Programa de Integración Escolar (PIE), agrupación», «Si, botón de opción, sin marcar» |
| Paso 6, flechas | «No, botón de opción, marcado» y luego «Pendiente, botón de opción, marcado» |
| Paso 6, alergias | «Alergias y contacto sanitario, agrupación», «Alergias declaradas \*, edición, requerido» y el texto escrito |
| Confirmación con folio | «Copiar el folio, botón»; tras Enter, «Folio copiado al portapapeles.» |

Esto confirma, escuchando la salida real del lector:

1. Las etiquetas de los campos y su estado se anuncian, incluido «requerido».
2. El resumen de errores se anuncia **solo** al aparecer, con `role="alert"`, y se lee como una lista numerada de elementos, sin que el usuario tenga que buscarlo.
3. Los grupos de opciones se anuncian como **agrupación con su leyenda** («Programa de Integración Escolar (PIE)»), no como radios sueltos.
4. Al recorrer un grupo con flechas se anuncia la opción que queda marcada.
5. El resultado de copiar el folio se anuncia por la región de estado, sin mover el foco.

### Límites de esta prueba

- Una sola ejecución, con NVDA 2025.3.2 portable, voz espeak en español y la configuración por defecto. Otra velocidad, otro sintetizador u otra versión pueden cambiar la redacción del anuncio, no el contenido.
- Se cubrieron login, panel, paso 1 con y sin errores, paso 6 y la confirmación con folio. No se recorrieron los siete pasos completos con NVDA.
- El anuncio del título de la confirmación no quedó dentro de su ventana de captura por un desfase de tiempo; el foco en `h1#envio-titulo` ya estaba verificado de forma programática en la ronda anterior.
- La captura proviene del registro de voz de NVDA, no de una grabación de audio: refleja exactamente el texto enviado al sintetizador, pero no cómo suena con una configuración concreta.
- En un momento de la sesión, otras ventanas del equipo tomaron el foco y NVDA leyó aplicación ajena al portal; esos anuncios se descartaron.
- No se probó el modo de navegación por caracteres ni los atajos de lectura de tablas.

## 3. Estado

Los dos puntos que quedaban abiertos de la auditoría UI/UX —NVDA y la revisión de errores por paso— están cubiertos con evidencia ejecutada. Sigue pendiente la revisión visual responsive (360 y 768 px), el zoom al 200 %, el alto contraste de Windows y los textos legales de las autorizaciones.

Evidencia cruda: `05-evidencias-nvda.json` (fases con hora y anuncios capturados por ventana).

## 4. Entorno

Servidor de prueba en `127.0.0.1:5196` sobre la copia temporal `mpaz_nvda_verificacion`; compilación en `artifacts/nvda-temporal/build`. La base `mpaz-matricula` y el puerto 5195 de Visual Studio no se tocaron. Al terminar se cerró NVDA, se eliminó la copia portable, el instalador descargado, la base temporal y el servidor.
