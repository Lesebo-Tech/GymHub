/* ====================================================
   GYMHUB DASHBOARD — dashboard.js
   QR codes served from DB via /Dashboard/GetQrCode
   Never generated client-side.
==================================================== */

function toggleSidebar() {
    var s = document.getElementById('sidebar');
    if (s) s.classList.toggle('open');
}

function confirmLogout() { return confirm('Are you sure you want to log out?'); }

function setGreeting() {
    var h = new Date().getHours();
    var el = document.getElementById('greeting-time');
    if (!el) return;
    el.textContent = h < 12 ? 'morning' : h < 17 ? 'afternoon' : 'evening';
}

/* ====================================================
   PAGE INIT — each page calls only what it needs
==================================================== */
document.addEventListener('DOMContentLoaded', function () {
    setGreeting();

    var page = document.body.dataset.page;

    if (page === 'overview') { loadQrCode(); }
    if (page === 'qrcode') { loadQrCode(); }
    if (page === 'profile') { loadProfile(); }
    if (page === 'freeze') { loadFreezeHistory(); }
    if (page === 'wallet') { loadWalletSection(); }
    if (page === 'fitness') { loadFitnessSection(); }
});

/* ====================================================
   QR CODE
==================================================== */
function loadQrCode() {
    var ovWrap = document.getElementById('overview-qr-wrap');
    var dashWrap = document.getElementById('dashboard-qr-wrap');

    if (ovWrap) ovWrap.innerHTML = '<div style="width:100px;height:100px;display:flex;align-items:center;justify-content:center;color:#94a3b8;font-size:10px;">Loading…</div>';
    if (dashWrap) dashWrap.innerHTML = '<div style="width:220px;height:220px;display:flex;align-items:center;justify-content:center;color:#64748b;font-size:13px;">Loading QR…</div>';

    fetch('/Dashboard/GetQrCode')
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (!data.success || !data.qrBase64) {
                var err = '<div style="text-align:center;color:#EF4444;font-size:11px;padding:8px;">QR unavailable.<br/>Contact support.</div>';
                if (ovWrap) ovWrap.innerHTML = err;
                if (dashWrap) dashWrap.innerHTML = err;
                return;
            }
            var src = 'data:image/png;base64,' + data.qrBase64;
            if (ovWrap) ovWrap.innerHTML = '<img src="' + src + '" style="width:88px;height:88px;border-radius:6px;display:block;" alt="QR Code"/>';
            if (dashWrap) dashWrap.innerHTML = '<img src="' + src + '" id="qr-img-full" style="width:220px;height:220px;border-radius:8px;display:block;" alt="GymHub QR Code"/>';
        })
        .catch(function (err) { console.error('[GymHub] QR load failed:', err); });
}

function downloadDashboardQR() {
    var img = document.getElementById('qr-img-full');
    if (!img) { loadQrCode(); setTimeout(downloadDashboardQR, 1200); return; }
    var canvas = document.createElement('canvas');
    canvas.width = canvas.height = 220;
    var ctx = canvas.getContext('2d');
    var tmp = new Image();
    tmp.onload = function () {
        ctx.drawImage(tmp, 0, 0, 220, 220);
        var a = document.createElement('a');
        a.download = 'GymHub-QR-' + memberInfo.memberNumber + '.png';
        a.href = canvas.toDataURL('image/png');
        a.click();
    };
    tmp.src = img.src;
}

function refreshQR() {
    loadQrCode();
    var btn = document.querySelector('.btn-secondary-full');
    if (btn) { btn.textContent = '✓ Refreshed!'; setTimeout(function () { btn.textContent = 'Refresh QR'; }, 2000); }
}

function downloadStatement() { window.location.href = '/Dashboard/DownloadStatement'; }

/* ====================================================
   PROFILE
==================================================== */
function loadProfile() {
    fetch('/Dashboard/GetProfile')
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (!data.success) return;
            document.getElementById('prof-firstname').value = data.firstName || '';
            document.getElementById('prof-lastname').value = data.lastName || '';
            document.getElementById('prof-email').value = data.email || '';
            document.getElementById('prof-phone').value = data.phone || '';
            document.getElementById('prof-idnumber').value = data.idNumber || '';
            document.getElementById('prof-dob').value = data.dob || '';
            document.getElementById('prof-gender').value = data.gender || '';
            var initials = (data.firstName.charAt(0) + data.lastName.charAt(0)).toUpperCase();
            var av = document.getElementById('prof-avatar-preview');
            var nm = document.getElementById('prof-preview-name');
            if (av) av.textContent = initials;
            if (nm) nm.textContent = data.firstName + ' ' + data.lastName;
            hideBanners();
        })
        .catch(function () { console.error('[GymHub] GetProfile failed.'); });
}

