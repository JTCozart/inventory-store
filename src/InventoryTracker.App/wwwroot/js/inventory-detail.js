/* Inventory item detail/edit modal */

let _currentItemId = null;
let _needsReload = false;
let _detailModal = null;
let _canWrite = false;

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
            if (_needsReload) location.reload();
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
    const isReusable = s.itemType === 'Reusable';

    // Header
    document.getElementById('modal-item-name').textContent = s.name;
    const typeBadge = document.getElementById('modal-type-badge');
    typeBadge.textContent = s.itemType;
    typeBadge.className = `badge ms-2 ${isReusable ? 'bg-info' : 'bg-secondary'}`;
    const stockBadge = document.getElementById('modal-stock-badge');
    stockBadge.classList.toggle('d-none', !s.isLowStock);
    if (_canWrite) document.getElementById('btn-switch-edit').classList.remove('d-none');

    // Stats
    document.getElementById('modal-stat-qty').textContent = s.quantity;
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

    // Details
    document.getElementById('modal-detail-location').textContent = s.location || '-';
    document.getElementById('modal-detail-sku').textContent = s.sku || '-';

    const descDt = document.getElementById('lbl-desc');
    const descDd = document.getElementById('modal-detail-desc');
    const warnDt = document.getElementById('lbl-warn');
    const warnDd = document.getElementById('modal-detail-warn');

    if (s.description) {
        descDd.textContent = s.description;
        descDt.classList.remove('d-none');
        descDd.classList.remove('d-none');
    } else {
        descDt.classList.add('d-none');
        descDd.classList.add('d-none');
    }
    if (s.scanWarning) {
        warnDd.textContent = s.scanWarning;
        warnDt.classList.remove('d-none');
        warnDd.classList.remove('d-none');
    } else {
        warnDt.classList.add('d-none');
        warnDd.classList.add('d-none');
    }

    // Actions
    document.getElementById('actions-reusable').classList.toggle('d-none', !isReusable);
    document.getElementById('actions-consumable').classList.toggle('d-none', isReusable);

    if (isReusable) buildActiveCheckouts(s.activeCheckouts || []);


    // Pre-fill edit form
    document.getElementById('edit-id').value = s.id;
    document.getElementById('edit-name').value = s.name;
    document.getElementById('edit-quantity').value = s.quantity;
    document.getElementById('edit-min-qty').value = s.minimumQuantity;
    document.getElementById('edit-location').value = s.location || '';
    document.getElementById('edit-sku').value = s.sku || '';
    document.getElementById('edit-scan-warning').value = s.scanWarning || '';
    document.getElementById('edit-description').value = s.description || '';

    const typeDisplay = document.getElementById('edit-type-display');
    if (s.itemType === 'Reusable') {
        typeDisplay.innerHTML = '<span class="badge bg-info"><i class="bi bi-arrow-repeat me-1"></i>Reusable</span>';
    } else {
        typeDisplay.innerHTML = '<span class="badge bg-secondary"><i class="bi bi-box me-1"></i>Consumable</span>';
    }

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

function showViewMode(focus = true) {
    document.getElementById('modal-view-body').classList.remove('d-none');
    document.getElementById('modal-edit-body').classList.add('d-none');
    document.getElementById('modal-view-footer').classList.add('d-none');
    document.getElementById('modal-edit-footer').classList.add('d-none');
    if (_canWrite) document.getElementById('btn-switch-edit').classList.remove('d-none');
    clearAlert('edit-alert');
}

function showEditMode() {
    document.getElementById('modal-view-body').classList.add('d-none');
    document.getElementById('modal-edit-body').classList.remove('d-none');
    document.getElementById('modal-view-footer').classList.add('d-none');
    document.getElementById('modal-edit-footer').classList.remove('d-none');
    document.getElementById('btn-switch-edit').classList.add('d-none');
    clearAlert('modal-alert');
}

async function handleCheckOut() {
    const by  = document.getElementById('co-by').value.trim();
    const qty = parseInt(document.getElementById('co-qty').value) || 1;
    const notes = document.getElementById('co-notes').value.trim();

    if (!by) { showAlert('modal-alert', 'Please enter the name of the person checking out.', 'warning'); return; }

    const res = await apiPost('CheckOutItem', { itemId: _currentItemId, checkedOutBy: by, quantity: qty, notes });
    if (res.success) {
        document.getElementById('co-by').value = '';
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

async function handleConsume() {
    const qty   = parseInt(document.getElementById('consume-qty').value) || 1;
    const notes = document.getElementById('consume-notes').value.trim();
    const res = await apiPost('ConsumeItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('consume-notes').value = '';
        showAlert('modal-alert', `Consumed ${qty}.`, 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Consume failed.', 'danger');
    }
}

async function handleRestock(suffix) {
    const qty   = parseInt(document.getElementById(`restock-qty-${suffix}`).value) || 1;
    const notes = document.getElementById(`restock-notes-${suffix}`).value.trim();
    const res = await apiPost('RestockItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById(`restock-notes-${suffix}`).value = '';
        showAlert('modal-alert', `Restocked ${qty}.`, 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Restock failed.', 'danger');
    }
}

async function handleSaveEdit() {
    const id              = parseInt(document.getElementById('edit-id').value);
    const name            = document.getElementById('edit-name').value.trim();
    const quantity        = parseInt(document.getElementById('edit-quantity').value) || 0;
    const minimumQuantity = parseInt(document.getElementById('edit-min-qty').value) || 0;
    const location        = document.getElementById('edit-location').value.trim();
    const sku             = document.getElementById('edit-sku').value.trim();
    const scanWarning     = document.getElementById('edit-scan-warning').value.trim();
    const description     = document.getElementById('edit-description').value.trim();

    if (!name) { showAlert('edit-alert', 'Name is required.', 'warning'); return; }

    const res = await apiPost('UpdateItem', {
        id, name, quantity, description, location, sku, minimumQuantity, scanWarning
    });
    if (res.success) {
        _needsReload = true;
        showAlert('modal-alert', 'Item updated.', 'success');
        showViewMode();
        await refreshModal();
    } else {
        showAlert('edit-alert', res.error || 'Save failed.', 'danger');
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

function esc(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
