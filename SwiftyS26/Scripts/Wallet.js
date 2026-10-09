/* ====================================================
   GYMHUB WALLET — wallet.js
   Handles: loyalty summary, café products, points redemption,
            wallet top-up (Paystack), wallet→points conversion,
            points history, wallet history, redemption history.
   The DashboardController endpoints are unchanged.
==================================================== */

/* ── Helper: get CSRF token ── */
function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

/* ── Helper: escape HTML ── */
function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}

/* ── Mobile sidebar toggle ── */
function toggleSidebar() {
    var s = document.getElementById('sidebar');
    if (s) s.classList.toggle('open');
}

/* ── Logout confirmation ── */
function confirmLogout() { return confirm('Are you sure you want to log out?'); }

/* ── Greeting ── */
function setGreeting() {
    var h = new Date().getHours();
    var el = document.getElementById('greeting-time');
    if (!el) return;
    el.textContent = h < 12 ? 'morning' : h < 17 ? 'afternoon' : 'evening';
}

/* ====================================================
   BOOTSTRAP — runs everything on page load
==================================================== */
document.addEventListener('DOMContentLoaded', function () {
    setGreeting();
    loadWalletPage();
});

function loadWalletPage() {
    loadLoyaltySummary();
    loadCafeProducts();
    loadPointsHistory();
    loadWalletHistory();
    loadRedemptionHistory();
}

/* ====================================================
   LOYALTY SUMMARY — points + wallet balance
==================================================== */
function loadLoyaltySummary() {
    fetch('/Dashboard/GetLoyaltySummary')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) return;
            var ptEl = document.getElementById('wp-points-balance');
            var wlEl = document.getElementById('wp-wallet-balance');
            if (ptEl) ptEl.textContent = data.currentPoints.toLocaleString() + ' pts';
            if (wlEl) wlEl.textContent = 'R' + data.walletBalance;
        })
        .catch(function () { console.error('[Wallet] Could not load loyalty summary.'); });
}

/* ====================================================
   CAFÉ PRODUCTS — 4 featured cards
==================================================== */
function loadCafeProducts() {
    fetch('/Dashboard/GetCafeProducts?featured=true')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var grid = document.getElementById('cafe-products-grid');
            if (!grid) return;

            if (!data.success || !data.products || data.products.length === 0) {
                grid.innerHTML = '<div style="text-align:center;padding:24px;color:#94A3B8;font-size:13px;grid-column:1/-1;">No products available right now.</div>';
                return;
            }

            grid.innerHTML = data.products.map(function (p) {
                var outBadge = p.inStock
                    ? ''
                    : '<div style="position:absolute;top:6px;right:6px;background:rgba(239,68,68,0.9);color:#fff;font-size:9px;font-weight:700;padding:2px 6px;border-radius:4px;text-transform:uppercase;">Out</div>';

                var btnStyle = p.inStock
                    ? 'background:linear-gradient(135deg,#2563EB,#3B82F6);color:#fff;cursor:pointer;'
                    : 'background:#F1F5F9;color:#CBD5E1;cursor:not-allowed;';

                return '<div style="position:relative;background:#F8FAFE;border:1px solid #E2E8F0;border-radius:12px;padding:16px 12px;text-align:center;transition:all 0.2s;">'
                    + outBadge
                    + '<div style="font-size:38px;margin-bottom:8px;">' + p.emoji + '</div>'
                    + '<div style="font-size:12px;font-weight:700;color:#0F172A;margin-bottom:4px;line-height:1.3;">' + escHtml(p.name) + '</div>'
                    + '<div style="font-family:\'Bebas Neue\';font-size:20px;color:#F59E0B;letter-spacing:1px;margin-bottom:12px;">' + p.pointsCost + ' pts</div>'
                    + '<button onclick="redeemProduct(' + p.id + ',\'' + escHtml(p.name) + '\',' + p.pointsCost + ',' + p.inStock + ')"'
                    + ' style="width:100%;padding:9px 0;border:none;border-radius:8px;font-family:\'DM Sans\';font-size:12px;font-weight:700;transition:all 0.2s;' + btnStyle + '">'
                    + (p.inStock ? 'Redeem' : 'Out of Stock')
                    + '</button>'
                    + '</div>';
            }).join('');
        })
        .catch(function () { console.error('[Wallet] Could not load café products.'); });
}

