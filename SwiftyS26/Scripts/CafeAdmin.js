/**
 * CafeAdmin.js  — GymHub Café Admin Module
 * Standalone script for /Views/CafeAdmin/Index.cshtml
 * Controller: CafeAdminController  →  routes are /CafeAdmin/*
 */

'use strict';

// =====================================================================
// GLOBAL STATE
// =====================================================================
var Cafe = {
    products: [],
    orders: [],
    currentEditProductId: null,
    currentOrderId: null
};

// =====================================================================
// HELPERS
// =====================================================================
function cafeToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

function cafeBanner(msg, type) {
    var existing = document.getElementById('cafe-success-banner');
    if (existing) existing.remove();

    var banner = document.createElement('div');
    banner.id = 'cafe-success-banner';
    banner.style.cssText = 'position:fixed;top:20px;right:20px;background:#fff;border:1px solid rgba(22,163,74,.3);border-left:4px solid #16a34a;border-radius:10px;padding:14px 20px;font-size:14px;font-weight:600;z-index:9999;box-shadow:0 8px 24px rgba(0,0,0,.12);max-width:380px;';

    if (type === 'error') {
        banner.style.borderLeftColor = '#DC2626';
        banner.style.borderColor = 'rgba(220,38,38,.3)';
        banner.style.color = '#991B1B';
    } else {
        banner.style.color = '#166534';
    }

    banner.innerHTML = msg;
    document.body.appendChild(banner);
    setTimeout(function () { if (banner.parentNode) banner.parentNode.removeChild(banner); }, 4500);
}

function cafePost(url, params) {
    var body = new URLSearchParams(params);
    body.append('__RequestVerificationToken', cafeToken());
    return fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: body.toString()
    }).then(function (r) { return r.json(); });
}

function set(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
}

// =====================================================================
// TAB NAVIGATION
// =====================================================================
var cafeSections = ['products', 'orders', 'revenue', 'collections'];
var cafeTitles = {
    products: 'CAFÉ — PRODUCTS',
    orders: 'CAFÉ — ORDERS',
    revenue: 'CAFÉ — REVENUE',
    collections: 'CAFÉ — COLLECTIONS'
};

function cafeShowSection(name) {
    cafeSections.forEach(function (s) {
        var sec = document.getElementById('cafe-section-' + s);
        var tab = document.getElementById('cafe-tab-' + s);
        if (sec) sec.classList.remove('active');
        if (tab) tab.classList.remove('active');
    });

    var sec = document.getElementById('cafe-section-' + name);
    var tab = document.getElementById('cafe-tab-' + name);
    if (sec) sec.classList.add('active');
    if (tab) tab.classList.add('active');

    var title = document.getElementById('cafe-section-title');
    if (title) title.textContent = cafeTitles[name] || 'CAFÉ MANAGEMENT';

    if (name === 'products') loadCafeProducts();
    if (name === 'orders') loadCafeOrders();
    if (name === 'revenue') loadCafeRevenue();
    if (name === 'collections') loadCollections();
}

// =====================================================================
// PRODUCTS — load & render
// =====================================================================
function loadCafeProducts() {
    fetch('/CafeAdmin/GetProducts')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) { cafeBanner('⚠️ Could not load products.', 'error'); return; }
            Cafe.products = data.products;
            renderProductsTable(data.products);
            set('cafe-stat-total', data.totalCount || 0);
            set('cafe-stat-active', data.activeCount || 0);
            set('cafe-stat-instock', data.inStockCount || 0);
            set('cafe-products-count', (data.totalCount || 0) + ' products');
        })
        .catch(function () { cafeBanner('⚠️ Could not load products.', 'error'); });
}

