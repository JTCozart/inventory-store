(function () {
    var scanBtn       = document.getElementById('scanBtn');
    var scanStopBtn   = document.getElementById('scanStopBtn');
    var scannerIdle   = document.getElementById('scanner-idle');
    var scannerActive = document.getElementById('scanner-active');
    var scannerError  = document.getElementById('scanner-error');
    var scannerProc   = document.getElementById('scanner-processing');
    var scannerResult = document.getElementById('scanner-result');
    var captureInput  = document.getElementById('capture-input');
    var videoEl       = document.getElementById('scanner-video');
    var bsModal           = null;
    var scanCtl           = null;
    var scanning          = false;
    var currentItem       = null;
    window.currentScannedSku = null;
    var pendingAddQuery   = '';
    var _scannerDidAction = false;

    // Maintenance module: build a due/overdue (or currently-out) warning from an item status.
    // Exposed on window so the Terminal reuses the exact same logic. Returns {text, cls} or null.
    window.maintenanceWarning = function (item) {
        if (!item) return null;
        if (item.maintenanceOut) {
            return { text: 'This item is currently out for maintenance.', cls: 'alert-info' };
        }
        if (!item.nextMaintenanceDue) return null;
        var d = new Date(item.nextMaintenanceDue + 'T00:00:00');
        if (isNaN(d)) return null;
        var today = new Date(); today.setHours(0, 0, 0, 0);
        var days = Math.round((d - today) / 86400000);
        var pretty = d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        if (days < 0) return { text: 'Maintenance overdue — service was due ' + pretty + '.', cls: 'alert-danger' };
        if (days <= 14) return { text: 'Maintenance due soon — service is due ' + pretty + '.', cls: 'alert-warning' };
        return null;
    };

    // Show manage-only elements if the user has write access
    if (window.canManage) {
        document.querySelectorAll('.scanner-manage-only').forEach(function (el) {
            el.classList.remove('d-none');
        });
    }

    if (!window.isSecureContext) {
        document.getElementById('scanner-idle-label').textContent = 'Tap to take a photo of the barcode';
        scanBtn.innerHTML = '<i class="bi bi-camera me-1"></i> Open Camera';
    }

    window.openScannerModal = function (sku) {
        resetScanner();
        _scannerDidAction = false;
        bsModal = bsModal || new bootstrap.Modal(document.getElementById('scannerModal'));
        bsModal.show();
        if (sku) {
            var input = document.getElementById('modal-manual-sku');
            if (input) input.value = sku.trim();
            lookupBarcode(sku.trim());
        }
    };

    window.closeModal = function () { stopLive(true); };

    document.getElementById('scannerModal').addEventListener('hidden.bs.modal', function () {
        stopLive(true);
        if (_scannerDidAction) {
            _scannerDidAction = false;
            showFlashToast('Action completed.');
            location.reload();
        }
    });

    scanBtn.addEventListener('click', function () {
        if (window.isSecureContext) { startLive(); }
        else { captureInput.value = ''; captureInput.click(); }
    });

    scanStopBtn && scanStopBtn.addEventListener('click', function () { stopLive(true); });

    captureInput.addEventListener('change', function () {
        var file = captureInput.files[0];
        captureInput.value = '';
        if (!file) return;
        scannerIdle.classList.add('d-none');
        scannerProc.classList.remove('d-none');
        BarcodeScanner.scanFromImageFile(file)
            .then(function (text) {
                scannerProc.classList.add('d-none');
                lookupBarcode(text);
            })
            .catch(function () {
                scannerProc.classList.add('d-none');
                document.getElementById('scanner-error-msg').textContent =
                    'Could not read barcode — try again with the barcode clearly in frame.';
                scannerError.classList.remove('d-none');
                scannerIdle.classList.remove('d-none');
            });
    });

    function startLive() {
        scannerIdle.classList.add('d-none');
        scannerError.classList.add('d-none');
        scannerActive.classList.remove('d-none');
        scanCtl = BarcodeScanner.scanFromVideo(videoEl, {
            onResult: function (text) { stopLive(false); lookupBarcode(text); },
            onError: function (err) {
                stopLive(true);
                document.getElementById('scanner-error-msg').textContent = 'Camera unavailable: ' + (err.message || err);
                scannerError.classList.remove('d-none');
            }
        });
        scanning = true;
        scanBtn.classList.add('d-none');
    }

    function stopLive(showIdle) {
        scanning = false;
        scanBtn.classList.remove('d-none');
        if (scanCtl) { scanCtl.stop(); scanCtl = null; }
        if (videoEl.srcObject) {
            videoEl.srcObject.getTracks().forEach(function (t) { t.stop(); });
            videoEl.srcObject = null;
        }
        scannerActive.classList.add('d-none');
        if (showIdle) scannerIdle.classList.remove('d-none');
    }

    window.resetScanner = function () {
        scannerResult.classList.add('d-none');
        scannerError.classList.add('d-none');
        scannerIdle.classList.remove('d-none');
        document.getElementById('scanner-action-alert').classList.add('d-none');
        var manualSku = document.getElementById('modal-manual-sku');
        if (manualSku) manualSku.value = '';
        document.getElementById('checkout-client-input').value = '';
        document.getElementById('checkout-client-id').value = '';
        document.getElementById('checkout-client-dd').classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (qaForm) qaForm.classList.add('d-none');
        currentItem = null;
        currentScannedSku = null;
    };

    document.getElementById('modal-manual-sku').addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); doManualSearch(); }
    });

    document.querySelectorAll('input[name="modal-search-type"]').forEach(function (radio) {
        radio.addEventListener('change', function () {
            if (document.getElementById('modal-manual-sku').value.trim()) doManualSearch();
        });
    });

    window.doManualSearch = function () {
        var val = (document.getElementById('modal-manual-sku').value || '').trim();
        if (!val) return;
        var byName = document.getElementById('modal-search-name') && document.getElementById('modal-search-name').checked;
        if (byName) { searchByName(val); } else { lookupBarcode(val); }
    };

    function searchByName(query) {
        currentScannedSku = null;
        scannerIdle.classList.add('d-none');
        scannerResult.classList.remove('d-none');
        document.getElementById('scanner-action-alert').classList.add('d-none');
        document.getElementById('item-name').textContent = 'Searching…';
        document.getElementById('item-meta').textContent = '';
        document.getElementById('item-stats').innerHTML = '';
        ['scan-warning', 'reusable-actions', 'consumable-actions', 'not-found', 'search-results-picker'].forEach(function (id) {
            var el = document.getElementById(id); if (el) el.classList.add('d-none');
        });
        var qaForm = document.getElementById('quick-add-form'); if (qaForm) qaForm.classList.add('d-none');

        fetch('/api/inventory/search?q=' + encodeURIComponent(query), { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (results) {
                if (!results || results.length === 0) {
                    showNotFound(query);
                } else if (results.length === 1) {
                    fetch('/api/inventory/status/' + results[0].id, { credentials: 'include' })
                        .then(function (r) { return r.json(); })
                        .then(function (s) { s && s.id ? loadItemStatus(s) : showNotFound(query); })
                        .catch(function () { showNotFound(query); });
                } else {
                    showPickDialog(results, query);
                }
            })
            .catch(function () { showNotFound(query); });
    }

    // SDS ("hazmat") module: show a Safety section (signal word + GHS pictograms) for the scanned item.
    function clearScanSafety() {
        var section = document.getElementById('scan-sds-section');
        if (section) section.classList.add('d-none');
        var sig = document.getElementById('scan-sds-signal');
        if (sig) { sig.classList.add('d-none'); sig.textContent = ''; }
        var pic = document.getElementById('scan-sds-pictograms');
        if (pic) pic.innerHTML = '';
    }

    function loadScanSafety(itemId) {
        clearScanSafety();
        var section = document.getElementById('scan-sds-section');
        if (!section || !(window.modules && window.modules.sds)) return;
        fetch('/api/sds/item/' + itemId, { credentials: 'include' })
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (rows) {
                rows = rows || [];
                if (!rows.length) return;
                var signal = (rows.find(function (r) { return r.signalWord; }) || {}).signalWord;
                var pics = rows.map(function (r) { return r.pictograms || ''; }).filter(Boolean).join('; ');
                window.applySignalBadge(document.getElementById('scan-sds-signal'), signal);
                var n = window.renderPictograms(document.getElementById('scan-sds-pictograms'), pics, 28);
                if (signal || n > 0) section.classList.remove('d-none');
            })
            .catch(function () {});
    }

    function loadItemStatus(item) {
        currentItem = item;
        currentScannedSku = item.sku || null;
        document.getElementById('item-name').textContent = item.name;
        document.getElementById('item-meta').textContent =
            (item.location || '') + (item.sku ? ' · ' + item.sku : '');
        document.getElementById('item-edit-link').href = '/Inventory/Edit?id=' + item.id;
        loadScanSafety(item.id);
        updateItemImage(item);
        document.getElementById('scan-warning').classList.add('d-none');
        document.getElementById('reusable-actions').classList.add('d-none');
        document.getElementById('consumable-actions').classList.add('d-none');
        document.getElementById('not-found').classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (qaForm) qaForm.classList.add('d-none');

        if (item.scanWarning) {
            document.getElementById('scan-warning-text').textContent = item.scanWarning;
            document.getElementById('scan-warning').classList.remove('d-none');
        }

        var scanMaint = document.getElementById('scan-maintenance-warning');
        if (scanMaint) {
            scanMaint.classList.add('d-none');
            var mw = (window.modules && window.modules.maintenance) ? maintenanceWarning(item) : null;
            if (mw) {
                document.getElementById('scan-maintenance-text').textContent = mw.text;
                scanMaint.className = 'alert ' + mw.cls + ' py-2 px-3 mb-2';
                scanMaint.classList.remove('d-none');
            }
        }

        var stats = document.getElementById('item-stats');
        if (item.itemType === 1) {
            stats.className = 'row g-2 text-center small';
            var statsHtml =
                '<div class="col-4"><div class="border rounded p-1"><div class="fw-bold">' + item.quantity + '</div><div class="text-muted">Total</div></div></div>' +
                '<div class="col-4"><div class="border rounded p-1 border-success"><div class="fw-bold text-success">' + item.availableQuantity + '</div><div class="text-muted">Available</div></div></div>' +
                '<div class="col-4"><div class="border rounded p-1 border-warning"><div class="fw-bold text-warning">' + item.checkedOutCount + '</div><div class="text-muted">Out</div></div></div>';
            if (item.lostCount > 0) {
                statsHtml += '<div class="col-4"><div class="border rounded p-1 border-danger"><div class="fw-bold text-danger">' + item.lostCount + '</div><div class="text-muted">Lost</div></div></div>';
            }
            stats.innerHTML = statsHtml;
            updateScanStockBadge(item);
            renderReusableActions(item);
        } else {
            // Mimic the consumable detail view: stat cells + a single − / + stepper.
            var healthCls = item.quantity <= 0 ? 'text-danger' : (item.isLowStock ? 'text-warning' : '');
            stats.className = 'd-flex flex-wrap gap-2';
            stats.innerHTML =
                '<div class="stat-cell text-center"><div class="stat-cell-value ' + healthCls + '">' + item.quantity + '</div><div class="stat-cell-label">In Stock</div></div>' +
                '<div class="stat-cell text-center"><div class="stat-cell-value text-muted">' + item.minimumQuantity + '</div><div class="stat-cell-label">Min Qty</div></div>';
            updateScanStockBadge(item);
            document.getElementById('consumable-actions').classList.remove('d-none');
        }
    }

    function updateScanStockBadge(item) {
        var badge = document.getElementById('item-stock-badge');
        if (!badge) return;
        var avail = item.itemType === 1 ? item.availableQuantity : item.quantity;
        if (avail <= 0) {
            badge.className = 'badge bg-danger';
            badge.textContent = 'Out of Stock';
        } else if (item.isLowStock) {
            badge.className = 'badge bg-warning text-dark';
            badge.textContent = 'Low Stock';
        } else {
            badge.className = 'badge d-none';
            badge.textContent = '';
        }
    }

    function showNotFound(query) {
        pendingAddQuery = query || '';
        document.getElementById('item-name').textContent = 'Item not found';
        document.getElementById('item-meta').textContent = '';
        document.getElementById('item-stats').innerHTML = '';
        clearScanSafety();
        document.getElementById('scan-warning').classList.add('d-none');
        document.getElementById('reusable-actions').classList.add('d-none');
        document.getElementById('consumable-actions').classList.add('d-none');
        document.getElementById('not-found').classList.remove('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (qaForm) qaForm.classList.add('d-none');
    }

    function showPickDialog(results, query) {
        pendingAddQuery = query || '';
        document.getElementById('item-name').textContent = 'Select an item';
        document.getElementById('item-meta').textContent = '';
        document.getElementById('item-stats').innerHTML = '';
        document.getElementById('not-found').classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (qaForm) qaForm.classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        picker.classList.remove('d-none');
        document.getElementById('search-results-list').innerHTML = results.map(function (r) {
            var avail = r.availableQuantity > 0
                ? '<span class="badge bg-success ms-1">' + r.availableQuantity + ' avail</span>'
                : '<span class="badge bg-secondary ms-1">0 avail</span>';
            return '<button class="btn btn-sm btn-outline-secondary w-100 text-start mb-1 py-1" onclick="lookupById(' + r.id + ')">' +
                '<span class="fw-semibold">' + escHtml(r.name) + '</span>' + avail +
                (r.location ? '<span class="text-muted ms-2 small">' + escHtml(r.location) + '</span>' : '') +
                '</button>';
        }).join('');
    }

    window.showQuickAddForm = function () {
        document.getElementById('not-found').classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (!qaForm) return;
        document.getElementById('qa-name').value = '';
        document.getElementById('qa-sku').value = window.currentScannedSku || '';
        document.getElementById('qa-location').value = '';
        document.getElementById('qa-qty').value = '1';
        document.getElementById('qa-type').value = '1';
        document.getElementById('qa-metadata-id').value = '';
        var nameErr = document.getElementById('qa-name-error');
        if (nameErr) nameErr.classList.add('d-none');
        qaForm.classList.remove('d-none');
        document.getElementById('qa-name').focus();
    };

    window.lookupScannedBarcode = function () {
        if (!window.currentScannedSku) {
            alert('No barcode to lookup');
            return;
        }
        // Create/update hidden input with the scanned barcode
        var tempInput = document.getElementById('scanner-lookup-sku-temp');
        if (!tempInput) {
            tempInput = document.createElement('input');
            tempInput.type = 'hidden';
            tempInput.id = 'scanner-lookup-sku-temp';
            document.body.appendChild(tempInput);
        }
        tempInput.value = window.currentScannedSku;
        // Call the UPC lookup function from _Layout.cshtml
        if (typeof openUpcLookup === 'function') {
            openUpcLookup('scanner-lookup-sku-temp', 'scanner-lookup-name');
        } else {
            alert('Lookup function not available');
        }
    };

    window.lookupQaBarcode = function () {
        if (!window.currentScannedSku) {
            alert('No barcode to lookup');
            return;
        }
        var tempInput = document.getElementById('scanner-lookup-sku-temp');
        if (!tempInput) {
            tempInput = document.createElement('input');
            tempInput.type = 'hidden';
            tempInput.id = 'scanner-lookup-sku-temp';
            document.body.appendChild(tempInput);
        }
        tempInput.value = window.currentScannedSku;
        if (typeof openUpcLookup === 'function') {
            openUpcLookup('scanner-lookup-sku-temp', 'scanner-lookup-name');
        } else {
            alert('Lookup function not available');
        }
    };

    window.lookupById = function (id) {
        scannerIdle.classList.add('d-none');
        scannerResult.classList.remove('d-none');
        document.getElementById('scanner-action-alert').classList.add('d-none');
        document.getElementById('item-name').textContent = 'Loading…';
        document.getElementById('item-meta').textContent = '';
        document.getElementById('item-stats').innerHTML = '';
        document.getElementById('reusable-actions').classList.add('d-none');
        document.getElementById('consumable-actions').classList.add('d-none');
        document.getElementById('not-found').classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        bsModal = bsModal || new bootstrap.Modal(document.getElementById('scannerModal'));
        bsModal.show();
        fetch('/api/inventory/status/' + id, { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (item) {
                if (!item || !item.id) { showNotFound(''); return; }
                loadItemStatus(item);
            })
            .catch(function () { document.getElementById('item-name').textContent = 'Item not found'; });
    };

    window.lookupBarcode = function (query) {
        if (!query) return;
        currentScannedSku = query;
        scannerIdle.classList.add('d-none');
        scannerResult.classList.remove('d-none');
        document.getElementById('scanner-action-alert').classList.add('d-none');
        document.getElementById('checkout-client-err').classList.add('d-none');
        document.getElementById('item-name').textContent = 'Looking up…';
        document.getElementById('item-meta').textContent = '';
        document.getElementById('item-stats').innerHTML = '';
        document.getElementById('scan-warning').classList.add('d-none');
        document.getElementById('reusable-actions').classList.add('d-none');
        document.getElementById('consumable-actions').classList.add('d-none');
        document.getElementById('not-found').classList.add('d-none');
        var picker = document.getElementById('search-results-picker');
        if (picker) picker.classList.add('d-none');
        var qaForm = document.getElementById('quick-add-form');
        if (qaForm) qaForm.classList.add('d-none');

        fetch('/api/inventory/status?sku=' + encodeURIComponent(query), { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (item) {
                if (!item || !item.id) {
                    return fetch('/api/inventory/search?q=' + encodeURIComponent(query), { credentials: 'include' })
                        .then(function (r) { return r.json(); })
                        .then(function (results) {
                            if (!results || results.length === 0) {
                                showNotFound(query);
                            } else if (results.length === 1) {
                                return fetch('/api/inventory/status/' + results[0].id, { credentials: 'include' })
                                    .then(function (r) { return r.json(); })
                                    .then(function (s) { s && s.id ? loadItemStatus(s) : showNotFound(query); });
                            } else {
                                showPickDialog(results, query);
                            }
                        });
                }
                loadItemStatus(item);
            })
            .catch(function () { showNotFound(query); });
    };

    function refreshCurrentItem() {
        if (!currentItem) return;
        if (currentItem.sku) { lookupBarcode(currentItem.sku); }
        else { lookupById(currentItem.id); }
    }

    window.doQuickAdd = function () {
        var name = document.getElementById('qa-name').value.trim();
        var nameErr = document.getElementById('qa-name-error');
        if (!name) { nameErr.classList.remove('d-none'); document.getElementById('qa-name').focus(); return; }
        nameErr.classList.add('d-none');
        var qty      = parseInt(document.getElementById('qa-qty').value) || 1;
        var type     = parseInt(document.getElementById('qa-type').value);
        var location = document.getElementById('qa-location').value.trim() || null;
        var sku      = document.getElementById('qa-sku').value.trim() || null;
        var metadataId = parseInt(document.getElementById('qa-metadata-id').value) || null;
        setBtn('btn-quick-add', true);
        apiPost('/api/inventory/quick-add', { name: name, quantity: qty, itemType: type, location: location, sku: sku, metadataId: metadataId })
            .then(function (r) { return r.json(); })
            .then(function (status) {
                loadItemStatus(status);
                showActionAlert('Item added successfully.', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
            })
            .catch(function (msg) { showActionAlert(msg || 'Failed to add item.'); })
            .finally(function () { setBtn('btn-quick-add', false); });
    };

    function renderReusableActions(item) {
        document.getElementById('reusable-actions').classList.remove('d-none');
        var checkouts = item.activeCheckouts || [];
        var listEl    = document.getElementById('checkout-list');
        var activeEl  = document.getElementById('active-checkouts');
        if (checkouts.length > 0) {
            activeEl.classList.remove('d-none');
            listEl.innerHTML = checkouts.map(function (c) {
                return '<div class="d-flex align-items-center justify-content-between border rounded p-1 mb-1 small">' +
                    '<div><span class="fw-semibold">' + escHtml(c.checkedOutBy) + '</span>' +
                    '<span class="text-muted ms-1">x' + c.quantity + '</span>' +
                    '<div class="text-muted" style="font-size:0.7rem">' + window.fmtUtc(c.checkedOutAt, 'date') + '</div></div>' +
                    '<div class="d-flex gap-1">' +
                    '<button class="btn btn-xs btn-outline-success py-0 px-1" onclick="doCheckin(' + c.id + ')" title="Check In"><i class="bi bi-box-arrow-in-down"></i></button>' +
                    '<button class="btn btn-xs btn-outline-danger py-0 px-1" onclick="doMarkLost(' + c.id + ')" title="Mark Lost"><i class="bi bi-x-circle"></i></button>' +
                    '</div></div>';
            }).join('');
        } else {
            activeEl.classList.add('d-none');
        }
    }

    function showActionAlert(msg, type) {
        var el = document.getElementById('scanner-action-alert');
        el.className = 'alert alert-' + (type || 'danger') + ' py-2 px-3 mb-2';
        el.textContent = msg;
        el.classList.remove('d-none');
        if (type === 'success') setTimeout(function () { el.classList.add('d-none'); }, 3000);
    }

    function setBtn(id, loading) {
        var btn = document.getElementById(id);
        if (!btn) return;
        btn.disabled = loading;
    }

    window.doCheckout = function () {
        if (!currentItem) return;
        var name     = document.getElementById('checkout-client-input').value.trim();
        var clientId = parseInt(document.getElementById('checkout-client-id').value) || null;
        var qty      = parseInt(document.getElementById('action-qty').value) || 1;
        var notes    = document.getElementById('action-notes').value.trim() || null;
        var nameErr  = document.getElementById('checkout-client-err');
        if (!name) { nameErr.classList.remove('d-none'); document.getElementById('checkout-client-input').focus(); return; }
        nameErr.classList.add('d-none');
        setBtn('btn-checkout', true);
        apiPost('/api/inventory/checkout', { itemId: currentItem.id, checkedOutBy: name, clientId: clientId, quantity: qty, notes: notes })
            .then(function () {
                document.getElementById('checkout-client-input').value = '';
                document.getElementById('checkout-client-id').value = '';
                document.getElementById('checkout-client-dd').classList.add('d-none');
                showActionAlert('Checked out successfully.', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
                refreshCurrentItem();
            })
            .catch(function (msg) { showActionAlert(msg || 'Check out failed.'); })
            .finally(function () { setBtn('btn-checkout', false); });
    };

    window.doCheckin = function (recordId) {
        document.querySelectorAll('#checkout-list button').forEach(function (b) { b.disabled = true; });
        apiPost('/api/inventory/checkin', { recordId: recordId, notes: null })
            .then(function () {
                showActionAlert('Checked in successfully.', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
                refreshCurrentItem();
            })
            .catch(function (msg) {
                showActionAlert(msg || 'Check in failed.');
                document.querySelectorAll('#checkout-list button').forEach(function (b) { b.disabled = false; });
            });
    };

    window.doMarkLost = function (recordId) {
        if (!confirm('Mark this item as lost?')) return;
        document.querySelectorAll('#checkout-list button').forEach(function (b) { b.disabled = true; });
        apiPost('/api/inventory/lost', { recordId: recordId, notes: null })
            .then(function () {
                showActionAlert('Item marked as lost.', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
                refreshCurrentItem();
            })
            .catch(function (msg) {
                showActionAlert(msg || 'Action failed.');
                document.querySelectorAll('#checkout-list button').forEach(function (b) { b.disabled = false; });
            });
    };

    window.doConsume = function () {
        if (!currentItem) return;
        var qty   = parseInt(document.getElementById('consume-qty').value) || 1;
        var notes = document.getElementById('consume-notes').value.trim() || null;
        setBtn('btn-consume', true);
        apiPost('/api/inventory/consume', { itemId: currentItem.id, quantity: qty, notes: notes })
            .then(function () {
                showActionAlert('Consumed ' + qty + ' unit(s).', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
                refreshCurrentItem();
            })
            .catch(function (msg) { showActionAlert(msg || 'Consume failed.'); })
            .finally(function () { setBtn('btn-consume', false); });
    };

    window.doRestock = function () {
        if (!currentItem) return;
        // Shares the stepper's quantity/notes inputs with consume.
        var qty   = parseInt(document.getElementById('consume-qty').value) || 1;
        var notes = document.getElementById('consume-notes').value.trim() || null;
        setBtn('btn-restock', true);
        apiPost('/api/inventory/restock', { itemId: currentItem.id, quantity: qty, notes: notes })
            .then(function () {
                showActionAlert('Restocked ' + qty + ' unit(s).', 'success');
                _scannerDidAction = true;
                if (typeof loadAvailableItems === 'function') loadAvailableItems();
                refreshCurrentItem();
            })
            .catch(function (msg) { showActionAlert(msg || 'Restock failed.'); })
            .finally(function () { setBtn('btn-restock', false); });
    };

    var _clientTimer = null;

    window.onClientInput = function () {
        clearTimeout(_clientTimer);
        document.getElementById('checkout-client-id').value = '';
        var q = (document.getElementById('checkout-client-input').value || '').trim();
        if (q.length < 1) { hideClientDd(); return; }
        _clientTimer = setTimeout(function () {
            fetch('/api/clients/search?q=' + encodeURIComponent(q), { credentials: 'include' })
                .then(function (r) { return r.json(); })
                .then(function (clients) { showClientDd(clients, q); })
                .catch(function () { hideClientDd(); });
        }, 200);
    };

    function showClientDd(clients, query) {
        var dd = document.getElementById('checkout-client-dd');
        var rows = (clients || []).map(function (c) {
            return '<div class="px-2 py-1" style="cursor:pointer" ' +
                'onmousedown="selectClient(' + c.id + ',\'' + escHtml(c.displayName).replace(/'/g, "&#39;") + '\')"' +
                'onmouseover="this.classList.add(\'bg-body-secondary\')" onmouseout="this.classList.remove(\'bg-body-secondary\')">' +
                escHtml(c.displayName) + (c.phone ? '<span class="text-muted ms-2">' + escHtml(c.phone) + '</span>' : '') +
                '</div>';
        });
        rows.push('<div class="px-2 py-1 text-primary" style="cursor:pointer;border-top:1px solid var(--bs-border-color)" ' +
            'onmousedown="quickCreateClient(\'' + escHtml(query).replace(/'/g, "&#39;") + '\')"' +
            'onmouseover="this.classList.add(\'bg-body-secondary\')" onmouseout="this.classList.remove(\'bg-body-secondary\')">' +
            '<i class="bi bi-person-plus me-1"></i>Create “' + escHtml(query) + '”</div>');
        dd.innerHTML = rows.join('');
        dd.classList.remove('d-none');
    }

    function hideClientDd() {
        document.getElementById('checkout-client-dd').classList.add('d-none');
    }

    window.selectClient = function (id, name) {
        document.getElementById('checkout-client-id').value = id;
        document.getElementById('checkout-client-input').value = name;
        hideClientDd();
    };

    window.quickCreateClient = function (name) {
        hideClientDd();
        fetch('/api/clients/quick-create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ name: name })
        })
        .then(function (r) { return r.json(); })
        .then(function (c) { selectClient(c.id, c.displayName); })
        .catch(function () { showActionAlert('Failed to create client.'); });
    };

    function escHtml(s) {
        return s == null ? '' : String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    window.showMetadataInfo = function () {
        var item = currentItem || (typeof _currentItemData !== 'undefined' ? _currentItemData : null);
        if (!item) return;
        var img = document.getElementById('metadata-info-image');
        var placeholder = document.getElementById('metadata-info-placeholder');
        var brand = document.getElementById('metadata-info-brand');
        var category = document.getElementById('metadata-info-category');
        var desc = document.getElementById('metadata-info-description');

        if (item.metadataImageUrl) {
            img.src = item.metadataImageUrl;
            img.style.display = 'block';
            if (placeholder) placeholder.style.display = 'none';
        } else {
            img.style.display = 'none';
            if (placeholder) placeholder.style.display = 'block';
        }

        brand.textContent = item.metadataBrand || 'Not available';
        category.textContent = item.metadataCategory || 'Not available';
        desc.textContent = item.metadataDescription || 'Not available';

        bootstrap.Modal.getOrCreateInstance(document.getElementById('metadataInfoModal')).show();
    };

    function updateItemImage(item) {
        var container = document.getElementById('item-image-container');
        var img = document.getElementById('item-image');

        if (item.metadataImageUrl) {
            img.src = item.metadataImageUrl;
            container.classList.remove('d-none');
        } else {
            container.classList.add('d-none');
        }
    }

    function apiPost(url, body) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify(body)
        }).then(function (r) {
            if (!r.ok) return r.text().then(function (t) { throw t || ('Server error ' + r.status); });
            return r;
        });
    }
})();
