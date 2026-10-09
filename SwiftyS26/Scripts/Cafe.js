/* ====================================================
   GYMHUB CAFÉ — Cafe.js
   External café page — product display & redemption
==================================================== */

var currentPoints = 0;
var selectedProductId = null;
var selectedProductPts = 0;
var allProducts = [];

/* ====================================================
   INIT
==================================================== */
document.addEventListener('DOMContentLoaded', function () {
    loadPoints();
    loadProducts();
});

/* ====================================================
   LOAD MEMBER POINTS BALANCE
   Uses the same endpoint as the dashboard wallet section
==================================================== */
function loadPoints() {
    fetch('/Dashboard/GetLoyaltySummary')
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (!d.success) return;
            currentPoints = d.currentPoints || 0;
            var el = document.getElementById('hero-pts-display');
            if (el) el.textContent = currentPoints.toLocaleString() + ' pts';
        })
        .catch(function () {
            var el = document.getElementById('hero-pts-display');
            if (el) el.textContent = '— pts';
        });
}

/* ====================================================
   LOAD ALL CAFÉ PRODUCTS
   Calls CafeController.GetProducts — returns full list
==================================================== */
function loadProducts() {
    var grid = document.getElementById('cafe-products-grid');
    if (!grid) return;

    grid.innerHTML = '<div class="cafe-empty"><div class="cafe-empty-icon">☕</div><div>Loading products…</div></div>';

    fetch('/Cafe/GetProducts')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success || !data.products || data.products.length === 0) {
                grid.innerHTML = '<div class="cafe-empty"><div class="cafe-empty-icon">🍽️</div><div>No products available right now.</div></div>';
                return;
            }

            allProducts = data.products;

            // Build filter tabs from actual categories returned
            buildFilterTabs(data.categories);

            renderProducts(allProducts);
        })
        .catch(function () {
            if (grid) grid.innerHTML = '<div class="cafe-empty"><div class="cafe-empty-icon">⚠️</div><div>Could not load products. Try refreshing.</div></div>';
        });
}

/* ====================================================
   BUILD FILTER TABS FROM CATEGORIES
==================================================== */
function buildFilterTabs(categories) {
    var container = document.getElementById('cafe-filters');
    if (!container || !categories) return;

    var html = '<button class="filter-tab active" onclick="filterProducts(\'All\', this)">All</button>';
    categories.forEach(function (cat) {
        html += '<button class="filter-tab" onclick="filterProducts(\'' + cat + '\', this)">' + cat + '</button>';
    });
    container.innerHTML = html;
}

/* ====================================================
   FILTER PRODUCTS
==================================================== */
function filterProducts(category, btn) {
    document.querySelectorAll('.filter-tab').forEach(function (t) { t.classList.remove('active'); });
    if (btn) btn.classList.add('active');

    var filtered = category === 'All'
        ? allProducts
        : allProducts.filter(function (p) { return p.category === category; });

    renderProducts(filtered);
}

/* ====================================================
   RENDER PRODUCT CARDS
   Property names match what CafeController returns:
   id, name, description, emoji, imagePath, category,
   pointsCost, randValue, inStock
==================================================== */
function renderProducts(products) {
    var grid = document.getElementById('cafe-products-grid');
    if (!grid) return;

    if (!products || products.length === 0) {
        grid.innerHTML = '<div class="cafe-empty"><div class="cafe-empty-icon">🍽️</div><div>No products in this category.</div></div>';
        return;
    }

    grid.innerHTML = products.map(function (p) {
        var outOfStock = !p.inStock;
        var canAfford = currentPoints >= p.pointsCost;
        var disabled = outOfStock || !canAfford;

        // Show product image if uploaded, otherwise fall back to emoji
        var imgHtml = p.imagePath
            ? '<img src="' + p.imagePath + '" alt="' + escHtml(p.name) + '" onerror="this.style.display=\'none\';document.getElementById(\'emoji-' + p.id + '\').style.display=\'block\'">'
            + '<div id="emoji-' + p.id + '" class="img-placeholder" style="display:none;">' + p.emoji + '</div>'
            : '<div class="img-placeholder">' + p.emoji + '</div>';

        var ribbon = outOfStock
            ? '<div class="out-of-stock-ribbon">Out of Stock</div>'
            : !canAfford
                ? '<div class="out-of-stock-ribbon low-pts">Need ' + (p.pointsCost - currentPoints).toLocaleString() + ' more pts</div>'
                : '';

        var btnText = outOfStock ? 'Out of Stock'
            : !canAfford ? 'Not Enough Points'
                : '🎟 Redeem ' + p.pointsCost.toLocaleString() + ' pts';

        return '<div class="cafe-card' + (outOfStock ? ' out-of-stock' : '') + '" data-category="' + p.category + '">'
            + '<div class="cafe-card-img">'
            + imgHtml
            + '<span class="cafe-cat-badge">' + p.category + '</span>'
            + ribbon
            + '</div>'
            + '<div class="cafe-card-body">'
            + '<div class="cafe-card-name">' + escHtml(p.name) + '</div>'
            + '<div class="cafe-card-desc">' + escHtml(p.description || '') + '</div>'
            + '<div class="cafe-card-pts">' + p.pointsCost.toLocaleString() + ' <span>pts</span></div>'
            + '</div>'
            + '<button class="cafe-redeem-btn" '
            + (disabled ? 'disabled' : 'onclick="openModal(' + p.id + ',' + p.pointsCost + ')"')
            + '>' + btnText + '</button>'
            + '</div>';
    }).join('');
}