function renderProductsTable(products) {
    var tbody = document.getElementById('cafe-products-tbody');
    if (!tbody) return;

    if (!products || products.length === 0) {
        tbody.innerHTML = '<tr><td colspan="9" style="text-align:center;padding:32px;color:var(--muted);">No products yet. Click ➕ Add Product to get started.</td></tr>';
        return;
    }

    tbody.innerHTML = products.map(function (p) {
        var imgHtml = p.imagePath
            ? '<img src="' + p.imagePath + '" alt="' + p.name + '" style="width:48px;height:48px;object-fit:cover;border-radius:8px;border:1px solid var(--border);" />'
            : '<div style="font-size:28px;text-align:center;">' + (p.emoji || '☕') + '</div>';

        var activeBadge = p.isActive
            ? '<span class="badge badge-active">Visible</span>'
            : '<span class="badge badge-inactive">Hidden</span>';

        var stockBadge = p.inStock
            ? '<span class="badge badge-instock">In Stock</span>'
            : '<span class="badge badge-outstock">Out of Stock</span>';

        return '<tr>'
            + '<td>' + imgHtml + '</td>'
            + '<td><div style="font-weight:700;font-size:13px;">' + p.name + '</div>'
            + (p.description ? '<div style="font-size:11px;color:var(--muted);margin-top:2px;max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="' + p.description + '">' + p.description + '</div>' : '')
            + '</td>'
            + '<td><span class="badge badge-ready">' + p.category + '</span></td>'
            + '<td style="font-weight:700;color:var(--purple);">' + p.pointsCost + ' <span style="font-size:11px;color:var(--muted);font-weight:600;">pts</span></td>'
            + '<td style="font-weight:600;">R' + p.randValue + '</td>'
            + '<td>' + activeBadge + '</td>'
            + '<td>' + stockBadge + '</td>'
            + '<td style="font-size:12px;color:var(--muted);white-space:nowrap;">' + p.createdAt + '</td>'
            + '<td><div style="display:flex;gap:6px;align-items:center;">'
            + '<button class="btn-sm-edit" onclick="openProductModal(' + p.cafeProductId + ')">✏️ Edit</button>'
            + '<button class="btn-ghost"   onclick="toggleProduct(' + p.cafeProductId + ',\'active\')" title="' + (p.isActive ? 'Hide' : 'Show') + '">' + (p.isActive ? '👁️' : '🙈') + '</button>'
            + '<button class="btn-ghost"   onclick="toggleProduct(' + p.cafeProductId + ',\'stock\')"  title="' + (p.inStock ? 'Mark out of stock' : 'Mark in stock') + '">' + (p.inStock ? '✅' : '❌') + '</button>'
            + '<button class="btn-sm-delete" onclick="deleteProduct(' + p.cafeProductId + ')" title="Delete">🗑️</button>'
            + '</div></td></tr>';
    }).join('');
}

// =====================================================================
// PRODUCT MODAL — open / close / save
// =====================================================================
function openProductModal(productId) {
    Cafe.currentEditProductId = productId || null;
    var isEdit = !!productId;

    document.getElementById('cafe-modal-title').textContent = isEdit ? 'EDIT PRODUCT' : 'ADD PRODUCT';
    document.getElementById('cafe-modal-error').style.display = 'none';
    document.getElementById('cafe-img-file').value = '';
    document.getElementById('cafe-img-preview').style.display = 'none';

    if (isEdit) {
        var p = Cafe.products.find(function (x) { return x.cafeProductId === productId; });
        if (!p) return;
        document.getElementById('cafe-edit-id').value = p.cafeProductId;
        document.getElementById('cafe-name').value = p.name || '';
        document.getElementById('cafe-description').value = p.description || '';
        document.getElementById('cafe-category').value = p.category || '';
        document.getElementById('cafe-emoji').value = p.emoji || '';
        document.getElementById('cafe-points').value = p.pointsCost || '';
        document.getElementById('cafe-rand').value = p.randValue || '';
        document.getElementById('cafe-isactive').checked = p.isActive;
        document.getElementById('cafe-instock').checked = p.inStock;
        if (p.imagePath) {
            var img = document.getElementById('cafe-img-preview');
            img.src = p.imagePath;
            img.style.display = 'block';
        }
    } else {
        ['cafe-edit-id', 'cafe-name', 'cafe-description', 'cafe-emoji', 'cafe-points', 'cafe-rand'].forEach(function (id) {
            document.getElementById(id).value = '';
        });
        document.getElementById('cafe-category').value = '';
        document.getElementById('cafe-isactive').checked = true;
        document.getElementById('cafe-instock').checked = true;
    }

    document.getElementById('cafe-product-modal').style.display = 'flex';
}

function closeProductModal() {
    document.getElementById('cafe-product-modal').style.display = 'none';
    Cafe.currentEditProductId = null;
}

function previewImage(input) {
    var img = document.getElementById('cafe-img-preview');
    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) { img.src = e.target.result; img.style.display = 'block'; };
        reader.readAsDataURL(input.files[0]);
    } else {
        img.style.display = 'none';
    }
}

