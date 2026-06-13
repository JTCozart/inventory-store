/* Tag type-ahead widget + shared tag colouring.
 *
 * Tags are auto-coloured from their name (no user picker) so a given tag looks the same
 * everywhere it appears. The palette and hash here MUST stay in sync with the server-side
 * TagColors helper used for Razor-rendered pills.
 */
(function () {
    var PALETTE = ['#0d6efd', '#198754', '#dc3545', '#ffc107', '#fd7e14', '#6f42c1',
                   '#d63384', '#0dcaf0', '#20c997', '#6c757d', '#212529'];
    // Yellow/cyan pills need dark text to stay readable.
    var DARK_TEXT = { '#ffc107': 1, '#0dcaf0': 1, '#20c997': 1 };

    function stableHash(s) {
        s = (s || '').toLowerCase();
        var h = 0;
        for (var i = 0; i < s.length; i++) { h = (h * 31 + s.charCodeAt(i)) | 0; }
        return Math.abs(h);
    }

    function escHtml(s) {
        return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    window.tagColor = function (name) { return PALETTE[stableHash(name) % PALETTE.length]; };

    // A read-only coloured pill (used in the view modal and Terminal results).
    window.tagPillHtml = function (name) {
        var color = window.tagColor(name);
        var textCls = DARK_TEXT[color] ? ' text-dark' : '';
        return '<span class="badge rounded-pill' + textCls + '" style="background:' + color +
            '">' + escHtml(name) + '</span>';
    };

    // initTagInput(rootId, allTagNames, selectedNames)
    //   Builds an editable, token-style tag field inside the given root element and returns a
    //   small controller with getTags()/setTags(). New (unknown) names are allowed and created
    //   on save by the server.
    window.initTagInput = function (rootId, allTagNames, selectedNames) {
        var root = document.getElementById(rootId);
        if (!root) return null;

        var all = (allTagNames || []).map(function (t) { return typeof t === 'string' ? t : t.name; });
        var selected = [];                 // preserves display case
        var seen = Object.create(null);    // lowercased -> true

        root.innerHTML =
            '<div class="form-control d-flex flex-wrap align-items-center gap-1 tag-input-box" style="min-height:38px;cursor:text">' +
                '<span class="tag-input-pills d-flex flex-wrap gap-1"></span>' +
                '<input type="text" class="tag-input-text border-0 flex-grow-1 p-0" ' +
                       'style="outline:none;background:transparent;min-width:80px" ' +
                       'placeholder="Add a tag…" autocomplete="off" />' +
            '</div>' +
            '<div class="tag-input-suggest position-absolute border rounded bg-body shadow-sm d-none" ' +
                 'style="z-index:1065;max-height:180px;overflow-y:auto;font-size:.875rem"></div>';

        var box     = root.querySelector('.tag-input-box');
        var pillsEl = root.querySelector('.tag-input-pills');
        var input   = root.querySelector('.tag-input-text');
        var suggest = root.querySelector('.tag-input-suggest');

        root.style.position = 'relative';

        function renderPills() {
            pillsEl.innerHTML = selected.map(function (name) {
                var color = window.tagColor(name);
                var textCls = DARK_TEXT[color] ? ' text-dark' : '';
                return '<span class="badge rounded-pill d-inline-flex align-items-center' + textCls +
                    '" style="background:' + color + ';font-weight:500" data-tag="' + escHtml(name) + '">' +
                    escHtml(name) +
                    '<button type="button" class="btn-close ' + (DARK_TEXT[color] ? '' : 'btn-close-white ') +
                        'ms-1" style="font-size:.55rem" aria-label="Remove ' + escHtml(name) + '"></button>' +
                    '</span>';
            }).join('');
            pillsEl.querySelectorAll('.btn-close').forEach(function (b) {
                b.addEventListener('click', function (e) {
                    e.stopPropagation();
                    removeTag(b.parentElement.dataset.tag);
                });
            });
        }

        function addTag(name) {
            name = (name || '').trim();
            if (!name) return;
            var key = name.toLowerCase();
            if (seen[key]) return;
            seen[key] = true;
            selected.push(name);
            renderPills();
        }

        function removeTag(name) {
            var key = (name || '').toLowerCase();
            selected = selected.filter(function (n) { return n.toLowerCase() !== key; });
            delete seen[key];
            renderPills();
        }

        function hideSuggest() { suggest.classList.add('d-none'); }

        function showSuggest() {
            var q = input.value.trim().toLowerCase();
            var matches = all.filter(function (n) {
                return !seen[n.toLowerCase()] && (q === '' || n.toLowerCase().indexOf(q) !== -1);
            }).slice(0, 8);

            var rows = matches.map(function (n) {
                return '<div class="px-2 py-1 tag-suggest-item" style="cursor:pointer" data-name="' + escHtml(n) + '">' +
                    window.tagPillHtml(n) + '</div>';
            });

            // Offer to create a brand-new tag when the typed text isn't an exact existing match.
            var exact = q && all.some(function (n) { return n.toLowerCase() === q; });
            if (q && !exact && !seen[q]) {
                rows.push('<div class="px-2 py-1 text-primary tag-suggest-create" style="cursor:pointer;border-top:1px solid var(--bs-border-color)" ' +
                    'data-name="' + escHtml(input.value.trim()) + '"><i class="bi bi-plus-lg me-1"></i>Create "' +
                    escHtml(input.value.trim()) + '"</div>');
            }

            if (!rows.length) { hideSuggest(); return; }
            suggest.innerHTML = rows.join('');
            suggest.style.left = '0';
            suggest.style.right = '0';
            suggest.style.top = (box.offsetTop + box.offsetHeight + 2) + 'px';
            suggest.querySelectorAll('[data-name]').forEach(function (el) {
                el.addEventListener('mousedown', function (e) {
                    e.preventDefault();
                    addTag(el.dataset.name);
                    input.value = '';
                    hideSuggest();
                    input.focus();
                });
            });
            suggest.classList.remove('d-none');
        }

        box.addEventListener('click', function () { input.focus(); });
        input.addEventListener('focus', showSuggest);
        input.addEventListener('input', showSuggest);
        input.addEventListener('blur', function () { setTimeout(hideSuggest, 150); });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ',') {
                e.preventDefault();
                if (input.value.trim()) { addTag(input.value); input.value = ''; showSuggest(); }
            } else if (e.key === 'Backspace' && input.value === '' && selected.length) {
                removeTag(selected[selected.length - 1]);
            } else if (e.key === 'Escape') {
                hideSuggest();
            }
        });

        // Seed selection
        (selectedNames || []).forEach(function (t) { addTag(typeof t === 'string' ? t : t.name); });

        return {
            getTags: function () { return selected.slice(); },
            setTags: function (names) {
                selected = []; seen = Object.create(null);
                (names || []).forEach(function (t) { addTag(typeof t === 'string' ? t : t.name); });
            },
            setAll: function (names) { all = (names || []).map(function (t) { return typeof t === 'string' ? t : t.name; }); }
        };
    };
})();
