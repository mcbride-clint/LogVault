window.themeInterop = {
    getPreferredTheme: function () {
        const saved = localStorage.getItem('logvault-theme');
        if (saved !== null) return saved === 'dark';
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    },
    saveTheme: function (isDark) {
        localStorage.setItem('logvault-theme', isDark ? 'dark' : 'light');
    },
    watchSystemTheme: function (dotNetRef) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
            // Only follow system changes when the user hasn't set a manual preference
            if (localStorage.getItem('logvault-theme') === null) {
                dotNetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches);
            }
        });
    }
};