function saveProfile() {
    var btn = document.getElementById('prof-save-btn');
    var fn = document.getElementById('prof-firstname').value.trim();
    var ln = document.getElementById('prof-lastname').value.trim();
    var em = document.getElementById('prof-email').value.trim();
    var ph = document.getElementById('prof-phone').value.trim();

    if (!fn || !ln || !em || !ph) { showProfileError('Please fill in all required fields.'); return; }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) { showProfileError('Please enter a valid email address.'); return; }

    btn.textContent = 'Saving…'; btn.disabled = true;

    fetch('/Dashboard/UpdateProfile', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ FirstName: fn, LastName: ln, Email: em, Phone: ph }).toString()
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            btn.textContent = 'Save Changes'; btn.disabled = false;
            if (data.success) {
                showProfileSuccess(data.message);
                var sn = document.querySelector('.member-name');
                if (sn) sn.textContent = data.fullName;
                var gn = document.querySelector('.topbar-greeting strong');
                if (gn) gn.textContent = data.fullName.split(' ')[0];
                var parts = data.fullName.split(' ');
                var ini = (parts[0].charAt(0) + (parts[1] ? parts[1].charAt(0) : '')).toUpperCase();
                document.querySelectorAll('.member-avatar,.qr-avatar').forEach(function (el) { el.textContent = ini; });
                var av = document.getElementById('prof-avatar-preview');
                var nm = document.getElementById('prof-preview-name');
                if (av) av.textContent = ini;
                if (nm) nm.textContent = data.fullName;
                if (typeof memberInfo !== 'undefined') {
                    memberInfo.name = data.fullName;
                    memberInfo.firstName = data.fullName.split(' ')[0];
                    memberInfo.email = data.email;
                }
            } else { showProfileError(data.message); }
        })
        .catch(function () { btn.textContent = 'Save Changes'; btn.disabled = false; showProfileError('Connection error.'); });
}

function hideBanners() {
    var e = document.getElementById('profile-error-banner');
    var s = document.getElementById('profile-success-banner');
    if (e) e.style.display = 'none';
    if (s) s.style.display = 'none';
}

function showProfileError(msg) {
    var e = document.getElementById('profile-error-banner');
    var s = document.getElementById('profile-success-banner');
    if (!e) return;
    e.innerHTML = '&#9888; ' + msg; e.style.display = 'block';
    if (s) s.style.display = 'none';
    e.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

function showProfileSuccess(msg) {
    var e = document.getElementById('profile-error-banner');
    var s = document.getElementById('profile-success-banner');
    if (!s) return;
    s.innerHTML = '&#10003; ' + msg; s.style.display = 'block';
    if (e) e.style.display = 'none';
}

/* ====================================================
   PLAN CHANGE MODAL
==================================================== */
var pendingPlan = null, pendingFee = null, pendingType = null;

function openPlanModal(plan, fee, type) {
    pendingPlan = plan; pendingFee = parseFloat(fee); pendingType = type;
    var icon = type === 'Upgrade' ? '⬆️' : '⬇️';
    var color = type === 'Upgrade' ? '#2563EB' : '#D97706';
    document.getElementById('modalIcon').textContent = icon;
    document.getElementById('modalTitle').textContent = type + ' Plan';
    document.getElementById('modalTitle').style.color = color;
    document.getElementById('modalBody').innerHTML =
        'Switching from <strong>' + memberInfo.plan.toUpperCase() + '</strong> (R' + parseFloat(memberInfo.monthlyFee).toFixed(2) + '/mo) to ' +
        '<strong>' + plan.toUpperCase() + '</strong> (R' + parseFloat(fee).toFixed(2) + '/mo).<br/><br/>' +
        'Change takes effect <strong>immediately</strong>. A prorated adjustment will be logged.';
    document.getElementById('plan-modal-error').style.display = 'none';
    var cb = document.getElementById('modalConfirmBtn');
    cb.textContent = 'Confirm ' + type;
    cb.style.background = type === 'Upgrade'
        ? 'linear-gradient(135deg,#2563EB,#3B82F6)'
        : 'linear-gradient(135deg,#D97706,#F59E0B)';
    cb.disabled = false;
    document.getElementById('planModal').classList.add('open');
}

function closePlanModal() {
    document.getElementById('planModal').classList.remove('open');
    pendingPlan = pendingFee = pendingType = null;
}

function confirmPlanChange() {
    if (!pendingPlan) return;
    var btn = document.getElementById('modalConfirmBtn');
    btn.disabled = true; btn.textContent = 'Processing…';
    fetch('/Dashboard/ChangePlan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ newPlan: pendingPlan }).toString()
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (data.success) {
                closePlanModal();
                memberInfo.plan = data.newPlan;
                memberInfo.monthlyFee = parseFloat(data.newFee);
                showPageSuccessBanner(data.changeType + ' successful! ' + data.message);
                setTimeout(function () { window.location.reload(); }, 2500);
            } else {
                document.getElementById('plan-modal-error').textContent = data.message;
                document.getElementById('plan-modal-error').style.display = 'block';
                btn.disabled = false; btn.textContent = 'Confirm ' + pendingType;
            }
        })
        .catch(function () {
            document.getElementById('plan-modal-error').textContent = 'Connection error.';
            document.getElementById('plan-modal-error').style.display = 'block';
            btn.disabled = false; btn.textContent = 'Confirm ' + pendingType;
        });
}