/* ====================================================
   REDEEM A PRODUCT
==================================================== */
function redeemProduct(productId, productName, pointsCost, inStock) {
    if (!inStock) return;

    var errEl = document.getElementById('cafe-redeem-error');
    var succEl = document.getElementById('cafe-redeem-success');
    if (errEl) errEl.style.display = 'none';
    if (succEl) succEl.style.display = 'none';

    if (!confirm('Redeem ' + productName + ' for ' + pointsCost + ' points?')) return;

    fetch('/Dashboard/RedeemProduct', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            productId: productId,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (data.success) {
                if (succEl) {
                    succEl.innerHTML =
                        '✅ <strong>' + escHtml(data.productName) + '</strong> redeemed!<br/>'
                        + '🎟️ Collection Code: <strong style="font-size:22px;letter-spacing:4px;color:#F59E0B;">' + data.collectionCode + '</strong><br/>'
                        + '<span style="font-size:12px;color:#16A34A;">Show this code at the café counter. A confirmation email has been sent to you.</span>';
                    succEl.style.display = 'block';
                    succEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
                loadLoyaltySummary();
                loadPointsHistory();
                loadRedemptionHistory();
                loadCafeProducts(); // refresh stock status
            } else {
                if (errEl) { errEl.textContent = data.message; errEl.style.display = 'block'; }
            }
        })
        .catch(function () {
            if (errEl) { errEl.textContent = 'Connection error. Please try again.'; errEl.style.display = 'block'; }
        });
}

/* ====================================================
   CONVERT WALLET BALANCE → POINTS  (R1 = 10 pts)
==================================================== */
function setConvertAmount(amount) {
    var input = document.getElementById('wp-convert-input');
    if (input) { input.value = amount; updateConvertPreview(); }
}

function updateConvertPreview() {
    var input = document.getElementById('wp-convert-input');
    var preview = document.getElementById('wp-convert-preview');
    if (!input || !preview) return;
    var amount = parseFloat(input.value) || 0;
    preview.textContent = (amount * 10).toLocaleString() + ' pts';
}

function convertWalletToPoints() {
    var input = document.getElementById('wp-convert-input');
    var errEl = document.getElementById('wp-convert-error');
    var succEl = document.getElementById('wp-convert-success');
    var btn = document.getElementById('wp-convert-btn');
    var amount = parseFloat(input ? input.value : 0) || 0;

    if (errEl) errEl.style.display = 'none';
    if (succEl) succEl.style.display = 'none';

    if (amount < 10) {
        if (errEl) { errEl.textContent = 'Minimum conversion is R10.00 (= 100 points).'; errEl.style.display = 'block'; }
        return;
    }

    if (btn) { btn.disabled = true; btn.textContent = '⏳ Converting...'; }

    fetch('/Dashboard/ConvertWalletToPoints', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            amount: amount,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (btn) { btn.disabled = false; btn.textContent = '💱 Convert to Points'; }
            if (data.success) {
                if (succEl) { succEl.textContent = data.message; succEl.style.display = 'block'; }
                if (input) input.value = '';
                var preview = document.getElementById('wp-convert-preview');
                if (preview) preview.textContent = '0 pts';
                loadLoyaltySummary();
                loadPointsHistory();
                loadWalletHistory();
            } else {
                if (errEl) { errEl.textContent = data.message; errEl.style.display = 'block'; }
            }
        })
        .catch(function () {
            if (btn) { btn.disabled = false; btn.textContent = '💱 Convert to Points'; }
            if (errEl) { errEl.textContent = 'Connection error. Please try again.'; errEl.style.display = 'block'; }
        });
}

