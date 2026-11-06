/* Mejoras de fluidez y feedback de carga, sin dependencias ni cambios de flujo. */
(() => {
    'use strict';

    const reducido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    document.documentElement.classList.add('ux-js');

    // Barra de progreso superior durante navegaciones y envíos.
    const barra = document.createElement('div');
    barra.id = 'ux-progreso';
    barra.setAttribute('aria-hidden', 'true');
    document.body.appendChild(barra);

    // Velo con rueda de carga para envíos que tardan (envío a revisión).
    const velo = document.createElement('div');
    velo.className = 'ux-cargando';
    velo.setAttribute('role', 'status');
    velo.setAttribute('aria-live', 'polite');
    velo.innerHTML = '<span class="ux-cargando__rueda" aria-hidden="true"></span>'
        + '<p class="ux-cargando__texto"></p>';
    document.body.appendChild(velo);

    const mostrarVelo = texto => {
        velo.querySelector('.ux-cargando__texto').textContent = texto || 'Procesando…';
        velo.classList.add('ux-cargando--visible');
    };

    let avance = 0;
    let temporizador = null;

    const empezar = () => {
        if (reducido) {
            return;
        }

        window.clearInterval(temporizador);
        avance = 12;
        barra.style.width = avance + '%';
        barra.classList.add('ux-progreso--visible');

        temporizador = window.setInterval(() => {
            avance += (88 - avance) * 0.12;
            barra.style.width = avance.toFixed(1) + '%';
        }, 180);
    };

    const terminar = () => {
        window.clearInterval(temporizador);

        if (!barra.classList.contains('ux-progreso--visible')) {
            return;
        }

        barra.style.width = '100%';

        window.setTimeout(() => {
            barra.classList.remove('ux-progreso--visible');
            barra.style.width = '0';
        }, 220);
    };

    window.addEventListener('load', terminar);
    window.addEventListener('pageshow', terminar);

    // Navegación interna: mostrar progreso de inmediato.
    document.addEventListener('click', evento => {
        if (evento.defaultPrevented || evento.button !== 0 || evento.metaKey || evento.ctrlKey
            || evento.shiftKey || evento.altKey) {
            return;
        }

        const enlace = evento.target.closest('a[href]');

        if (!enlace || enlace.target === '_blank' || enlace.hasAttribute('download')
            || enlace.dataset.uxSinProgreso !== undefined) {
            return;
        }

        const destino = new URL(enlace.href, window.location.href);

        if (destino.origin !== window.location.origin) {
            return;
        }

        if (destino.pathname === window.location.pathname && destino.hash) {
            return;
        }

        empezar();
    }, true);

    // Formularios: botón con spinner y bloqueo del doble envío.
    document.addEventListener('submit', evento => {
        const formulario = evento.target;

        if (!(formulario instanceof HTMLFormElement)) {
            return;
        }

        if (formulario.dataset.enviando === 'si') {
            evento.preventDefault();
            return;
        }

        formulario.dataset.enviando = 'si';
        formulario.setAttribute('aria-busy', 'true');

        const boton = evento.submitter
            || formulario.querySelector('button[type="submit"], input[type="submit"]');

        if (boton) {
            boton.classList.add('is-loading');
            boton.setAttribute('aria-disabled', 'true');
        }

        empezar();

        if (formulario.dataset.uxEnvio) {
            mostrarVelo(formulario.dataset.uxEnvio);
        }
    }, true);

    // Descargas generadas al vuelo (PDF): spinner mientras el archivo se genera.
    document.addEventListener('click', evento => {
        const enlace = evento.target.closest('a[data-ux-cargando]');

        if (!enlace) {
            return;
        }

        enlace.classList.add('is-loading');
        window.setTimeout(() => enlace.classList.remove('is-loading'), 6000);
    });

    // Vista previa del PDF: quitar el aviso de carga cuando el iframe está listo.
    document.querySelectorAll('.wizard__pdf-marco').forEach(marco => {
        const caja = marco.closest('.wizard__pdf');

        if (!caja) {
            return;
        }

        const listo = () => caja.classList.add('wizard__pdf--lista');

        if (marco.contentDocument && marco.contentDocument.readyState === 'complete') {
            listo();
        }

        marco.addEventListener('load', listo);
    });

    // Notificación flotante tras completar una acción: entra sola y se retira.
    document.querySelectorAll('[data-ux-toast]').forEach(aviso => {
        // La barra de acciones de la ficha es fija y puede ocupar dos filas:
        // la notificación se apoya sobre su alto real en lugar de un valor fijo.
        const barraFija = document.querySelector('.wizard__botones');

        const posicionar = () => {
            if (!barraFija || window.getComputedStyle(barraFija).position !== 'fixed') {
                return;
            }

            const altoBarra = window.innerHeight - barraFija.getBoundingClientRect().top;
            aviso.style.bottom = Math.max(16, Math.round(altoBarra + 28)) + 'px';
        };

        posicionar();
        window.addEventListener('resize', posicionar);

        // La barra cambia de alto si los botones pasan a dos filas o si las
        // fuentes terminan de cargar: se vuelve a medir en cada cambio.
        if (barraFija && 'ResizeObserver' in window) {
            new ResizeObserver(posicionar).observe(barraFija);
        }

        if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(posicionar);
            window.setTimeout(posicionar, 600);
        }

        window.setTimeout(() => aviso.classList.add('ux-toast--visible'), 20);

        let ida = null;

        const cerrar = () => {
            if (ida) {
                return;
            }

            ida = window.setTimeout(() => {
                aviso.classList.remove('ux-toast--visible');
                window.setTimeout(() => aviso.remove(), 300);
            }, 0);
        };

        const boton = aviso.querySelector('[data-ux-cerrar]');

        if (boton) {
            boton.addEventListener('click', cerrar);
        }

        window.setTimeout(cerrar, 12000);
    });
})();
