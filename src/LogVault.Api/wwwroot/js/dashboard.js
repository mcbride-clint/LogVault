// Dashboard widget loader
(function () {
    const widgets = document.querySelectorAll('[data-widget-type]');
    widgets.forEach(card => {
        const type = card.dataset.widgetType;
        const config = JSON.parse(card.dataset.widgetConfig || '{}');
        const hours = config.hours ?? 24;
        const limit = config.limit ?? 5;
        const content = card.querySelector('.lv-widget-content');
        const badge = card.querySelector('.lv-widget-badge');

        const from = new Date(Date.now() - hours * 3600000).toISOString();
        const to = new Date().toISOString();

        switch (type) {
            case 'ByLevel':
                fetch(`/api/logs/stats?from=${from}&to=${to}`)
                    .then(r => r.json())
                    .then(data => {
                        const counts = data.countsByLevel ?? {};
                        const total = Object.values(counts).reduce((a, b) => a + b, 0);
                        badge.textContent = total.toLocaleString();
                        content.innerHTML = Object.entries(counts)
                            .filter(([, v]) => v > 0)
                            .sort(([, a], [, b]) => b - a)
                            .map(([k, v]) => `<div class="d-flex justify-content-between"><span>${k}</span><strong>${v.toLocaleString()}</strong></div>`)
                            .join('');
                        if (!content.innerHTML) content.innerHTML = '<span class="text-muted">No events</span>';
                    })
                    .catch(() => { content.textContent = 'Failed to load'; });
                break;

            case 'EventRate':
                fetch(`/api/logs/stats?from=${from}&to=${to}`)
                    .then(r => r.json())
                    .then(data => {
                        const total = data.totalCount ?? 0;
                        const rate = (total / hours).toFixed(1);
                        badge.textContent = total.toLocaleString();
                        content.innerHTML = `<div class="display-6 text-center">${rate}</div><div class="text-center text-muted small">events/hr</div>`;
                    })
                    .catch(() => { content.textContent = 'Failed to load'; });
                break;

            case 'TopApplications':
                fetch(`/api/logs/stats/top-applications?from=${from}&to=${to}&limit=${limit}`)
                    .then(r => r.json())
                    .then(data => {
                        badge.textContent = (data.length ?? 0) + ' apps';
                        content.innerHTML = (data ?? [])
                            .map(a => `<div class="d-flex justify-content-between"><span class="text-truncate me-2">${a.application ?? '—'}</span><strong>${a.count?.toLocaleString()}</strong></div>`)
                            .join('') || '<span class="text-muted">No data</span>';
                    })
                    .catch(() => { content.textContent = 'Failed to load'; });
                break;

            case 'ErrorList':
                fetch(`/api/logs?level=Error&from=${from}&to=${to}&pageSize=${limit}&sort=Timestamp&desc=true`)
                    .then(r => r.json())
                    .then(data => {
                        const items = data.items ?? [];
                        badge.textContent = (data.totalCount ?? 0).toLocaleString();
                        content.innerHTML = items
                            .map(e => `<div class="small text-truncate lv-log-row" style="cursor:pointer" onclick="window.location='/logs/${e.id}'">`
                                + `<span class="text-muted me-1">${new Date(e.timestamp).toLocaleTimeString()}</span>`
                                + `${e.renderedMessage ?? ''}</div>`)
                            .join('') || '<span class="text-muted">No errors</span>';
                    })
                    .catch(() => { content.textContent = 'Failed to load'; });
                break;

            default:
                content.textContent = 'Unknown widget type: ' + type;
        }
    });
})();
