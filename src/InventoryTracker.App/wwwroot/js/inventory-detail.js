/* Inventory item detail/edit modal */

let _currentItemId = null;
let _needsReload = false;
let _detailModal = null;
let _editModal = null;
let _editItemId = null;
let _canWrite = false;

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
    if (_canWrite) {
        var editBtn = document.getElementById('btn-switch-edit');
        editBtn.onclick = function(e) { e.preventDefault(); openEditModal(s.id); };
        editBtn.classList.remove('d-none');
    }

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

    if (isReusable) {
        buildActiveCheckouts(s.activeCheckouts || []);
        buildLostCheckouts(s.lostCheckouts || []);
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

async function handleConsume() {
    const qty   = parseInt(document.getElementById('consume-qty').value) || 1;
    const notes = document.getElementById('consume-notes').value.trim();
    const res = await apiPost('ConsumeItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('consume-notes').value = '';
        document.getElementById('consume-qty').value = '1';
        showAlert('modal-alert', `Consumed ${qty}.`, 'success');
        _needsReload = true;
        await refreshModal();
    } else {
        showAlert('modal-alert', res.error || 'Consume failed.', 'danger');
    }
}

async function handleRestock() {
    const qty   = parseInt(document.getElementById('consume-qty').value) || 1;
    const notes = document.getElementById('consume-notes').value.trim();
    const res = await apiPost('RestockItem', { itemId: _currentItemId, quantity: qty, notes });
    if (res.success) {
        document.getElementById('consume-notes').value = '';
        document.getElementById('consume-qty').value = '1';
        showAlert('modal-alert', `Restocked ${qty}.`, 'success');
        _needsReload = true;
        await refreshModal();
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

function esc(str) {
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

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
                id:              _editItemId,
                name,
                quantity:        document.getElementById('edit-quantity').value,
                minimumQuantity: document.getElementById('edit-min-quantity').value,
                sku:             document.getElementById('edit-sku').value.trim(),
                location:        document.getElementById('edit-location').value.trim(),
                categoryId:      document.getElementById('edit-category').value,
                expiryDate:      document.getElementById('edit-expiry').value,
                scanWarning:     document.getElementById('edit-scan-warning').value.trim(),
                description:     document.getElementById('edit-description').value.trim(),
                __RequestVerificationToken: token
            }).toString()
        });
        const data = await res.json();
        if (data.success) {
            _needsReload = true;
            _editModal.hide();
            openItemModal(_editItemId);
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
