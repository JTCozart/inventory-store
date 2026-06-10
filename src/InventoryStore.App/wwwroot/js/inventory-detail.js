/* Inventory item detail/edit modal */

let _currentItemId = null;
let _needsReload = false;
let _detailModal = null;
let _editModal = null;
let _editItemId = null;
let _canWrite = false;
let _currentItemData = null;
let _currentSdsRows = [];

const _SWATCH_COLORS = ['#0d6efd','#198754','#dc3545','#ffc107','#fd7e14','#6f42c1','#d63384','#0dcaf0','#20c997','#6c757d','#212529'];

document.addEventListener('DOMContentLoaded', () => {
    // Row click -> open modal in view mode
    document.querySelectorAll('.inventory-row').forEach(row => {
        row.addEventListener('click', () => openItemModal(parseInt(row.dataset.itemId)));
    });

    // Close -> reload if data changed
    const el = document.getElementById('itemDetailModal');
    if (el) {
        _detailModal = new bootstrap.Modal(el);
        _canWrite = el.dataset.canWrite === '1';
        el.addEventListener('hidden.bs.modal', () => {
            if (_needsReload) { showFlashToast('Changes saved.'); location.reload(); }
        });
    }
});

async function openItemModal(id) {
    _currentItemId = id;
    _needsReload = false;

    setLoading(true);
    showViewMode(false);

    _detailModal.show();
    await refreshModal();
}

async function refreshModal() {
    try {
        const res = await fetch(`/Inventory?handler=ItemStatus&id=${_currentItemId}`);
        if (!res.ok) throw new Error('Failed to load item');
        const status = await res.json();
        populateView(status);
        setLoading(false);
    } catch (e) {
        showAlert('modal-alert', 'Failed to load item details.', 'danger');
        setLoading(false);
    }
}

