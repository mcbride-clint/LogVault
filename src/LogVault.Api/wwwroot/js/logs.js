// Log Explorer — live tail via SignalR
(function () {
    let connection = null;
    let liveTailActive = false;

    const btnToggle = document.getElementById('btn-live-toggle');
    const indicator = document.getElementById('live-indicator');
    const tbody = document.getElementById('log-rows');

    if (!btnToggle) return;

    btnToggle.addEventListener('click', () => {
        if (liveTailActive) stopLiveTail();
        else startLiveTail();
    });

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
                tr.onclick = () => window.location = '/logs/' + ev.id;
                tr.innerHTML =
                    `<td class="text-nowrap small">${formatTs(ev.timestamp)}</td>` +
                    `<td><span class="badge ${levelClass(ev.level)}">${ev.level}</span></td>` +
                    `<td class="small text-truncate" style="max-width:120px">${ev.sourceApplication ?? ''}</td>` +
                    `<td class="small text-truncate" style="max-width:500px">${ev.renderedMessage ?? ''}</td>`;
                tbody.insertBefore(tr, tbody.firstChild);
            });
        });

        connection.start()
            .then(() => {
                liveTailActive = true;
                btnToggle.textContent = 'Stop Live Tail';
                btnToggle.classList.replace('btn-outline-secondary', 'btn-outline-danger');
                indicator.classList.remove('d-none');
            })
            .catch(err => console.error('SignalR connection error:', err));
    }

    function stopLiveTail() {
        if (connection) connection.stop();
        liveTailActive = false;
        btnToggle.textContent = 'Start Live Tail';
        btnToggle.classList.replace('btn-outline-danger', 'btn-outline-secondary');
        indicator.classList.add('d-none');
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
