document.addEventListener('DOMContentLoaded', function () {
    if (typeof JsBarcode === 'undefined') return;
    document.querySelectorAll('.barcode-svg').forEach(function (el) {
        var sku = el.getAttribute('data-sku');
        if (!sku) return;
        try {
            JsBarcode(el, sku, {
                format: 'CODE128',
                displayValue: false,
                width: 1.8,
                height: 50,
                margin: 4
            });
        } catch (e) {
            el.parentElement.style.opacity = '0.5';
        }
    });
});
