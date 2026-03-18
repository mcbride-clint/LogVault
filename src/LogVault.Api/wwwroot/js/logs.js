// Log Explorer — live tail via SignalR
(function () {

    // ── Scroll state persistence ──────────────────────────────────────────────
    // Save the current scroll position + search URL so the detail page's Back
    // button can return to exactly this URL and we can restore the scroll.

    const SCROLL_KEY = 'lv_scroll_restore';

    window.lvNavigateToLog = function (id) {
        const content = document.querySelector('.lv-content');
        if (content) {
            try {
                sessionStorage.setItem(SCROLL_KEY, JSON.stringify({
                    url: window.location.href,
                    scrollTop: content.scrollTop
                }));
            } catch (_) {}
        }
        window.location = '/logs/' + id;
    };

    // Restore scroll if we're returning to the page that was saved
    (function restoreScroll() {
        let saved;
        try { saved = JSON.parse(sessionStorage.getItem(SCROLL_KEY)); } catch (_) {}
        if (!saved || saved.url !== window.location.href) return;
        sessionStorage.removeItem(SCROLL_KEY);
        const content = document.querySelector('.lv-content');
        if (!content) return;
        // rAF ensures layout is complete before scrolling
        requestAnimationFrame(() => { content.scrollTop = saved.scrollTop; });
    })();

    // ─────────────────────────────────────────────────────────────────────────

    let connection = null;
    let liveTailActive = false;

    const btnToggle = document.getElementById('btn-live-toggle');
    const indicator = document.getElementById('live-indicator');
    const tbody = document.getElementById('log-rows');
    const exprInput = document.querySelector('[data-expr-editor]');

    if (!btnToggle) return;

    btnToggle.addEventListener('click', () => {
        if (liveTailActive) stopLiveTail();
        else startLiveTail();
    });

    function buildFilter() {
        const get = name => {
            const el = document.querySelector(`[name="${name}"]`);
            return el?.value?.trim() || null;
        };
        return {
            messageContains: get('q'),
            minLevel: get('level'),
            sourceApplication: get('app'),
            sourceEnvironment: get('env'),
            traceId: get('traceId')
        };
    }

    function setExprDisabled(disabled) {
        if (!exprInput) return;
        exprInput.disabled = disabled;
        exprInput.title = disabled ? 'Expression filters are not supported during Live Tail' : '';
    }

    function startLiveTail() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/logs')
            .withAutomaticReconnect()
            .build();

        connection.on('NewEvents', (events) => {
            if (!tbody) return;
            events.forEach(ev => {
                const tr = document.createElement('tr');
                tr.className = 'lv-log-row';
                tr.style.cursor = 'pointer';
                tr.onclick = () => window.lvNavigateToLog(ev.id);
                tr.innerHTML =
                    `<td class="text-nowrap small">${formatTs(ev.timestamp)}</td>` +
                    `<td><span class="badge ${levelClass(ev.level)}">${ev.level}</span></td>` +
                    `<td class="small text-truncate" style="max-width:120px">${ev.sourceApplication ?? ''}</td>` +
                    `<td class="small text-truncate" style="max-width:500px">${ev.renderedMessage ?? ''}</td>`;
                tbody.insertBefore(tr, tbody.firstChild);
            });
        });

        connection.start()
            .then(() => connection.invoke('SetFilter', buildFilter()))
            .then(() => {
                liveTailActive = true;
                btnToggle.textContent = 'Stop Live Tail';
                btnToggle.classList.replace('btn-outline-secondary', 'btn-outline-danger');
                indicator.classList.remove('d-none');
                setExprDisabled(true);
            })
            .catch(err => console.error('SignalR connection error:', err));
    }

    function stopLiveTail() {
        if (connection) connection.stop();
        liveTailActive = false;
        btnToggle.textContent = 'Start Live Tail';
        btnToggle.classList.replace('btn-outline-danger', 'btn-outline-secondary');
        indicator.classList.add('d-none');
        setExprDisabled(false);
    }

    function formatTs(ts) {
        const d = new Date(ts);
        return d.getFullYear() + '-' +
            pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + ' ' +
            pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
    }

    function pad(n) { return n < 10 ? '0' + n : '' + n; }

    function levelClass(level) {
        const map = {
            Fatal: 'lv-badge-fatal', Error: 'lv-badge-error',
            Warning: 'lv-badge-warning', Information: 'lv-badge-information',
            Debug: 'lv-badge-debug', Verbose: 'lv-badge-verbose'
        };
        return map[level] ?? 'lv-badge-verbose';
    }

    // Saved filter helper
    window.lvApplySavedFilter = function (expr) {
        if (!expr) return;
        const url = new URL(window.location.href);
        url.searchParams.set('expr', expr);
        window.location = url.toString();
    };
})();
