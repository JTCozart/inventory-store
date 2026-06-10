// ── Shared utilities ─────────────────────────────────────────────────────────

window.escHtml = function(s) {
    return s == null ? '' : String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
};

// Maps a GHS pictogram name (e.g. "Flammable") to its bundled symbol path. Keep in sync with
// the server-side GhsPictograms helper.
window.ghsPictogramImg = function (name) {
    if (!name) return null;
    var n = String(name).toLowerCase();
    var map = [
        ['explos', 'GHS01'], ['flam', 'GHS02'], ['oxidi', 'GHS03'],
        ['compressed gas', 'GHS04'], ['gas cylinder', 'GHS04'], ['corros', 'GHS05'],
        ['acute tox', 'GHS06'], ['toxic', 'GHS06'], ['environ', 'GHS09'],
        ['health', 'GHS08'], ['irritant', 'GHS07'], ['harmful', 'GHS07']
    ];
    for (var i = 0; i < map.length; i++) {
        if (n.indexOf(map[i][0]) !== -1) return '/img/ghs/' + map[i][1] + '.svg';
    }
    return null;
};

// Renders GHS pictogram <img> tags from a "A; B" string into the given element (SDS module).
// Returns the number of pictograms rendered. Skips duplicates.
window.renderPictograms = function (el, pictogramsString, size) {
    if (!el) return 0;
    el.innerHTML = '';
    if (!(window.modules && window.modules.sds) || !pictogramsString) return 0;
    var px = size || 20, seen = {}, count = 0;
    pictogramsString.split(';').map(function (s) { return s.trim(); }).filter(Boolean).forEach(function (name) {
        var src = window.ghsPictogramImg(name);
        if (!src || seen[src]) return;
        seen[src] = true;
        var img = document.createElement('img');
        img.src = src; img.alt = name; img.title = name;
        img.width = px; img.height = px; img.className = 'ghs-pictogram';
        el.appendChild(img);
        count++;
    });
    return count;
};

// Sets a GHS signal-word badge (Danger / Warning) on an element, or hides it when empty.
window.applySignalBadge = function (el, signal) {
    if (!el) return;
    if (!signal) { el.classList.add('d-none'); el.textContent = ''; return; }
    el.textContent = signal;
    el.className = 'badge ' + (/danger/i.test(signal) ? 'text-bg-danger' : 'text-bg-warning');
    el.classList.remove('d-none');
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