function saveProduct() {
    var errEl = document.getElementById('cafe-modal-error');
    var saveBtn = document.getElementById('cafe-save-btn');
    errEl.style.display = 'none';

    var name = document.getElementById('cafe-name').value.trim();
    var category = document.getElementById('cafe-category').value;
    var points = parseInt(document.getElementById('cafe-points').value);
    var rand = parseFloat(document.getElementById('cafe-rand').value) || 0;

    if (!name) { errEl.textContent = 'Product name is required.'; errEl.style.display = 'block'; return; }
    if (!category) { errEl.textContent = 'Please select a category.'; errEl.style.display = 'block'; return; }
    if (!points || points < 1) { errEl.textContent = 'Points cost must be at least 1.'; errEl.style.display = 'block'; return; }

    var formData = new FormData();
    formData.append('name', name);
    formData.append('description', document.getElementById('cafe-description').value.trim());
    formData.append('category', category);
    formData.append('emoji', document.getElementById('cafe-emoji').value.trim());
    formData.append('pointsCost', points);
    formData.append('randValue', rand);
    formData.append('isActive', document.getElementById('cafe-isactive').checked);
    formData.append('inStock', document.getElementById('cafe-instock').checked);
    formData.append('__RequestVerificationToken', cafeToken());

    var fileInput = document.getElementById('cafe-img-file');
    if (fileInput.files[0]) formData.append('imageFile', fileInput.files[0]);

    var isEdit = !!Cafe.currentEditProductId;
    if (isEdit) formData.append('cafeProductId', Cafe.currentEditProductId);

    saveBtn.disabled = true;
    saveBtn.textContent = 'Saving…';

    fetch(isEdit ? '/CafeAdmin/EditProduct' : '/CafeAdmin/AddProduct', {
        method: 'POST',
        body: formData
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            saveBtn.disabled = false;
            saveBtn.textContent = '✅ Save Product';
            if (!data.success) { errEl.textContent = data.message; errEl.style.display = 'block'; return; }
            closeProductModal();
            loadCafeProducts();
            cafeBanner(data.message);
        })
        .catch(function () {
            saveBtn.disabled = false;
            saveBtn.textContent = '✅ Save Product';
            errEl.textContent = 'Connection error. Please try again.';
            errEl.style.display = 'block';
        });
}

function toggleProduct(productId, toggleType) {
    cafePost('/CafeAdmin/ToggleProduct', { cafeProductId: productId, toggleType: toggleType })
        .then(function (data) {
            if (data.success) { loadCafeProducts(); cafeBanner(data.message); }
            else { cafeBanner(data.message || 'Toggle failed.', 'error'); }
        })
        .catch(function () { cafeBanner('Connection error.', 'error'); });
}

function deleteProduct(productId) {
    var p = Cafe.products.find(function (x) { return x.cafeProductId === productId; });
    if (!confirm('Delete "' + (p ? p.name : 'this product') + '"? This cannot be undone.')) return;

    cafePost('/CafeAdmin/DeleteProduct', { cafeProductId: productId })
        .then(function (data) {
            if (data.success) { loadCafeProducts(); cafeBanner(data.message); }
            else { cafeBanner(data.message, 'error'); }
        })
        .catch(function () { cafeBanner('Connection error.', 'error'); });
}

// =====================================================================
// ORDERS — load & render
// =====================================================================
var currentOrderFilter = 'all';

function loadCafeOrders(status) {
    if (status !== undefined) currentOrderFilter = status;

    var url = '/CafeAdmin/GetOrders?status=' + currentOrderFilter;
    var fromEl = document.getElementById('cafe-order-from');
    var toEl = document.getElementById('cafe-order-to');
    if (fromEl && fromEl.value) url += '&from=' + fromEl.value;
    if (toEl && toEl.value) url += '&to=' + toEl.value;

    fetch(url)
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) { cafeBanner('⚠️ Could not load orders.', 'error'); return; }
            Cafe.orders = data.orders;
            renderOrdersTable(data.orders);

            set('cafe-order-stat-pending', data.pendingCount || 0);
            set('cafe-order-stat-ready', data.readyCount || 0);
            set('cafe-order-stat-collected', data.collectedCount || 0);
            set('cafe-order-stat-cancelled', data.cancelledCount || 0);
            set('cafe-order-revenue', 'R' + (data.totalRevenue || '0.00'));
            set('cafe-orders-count', (data.totalCount || 0) + ' orders');

            ['all', 'Pending', 'Ready', 'Collected', 'Cancelled'].forEach(function (s) {
                var btn = document.getElementById('cafe-order-tab-' + s.toLowerCase());
                if (btn) btn.classList.toggle('active', currentOrderFilter === s);
            });
        })
        .catch(function () { cafeBanner('⚠️ Could not load orders.', 'error'); });
}

