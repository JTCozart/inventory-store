// ── Shared utilities ─────────────────────────────────────────────────────────

window.escHtml = function(s) {
    return s == null ? '' : String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
};

window.showToast = function(msg, type) {
    var container = document.getElementById('toast-container');
    if (!container) return;
    var el = document.createElement('div');
    el.className = 'toast align-items-center text-bg-' + (type || 'success') + ' border-0';
    el.setAttribute('role', 'alert');
    el.innerHTML = '<div class="d-flex"><div class="toast-body">' + escHtml(msg) + '</div>' +
        '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
    container.appendChild(el);
    var bsToast = new bootstrap.Toast(el, { delay: 3500 });
    bsToast.show();
    el.addEventListener('hidden.bs.toast', function() { el.remove(); });
};

window.showFlashToast = function(msg, type) {
    try { sessionStorage.setItem('_flash', JSON.stringify({ msg: msg, type: type || 'success' })); } catch {}
};

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
    var cls = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon';
    var icon = document.getElementById('theme-icon');
    if (icon) icon.className = cls;
    var mobileIcon = document.getElementById('mobile-theme-icon');
    if (mobileIcon) mobileIcon.className = cls;
}

// ── Bootstrap DOMContentLoaded ────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', function () {
    // Show any queued flash toast from a previous page action
    try {
        var flash = sessionStorage.getItem('_flash');
        if (flash) { sessionStorage.removeItem('_flash'); var f = JSON.parse(flash); showToast(f.msg, f.type); }
    } catch {}

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
