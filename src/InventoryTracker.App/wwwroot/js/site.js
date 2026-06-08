// ── Dark mode ────────────────────────────────────────────────────────────────

window.toggleTheme = function () {
    var current = document.documentElement.getAttribute('data-bs-theme') || 'light';
    var next    = current === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', next);
    localStorage.setItem('it-theme', next);
    applyThemeButton(next);
};

function applyThemeButton(theme) {
    var icon  = document.getElementById('theme-icon');
    var label = document.getElementById('theme-label');
    if (icon)  icon.className    = theme === 'dark' ? 'bi bi-sun'   : 'bi bi-moon';
    if (label) label.textContent = theme === 'dark' ? 'Light mode'  : 'Dark mode';
}

// ── Bootstrap DOMContentLoaded ────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', function () {
    // Sync toggle button with current theme
    applyThemeButton(document.documentElement.getAttribute('data-bs-theme') || 'light');

    // Auto-dismiss alerts
    document.querySelectorAll('.alert-dismissible').forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 4000);
    });

    // Mobile sidebar toggle
    var toggle = document.getElementById('sidebarToggle');
    var close = document.getElementById('sidebarClose');
    var sidebar = document.getElementById('sidebar');
    var overlay = document.getElementById('sidebar-overlay');

    function openSidebar() {
        sidebar && sidebar.classList.add('open');
        overlay && overlay.classList.add('active');
    }

    function closeSidebar() {
        sidebar && sidebar.classList.remove('open');
        overlay && overlay.classList.remove('active');
    }

    toggle && toggle.addEventListener('click', openSidebar);
    close && close.addEventListener('click', closeSidebar);
    overlay && overlay.addEventListener('click', closeSidebar);

    // Close sidebar on nav link click (mobile)
    sidebar && sidebar.querySelectorAll('.nav-link').forEach(function (link) {
        link.addEventListener('click', closeSidebar);
    });

    // Quick SKU field — press Enter to open scanner modal
    var quickSku = document.getElementById('quick-sku');
    if (quickSku) {
        quickSku.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && this.value.trim()) {
                if (typeof openScannerModal === 'function') openScannerModal(this.value.trim());
                this.value = '';
            }
        });
    }
});