/* ====================================================
   CANCEL MODAL
==================================================== */
function openCancelModal() {
    document.getElementById('cancel-modal-error').style.display = 'none';
    var btn = document.getElementById('cancelConfirmBtn');
    btn.disabled = false; btn.textContent = 'Yes, Cancel My Membership';
    document.getElementById('cancelModal').classList.add('open');
}

function closeCancelModal() { document.getElementById('cancelModal').classList.remove('open'); }

function confirmCancellation() {
    var btn = document.getElementById('cancelConfirmBtn');
    btn.disabled = true; btn.textContent = 'Processing…';
    fetch('/Dashboard/CancelMembership', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: ''
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (data.success) {
                closeCancelModal();
                var msg = data.isCoolingOff
                    ? 'Membership cancelled immediately. Refund of R' + data.refundAmount + ' requested — pending admin approval (5–7 business days).'
                    : 'Cancellation confirmed. ' + data.message;
                showPageSuccessBanner(msg);
                setTimeout(function () { window.location.reload(); }, 3000);
            } else {
                document.getElementById('cancel-modal-error').textContent = data.message;
                document.getElementById('cancel-modal-error').style.display = 'block';
                btn.disabled = false; btn.textContent = 'Yes, Cancel My Membership';
            }
        })
        .catch(function () {
            document.getElementById('cancel-modal-error').textContent = 'Connection error.';
            document.getElementById('cancel-modal-error').style.display = 'block';
            btn.disabled = false; btn.textContent = 'Yes, Cancel My Membership';
        });
}

/* ====================================================
   SHARED TOAST / BANNER HELPER
   Works on any page — no longer assumes #section-membership
==================================================== */
function showPageSuccessBanner(msg) {
    var existing = document.getElementById('page-success-banner');
    if (existing) existing.remove();
    var b = document.createElement('div');
    b.id = 'page-success-banner';
    b.style.cssText = 'background:rgba(34,197,94,0.08);border:1px solid rgba(34,197,94,0.25);border-left:4px solid #22C55E;border-radius:10px;padding:14px 18px;color:#16A34A;font-size:14px;font-weight:600;margin-bottom:20px;';
    b.textContent = '✓ ' + msg;
    var main = document.querySelector('.main-content');
    if (main) main.insertBefore(b, main.firstChild);
}

function showDashSuccessBanner(msg) {
    var existing = document.getElementById('dash-success-banner');
    if (existing) existing.remove();
    var el = document.createElement('div');
    el.id = 'dash-success-banner';
    el.style.cssText = 'position:fixed;top:20px;right:20px;background:#fff;border:1px solid rgba(74,222,128,0.3);border-left:4px solid #4ADE80;border-radius:10px;padding:14px 20px;color:#16A34A;font-size:14px;font-weight:600;z-index:9999;box-shadow:0 8px 32px rgba(0,0,0,0.12);max-width:400px;line-height:1.5';
    el.innerHTML = msg;
    document.body.appendChild(el);
    setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 5000);
}