function populateView(s) {
    _currentItemData = s;
    const isReusable = s.itemType === 'Reusable';

    // Header
    document.getElementById('modal-item-name').textContent = s.name;
    const typeBadge = document.getElementById('modal-type-badge');
    typeBadge.textContent = s.itemType;
    typeBadge.className = `badge ms-2 ${isReusable ? 'bg-info' : 'bg-secondary'}`;
    // Title stock badge: red when nothing is available, yellow when low.
    const stockBadge = document.getElementById('modal-stock-badge');
    const availForBadge = isReusable ? s.availableQuantity : s.quantity;
    if (availForBadge <= 0) {
        stockBadge.className = 'badge bg-danger ms-1';
        stockBadge.textContent = 'Out of Stock';
        stockBadge.classList.remove('d-none');
    } else if (s.isLowStock) {
        stockBadge.className = 'badge bg-warning text-dark ms-1';
        stockBadge.textContent = 'Low Stock';
        stockBadge.classList.remove('d-none');
    } else {
        stockBadge.classList.add('d-none');
    }
    if (_canWrite) {
        var editBtn = document.getElementById('btn-switch-edit');
        editBtn.onclick = function(e) {
            e.preventDefault();
            // Close the view modal first so the two never stack, then open edit.
            const detailEl = document.getElementById('itemDetailModal');
            detailEl.addEventListener('hidden.bs.modal', function onHidden() {
                detailEl.removeEventListener('hidden.bs.modal', onHidden);
                openEditModal(s.id);
            });
            _detailModal.hide();
        };
        editBtn.classList.remove('d-none');
    }

    // Stats — labelled differently per type. For consumables the single
    // quantity is "In Stock"; reusables break it into Total/Available/Out/Lost.
    document.getElementById('modal-stat-qty').textContent = s.quantity;
    document.getElementById('modal-stat-qty-label').textContent = isReusable ? 'Total' : 'In Stock';
    document.getElementById('modal-stat-min').textContent = s.minimumQuantity;

    const availCol = document.getElementById('stat-available-col');
    const outCol   = document.getElementById('stat-out-col');
    const lostCol  = document.getElementById('stat-lost-col');
    availCol.classList.toggle('d-none', !isReusable);
    outCol.classList.toggle('d-none', !isReusable);
    lostCol.classList.toggle('d-none', !isReusable);
    if (isReusable) {
        document.getElementById('modal-stat-available').textContent = s.availableQuantity;
        document.getElementById('modal-stat-out').textContent = s.checkedOutCount;
        document.getElementById('modal-stat-lost').textContent = s.lostCount;
    }

    // Colour the headline stock number by health (Available for reusables, In Stock for consumables).
    const available = isReusable ? s.availableQuantity : s.quantity;
    const headlineEl = isReusable
        ? document.getElementById('modal-stat-available')
        : document.getElementById('modal-stat-qty');
    headlineEl.classList.remove('text-success', 'text-warning', 'text-danger');
    let healthClass;
    if (available <= 0)    healthClass = 'text-danger';
    else if (s.isLowStock) healthClass = 'text-warning';
    else                   healthClass = 'text-success';
    // Reusables always tint the headline; consumables stay neutral when healthy.
    if (isReusable || available <= 0 || s.isLowStock) headlineEl.classList.add(healthClass);

    // Product image drives the two-column top layout: when present the image takes
    // the left column and info fills the remaining 8 cols; otherwise info is full width.
    const imageCol  = document.getElementById('modal-image-col');
    const infoCol   = document.getElementById('modal-info-col');
    const itemImage = document.getElementById('modal-item-image');
    if (s.metadataImageUrl) {
        itemImage.src = s.metadataImageUrl;
        imageCol.classList.remove('d-none');
        infoCol.classList.add('col-md-8');
    } else {
        imageCol.classList.add('d-none');
        infoCol.classList.remove('col-md-8');
    }

    // Details
    document.getElementById('modal-detail-location').textContent = s.location || '-';
    document.getElementById('modal-detail-sku').textContent = s.sku || '-';

    // Category — show as a coloured pill when set
    const catDt = document.getElementById('lbl-category');
    const catDd = document.getElementById('modal-detail-category');
    if (s.categoryName) {
        const color = s.categoryColor || '#6c757d';
        catDd.innerHTML = `<span class="badge rounded-pill" style="background:${esc(color)}">${esc(s.categoryName)}</span>`;
        catDt.classList.remove('d-none');
        catDd.classList.remove('d-none');
    } else {
        catDt.classList.add('d-none');
        catDd.classList.add('d-none');
    }

    // Expiry — a consumable concern; colour-code expired / expiring soon
    const expDt = document.getElementById('lbl-expiry');
    const expDd = document.getElementById('modal-detail-expiry');
    if (!isReusable && s.expiryDate) {
        expDd.innerHTML = formatExpiry(s.expiryDate);
        expDt.classList.remove('d-none');
        expDd.classList.remove('d-none');
    } else {
        expDt.classList.add('d-none');
        expDd.classList.add('d-none');
    }

    const descBlock = document.getElementById('modal-desc-block');
    const descDd    = document.getElementById('modal-detail-desc');
    if (s.description) {
        descDd.textContent = s.description;
        descBlock.classList.remove('d-none');
    } else {
        descBlock.classList.add('d-none');
    }

    // Scan warning is safety-relevant: surface it as a banner at the top, not a quiet row.
    const warnBanner = document.getElementById('modal-warning-banner');
    const warnText   = document.getElementById('modal-warning-text');
    if (s.scanWarning) {
        warnText.textContent = s.scanWarning;
        warnBanner.classList.remove('d-none');
    } else {
        warnBanner.classList.add('d-none');
    }

    // Actions
    document.getElementById('actions-reusable').classList.toggle('d-none', !isReusable);
    document.getElementById('actions-consumable').classList.toggle('d-none', isReusable);

    if (isReusable) {
        buildActiveCheckouts(s.activeCheckouts || []);
        buildLostCheckouts(s.lostCheckouts || []);
    }

    // SDS module: reveal the "View SDS Info" button only when this item has data attached.
    setupViewSds(s.id);

    document.getElementById('modal-content').classList.remove('d-none');
}