/* ====================================================
   TOP-UP WALLET via Paystack
==================================================== */
function setTopUp(amount) {
    var input = document.getElementById('topup-input');
    if (input) input.value = amount;
}

function startTopUp() {
    var input = document.getElementById('topup-input');
    var errEl = document.getElementById('topup-error');
    var succEl = document.getElementById('topup-success');
    var btn = document.getElementById('topup-btn');
    var amount = parseFloat(input ? input.value : 0) || 0;

    if (errEl) errEl.style.display = 'none';
    if (succEl) succEl.style.display = 'none';

    if (amount < 10) {
        if (errEl) { errEl.textContent = 'Minimum top-up is R10.00.'; errEl.style.display = 'block'; }
        return;
    }
    if (amount > 5000) {
        if (errEl) { errEl.textContent = 'Maximum top-up is R5,000.00.'; errEl.style.display = 'block'; }
        return;
    }

    if (btn) { btn.disabled = true; btn.textContent = '⏳ Loading...'; }

    var token = getToken();

    fetch('/Dashboard/InitializeTopUp', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ amount: amount, __RequestVerificationToken: token }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) {
                if (btn) { btn.disabled = false; btn.textContent = '💳 Add Funds'; }
                if (errEl) { errEl.textContent = data.message; errEl.style.display = 'block'; }
                return;
            }

            var handler = PaystackPop.setup({
                key: data.publicKey,
                email: data.email,
                amount: data.amount,
                currency: data.currency,
                ref: data.reference,
                firstname: data.firstName,
                lastname: data.lastName,
                metadata: {
                    custom_fields: [{
                        display_name: 'Transaction Type',
                        variable_name: 'transaction_type',
                        value: 'Wallet Top-Up'
                    }]
                },
                callback: function (response) {
                    if (btn) btn.textContent = '⏳ Verifying...';
                    fetch('/Dashboard/VerifyTopUp', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: new URLSearchParams({
                            reference: response.reference,
                            __RequestVerificationToken: token
                        }).toString()
                    })
                        .then(function (r) { return r.json(); })
                        .then(function (result) {
                            if (btn) { btn.disabled = false; btn.textContent = '💳 Add Funds'; }
                            if (result.success) {
                                if (succEl) { succEl.textContent = result.message; succEl.style.display = 'block'; }
                                if (input) input.value = '';
                                loadLoyaltySummary();
                                loadWalletHistory();
                            } else {
                                if (errEl) { errEl.textContent = result.message; errEl.style.display = 'block'; }
                            }
                        })
                        .catch(function () {
                            if (btn) { btn.disabled = false; btn.textContent = '💳 Add Funds'; }
                            if (errEl) { errEl.textContent = 'Verification error. Contact support.'; errEl.style.display = 'block'; }
                        });
                },
                onClose: function () {
                    if (btn) { btn.disabled = false; btn.textContent = '💳 Add Funds'; }
                }
            });

            handler.openIframe();
        })
        .catch(function () {
            if (btn) { btn.disabled = false; btn.textContent = '💳 Add Funds'; }
            if (errEl) { errEl.textContent = 'Connection error. Please try again.'; errEl.style.display = 'block'; }
        });
}

/* ====================================================
   POINTS HISTORY
==================================================== */
function loadPointsHistory() {
    fetch('/Dashboard/GetPointsHistory')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var el = document.getElementById('wp-points-history');
            if (!el) return;

            if (!data.success || data.transactions.length === 0) {
                el.innerHTML = '<div class="loading">No points transactions yet.</div>';
                return;
            }

            el.innerHTML = data.transactions.map(function (t) {
                var isPositive = t.points > 0;
                var color = isPositive ? '#22C55E' : '#EF4444';
                var icon = t.transactionType === 'Bonus' ? '🎯'
                    : t.transactionType === 'Redeemed' ? '☕'
                        : t.transactionType === 'Converted' ? '💱' : '⭐';

                return '<div class="history-row">'
                    + '<div style="font-size:20px;">' + icon + '</div>'
                    + '<div style="flex:1;">'
                    + '<div style="font-size:13px;font-weight:600;color:#0F172A;">' + escHtml(t.description) + '</div>'
                    + '<div style="font-size:11px;color:#94A3B8;margin-top:2px;">' + t.createdAt + '</div>'
                    + '</div>'
                    + '<div style="font-family:\'Bebas Neue\';font-size:18px;letter-spacing:1px;color:' + color + ';">'
                    + (isPositive ? '+' : '') + t.points + ' pts'
                    + '</div></div>';
            }).join('');
        })
        .catch(function () { console.error('[Wallet] Could not load points history.'); });
}

