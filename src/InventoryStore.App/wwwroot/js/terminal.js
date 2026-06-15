// Terminal — a self-contained, touch-first checkout/consume screen.
// Talks to the same JSON /api/inventory endpoints as the in-app scanner, but with its own
// large-button DOM. Staff are limited to this page; Admin/Manager also get a Restock button.
(function () {
    var currentItem = null;
    var zxingReader = null;
    var videoEl = document.getElementById('term-scan-video');

    function $(id) { return document.getElementById(id); }
    function esc(s) {
        return s == null ? '' : String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function showAlert(msg, type) {
        var el = $('term-alert');
        el.className = 'alert term-card fs-5 py-3 alert-' + (type || 'danger');
        el.textContent = msg;
        el.classList.remove('d-none');
        if (type === 'success') setTimeout(function () { el.classList.add('d-none'); }, 2500);
    }
    function hideAlert() { $('term-alert').classList.add('d-none'); }

    function hideAllCards() {
        ['term-result', 'term-notfound', 'term-picker'].forEach(function (id) { $(id).classList.add('d-none'); });
    }

    var termCanRestock = ($('term-kit') && $('term-kit').dataset.canRestock === '1') || window.termCanRestock;

    // ── Search / lookup ───────────────────────────────────────────────
    // One omni search box: try an exact SKU match first, then fall back to a name search.
    window.termSearch = function () {
        var val = ($('term-search-input').value || '').trim();
        if (!val) return;
        hideAlert();
        lookupSku(val);
    };

    $('term-search-input').addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); termSearch(); }
    });

    // Live results as you type: show name/SKU matches in the picker without auto-opening,
    // so typing "hyd" surfaces "Hydrogen peroxide" before you finish.
    var liveTimer = null;
    $('term-search-input').addEventListener('input', function () {
        clearTimeout(liveTimer);
        var v = ($('term-search-input').value || '').trim();
        if (v.length < 2) { hideAllCards(); return; }
        liveTimer = setTimeout(function () { liveSearch(v); }, 250);
    });

    function liveSearch(q) {
        fetch('/api/inventory/search?q=' + encodeURIComponent(q), { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (results) {
                // Ignore stale responses if the box changed while the request was in flight.
                if (($('term-search-input').value || '').trim() !== q) return;
                if (results && results.length) { showPicker(results); }
                else { hideAllCards(); }
            })
            .catch(function () {});
    }

    function lookupSku(sku) {
        fetch('/api/inventory/status?sku=' + encodeURIComponent(sku), { credentials: 'include' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (item) {
                if (item && item.id) { loadItem(item); return; }
                // No exact SKU hit — fall back to the name search.
                searchByName(sku);
            })
            // If the exact-SKU lookup itself fails, still try the name search
            // rather than dead-ending on "not found".
            .catch(function () { searchByName(sku); });
    }

    function searchByName(q) {
        fetch('/api/inventory/search?q=' + encodeURIComponent(q), { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (results) {
                if (!results || results.length === 0) { showNotFound(); }
                else if (results.length === 1) { lookupById(results[0].id); }
                else { showPicker(results); }
            })
            .catch(function () { showNotFound(); });
    }

    function lookupById(id) {
        fetch('/api/inventory/status/' + id, { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (item) { item && item.id ? loadItem(item) : showNotFound(); })
            .catch(function () { showNotFound(); });
    }

    function refresh() {
        if (!currentItem) return;
        lookupById(currentItem.id);
    }

    // ── Rendering ─────────────────────────────────────────────────────
    function showNotFound() {
        hideAllCards();
        currentItem = null;
        $('term-notfound').classList.remove('d-none');
    }

    function showPicker(results) {
        hideAllCards();
        $('term-picker-list').innerHTML = results.map(function (r) {
            var avail = r.availableQuantity > 0
                ? '<span class="badge bg-success ms-2">' + r.availableQuantity + ' avail</span>'
                : '<span class="badge bg-secondary ms-2">0 avail</span>';
            var tags = (r.tags && r.tags.length && window.tagPillHtml)
                ? '<div class="d-flex flex-wrap gap-1 mt-1">' +
                    r.tags.map(function (t) { return window.tagPillHtml(t); }).join('') + '</div>'
                : '';
            return '<button class="btn btn-outline-secondary term-btn text-start" onclick="termPick(' + r.id + ')">' +
                '<span class="fw-semibold">' + esc(r.name) + '</span>' + avail +
                (r.location ? '<span class="text-muted ms-2">' + esc(r.location) + '</span>' : '') +
                tags +
                '</button>';
        }).join('');
        $('term-picker').classList.remove('d-none');
    }
    window.termPick = function (id) { lookupById(id); };

    function loadItem(item) {
        currentItem = item;
        hideAllCards();
        $('term-result').classList.remove('d-none');
        $('term-item-name').textContent = item.name;
        $('term-item-meta').textContent = (item.location || '') + (item.sku ? ' · ' + item.sku : '');

        // Scan warning
        if (item.scanWarning) {
            $('term-warning-text').textContent = item.scanWarning;
            $('term-warning').classList.remove('d-none');
        } else {
            $('term-warning').classList.add('d-none');
        }

        // Maintenance due/overdue warning (shares the scanner's logic).
        var termMaint = $('term-maintenance-warning');
        if (termMaint) {
            termMaint.classList.add('d-none');
            var mw = (window.modules && window.modules.maintenance && window.maintenanceWarning)
                ? window.maintenanceWarning(item) : null;
            if (mw) {
                $('term-maintenance-text').textContent = mw.text;
                termMaint.className = 'alert ' + mw.cls + ' py-2 px-3 mb-2';
                termMaint.classList.remove('d-none');
            }
        }

        updateStockBadge(item);

        var statsEl = $('term-stats');
        var isReusable = item.itemType === 1;
        var isKit = item.itemType === 2;
        var avail = (isReusable || isKit) ? item.availableQuantity : item.quantity;
        var outOfStock = avail <= 0;

        // Prominent out-of-stock banner at the top of the card.
        if (outOfStock) {
            $('term-stock-warning-text').textContent = isKit
                ? 'No complete kits available right now.'
                : isReusable
                    ? 'Out of stock — no units are available to check out.'
                    : 'Out of stock — there is nothing left to use.';
            $('term-stock-warning').classList.remove('d-none');
        } else {
            $('term-stock-warning').classList.add('d-none');
        }

        if (isKit) {
            statsEl.innerHTML =
                statCell(item.buildableQuantity, 'Kits', item.buildableQuantity > 0 ? 'text-success' : 'text-danger') +
                statCell((item.components || []).length, 'Items', 'text-muted');
            $('term-reusable').classList.add('d-none');
            $('term-consumable').classList.add('d-none');
            $('term-kit').classList.remove('d-none');
            renderKitTerminal(item);
        } else if (isReusable) {
            var html =
                statCell(item.quantity, 'Total', '') +
                statCell(item.availableQuantity, 'Available', 'text-success') +
                statCell(item.checkedOutCount, 'Out', 'text-warning');
            if (item.lostCount > 0) html += statCell(item.lostCount, 'Lost', 'text-danger');
            statsEl.innerHTML = html;
            $('term-reusable').classList.remove('d-none');
            $('term-consumable').classList.add('d-none');
            $('term-kit').classList.add('d-none');
            renderActive(item);
            resetCheckoutInputs();
            // Nothing available means nothing to hand out.
            $('term-checkout-btn').disabled = outOfStock;
        } else {
            var healthCls = item.quantity <= 0 ? 'text-danger' : (item.isLowStock ? 'text-warning' : '');
            statsEl.innerHTML =
                statCell(item.quantity, 'In Stock', healthCls) +
                statCell(item.minimumQuantity, 'Min Qty', 'text-muted');
            $('term-reusable').classList.add('d-none');
            $('term-consumable').classList.remove('d-none');
            $('term-kit').classList.add('d-none');
            $('term-adjust-qty').value = '1';
            // Can't use stock that isn't there; restock (+) stays enabled.
            $('term-consume-btn').disabled = outOfStock;
        }
    }

    function statCell(value, label, cls) {
        return '<div class="col-4"><div class="term-stat border">' +
            '<div class="term-stat-value ' + cls + '">' + value + '</div>' +
            '<div class="term-stat-label text-muted">' + label + '</div></div></div>';
    }

    function updateStockBadge(item) {
        var badge = $('term-stock-badge');
        var avail = (item.itemType === 1 || item.itemType === 2) ? item.availableQuantity : item.quantity;
        if (avail <= 0) { badge.className = 'badge fs-6 bg-danger'; badge.textContent = 'Out of stock'; }
        else if (item.isLowStock) { badge.className = 'badge fs-6 bg-warning text-dark'; badge.textContent = 'Low stock'; }
        else { badge.className = 'badge fs-6 d-none'; badge.textContent = ''; }
    }

    function renderActive(item) {
        var checkouts = item.activeCheckouts || [];
        var box = $('term-active');
        var list = $('term-active-list');
        if (!checkouts.length) { box.classList.add('d-none'); list.innerHTML = ''; return; }
        box.classList.remove('d-none');
        list.innerHTML = checkouts.map(function (c) {
            return '<div class="d-flex align-items-center justify-content-between border rounded p-2 mb-2 term-co-item">' +
                '<div><span class="fw-semibold">' + esc(c.checkedOutBy) + '</span>' +
                '<span class="text-muted ms-1">×' + c.quantity + '</span>' +
                '<div class="text-muted small">' + new Date(c.checkedOutAt).toLocaleDateString() + '</div></div>' +
                '<button class="btn btn-success term-btn px-3" onclick="termCheckin(' + c.id + ')">' +
                '<i class="bi bi-box-arrow-in-down me-1"></i>Check In</button>' +
                '</div>';
        }).join('');
    }

    function resetCheckoutInputs() {
        $('term-client-input').value = '';
        $('term-client-id').value = '';
        $('term-client-dd').classList.add('d-none');
        $('term-co-qty').value = '1';
    }

    window.termReset = function () {
        hideAllCards();
        hideAlert();
        currentItem = null;
        var input = $('term-search-input');
        input.value = '';
        input.focus();
    };

    window.termStep = function (id, delta) {
        var el = $(id);
        var v = (parseInt(el.value) || 1) + delta;
        if (v < 1) v = 1;
        el.value = v;
    };

    // ── Actions ───────────────────────────────────────────────────────
    function apiPost(url, body, btn) {
        if (btn) btn.disabled = true;
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify(body)
        }).then(function (r) {
            if (!r.ok) return r.text().then(function (t) {
                // The API returns a plain friendly message on validation errors. Anything that
                // looks like an HTML error page is swapped for a generic line so we never dump markup.
                t = (t || '').trim();
                if (!t || t.charAt(0) === '<') t = 'Something went wrong (' + r.status + ').';
                throw t;
            });
            return r;
        }).finally(function () { if (btn) btn.disabled = false; });
    }

    window.termCheckout = function () {
        if (!currentItem) return;
        var name = $('term-client-input').value.trim();
        var clientId = parseInt($('term-client-id').value) || null;
        var qty = parseInt($('term-co-qty').value) || 1;
        if (!name) { showAlert('Enter a borrower name first.', 'warning'); $('term-client-input').focus(); return; }
        apiPost('/api/inventory/checkout',
            { itemId: currentItem.id, checkedOutBy: name, clientId: clientId, quantity: qty, notes: null },
            $('term-checkout-btn'))
            .then(function () { showAlert('Checked out to ' + name + '.', 'success'); refresh(); })
            .catch(function (msg) { showAlert(msg || 'Check out failed.'); });
    };

    window.termCheckin = function (recordId) {
        apiPost('/api/inventory/checkin', { recordId: recordId, notes: null })
            .then(function () { showAlert('Checked in.', 'success'); refresh(); })
            .catch(function (msg) { showAlert(msg || 'Check in failed.'); });
    };

    window.termConsume = function () {
        if (!currentItem) return;
        var qty = parseInt($('term-adjust-qty').value) || 1;
        apiPost('/api/inventory/consume', { itemId: currentItem.id, quantity: qty, notes: null })
            .then(function () { showAlert('Consumed ' + qty + ' unit(s).', 'success'); refresh(); })
            .catch(function (msg) { showAlert(msg || 'Consume failed.'); });
    };

    window.termRestock = function () {
        if (!currentItem || !window.termCanRestock) return;
        var qty = parseInt($('term-adjust-qty').value) || 1;
        apiPost('/api/inventory/restock', { itemId: currentItem.id, quantity: qty, notes: null })
            .then(function () { showAlert('Restocked ' + qty + ' unit(s).', 'success'); refresh(); })
            .catch(function (msg) { showAlert(msg || 'Restock failed.'); });
    };

    // ── Client picker ─────────────────────────────────────────────────
    var clientTimer = null;
    window.termClientInput = function () {
        clearTimeout(clientTimer);
        $('term-client-id').value = '';
        var q = ($('term-client-input').value || '').trim();
        if (q.length < 1) { $('term-client-dd').classList.add('d-none'); return; }
        clientTimer = setTimeout(function () {
            fetch('/api/clients/search?q=' + encodeURIComponent(q), { credentials: 'include' })
                .then(function (r) { return r.json(); })
                .then(function (clients) { showClientDd(clients, q); })
                .catch(function () { $('term-client-dd').classList.add('d-none'); });
        }, 200);
    };

    function showClientDd(clients, query) {
        var dd = $('term-client-dd');
        var rows = (clients || []).map(function (c) {
            return '<div style="cursor:pointer" onmousedown="termSelectClient(' + c.id + ',\'' + esc(c.displayName) + '\')">' +
                esc(c.displayName) + (c.phone ? '<span class="text-muted ms-2">' + esc(c.phone) + '</span>' : '') + '</div>';
        });
        rows.push('<div class="text-primary" style="cursor:pointer;border-top:1px solid var(--bs-border-color)" ' +
            'onmousedown="termQuickCreateClient(\'' + esc(query) + '\')">' +
            '<i class="bi bi-person-plus me-1"></i>Create “' + esc(query) + '”</div>');
        dd.innerHTML = rows.join('');
        dd.classList.remove('d-none');
    }

    window.termSelectClient = function (id, name) {
        $('term-client-id').value = id;
        $('term-client-input').value = name;
        $('term-client-dd').classList.add('d-none');
    };

    window.termQuickCreateClient = function (name) {
        $('term-client-dd').classList.add('d-none');
        fetch('/api/clients/quick-create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ name: name })
        })
            .then(function (r) { return r.json(); })
            .then(function (c) { termSelectClient(c.id, c.displayName); })
            .catch(function () { showAlert('Could not create borrower.'); });
    };

    // ── Kits ──────────────────────────────────────────────────────────
    // Builds the kit panel: contents summary, big checkout/consume/restock buttons, and any
    // kits currently out (with check-in). Members are never acted on individually here.
    function renderKitTerminal(item) {
        var comps = item.components || [];
        var checkouts = item.activeKitCheckouts || [];
        var hasConsumables = comps.some(function (c) { return c.itemType === 0; });
        var avail = item.availableQuantity;

        var members = comps.map(function (c) {
            var short = c.availableQuantity < c.perKitQuantity;
            return '<div class="d-flex justify-content-between border-bottom py-1">' +
                '<span>' + esc(c.name) + '</span>' +
                '<span class="' + (short ? 'text-danger fw-semibold' : 'text-muted') + '">' +
                c.perKitQuantity + ' needed · ' + c.availableQuantity + ' avail</span></div>';
        }).join('');

        var checkoutsHtml = checkouts.length ? '<div class="mt-3"><div class="fw-semibold fs-6 mb-1">Kits out</div>' +
            checkouts.map(function (k) {
                return '<div class="d-flex align-items-center justify-content-between border rounded p-2 mb-2 term-co-item">' +
                    '<div><span class="fw-semibold">' + esc(k.checkedOutBy) + '</span>' +
                    '<span class="text-muted ms-1">×' + k.quantity + '</span></div>' +
                    '<button class="btn btn-success term-btn px-3" onclick="termKitCheckin(' + k.id + ')">' +
                    '<i class="bi bi-box-arrow-in-down me-1"></i>Check In</button></div>';
            }).join('') + '</div>' : '';

        var useRow = hasConsumables ?
            '<div class="mt-3"><label class="form-label fs-6 fw-semibold mb-1">Use / restock</label>' +
            '<div class="input-group" style="max-width:320px">' +
            '<button class="btn btn-danger term-step-btn" type="button" onclick="termKitConsume()" title="Use one kit\'s worth"><i class="bi bi-dash-lg"></i></button>' +
            '<input type="number" id="term-kit-qty" class="form-control term-step-input" value="1" min="1">' +
            (termCanRestock ? '<button class="btn btn-success term-step-btn" type="button" onclick="termKitRestock()" title="Restock"><i class="bi bi-plus-lg"></i></button>' : '') +
            '</div></div>' : '';

        $('term-kit').innerHTML =
            '<div id="term-kit-alert" class="alert d-none py-2 px-3 fs-6"></div>' +
            '<div class="mb-2">' + (members || '<span class="text-muted">This kit has no items.</span>') + '</div>' +
            '<label class="form-label fs-6 fw-semibold mb-1">Borrower</label>' +
            '<input type="text" id="term-kit-by" class="form-control term-input mb-2" placeholder="Name" autocomplete="off">' +
            '<div class="d-flex align-items-center gap-2 mb-2">' +
            '<div class="input-group" style="max-width:200px">' +
            '<button class="btn btn-outline-secondary term-step-btn" type="button" onclick="termStep(\'term-kit-co-qty\',-1)">−</button>' +
            '<input type="number" id="term-kit-co-qty" class="form-control term-step-input" value="1" min="1">' +
            '<button class="btn btn-outline-secondary term-step-btn" type="button" onclick="termStep(\'term-kit-co-qty\',1)">+</button>' +
            '</div></div>' +
            '<button class="btn btn-primary term-btn term-btn-lg w-100" onclick="termKitCheckout()" ' + (avail <= 0 ? 'disabled' : '') + '>' +
            '<i class="bi bi-box-arrow-up-right me-1"></i> Check Out Kit</button>' +
            useRow + checkoutsHtml;
    }

    function kitAlert(msg, type) {
        var el = $('term-kit-alert');
        if (!el) { showAlert(msg, type); return; }
        el.className = 'alert py-2 px-3 fs-6 alert-' + (type || 'danger');
        el.innerHTML = msg;
        el.classList.remove('d-none');
    }

    function kitShortageList(result) {
        return '<div class="fw-semibold mb-1">Some items are short:</div><ul class="mb-0">' +
            (result.shortages || []).map(function (s) {
                return '<li>' + esc(s.name) + ': need ' + s.required + ', ' + s.available + ' avail</li>';
            }).join('') + '</ul>';
    }

    // Runs a kit checkout/consume, surfacing the cancel / proceed-with-available choice on shortages.
    function runKitAction(path, body, okMsg) {
        apiPost('/api/inventory/kit/' + path, body)
            .then(function (r) { return r.json(); })
            .then(function (result) {
                if (result.needsConfirmation) {
                    if (!result.allowPartial) {
                        kitAlert(kitShortageList(result) + '<div class="mt-1">This kit must be complete.</div>', 'danger');
                        return;
                    }
                    kitAlert(kitShortageList(result) +
                        '<div class="mt-2 d-flex gap-2">' +
                        '<button class="btn btn-warning" id="term-kit-partial">Use what\'s available</button>' +
                        '<button class="btn btn-outline-secondary" onclick="$id(\'term-kit-alert\').classList.add(\'d-none\')">Cancel</button>' +
                        '</div>', 'warning');
                    $('term-kit-partial').onclick = function () {
                        body.allowPartialFallback = true;
                        apiPost('/api/inventory/kit/' + path, body)
                            .then(function () { showAlert(okMsg, 'success'); refresh(); })
                            .catch(function (m) { kitAlert(m || 'Action failed.', 'danger'); });
                    };
                    return;
                }
                showAlert(okMsg, 'success');
                refresh();
            })
            .catch(function (m) { kitAlert(m || 'Action failed.', 'danger'); });
    }

    // Small helper so inline onclick can reach elements without polluting globals further.
    window.$id = function (id) { return document.getElementById(id); };

    window.termKitCheckout = function () {
        if (!currentItem) return;
        var by = ($('term-kit-by').value || '').trim();
        var qty = parseInt($('term-kit-co-qty').value) || 1;
        if (!by) { kitAlert('Enter a borrower name first.', 'warning'); $('term-kit-by').focus(); return; }
        runKitAction('checkout', { kitId: currentItem.id, quantity: qty, checkedOutBy: by }, 'Kit checked out to ' + by + '.');
    };

    window.termKitConsume = function () {
        if (!currentItem) return;
        var qty = parseInt($('term-kit-qty').value) || 1;
        runKitAction('consume', { kitId: currentItem.id, quantity: qty }, 'Used ' + qty + ' kit(s).');
    };

    window.termKitRestock = function () {
        if (!currentItem || !termCanRestock) return;
        var qty = parseInt($('term-kit-qty').value) || 1;
        apiPost('/api/inventory/kit/restock', { kitId: currentItem.id, quantity: qty })
            .then(function () { showAlert('Restocked ' + qty + ' kit(s) worth.', 'success'); refresh(); })
            .catch(function (m) { kitAlert(m || 'Restock failed.', 'danger'); });
    };

    window.termKitCheckin = function (kitCheckoutId) {
        apiPost('/api/inventory/kit/checkin', { kitCheckoutId: kitCheckoutId })
            .then(function () { showAlert('Kit checked in.', 'success'); refresh(); })
            .catch(function (m) { kitAlert(m || 'Check in failed.', 'danger'); });
    };

    // ── Camera scanning ───────────────────────────────────────────────
    window.termStartScan = function () {
        if (!window.isSecureContext) { showAlert('Camera needs HTTPS. Type the SKU instead.', 'warning'); return; }
        // The scan button stays visible at all times, so guard against starting a second
        // camera session if one is already running.
        if (zxingReader) return;
        $('term-scan-wrap').classList.remove('d-none');
        var hints = new Map();
        hints.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, [
            ZXing.BarcodeFormat.UPC_A, ZXing.BarcodeFormat.UPC_E,
            ZXing.BarcodeFormat.EAN_13, ZXing.BarcodeFormat.EAN_8,
            ZXing.BarcodeFormat.CODE_128
        ]);
        zxingReader = new ZXing.BrowserMultiFormatReader(hints);
        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } } })
            .then(function (tmp) {
                var track = tmp.getVideoTracks()[0];
                var deviceId = track ? track.getSettings().deviceId : null;
                tmp.getTracks().forEach(function (t) { t.stop(); });
                return zxingReader.decodeFromVideoDevice(deviceId, videoEl, function (result) {
                    if (result) {
                        termStopScan();
                        $('term-search-input').value = result.getText();
                        lookupSku(result.getText());
                    }
                });
            })
            .catch(function (err) {
                termStopScan();
                showAlert('Camera unavailable: ' + (err.message || err), 'warning');
            });
    };

    window.termStopScan = function () {
        if (zxingReader) {
            try { zxingReader.stopContinuousDecode(); } catch (e) {}
            try { zxingReader.reset(); } catch (e) {}
            zxingReader = null;
        }
        if (videoEl.srcObject) {
            videoEl.srcObject.getTracks().forEach(function (t) { t.stop(); });
            videoEl.srcObject = null;
        }
        // The green scan button lives next to the search box and is always available,
        // so we only need to close the live-camera card here.
        $('term-scan-wrap').classList.add('d-none');
    };

    // ── Theme toggle ──────────────────────────────────────────────────
    window.termToggleTheme = function () {
        var cur = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-bs-theme', cur);
        localStorage.setItem('it-theme', cur);
        syncThemeIcon();
    };
    function syncThemeIcon() {
        var dark = document.documentElement.getAttribute('data-bs-theme') === 'dark';
        var ic = $('term-theme-icon');
        if (ic) ic.className = dark ? 'bi bi-sun' : 'bi bi-moon';
    }
    syncThemeIcon();
})();
