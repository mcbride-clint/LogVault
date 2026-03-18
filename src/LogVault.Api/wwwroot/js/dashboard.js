// Dashboard widget loader and management
(function () {
    const QUERY_TYPES = ['QueryList', 'QueryTrend'];
    const LIMIT_TYPES = ['ErrorList', 'TopApplications', 'QueryList'];

    // Cache for saved filters fetched from the API
    let _filtersCache = null;

    async function fetchFilters() {
        if (!_filtersCache) {
            const res = await fetch('/api/savedfilters');
            const list = await res.json();
            _filtersCache = Object.fromEntries(list.map(f => [String(f.id), f.filterJson]));
        }
        return _filtersCache;
    }

    async function resolveFilter(filterId) {
        const cache = await fetchFilters();
        return cache[String(filterId)] ?? '';
    }

    // ── Widget rendering ───────────────────────────────────────────────────────

    function loadWidgets() {
        document.querySelectorAll('[data-widget-type]').forEach(card => {
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

                case 'QueryList':
                    resolveFilter(config.savedFilterId)
                        .then(expr => {
                            const url = `/api/logs?expr=${encodeURIComponent(expr)}&from=${from}&to=${to}&pageSize=${limit}&sort=Timestamp&desc=true`;
                            return fetch(url).then(r => r.json()).then(data => {
                                const items = data.items ?? [];
                                badge.textContent = (data.totalCount ?? 0).toLocaleString();
                                content.innerHTML = items
                                    .map(e => `<div class="small text-truncate lv-log-row" style="cursor:pointer" onclick="window.location='/logs/${e.id}'">`
                                        + `<span class="text-muted me-1">${new Date(e.timestamp).toLocaleTimeString()}</span>`
                                        + `${e.renderedMessage ?? ''}</div>`)
                                    .join('') || '<span class="text-muted">No events</span>';
                            });
                        })
                        .catch(() => { content.textContent = 'Failed to load'; });
                    break;

                case 'QueryTrend':
                    resolveFilter(config.savedFilterId)
                        .then(expr => {
                            const url = `/api/logs/stats?expr=${encodeURIComponent(expr)}&from=${from}&to=${to}`;
                            return fetch(url).then(r => r.json()).then(data => {
                                const hourly = data.byHour ?? [];
                                const total = hourly.reduce((s, h) => s + h.count, 0);
                                const max = Math.max(1, ...hourly.map(h => h.count));
                                badge.textContent = total.toLocaleString();
                                if (!hourly.length) {
                                    content.innerHTML = '<span class="text-muted">No events</span>';
                                    return;
                                }
                                const bars = hourly.map(h => {
                                    const pct = Math.round((h.count / max) * 100);
                                    const label = new Date(h.hour).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                                    return `<div style="flex:1;min-width:2px;background:var(--bs-primary);opacity:0.75;height:${pct}%" title="${label}: ${h.count}"></div>`;
                                }).join('');
                                const firstLabel = new Date(hourly[0].hour).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                                content.innerHTML =
                                    `<div class="d-flex align-items-end" style="height:70px;gap:1px">${bars}</div>` +
                                    `<div class="d-flex justify-content-between text-muted mt-1" style="font-size:0.65rem"><span>${firstLabel}</span><span>now</span></div>`;
                            });
                        })
                        .catch(() => { content.textContent = 'Failed to load'; });
                    break;

                default:
                    content.textContent = 'Unknown widget type: ' + type;
            }
        });
    }

    // ── Widget management ──────────────────────────────────────────────────────

    const container = document.getElementById('widgets-container');
    const dashboardId = container?.dataset.dashboardId;

    function getWidgets() {
        return JSON.parse(container.dataset.widgetsJson || '[]');
    }

    function setWidgets(widgets) {
        container.dataset.widgetsJson = JSON.stringify(widgets);
    }

    async function saveWidgets(widgets) {
        const payload = widgets.map((w, i) => ({
            widgetType: w.widgetType,
            title: w.title,
            sortOrder: i,
            configJson: w.configJson
        }));
        await fetch(`/api/dashboards/${dashboardId}/widgets`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ widgets: payload })
        });
        location.reload();
    }

    // Modal elements
    const modalEl = document.getElementById('widget-modal');
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;
    const wmEditId = document.getElementById('wm-edit-id');
    const wmTitle = document.getElementById('wm-title');
    const wmType = document.getElementById('wm-type');
    const wmFilterId = document.getElementById('wm-filter-id');
    const wmHours = document.getElementById('wm-hours');
    const wmLimit = document.getElementById('wm-limit');
    const wmSave = document.getElementById('wm-save');
    const wmModalTitle = document.getElementById('widget-modal-title');

    function updateModalFields() {
        const type = wmType?.value ?? '';
        const isQuery = QUERY_TYPES.includes(type);
        const hasLimit = LIMIT_TYPES.includes(type);
        document.querySelectorAll('.wm-query-field').forEach(el => el.classList.toggle('d-none', !isQuery));
        document.querySelectorAll('.wm-limit-field').forEach(el => el.classList.toggle('d-none', !hasLimit));
    }

    async function populateFilterDropdown(selectedId) {
        if (!wmFilterId) return;
        try {
            const filters = await fetchFilters();
            const entries = Object.entries(filters);
            if (!entries.length) {
                wmFilterId.innerHTML = '<option value="">No saved queries</option>';
                return;
            }
            wmFilterId.innerHTML = entries.map(([id, expr]) =>
                `<option value="${id}"${String(id) === String(selectedId) ? ' selected' : ''}>${expr.length > 60 ? expr.slice(0, 57) + '…' : expr} (id:${id})</option>`
            ).join('');
        } catch {
            wmFilterId.innerHTML = '<option value="">Failed to load queries</option>';
        }
    }

    function openAddModal() {
        if (!modal) return;
        wmEditId.value = '';
        wmTitle.value = '';
        wmType.value = 'ByLevel';
        wmHours.value = '24';
        wmLimit.value = '10';
        wmModalTitle.textContent = 'Add Widget';
        updateModalFields();
        populateFilterDropdown(null);
        modal.show();
    }

    function openEditModal(widgetId) {
        if (!modal) return;
        const widgets = getWidgets();
        const w = widgets.find(x => String(x.id) === String(widgetId));
        if (!w) return;
        const config = JSON.parse(w.configJson || '{}');
        wmEditId.value = w.id;
        wmTitle.value = w.title;
        wmType.value = w.widgetType;
        wmHours.value = config.hours ?? 24;
        wmLimit.value = config.limit ?? 10;
        wmModalTitle.textContent = 'Edit Widget';
        updateModalFields();
        populateFilterDropdown(config.savedFilterId);
        modal.show();
    }

    if (wmType) wmType.addEventListener('change', updateModalFields);

    document.getElementById('btn-add-widget')?.addEventListener('click', openAddModal);

    document.getElementById('widgets-container')?.addEventListener('click', e => {
        const editBtn = e.target.closest('.lv-widget-edit');
        const removeBtn = e.target.closest('.lv-widget-remove');
        if (editBtn) {
            const card = editBtn.closest('[data-widget-id]');
            if (card) openEditModal(card.dataset.widgetId);
        }
        if (removeBtn) {
            const card = removeBtn.closest('[data-widget-id]');
            if (card && confirm('Remove this widget?')) {
                const widgets = getWidgets().filter(w => String(w.id) !== String(card.dataset.widgetId));
                saveWidgets(widgets);
            }
        }
    });

    wmSave?.addEventListener('click', () => {
        const editId = wmEditId.value;
        const type = wmType.value;
        const title = wmTitle.value.trim() || type;
        const hours = parseInt(wmHours.value, 10) || 24;
        const limit = parseInt(wmLimit.value, 10) || 10;
        const isQuery = QUERY_TYPES.includes(type);
        const filterId = isQuery ? wmFilterId.value : undefined;

        const config = { hours };
        if (LIMIT_TYPES.includes(type)) config.limit = limit;
        if (isQuery && filterId) config.savedFilterId = parseInt(filterId, 10);

        const widgets = getWidgets();
        if (editId) {
            const idx = widgets.findIndex(w => String(w.id) === String(editId));
            if (idx !== -1) {
                widgets[idx] = { ...widgets[idx], widgetType: type, title, configJson: JSON.stringify(config) };
            }
        } else {
            const nextSort = widgets.length > 0 ? Math.max(...widgets.map(w => w.sortOrder)) + 1 : 0;
            widgets.push({ id: 0, widgetType: type, title, sortOrder: nextSort, configJson: JSON.stringify(config) });
        }
        modal?.hide();
        saveWidgets(widgets);
    });

    // ── Init ───────────────────────────────────────────────────────────────────
    loadWidgets();
})();