function renderOrdersTable(orders) {
    var tbody = document.getElementById('cafe-orders-tbody');
    if (!tbody) return;

    if (!orders || orders.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;padding:32px;color:var(--muted);">No orders found.</td></tr>';
        return;
    }

    tbody.innerHTML = orders.map(function (o) {
        var badgeClass = 'badge badge-' + o.status.toLowerCase();
        var statusOptions = ['Pending', 'Ready', 'Collected', 'Cancelled'].map(function (s) {
            return '<option value="' + s + '"' + (o.status === s ? ' selected' : '') + '>' + s + '</option>';
        }).join('');

        return '<tr>'
            + '<td style="font-weight:600;">' + o.memberName + '</td>'
            + '<td><span class="member-id">' + o.memberNumber + '</span></td>'
            + '<td><div style="font-weight:700;font-size:13px;">' + o.productName + '</div>'
            + '<div style="font-size:11px;color:var(--muted);">' + o.category + '</div></td>'
            + '<td style="font-weight:700;color:var(--purple);">' + o.pointsSpent + ' <span style="font-size:10px;color:var(--muted);">pts</span></td>'
            + '<td style="font-weight:600;">R' + o.randValue + '</td>'
            + '<td><span class="' + badgeClass + '">' + o.status + '</span></td>'
            + '<td style="font-size:12px;color:var(--muted);white-space:nowrap;">' + o.redeemedAt + '</td>'
            + '<td><select class="cafe-status-select" onchange="updateOrderStatus(' + o.redemptionId + ', this.value)">' + statusOptions + '</select></td>'
            + '</tr>';
    }).join('');
}

function updateOrderStatus(redemptionId, newStatus) {
    cafePost('/CafeAdmin/UpdateOrderStatus', { redemptionId: redemptionId, newStatus: newStatus, notes: '' })
        .then(function (data) {
            if (data.success) { loadCafeOrders(); cafeBanner('✅ ' + data.message); }
            else { cafeBanner(data.message || 'Update failed.', 'error'); loadCafeOrders(); }
        })
        .catch(function () { cafeBanner('Connection error.', 'error'); });
}

// =====================================================================
// REVENUE — load & render
// =====================================================================
function loadCafeRevenue() {
    fetch('/CafeAdmin/GetRevenueSummary')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) { cafeBanner('⚠️ Could not load revenue data.', 'error'); return; }

            set('cafe-rev-alltime', 'R' + data.allTimeRevenue);
            set('cafe-rev-alltime-orders', data.allTimeOrders + ' orders');
            set('cafe-rev-alltime-pts', data.allTimePoints + ' pts redeemed');
            set('cafe-rev-thismonth', 'R' + data.thisMonthRevenue);
            set('cafe-rev-thismonth-orders', data.thisMonthOrders + ' orders');
            set('cafe-rev-lastmonth', 'R' + data.lastMonthRevenue);
            set('cafe-rev-lastmonth-orders', data.lastMonthOrders + ' orders');

            var tbody = document.getElementById('cafe-top-products-tbody');
            if (tbody) {
                tbody.innerHTML = (!data.topProducts || data.topProducts.length === 0)
                    ? '<tr><td colspan="4" style="text-align:center;padding:20px;color:var(--muted);">No data yet.</td></tr>'
                    : data.topProducts.map(function (p, i) {
                        return '<tr>'
                            + '<td style="font-weight:700;color:var(--muted);">' + (i + 1) + '</td>'
                            + '<td style="font-weight:600;">' + p.productName + '</td>'
                            + '<td style="font-weight:700;color:var(--orange);">' + p.orderCount + '</td>'
                            + '<td style="font-weight:700;color:var(--green);">R' + p.revenue + '</td>'
                            + '</tr>';
                    }).join('');
            }

            var catBody = document.getElementById('cafe-category-tbody');
            if (catBody) {
                catBody.innerHTML = (!data.byCategory || data.byCategory.length === 0)
                    ? '<tr><td colspan="3" style="text-align:center;padding:20px;color:var(--muted);">No data yet.</td></tr>'
                    : data.byCategory.map(function (c) {
                        return '<tr>'
                            + '<td><span class="badge badge-ready">' + c.category + '</span></td>'
                            + '<td style="font-weight:700;color:var(--orange);">' + c.orderCount + '</td>'
                            + '<td style="font-weight:700;color:var(--green);">R' + c.revenue + '</td>'
                            + '</tr>';
                    }).join('');
            }
        })
        .catch(function () { cafeBanner('⚠️ Could not load revenue data.', 'error'); });
}

