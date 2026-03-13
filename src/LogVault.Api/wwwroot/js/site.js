/* ============================================================
   LogVault – site.js
   Global JS: dark mode, fetch helper, clipboard
   ============================================================ */

// ----- Dark mode -----
(function () {
    const STORAGE_KEY = 'logvault-theme';
    const html = document.documentElement;
    const darkIcon = document.getElementById('darkIcon');
    const lightIcon = document.getElementById('lightIcon');

    function applyTheme(isDark) {
        html.setAttribute('data-bs-theme', isDark ? 'dark' : 'light');
        if (darkIcon) darkIcon.style.display = isDark ? 'none' : '';
        if (lightIcon) lightIcon.style.display = isDark ? '' : 'none';
    }

    // Read saved preference or system preference
    const saved = localStorage.getItem(STORAGE_KEY);
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const isDark = saved !== null ? saved === 'dark' : prefersDark;
    applyTheme(isDark);

    document.addEventListener('DOMContentLoaded', function () {
        applyTheme(isDark); // re-apply after DOM ready to sync icons

        const btn = document.getElementById('darkModeToggle');
        if (btn) {
            btn.addEventListener('click', function () {
                const currentlyDark = html.getAttribute('data-bs-theme') === 'dark';
                const next = !currentlyDark;
                applyTheme(next);
                localStorage.setItem(STORAGE_KEY, next ? 'dark' : 'light');
            });
        }

        // Watch OS preference changes
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
                if (localStorage.getItem(STORAGE_KEY) === null) {
                    applyTheme(e.matches);
                }
            });
        }
    });
})();

// ----- Fetch helper (attaches CSRF token) -----
window.lvFetch = function (url, options) {
    options = options || {};
    options.headers = options.headers || {};
    const csrfMeta = document.querySelector('meta[name="csrf-token"]');
    if (csrfMeta && options.method && options.method.toUpperCase() !== 'GET') {
        options.headers['X-CSRF-TOKEN'] = csrfMeta.content;
    }
    return fetch(url, options);
};

// ----- Clipboard helper -----
window.lvCopy = function (text, btn) {
    navigator.clipboard.writeText(text).then(function () {
        if (btn) {
            const orig = btn.textContent;
            btn.textContent = 'Copied!';
            btn.disabled = true;
            setTimeout(function () {
                btn.textContent = orig;
                btn.disabled = false;
            }, 2000);
        }
    });
};