/* ====================================================
   WALLET HISTORY
==================================================== */
function loadWalletHistory() {
    fetch('/Dashboard/GetWalletHistory')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var el = document.getElementById('wp-wallet-history');
            if (!el) return;

            if (!data.success || data.transactions.length === 0) {
                el.innerHTML = '<div class="loading">No wallet transactions yet.</div>';
                return;
            }

            el.innerHTML = data.transactions.map(function (t) {
                var icon = t.transactionType === 'TopUp' ? '💳'
                    : (t.transactionType === 'TopUpToPoints' || t.transactionType === 'PointsConversion') ? '💱'
                        : '🛒';
                var isDebit = t.transactionType === 'TopUpToPoints';
                var amtColor = isDebit ? '#EF4444' : '#22C55E';
                var amtSign = isDebit ? '-' : '+';

                return '<div class="history-row">'
                    + '<div style="font-size:20px;">' + icon + '</div>'
                    + '<div style="flex:1;">'
                    + '<div style="font-size:13px;font-weight:600;color:#0F172A;">' + escHtml(t.description) + '</div>'
                    + '<div style="font-size:11px;color:#94A3B8;margin-top:2px;">' + t.createdAt + '</div>'
                    + '</div>'
                    + '<div style="font-family:\'Bebas Neue\';font-size:18px;letter-spacing:1px;color:' + amtColor + ';">'
                    + amtSign + 'R' + t.amount
                    + '</div></div>';
            }).join('');
        })
        .catch(function () { console.error('[Wallet] Could not load wallet history.'); });
}

/* ====================================================
   REDEMPTION HISTORY
==================================================== */
function loadRedemptionHistory() {
    fetch('/Dashboard/GetRedemptionHistory')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var el = document.getElementById('wp-redemption-history');
            if (!el) return;

            if (!data.success || data.redemptions.length === 0) {
                el.innerHTML = '<div class="loading">No café redemptions yet. Start spending your points!</div>';
                return;
            }

            el.innerHTML = data.redemptions.map(function (r) {
                var statusColor = r.status === 'Collected' ? '#22C55E'
                    : r.status === 'Expired' ? '#EF4444' : '#F59E0B';

                return '<div class="history-row" style="align-items:flex-start;">'
                    + '<div style="font-size:22px;padding-top:2px;">' + r.emoji + '</div>'
                    + '<div style="flex:1;">'
                    + '<div style="font-size:13px;font-weight:600;color:#0F172A;">' + escHtml(r.productName) + '</div>'
                    + '<div style="font-size:11px;color:#94A3B8;margin-top:2px;">'
                    + r.redeemedAt + ' &nbsp;·&nbsp; Code: <strong style="color:#64748B;letter-spacing:2px;">' + r.collectionCode + '</strong>'
                    + '</div></div>'
                    + '<div style="text-align:right;flex-shrink:0;">'
                    + '<div style="font-family:\'Bebas Neue\';font-size:16px;color:#EF4444;letter-spacing:1px;">-' + r.pointsSpent + ' pts</div>'
                    + '<div style="font-size:11px;font-weight:700;color:' + statusColor + ';">' + r.status + '</div>'
                    + '</div></div>';
            }).join('');
        })
        .catch(function () { console.error('[Wallet] Could not load redemption history.'); });
}