// =====================================================================
// COLLECTIONS
// =====================================================================
var colVideoStream = null;
var colScanInterval = null;
var colScanCooldown = false;
var currentRedemptionId = null;

function loadCollections() {
    fetch('/CafeAdmin/GetCollections')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) return;
            renderCollectionsTable(data.collections);
            set('col-total', data.totalToday || 0);
            set('col-pending', data.pendingCount || 0);
            set('col-collected', data.collectedCount || 0);
            set('col-revenue', 'R' + (data.revenueToday || '0.00'));
            set('col-table-count', (data.totalToday || 0) + ' orders today');
        })
        .catch(function () { cafeBanner('⚠️ Could not load collections.', 'error'); });
}

function renderCollectionsTable(collections) {
    var tbody = document.getElementById('col-table-body');
    if (!tbody) return;

    if (!collections || collections.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;padding:32px;color:var(--muted);">No orders today yet.</td></tr>';
        return;
    }

    tbody.innerHTML = collections.map(function (c) {
        var badgeClass = c.status === 'Collected' ? 'badge badge-collected'
            : c.status === 'Cancelled' ? 'badge badge-cancelled'
                : 'badge badge-pending';
        return '<tr>'
            + '<td style="font-weight:600;">' + c.memberName + '</td>'
            + '<td><span class="member-id">' + c.memberNumber + '</span></td>'
            + '<td>' + c.productEmoji + ' ' + c.productName + '</td>'
            + '<td style="font-weight:700;color:var(--purple);">' + c.pointsSpent + '</td>'
            + '<td style="font-weight:700;letter-spacing:2px;color:var(--orange);">' + c.collectionCode + '</td>'
            + '<td><span class="' + badgeClass + '">' + c.status + '</span></td>'
            + '<td style="font-size:12px;color:var(--muted);">' + c.redeemedAt + '</td>'
            + '<td style="font-size:12px;color:var(--muted);">' + (c.collectedAt || '—') + '</td>'
            + '</tr>';
    }).join('');
}

function startColCamera() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        cafeBanner('Camera not supported. Use manual entry.', 'error'); return;
    }
    navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
        .then(function (stream) {
            colVideoStream = stream;
            var video = document.getElementById('col-camera-feed');
            video.srcObject = stream;
            video.style.display = 'block';
            video.play();
            document.getElementById('col-camera-idle').style.display = 'none';
            document.getElementById('col-scan-overlay').style.display = 'block';
            document.getElementById('col-start-btn').style.display = 'none';
            document.getElementById('col-stop-btn').style.display = 'block';
            colScanInterval = setInterval(colScanFrame, 500);
        })
        .catch(function (err) { cafeBanner('Camera error: ' + err.message, 'error'); });
}

function stopColCamera() {
    if (colVideoStream) { colVideoStream.getTracks().forEach(function (t) { t.stop(); }); colVideoStream = null; }
    if (colScanInterval) { clearInterval(colScanInterval); colScanInterval = null; }
    var v = document.getElementById('col-camera-feed'); if (v) v.style.display = 'none';
    var i = document.getElementById('col-camera-idle'); if (i) i.style.display = 'flex';
    var o = document.getElementById('col-scan-overlay'); if (o) o.style.display = 'none';
    var sb = document.getElementById('col-start-btn'); if (sb) sb.style.display = 'block';
    var st = document.getElementById('col-stop-btn'); if (st) st.style.display = 'none';
}