function buildActiveCheckouts(checkouts) {
    const list = document.getElementById('active-checkouts-list');
    if (!checkouts.length) {
        list.innerHTML = '<p class="text-muted small">No active checkouts.</p>';
        return;
    }
    list.innerHTML = checkouts.map(c => {
        const daysOut = Math.floor((Date.now() - new Date(c.checkedOutAt)) / 86400000);
        const dayLabel = daysOut === 0 ? 'today' : daysOut === 1 ? '1 day ago' : `${daysOut} days ago`;
        return `
        <div class="border rounded p-2 mb-2">
            <div class="d-flex justify-content-between align-items-start flex-wrap gap-2">
                <div>
                    <strong>${esc(c.checkedOutBy)}</strong>
                    <span class="badge bg-secondary ms-1">x${c.quantity}</span>
                    <span class="text-muted small ms-2">${dayLabel}</span>
                    ${c.notes ? `<span class="text-muted small ms-2">${esc(c.notes)}</span>` : ''}
                </div>
                ${_canWrite ? `
                <div class="d-flex gap-2 align-items-center flex-wrap">
                    <input type="text" id="ci-notes-${c.id}" class="form-control form-control-sm"
                           style="width:140px" placeholder="Notes">
                    <button class="btn btn-sm btn-outline-success"
                            onclick="handleCheckIn(${c.id})">
                        <i class="bi bi-box-arrow-in-down me-1"></i>Check In
                    </button>
                    <button class="btn btn-sm btn-outline-danger"
                            onclick="handleMarkLost(${c.id})">
                        <i class="bi bi-x-circle me-1"></i>Mark Lost
                    </button>
                </div>` : ''}
            </div>
        </div>`;
    }).join('');
}

function buildLostCheckouts(checkouts) {
    const section = document.getElementById('lost-checkouts-section');
    if (!section) return;
    if (!checkouts.length) {
        section.classList.add('d-none');
        return;
    }
    section.classList.remove('d-none');
    const list = document.getElementById('lost-checkouts-list');
    list.innerHTML = checkouts.map(c => {
        const lostAt = c.checkedInAt ? new Date(c.checkedInAt).toLocaleDateString() : '-';
        return `
        <div class="border rounded p-2 mb-2 border-danger-subtle">
            <div class="d-flex justify-content-between align-items-start flex-wrap gap-2">
                <div>
                    <strong>${esc(c.checkedOutBy)}</strong>
                    <span class="badge bg-secondary ms-1">x${c.quantity}</span>
                    <span class="text-muted small ms-2">lost ${lostAt}</span>
                    ${c.notes ? `<span class="text-muted small ms-2">${esc(c.notes)}</span>` : ''}
                </div>
                ${_canWrite ? `
                <div class="d-flex gap-2 align-items-center flex-wrap">
                    <input type="text" id="found-notes-${c.id}" class="form-control form-control-sm"
                           style="width:140px" placeholder="Notes">
                    <button class="btn btn-sm btn-outline-success"
                            onclick="handleMarkFound(${c.id})">
                        <i class="bi bi-check-circle me-1"></i>Mark Found
                    </button>
                </div>` : ''}
            </div>
        </div>`;
    }).join('');
}

function showViewMode() {
    document.getElementById('modal-view-body').classList.remove('d-none');
    document.getElementById('modal-view-footer').classList.add('d-none');
}