/* ====================================================
   FREEZE
==================================================== */
function escHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}

function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

function updateFreezeDurationPreview() {
    var startVal = document.getElementById('freeze-start-input') ? document.getElementById('freeze-start-input').value : '';
    var endVal = document.getElementById('freeze-end-input') ? document.getElementById('freeze-end-input').value : '';
    var preview = document.getElementById('freeze-duration-preview');
    if (!preview) return;
    if (!startVal || !endVal) { preview.style.display = 'none'; return; }
    var days = Math.round((new Date(endVal) - new Date(startVal)) / (1000 * 60 * 60 * 24));
    if (days < 1) { preview.style.display = 'none'; return; }
    var valid = days >= 7 && days <= 90;
    preview.style.display = 'block';
    preview.style.background = valid ? 'rgba(37,99,235,0.08)' : 'rgba(239,68,68,0.08)';
    preview.style.border = valid ? '1px solid rgba(37,99,235,0.2)' : '1px solid rgba(239,68,68,0.2)';
    preview.style.color = valid ? '#2563EB' : '#DC2626';
    preview.textContent = valid
        ? '❄️ ' + days + ' day freeze — your membership expiry will be extended by ' + days + ' days'
        : '⚠️ ' + days + ' days selected — must be between 7 and 90 days';
}

function submitFreezeRequest() {
    var startInput = document.getElementById('freeze-start-input');
    var endInput = document.getElementById('freeze-end-input');
    var reasonInput = document.getElementById('freeze-reason-input');
    var errEl = document.getElementById('freeze-form-error');
    var submitBtn = document.getElementById('freeze-submit-btn');
    if (!startInput || !endInput || !reasonInput || !errEl || !submitBtn) return;

    var startVal = startInput.value;
    var endVal = endInput.value;
    var reason = reasonInput.value.trim();

    errEl.style.display = 'none'; errEl.innerHTML = '';

    if (!startVal || !endVal) { errEl.textContent = 'Please select both a start and end date.'; errEl.style.display = 'block'; return; }
    var days = Math.round((new Date(endVal) - new Date(startVal)) / 86400000);
    if (days < 7) { errEl.textContent = 'Minimum freeze duration is 7 days.'; errEl.style.display = 'block'; return; }
    if (days > 90) { errEl.textContent = 'Maximum freeze duration is 90 days.'; errEl.style.display = 'block'; return; }
    if (reason.length < 10) { errEl.textContent = 'Please provide a reason of at least 10 characters.'; errEl.style.display = 'block'; return; }

    submitBtn.disabled = true; submitBtn.textContent = '⏳ Submitting…';

    fetch('/Dashboard/RequestFreeze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ freezeStart: startVal, freezeEnd: endVal, reason: reason, __RequestVerificationToken: getToken() }).toString()
    })
        .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
        .then(function (data) {
            submitBtn.disabled = false; submitBtn.textContent = 'Submit Freeze Request';
            if (data.success) {
                startInput.value = ''; endInput.value = ''; reasonInput.value = '';
                var preview = document.getElementById('freeze-duration-preview');
                if (preview) preview.style.display = 'none';
                showDashSuccessBanner('❄️ Freeze request submitted for <strong>' + data.freezeStart + ' – ' + data.freezeEnd + '</strong> (' + data.days + ' days). Pending admin approval.');
                loadFreezeHistory();
            } else {
                errEl.innerHTML = data.message || 'Could not submit freeze request.';
                errEl.style.display = 'block';
            }
        })
        .catch(function () {
            submitBtn.disabled = false; submitBtn.textContent = 'Submit Freeze Request';
            errEl.textContent = 'Connection error. Please try again.';
            errEl.style.display = 'block';
        });
}