function colScanFrame() {
    if (colScanCooldown) return;
    var video = document.getElementById('col-camera-feed');
    var canvas = document.getElementById('col-scan-canvas');
    if (!video || video.readyState !== video.HAVE_ENOUGH_DATA) return;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    var ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    if (typeof jsQR === 'undefined') return;
    var code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: 'dontInvert' });
    if (code && code.data) {
        colScanCooldown = true;
        var parts = code.data.split('|');
        var codePart = parts.find(function (p) { return p.startsWith('CODE:'); });
        var extractedCode = codePart ? codePart.replace('CODE:', '') : code.data;
        document.getElementById('col-code-input').value = extractedCode;
        verifyCode();
    }
}

function verifyCode() {
    var code = document.getElementById('col-code-input').value.trim().toUpperCase();
    if (!code || code.length < 4) { cafeBanner('Please enter a valid collection code.', 'error'); return; }
    stopColCamera();
    showColState('waiting');

    cafePost('/CafeAdmin/VerifyCollectionCode', { code: code })
        .then(function (data) {
            if (!data.success) {
                document.getElementById('col-error-msg').textContent = data.message;
                showColState('error');
                setTimeout(function () { colScanCooldown = false; }, 2000);
                return;
            }
            currentRedemptionId = data.redemptionId;
            document.getElementById('col-product-emoji').textContent = data.productEmoji || '☕';
            document.getElementById('col-product-name').textContent = data.productName || '—';
            document.getElementById('col-member-name').textContent = data.memberName || '—';
            document.getElementById('col-member-number').textContent = data.memberNumber || '—';
            document.getElementById('col-points-spent').textContent = data.pointsSpent + ' pts';
            document.getElementById('col-redeemed-at').textContent = data.redeemedAt || '—';
            document.getElementById('col-admin-notes').value = '';
            document.getElementById('col-error').style.display = 'none';

            var statusBadge = data.status === 'Collected'
                ? '<span class="badge badge-collected">Collected</span>'
                : data.status === 'Pending'
                    ? '<span class="badge badge-pending">Pending</span>'
                    : '<span class="badge">' + data.status + '</span>';
            document.getElementById('col-order-status').innerHTML = statusBadge;
            showColState('order');
        })
        .catch(function () {
            document.getElementById('col-error-msg').textContent = 'Connection error. Please try again.';
            showColState('error');
        });
}

function confirmCollection() {
    if (!currentRedemptionId) return;
    var notes = document.getElementById('col-admin-notes').value.trim();
    var errEl = document.getElementById('col-error');
    var btn = document.getElementById('col-confirm-btn');
    errEl.style.display = 'none';
    btn.disabled = true;
    btn.textContent = 'Confirming…';

    cafePost('/CafeAdmin/ConfirmCollection', { redemptionId: currentRedemptionId, adminNotes: notes })
        .then(function (data) {
            btn.disabled = false;
            btn.textContent = '✅ Confirm Collection';
            if (!data.success) { errEl.textContent = data.message; errEl.style.display = 'block'; return; }
            document.getElementById('col-confirmed-msg').textContent =
                data.productName + ' collected by ' + data.memberName + ' at ' + data.collectedAt;
            showColState('confirmed');
            loadCollections();
            cafeBanner('🎉 ' + data.message);
            setTimeout(function () { colScanCooldown = false; }, 1500);
        })
        .catch(function () {
            btn.disabled = false;
            btn.textContent = '✅ Confirm Collection';
            errEl.textContent = 'Connection error.';
            errEl.style.display = 'block';
        });
}

function showColState(state) {
    ['waiting', 'order', 'error', 'confirmed'].forEach(function (s) {
        var el = document.getElementById('col-result-' + s);
        if (el) el.style.display = 'none';
    });
    var target = document.getElementById('col-result-' + state);
    if (target) target.style.display = 'block';
}

function resetCollection() {
    currentRedemptionId = null;
    colScanCooldown = false;
    var inp = document.getElementById('col-code-input');
    if (inp) inp.value = '';
    showColState('waiting');
}

// =====================================================================
// INIT — runs once DOM is ready
// =====================================================================
document.addEventListener('DOMContentLoaded', function () {
    // Show products tab by default
    cafeShowSection('products');

    // Close modal when clicking the dark overlay behind it
    var modal = document.getElementById('cafe-product-modal');
    if (modal) {
        modal.addEventListener('click', function (e) {
            if (e.target === modal) closeProductModal();
        });
    }
});