/* ====================================================
   OPEN REDEMPTION MODAL
==================================================== */
function openModal(productId, pointsCost) {
    var product = allProducts.find(function (p) { return p.id === productId; });
    if (!product) return;

    selectedProductId = productId;
    selectedProductPts = pointsCost;

    // Populate modal fields
    var imgEl = document.getElementById('modal-img');
    if (imgEl) {
        if (product.imagePath) {
            imgEl.src = product.imagePath;
            imgEl.style.display = 'block';
        } else {
            imgEl.style.display = 'none';
        }
    }

    setText('modal-name', product.emoji + ' ' + product.name);
    setText('modal-desc', product.description || '');
    setText('modal-pts', product.pointsCost.toLocaleString() + ' pts');
    setText('modal-balance', currentPoints.toLocaleString() + ' pts');

    // Reset state
    hideEl('modal-error');
    hideEl('modal-success');
    hideEl('modal-code-box');
    setText('modal-code-value', '——');

    var btn = document.getElementById('modal-confirm-btn');
    if (btn) {
        btn.disabled = false;
        btn.textContent = 'Redeem Now';
        btn.style.display = 'block';
    }

    document.getElementById('redeemModal').classList.add('open');
}

function closeModal() {
    document.getElementById('redeemModal').classList.remove('open');
    selectedProductId = selectedProductPts = null;
}

/* ====================================================
   CONFIRM REDEMPTION
   Posts to /Dashboard/RedeemProduct (correct endpoint)
   Includes CSRF token from the hidden input
==================================================== */
function confirmRedeem() {
    if (!selectedProductId) return;

    if (currentPoints < selectedProductPts) {
        showModalError('You need ' + selectedProductPts.toLocaleString() + ' pts but only have ' + currentPoints.toLocaleString() + ' pts.');
        return;
    }

    var btn = document.getElementById('modal-confirm-btn');
    btn.disabled = true;
    btn.textContent = '⏳ Redeeming…';
    hideEl('modal-error');

    // Get CSRF token
    var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    var token = tokenEl ? tokenEl.value : '';

    fetch('/Dashboard/RedeemProduct', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            productId: selectedProductId,
            __RequestVerificationToken: token
        }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (d) {
            if (d.success) {
                // Update local points balance
                currentPoints -= selectedProductPts;
                var heroEl = document.getElementById('hero-pts-display');
                if (heroEl) heroEl.textContent = currentPoints.toLocaleString() + ' pts';
                setText('modal-balance', currentPoints.toLocaleString() + ' pts');

                // Show collection code
                setText('modal-code-value', d.collectionCode || '??????');
                showEl('modal-code-box');

                // Show success message
                var succEl = document.getElementById('modal-success');
                if (succEl) {
                    succEl.textContent = d.message || '✅ Redeemed! Show the code at the café counter.';
                    succEl.style.display = 'block';
                }

                btn.textContent = '✓ Done!';
                btn.style.display = 'none'; // hide confirm btn after success

                // Refresh product cards so affordability updates
                renderProducts(allProducts);

                showToast('🎉 Redeemed! Collection code: ' + d.collectionCode, 'success');
            } else {
                showModalError(d.message || 'Redemption failed. Please try again.');
                btn.disabled = false;
                btn.textContent = 'Redeem Now';
            }
        })
        .catch(function () {
            showModalError('Connection error. Please try again.');
            btn.disabled = false;
            btn.textContent = 'Redeem Now';
        });
}

/* ====================================================
   HELPERS
==================================================== */
function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
}

function showEl(id) {
    var el = document.getElementById(id);
    if (el) el.style.display = 'block';
}

function hideEl(id) {
    var el = document.getElementById(id);
    if (el) el.style.display = 'none';
}

function showModalError(msg) {
    var el = document.getElementById('modal-error');
    if (el) { el.textContent = msg; el.style.display = 'block'; }
}

function showToast(msg, type) {
    var t = document.getElementById('toast');
    if (!t) return;
    t.textContent = msg;
    t.className = 'toast ' + (type || '');
    t.classList.add('show');
    setTimeout(function () { t.classList.remove('show'); }, 4000);
}

function escHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}