// ── Dark mode ────────────────────────────────────────────────────────────────

window.quickScan = function () {
    if (typeof openScannerModal === 'function') {
        openScannerModal(null);
    } else {
        window.location.href = '/?scan=1';
    }
};

window.toggleMobileNav = function () {
    var nav = document.getElementById('mobile-nav');
    if (nav) nav.classList.toggle('open');
};

window.toggleTheme = function () {
    var current = document.documentElement.getAttribute('data-bs-theme') || 'light';
    var next    = current === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', next);
    localStorage.setItem('it-theme', next);
    applyThemeButton(next);
};

function applyThemeButton(theme) {
    var icon = document.getElementById('theme-icon');
    if (icon) icon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon';
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

    // Mobile nav toggle
    var mobileNav = document.getElementById('mobile-nav');
    document.querySelectorAll('#mobile-nav a').forEach(function (link) {
        link.addEventListener('click', function () {
            if (mobileNav) mobileNav.classList.remove('open');
        });
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