function loadFreezeHistory() {
    var body = document.getElementById('freeze-history-body');
    if (!body) return;
    body.innerHTML = '<div style="text-align:center;padding:32px;color:#64748B;font-size:14px;">Loading…</div>';

    fetch('/Dashboard/GetFreezeStatus')
        .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
        .then(function (data) {
            if (!data.success) { body.innerHTML = '<div style="text-align:center;padding:32px;color:#EF4444;font-size:14px;">Could not load freeze history.</div>'; return; }
            var requestCard = document.getElementById('freeze-request-card');
            if (requestCard) {
                var hasBlock = data.hasPendingFreeze || data.hasActiveFreeze ||
                    data.memberStatus === 'Frozen' || data.memberStatus === 'Suspended' ||
                    data.memberStatus === 'Cancelled' || data.memberStatus === 'Pending Cancellation';
                requestCard.style.display = hasBlock ? 'none' : 'block';
            }
            renderFreezeStatusBanner(data);
            if (!data.freezes || data.freezes.length === 0) {
                body.innerHTML = '<div style="text-align:center;padding:40px 32px;color:#94A3B8;font-size:14px;">You have not submitted any freeze requests yet.</div>';
                return;
            }
            body.innerHTML = data.freezes.map(function (f) {
                var isActive = f.isCurrentlyFrozen;
                var badgeClass = isActive ? 'fbadge-active' : 'fbadge-' + f.status.toLowerCase();
                var badgeLabel = isActive ? 'Active ❄️' : f.status;
                var rowClass = isActive ? 'freeze-active-row' :
                    f.status === 'Pending' ? 'freeze-pending-row' :
                        f.status === 'Rejected' ? 'freeze-rejected-row' :
                            f.status === 'Completed' ? 'freeze-completed-row' : '';
                var expiryHtml = f.extendedExpiry ? '<div class="fh-expiry-line">📅 Membership expiry extended to <strong class="fh-expiry-date">' + f.extendedExpiry + '</strong></div>' : '';
                var adminHtml = f.adminNotes ? '<div class="fh-admin-line">💬 Admin note: <em>' + escHtml(f.adminNotes) + '</em></div>' : '';
                var processedHtml = f.processedAt ? '<span class="fh-processed"> · Reviewed ' + f.processedAt + '</span>' : '';
                return '<div class="freeze-history-row ' + rowClass + '">' +
                    '<div class="fhr-top"><div class="fhr-dates-wrap">' +
                    '<div class="fhr-dates">❄️ ' + f.freezeStart + ' → ' + f.freezeEnd + '</div>' +
                    '<div class="fhr-meta">' + f.days + ' days · Requested ' + f.requestedAt + processedHtml + '</div>' +
                    '</div><span class="fbadge ' + badgeClass + '">' + badgeLabel + '</span></div>' +
                    '<div class="fhr-reason"><strong>Reason:</strong> ' + escHtml(f.reason || '—') + '</div>' +
                    expiryHtml + adminHtml + '</div>';
            }).join('');
        })
        .catch(function () {
            if (body) body.innerHTML = '<div style="text-align:center;padding:32px;color:#EF4444;font-size:14px;">Connection error.</div>';
        });
}

function renderFreezeStatusBanner(data) {
    var banner = document.getElementById('freeze-status-banner');
    if (!banner) return;
    banner.innerHTML = '';
    if (data.hasActiveFreeze || data.memberStatus === 'Frozen') {
        banner.innerHTML = '<div class="freeze-active-box"><div class="fab-title">❄️ Your membership is currently frozen</div><div class="fab-body">Gym access is paused during this period. Your membership will resume automatically once the freeze ends.</div></div>';
    } else if (data.hasPendingFreeze) {
        banner.innerHTML = '<div class="freeze-warn-box">⏳ <strong>Freeze request pending admin review.</strong> You will receive an email once a decision is made.</div>';
    } else if (data.memberStatus === 'Suspended') {
        banner.innerHTML = '<div class="freeze-warn-box" style="border-left-color:#EF4444;background:rgba(239,68,68,0.05);">🚫 Your membership is <strong>suspended</strong>. Freeze requests are unavailable. Please contact support to reactivate.</div>';
    } else if (data.memberStatus === 'Cancelled' || data.memberStatus === 'Pending Cancellation') {
        banner.innerHTML = '<div class="freeze-warn-box" style="border-left-color:#EF4444;background:rgba(239,68,68,0.05);">🚫 Freeze requests are not available — your membership is ' + escHtml(data.memberStatus) + '.</div>';
    }
}