async function handleCheckOut() {
    const by       = document.getElementById('co-by').value.trim();
    const clientId = parseInt(document.getElementById('co-client-id').value) || null;
    const qty      = parseInt(document.getElementById('co-qty').value) || 1;
    const notes    = document.getElementById('co-notes').value.trim();
    const err      = document.getElementById('co-client-err');

    if (!by) {
        err.classList.remove('d-none');
        document.getElementById('co-by').focus();
        return;
    }
    err.classList.add('d-none');

    const res = await apiPost('CheckOutItem', { itemId: _currentItemId, checkedOutBy: by, clientId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('co-by').value = '';
        document.getElementById('co-client-id').value = '';
        document.getElementById('co-client-dd').classList.add('d-none');
        document.getElementById('co-notes').value = '';
        document.getElementById('co-qty').value = '1';
        showAlert('modal-alert', 'Checked out successfully.', 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Check out failed.', 'danger');
    }
}

async function handleCheckIn(recordId) {
    const notes = (document.getElementById(`ci-notes-${recordId}`)?.value || '').trim();
    const res = await apiPost('CheckInItem', { recordId, notes });
    if (res.success) {
        showAlert('modal-alert', 'Checked in successfully.', 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Check in failed.', 'danger');
    }
}

async function handleMarkLost(recordId) {
    const notes = (document.getElementById(`ci-notes-${recordId}`)?.value || '').trim();
    const res = await apiPost('MarkLostItem', { recordId, notes });
    if (res.success) {
        showAlert('modal-alert', 'Marked as lost.', 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Action failed.', 'danger');
    }
}

async function handleMarkFound(recordId) {
    const notes = (document.getElementById(`found-notes-${recordId}`)?.value || '').trim();
    const res = await apiPost('MarkFoundItem', { recordId, notes });
    if (res.success) {
        showAlert('modal-alert', 'Marked as found.', 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Action failed.', 'danger');
    }
}

// Consume (−) and restock (+) share one quantity/notes control in the detail modal.
async function handleConsume() {
    const qty   = parseInt(document.getElementById('adjust-qty').value) || 1;
    const notes = document.getElementById('adjust-notes').value.trim();
    const res = await apiPost('ConsumeItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('adjust-notes').value = '';
        document.getElementById('adjust-qty').value = '1';
        _needsReload = true;
        await refreshModal();
        // Glow the stock number red instead of a banner that shifts the buttons.
        flashStat('modal-stat-qty', 'down');
    } else {
        showAlert('modal-alert', res.error || 'Consume failed.', 'danger');
    }
}

async function handleRestock() {
    const qty   = parseInt(document.getElementById('adjust-qty').value) || 1;
    const notes = document.getElementById('adjust-notes').value.trim();
    const res = await apiPost('RestockItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('adjust-notes').value = '';
        document.getElementById('adjust-qty').value = '1';
        _needsReload = true;
        await refreshModal();
        // Glow the stock number green instead of a banner that shifts the buttons.
        flashStat('modal-stat-qty', 'up');
    } else {
        showAlert('modal-alert', res.error || 'Restock failed.', 'danger');
    }
}


async function apiPost(handler, data) {
    const token = document.getElementById('af-token').value;
    const body  = new URLSearchParams(data);
    const res   = await fetch(`/Inventory?handler=${handler}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': token
        },
        body: body.toString()
    });
    if (!res.ok) return { success: false, error: `Server error (${res.status})` };
    try { return await res.json(); } catch { return { success: false, error: 'Invalid response' }; }
}

function setLoading(show) {
    document.getElementById('modal-loading').classList.toggle('d-none', !show);
    if (show) document.getElementById('modal-content').classList.add('d-none');
}

function showAlert(elId, msg, type) {
    const el = document.getElementById(elId);
    if (!el) return;
    el.className = `alert alert-${type} mb-3`;
    el.textContent = msg;
    if (type === 'success') setTimeout(() => clearAlert(elId), 3000);
}

function clearAlert(elId) {
    const el = document.getElementById(elId);
    if (el) el.className = 'alert d-none mb-3';
}

// Briefly pulse a stat number to signal a change: green for an increase, red for a decrease.
function flashStat(elId, direction) {
    const el = document.getElementById(elId);
    if (!el) return;
    const cls = direction === 'up' ? 'stat-flash-up' : 'stat-flash-down';
    el.classList.remove('stat-flash-up', 'stat-flash-down');
    void el.offsetWidth; // force reflow so the animation restarts on repeat clicks
    el.classList.add(cls);
    setTimeout(() => el.classList.remove(cls), 1000);
}

function esc(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// Render an expiry date (yyyy-MM-dd) with a colour-coded badge when it is
// expired or expiring within 30 days, otherwise plain text.
function formatExpiry(dateStr) {
    const d = new Date(dateStr + 'T00:00:00');
    if (isNaN(d)) return esc(dateStr);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const daysLeft = Math.round((d - today) / 86400000);
    const pretty = d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
    if (daysLeft < 0) {
        return `<span class="badge bg-danger">Expired</span> <span class="text-muted small">${esc(pretty)}</span>`;
    }
    if (daysLeft <= 30) {
        return `<span class="badge bg-warning text-dark">Expires in ${daysLeft} day${daysLeft === 1 ? '' : 's'}</span> <span class="text-muted small">${esc(pretty)}</span>`;
    }
    return esc(pretty);
}

// ── Client typeahead for checkout ─────────────────────────────────────────────
var _coClientTimer = null;

window.onCoClientInput = function () {
    clearTimeout(_coClientTimer);
    document.getElementById('co-client-id').value = '';
    var q = (document.getElementById('co-by').value || '').trim();
    if (q.length < 1) { hideCoClientDd(); return; }
    _coClientTimer = setTimeout(function () {
        fetch('/api/clients/search?q=' + encodeURIComponent(q), { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (clients) { showCoClientDd(clients, q); })
            .catch(function () { hideCoClientDd(); });
    }, 200);
};

function showCoClientDd(clients, query) {
    var dd = document.getElementById('co-client-dd');
    var escH = function (s) { return s == null ? '' : String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); };
    var rows = (clients || []).map(function (c) {
        return '<div class="px-2 py-1" style="cursor:pointer" ' +
            'onmousedown="selectCoClient(' + c.id + ',\'' + escH(c.displayName).replace(/'/g,"&#39;") + '\')"' +
            'onmouseover="this.classList.add(\'bg-body-secondary\')" onmouseout="this.classList.remove(\'bg-body-secondary\')">' +
            escH(c.displayName) + (c.phone ? '<span class="text-muted ms-2">' + escH(c.phone) + '</span>' : '') +
            '</div>';
    });
    rows.push('<div class="px-2 py-1 text-primary" style="cursor:pointer;border-top:1px solid var(--bs-border-color)" ' +
        'onmousedown="quickCreateCoClient(\'' + escH(query).replace(/'/g,"&#39;") + '\')"' +
        'onmouseover="this.classList.add(\'bg-body-secondary\')" onmouseout="this.classList.remove(\'bg-body-secondary\')">' +
        '<i class="bi bi-person-plus me-1"></i>Create "' + escH(query) + '"</div>');
    dd.innerHTML = rows.join('');
    dd.classList.remove('d-none');
}

function hideCoClientDd() {
    document.getElementById('co-client-dd').classList.add('d-none');
}

window.selectCoClient = function (id, name) {
    document.getElementById('co-client-id').value = id;
    document.getElementById('co-by').value = name;
    hideCoClientDd();
};

window.quickCreateCoClient = function (name) {
    hideCoClientDd();
    fetch('/api/clients/quick-create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ name: name })
    })
    .then(function (r) { return r.json(); })
    .then(function (c) { selectCoClient(c.id, c.displayName); })
    .catch(function () {});
};

// ── Edit item modal ───────────────────────────────────────────────────────────

async function openEditModal(id) {
    _editItemId = id;
    const el = document.getElementById('editItemModal');
    if (!_editModal) _editModal = new bootstrap.Modal(el);

    document.getElementById('edit-loading').classList.remove('d-none');
    document.getElementById('edit-form').classList.add('d-none');
    document.getElementById('edit-alert').classList.add('d-none');
    document.getElementById('edit-save-btn').disabled = false;
    _editModal.show();

    try {
        const res = await fetch(`/Inventory?handler=EditItemData&id=${id}`);
        const data = await res.json();
        if (data.success) {
            populateEditForm(data);
            document.getElementById('edit-loading').classList.add('d-none');
            document.getElementById('edit-form').classList.remove('d-none');
        } else {
            showEditAlert(data.error || 'Failed to load item.', 'danger');
            document.getElementById('edit-loading').classList.add('d-none');
        }
    } catch {
        showEditAlert('Network error loading item.', 'danger');
        document.getElementById('edit-loading').classList.add('d-none');
    }
}

function populateEditForm(data) {
    const item = data.item;

    const typeDisplay = document.getElementById('edit-type-display');
    const isReusable = item.itemType === 'Reusable';
    typeDisplay.innerHTML = isReusable
        ? '<span class="badge bg-info fs-6"><i class="bi bi-arrow-repeat me-1"></i>Reusable</span>'
        : '<span class="badge bg-secondary fs-6"><i class="bi bi-box me-1"></i>Consumable</span>';
    typeDisplay.innerHTML += '<p class="text-muted small mt-1 mb-0">Item type cannot be changed after creation.</p>';

    // Type-aware fields: reusables track a total pool; expiry only applies to consumables.
    document.getElementById('edit-quantity-label').textContent = isReusable ? 'Total Quantity' : 'In Stock';
    document.getElementById('edit-expiry-col').classList.toggle('d-none', isReusable);

    // Safety context: for reusables, show what's currently out so the total isn't set
    // below outstanding checkouts by accident.
    const qtyHelp = document.getElementById('edit-quantity-help');
    if (isReusable) {
        const out  = item.checkedOutCount || 0;
        const lost = item.lostCount || 0;
        qtyHelp.textContent = `${out} checked out · ${lost} lost · ${item.availableQuantity} available`;
        qtyHelp.classList.remove('d-none');
    } else {
        qtyHelp.textContent = '';
        qtyHelp.classList.add('d-none');
    }

    // Reset product-match state; a fresh lookup during this edit will set it.
    const editMetaId = document.getElementById('edit-metadata-id');
    if (editMetaId) editMetaId.value = '';
    const editMatchBadge = document.getElementById('edit-match-badge');
    if (editMatchBadge) editMatchBadge.classList.add('d-none');

    document.getElementById('edit-name').value          = item.name || '';
    document.getElementById('edit-quantity').value      = item.quantity;
    document.getElementById('edit-min-quantity').value  = item.minimumQuantity;
    document.getElementById('edit-sku').value           = item.sku || '';
    document.getElementById('edit-location').value      = item.location || '';
    document.getElementById('edit-expiry').value        = item.expiryDate || '';
    document.getElementById('edit-scan-warning').value  = item.scanWarning || '';
    document.getElementById('edit-description').value   = item.description || '';

    const locList = document.getElementById('edit-location-list');
    locList.innerHTML = (data.locations || []).map(l => `<option value="${esc(l)}"></option>`).join('');

    const catSel = document.getElementById('edit-category');
    catSel.innerHTML = '<option value="">-- None --</option>' +
        (data.categories || []).map(c =>
            `<option value="${c.id}"${item.categoryId === c.id ? ' selected' : ''}>${esc(c.name)}</option>`
        ).join('');

    initEditCategoryWidget();
    setupEditSds(item);
}

function initEditCategoryWidget() {
    const swatchContainer = document.getElementById('edit-new-cat-swatches');
    if (!swatchContainer.dataset.built) {
        swatchContainer.dataset.built = '1';
        const none = `<button type="button" class="swatch-btn" data-color="" title="None" style="width:28px;height:28px;border-radius:4px;background:#f8f9fa;border:2px solid #dee2e6;"></button>`;
        const swatches = _SWATCH_COLORS.map(c =>
            `<button type="button" class="swatch-btn" data-color="${c}" title="${c}" style="width:28px;height:28px;border-radius:4px;background:${c};border:2px solid transparent;"></button>`
        ).join('');
        swatchContainer.innerHTML = none + swatches;

        const colorInput = document.getElementById('edit-new-cat-color');
        swatchContainer.querySelectorAll('.swatch-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const color = btn.dataset.color || '';
                colorInput.value = color;
                swatchContainer.querySelectorAll('.swatch-btn').forEach(b => {
                    const match = (b.dataset.color || '') === color;
                    b.style.border = match ? '2px solid #000' : '2px solid transparent';
                    if (b.dataset.color === '') b.style.border = match ? '2px solid #000' : '2px solid #dee2e6';
                });
            });
        });
    }

    const toggleBtn  = document.getElementById('edit-new-cat-btn');
    const form       = document.getElementById('edit-new-cat-form');
    const cancelBtn  = document.getElementById('edit-new-cat-cancel');
    const saveBtn    = document.getElementById('edit-new-cat-save');
    const nameInput  = document.getElementById('edit-new-cat-name');
    const colorInput = document.getElementById('edit-new-cat-color');
    const errorEl    = document.getElementById('edit-new-cat-error');
    const select     = document.getElementById('edit-category');

    function resetCatForm() {
        form.classList.add('d-none');
        nameInput.value = '';
        colorInput.value = '';
        errorEl.classList.add('d-none');
        swatchContainer.querySelectorAll('.swatch-btn').forEach(s => {
            s.style.border = s.dataset.color === '' ? '2px solid #dee2e6' : '2px solid transparent';
        });
    }

    const newToggle = toggleBtn.cloneNode(true);
    toggleBtn.replaceWith(newToggle);
    const newCancel = cancelBtn.cloneNode(true);
    cancelBtn.replaceWith(newCancel);
    const newSave = saveBtn.cloneNode(true);
    saveBtn.replaceWith(newSave);

    newToggle.addEventListener('click', () => {
        form.classList.toggle('d-none');
        if (!form.classList.contains('d-none')) nameInput.focus();
    });
    newCancel.addEventListener('click', resetCatForm);
    nameInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); newSave.click(); } });

    newSave.addEventListener('click', async () => {
        const name = nameInput.value.trim();
        if (!name) { errorEl.textContent = 'Name is required.'; errorEl.classList.remove('d-none'); return; }
        newSave.disabled = true;
        errorEl.classList.add('d-none');
        try {
            const token = document.getElementById('af-token').value;
            const res = await fetch('/Inventory?handler=CreateCategory', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: new URLSearchParams({ name, color: colorInput.value, __RequestVerificationToken: token }).toString()
            });
            if (res.ok) {
                const cat = await res.json();
                select.appendChild(new Option(cat.name, cat.id, true, true));
                select.value = cat.id;
                resetCatForm();
            } else {
                errorEl.textContent = await res.text() || 'Failed to create category.';
                errorEl.classList.remove('d-none');
            }
        } catch {
            errorEl.textContent = 'Network error.';
            errorEl.classList.remove('d-none');
        } finally {
            newSave.disabled = false;
        }
    });
}

async function saveEditItem() {
    const saveBtn = document.getElementById('edit-save-btn');
    saveBtn.disabled = true;

    const name = document.getElementById('edit-name').value.trim();
    if (!name) {
        showEditAlert('Name is required.', 'danger');
        saveBtn.disabled = false;
        return;
    }

    const token = document.getElementById('af-token').value;
    try {
        const res = await fetch('/Inventory?handler=EditItem', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({
                id:                _editItemId,
                name,
                quantity:          document.getElementById('edit-quantity').value,
                minimumQuantity:   document.getElementById('edit-min-quantity').value,
                sku:               document.getElementById('edit-sku').value.trim(),
                location:          document.getElementById('edit-location').value.trim(),
                categoryId:        document.getElementById('edit-category').value,
                expiryDate:        document.getElementById('edit-expiry').value,
                scanWarning:       document.getElementById('edit-scan-warning').value.trim(),
                description:       document.getElementById('edit-description').value.trim(),
                selectedMetadataId: document.getElementById('edit-metadata-id').value,
                __RequestVerificationToken: token
            }).toString()
        });
        const data = await res.json();
        if (data.success) {
            // Wait for the edit modal to fully close, then reopen the refreshed
            // view so the two never stack. Flag a list reload for when it closes.
            const editEl = document.getElementById('editItemModal');
            editEl.addEventListener('hidden.bs.modal', function onHidden() {
                editEl.removeEventListener('hidden.bs.modal', onHidden);
                openItemModal(_editItemId);
                _needsReload = true;
            });
            _editModal.hide();
        } else {
            showEditAlert(data.error || 'Save failed.', 'danger');
            saveBtn.disabled = false;
        }
    } catch {
        showEditAlert('Network error.', 'danger');
        saveBtn.disabled = false;
    }
}

function showEditAlert(msg, type) {
    const el = document.getElementById('edit-alert');
    el.className = `alert alert-${type} mb-3`;
    el.textContent = msg;
    el.classList.remove('d-none');
}

// ── Safety Data Sheets module ─────────────────────────────────────────────────

async function fetchItemSds(itemId) {
    try {
        const res = await fetch(`/api/sds/item/${itemId}`);
        if (!res.ok) return [];   // 404 when the module is disabled
        return await res.json();
    } catch { return []; }
}

// Shared SDS card markup, used by the edit panel, the view modal and lookups.
function sdsCardHtml(s) {
    const signal = s.signalWord
        ? `<span class="badge ${/danger/i.test(s.signalWord) ? 'bg-danger' : 'bg-warning text-dark'} me-2">${esc(s.signalWord)}</span>`
        : '';
    const cas = s.casNumber
        ? `<div class="small mb-1"><span class="text-muted">CAS:</span> ${esc(s.casNumber)}</div>` : '';
    const pics = s.pictograms
        ? `<div class="small mb-1"><span class="text-muted">Pictograms:</span> ${esc(s.pictograms)}</div>` : '';
    const hazards = s.hazardStatements
        ? `<div class="small mb-1"><span class="text-muted">Hazards:</span><br>${esc(s.hazardStatements).replace(/\n/g, '<br>')}</div>` : '';
    const precaution = s.precautionaryStatements
        ? `<div class="small mb-1"><span class="text-muted">Precautions:</span><br>${esc(s.precautionaryStatements).replace(/\n/g, '<br>')}</div>` : '';
    const link = s.sdsUrl
        ? `<a href="${esc(s.sdsUrl)}" target="_blank" class="btn btn-sm btn-outline-secondary mt-1"><i class="bi bi-box-arrow-up-right me-1"></i>Open in PubChem</a>` : '';
    return `<div class="border rounded p-2 mb-2">
        <div class="d-flex align-items-center mb-1">${signal}<strong>${esc(s.chemicalName)}</strong></div>
        ${cas}${pics}${hazards}${precaution}${link}
    </div>`;
}

// Edit panel: reveal the SDS section, prefill the chemical name, show any attached data.
function setupEditSds(item) {
    const section = document.getElementById('edit-sds-section');
    if (!section) return;
    if (!window.sdsModuleEnabled) { section.classList.add('d-none'); return; }
    section.classList.remove('d-none');
    document.getElementById('edit-sds-name').value = item.name || '';
    document.getElementById('edit-sds-status').innerHTML = '';
    document.getElementById('edit-sds-result').innerHTML = '';
    fetchItemSds(_editItemId).then(renderEditSds);
}

function renderEditSds(rows) {
    const el = document.getElementById('edit-sds-result');
    if (!el) return;
    el.innerHTML = (rows && rows.length)
        ? rows.map(sdsCardHtml).join('')
        : '<div class="text-muted small">No SDS attached yet.</div>';
}

// Look up safety data from PubChem and attach it to the current item (persists immediately).
async function lookupSds() {
    const name   = (document.getElementById('edit-sds-name').value || '').trim();
    const status = document.getElementById('edit-sds-status');
    const btn    = document.getElementById('edit-sds-lookup-btn');
    if (!name) { status.innerHTML = '<span class="text-danger">Enter a chemical name first.</span>'; return; }
    btn.disabled = true;
    status.innerHTML = '<span class="text-muted"><span class="spinner-border spinner-border-sm me-1"></span>Searching PubChem…</span>';
    try {
        const res = await fetch(`/api/sds/lookup?itemId=${_editItemId}&name=${encodeURIComponent(name)}`);
        if (!res.ok) { status.innerHTML = '<span class="text-danger">Lookup failed.</span>'; return; }
        const data = await res.json();
        if (!data.success) {
            let html = `<span class="text-warning">${esc(data.error || 'No data found.')}</span>`;
            if (data.suggestions && data.suggestions.length) {
                html += '<div class="mt-1"><span class="text-muted">Did you mean:</span> ' +
                    data.suggestions.map(s =>
                        `<button type="button" class="btn btn-sm btn-outline-secondary me-1 mb-1" data-name="${esc(s)}" onclick="sdsSuggest(this.dataset.name)">${esc(s)}</button>`
                    ).join('') + '</div>';
            } else {
                html += '<div class="text-muted small mt-1">Try a chemical name (e.g. “sodium hypochlorite”) rather than a brand or product name.</div>';
            }
            status.innerHTML = html;
            renderEditSds([]);
            return;
        }
        status.innerHTML = '<span class="text-success"><i class="bi bi-check-lg me-1"></i>Safety data attached.</span>';
        renderEditSds(data.sheets);
    } catch {
        status.innerHTML = '<span class="text-danger">Network error.</span>';
    } finally {
        btn.disabled = false;
    }
}

// Apply a PubChem suggestion and re-run the lookup with the exact chemical name.
function sdsSuggest(name) {
    document.getElementById('edit-sds-name').value = name;
    lookupSds();
}

// View modal: show the "View SDS Info" button only when this item has data attached.
function setupViewSds(itemId) {
    const block = document.getElementById('modal-sds-block');
    if (!block) return;
    _currentSdsRows = [];
    block.classList.add('d-none');
    if (!window.sdsModuleEnabled) return;
    fetchItemSds(itemId).then(rows => {
        _currentSdsRows = rows || [];
        block.classList.toggle('d-none', _currentSdsRows.length === 0);
    });
}

function viewSds() {
    const body = document.getElementById('sds-info-body');
    body.innerHTML = (_currentSdsRows && _currentSdsRows.length)
        ? _currentSdsRows.map(sdsCardHtml).join('')
        : '<div class="text-muted">No safety data attached.</div>';
    bootstrap.Modal.getOrCreateInstance(document.getElementById('sdsInfoModal')).show();
}
