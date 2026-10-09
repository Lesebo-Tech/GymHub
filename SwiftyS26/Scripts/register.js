/* ====================================================
   STATE
==================================================== */
let generatedQrContent = '';
let selectedGender = '';
let selectedPlan = null;
let selectedPayMethod = null;
let resendInterval = null;
let memberData = {};
let generatedMemberId = '';

/* ====================================================
   PLAN DATA
==================================================== */
const JOINING_FEE = 150;

const planData = {
    basic: { label: 'BASIC', monthly: 199 },
    standard: { label: 'STANDARD', monthly: 399 },
    premium: { label: 'PREMIUM', monthly: 699 },
};

/* ====================================================
   PLAN SELECTION
==================================================== */
function selectPlan(plan) {
    selectedPlan = plan;

    ['basic', 'standard', 'premium'].forEach(p => {
        const card = document.getElementById('plan-' + p);
        if (card) card.classList.remove('selected');
    });
    const selected = document.getElementById('plan-' + plan);
    if (selected) selected.classList.add('selected');

    const errPlan = document.getElementById('err-plan');
    if (errPlan) errPlan.style.display = 'none';

    const p = planData[plan];

    const sumPlan = document.getElementById('sum-plan');
    if (sumPlan) sumPlan.textContent = p.label;

    const sumMonthly = document.getElementById('sum-monthly');
    if (sumMonthly) sumMonthly.textContent = `R${p.monthly.toFixed(2)}`;

    const sumJoining = document.getElementById('sum-joining');
    if (sumJoining) sumJoining.textContent = `R${JOINING_FEE.toFixed(2)}`;

    const sumTotal = document.getElementById('sum-total');
    if (sumTotal) sumTotal.textContent = `R${(p.monthly + JOINING_FEE).toFixed(2)}`;

    const payPlanLabel = document.getElementById('pay-plan-label');
    if (payPlanLabel) payPlanLabel.textContent = p.label;

    const payMonthly = document.getElementById('pay-monthly');
    if (payMonthly) payMonthly.textContent = `R${p.monthly.toFixed(2)}`;

    const payTotal = document.getElementById('pay-total');
    if (payTotal) payTotal.textContent = `R${(p.monthly + JOINING_FEE).toFixed(2)}`;
}

/* ====================================================
   PAYMENT METHOD SELECTION
   — Card/EFT/Debit still shown for UX context,
     but actual charge goes through Paystack popup
==================================================== */
function selectPayMethod(method) {
    selectedPayMethod = method;

    ['card', 'eft', 'debit'].forEach(m => {
        const btn = document.getElementById('pay-' + m);
        if (btn) btn.classList.remove('selected');
    });
    const selBtn = document.getElementById('pay-' + method);
    if (selBtn) selBtn.classList.add('selected');

    document.getElementById('card-fields').style.display =
        method === 'card' ? 'block' : 'none';
    document.getElementById('eft-fields').style.display =
        method === 'eft' ? 'block' : 'none';
    document.getElementById('debit-fields').style.display =
        method === 'debit' ? 'block' : 'none';

    if (method === 'eft') {
        const fn = document.getElementById('firstName').value.trim();
        const ln = document.getElementById('lastName').value.trim();
        const ref = document.getElementById('eft-ref');
        if (ref && (fn || ln))
            ref.textContent = `${fn} ${ln}`.trim().toUpperCase();
    }

    const errPay = document.getElementById('err-paymethod');
    if (errPay) errPay.style.display = 'none';
}

/* ====================================================
   CARD NUMBER FORMATTING
==================================================== */
function formatCardNumber(input) {
    const val = input.value.replace(/\D/g, '').substring(0, 16);
    input.value = val.replace(/(.{4})/g, '$1 ').trim();
    const brand = document.getElementById('cardBrand');
    if (brand) {
        if (/^4/.test(val)) brand.textContent = '💳 Visa';
        else if (/^5[1-5]/.test(val)) brand.textContent = '💳 Mastercard';
        else if (/^3[47]/.test(val)) brand.textContent = '💳 Amex';
        else brand.textContent = '';
    }
}

function formatExpiry(input) {
    const val = input.value.replace(/\D/g, '').substring(0, 4);
    input.value = val.length >= 2 ?
        val.substring(0, 2) + '/' + val.substring(2) : val;
}

/* ====================================================
   ANTI FORGERY TOKEN
==================================================== */
function getToken() {
    const el = document.querySelector(
        'input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

/* ====================================================
   ERROR BANNER
==================================================== */
function showServerError(msg) {
    const banner = document.getElementById('server-error-banner');
    if (banner) {
        banner.textContent = '⚠️ ' + msg;
        banner.style.display = 'block';
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }
}

function hideServerError() {
    const banner = document.getElementById('server-error-banner');
    if (banner) banner.style.display = 'none';
}

/* ====================================================
   LUHN — SA ID VALIDATION
==================================================== */
function luhn(num) {
    let sum = 0, alt = false;
    for (let i = num.length - 1; i >= 0; i--) {
        let n = parseInt(num[i], 10);
        if (alt) { n *= 2; if (n > 9) n -= 9; }
        sum += n;
        alt = !alt;
    }
    return sum % 10 === 0;
}

function parseIdNumber(id) {
    if (id.length !== 13 || !/^\d+$/.test(id)) return null;
    if (!luhn(id)) return null;
    const yy = id.substring(0, 2),
        mm = id.substring(2, 4),
        dd = id.substring(4, 6);
    const genderDigit = parseInt(id.substring(6, 7), 10);
    const currentYear = new Date().getFullYear();
    const century = parseInt(yy, 10) <= (currentYear % 100) ? '20' : '19';
    const mNum = parseInt(mm, 10), dNum = parseInt(dd, 10);
    if (mNum < 1 || mNum > 12 || dNum < 1 || dNum > 31) return null;
    return {
        dob: `${century}${yy}-${mm}-${dd}`,
        gender: genderDigit >= 5 ? 'Male' : 'Female'
    };
}

function handleIdInput(input) {
    const val = input.value.replace(/\D/g, '');
    input.value = val;
    const badge = document.getElementById('idBadge');
    const errEl = document.getElementById('err-id');
    const okEl = document.getElementById('ok-id');
    input.classList.remove('valid', 'invalid');
    badge.textContent = '';
    errEl.style.display = 'none';
    okEl.style.display = 'none';
    if (val.length === 13) {
        const parsed = parseIdNumber(val);
        if (parsed) {
            input.classList.add('valid');
            badge.textContent = '✅';
            okEl.style.display = 'block';
            document.getElementById('dob').value = parsed.dob;
            calculateAge(parsed.dob);
            selectGender(parsed.gender);
            updateSummary();
        } else {
            input.classList.add('invalid');
            badge.textContent = '❌';
            errEl.style.display = 'block';
        }
    }
}

/* ====================================================
   AGE CALCULATION
==================================================== */
function calculateAge(dobStr) {
    if (!dobStr) return;
    const dob = new Date(dobStr), today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const m = today.getMonth() - dob.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
    const ageField = document.getElementById('ageField');
    if (ageField) ageField.value = age >= 0 ? age + ' years old' : '';
    updateSummary();
}

document.getElementById('dob').addEventListener('input', function () {
    calculateAge(this.value);
});

/* ====================================================
   GENDER SELECTION
==================================================== */
function selectGender(g) {
    selectedGender = g;
    ['male', 'female', 'notspecified'].forEach(id => {
        const btn = document.getElementById('gBtn-' + id);
        if (btn) btn.classList.remove('selected');
    });
    const map = { 'Male': 'male', 'Female': 'female', 'Not Specified': 'notspecified' };
    if (map[g]) {
        const btn = document.getElementById('gBtn-' + map[g]);
        if (btn) btn.classList.add('selected');
    }
    const errGender = document.getElementById('err-gender');
    if (errGender) errGender.style.display = 'none';
    updateSummary();
}

/* ====================================================
   LIVE SUMMARY
==================================================== */
function updateSummary() {
    const fn = document.getElementById('firstName').value.trim();
    const ln = document.getElementById('lastName').value.trim();
    const em = document.getElementById('email').value.trim();
    const dob = document.getElementById('dob').value;

    const sumName = document.getElementById('sum-name');
    if (sumName) sumName.textContent = (fn || ln) ? `${fn} ${ln}`.trim() : '—';

    const sumEmail = document.getElementById('sum-email');
    if (sumEmail) sumEmail.textContent = em || '—';

    const sumGender = document.getElementById('sum-gender');
    if (sumGender) sumGender.textContent = selectedGender || '—';

    const sumDob = document.getElementById('sum-dob');
    if (sumDob) sumDob.textContent = dob ? formatDate(dob) : '—';
}

function formatDate(str) {
    if (!str) return '—';
    return new Date(str).toLocaleDateString('en-ZA', {
        day: '2-digit', month: 'long', year: 'numeric'
    });
}

/* ====================================================
   PASSWORD STRENGTH
==================================================== */
function isStrongPassword(val) {
    return val.length >= 8 &&
        /[A-Z]/.test(val) &&
        /[0-9]/.test(val) &&
        /[^A-Za-z0-9]/.test(val);
}

function checkPasswordStrength(val) {
    const bar = document.getElementById('strengthBar');
    const text = document.getElementById('strengthText');
    if (!bar || !text) return;

    let score = 0;
    if (val.length >= 8) score++;
    if (/[A-Z]/.test(val)) score++;
    if (/[0-9]/.test(val)) score++;
    if (/[^A-Za-z0-9]/.test(val)) score++;

    const levels = [
        { label: '', color: 'transparent', width: '0%' },
        { label: '⚠ Weak', color: '#EF4444', width: '25%' },
        { label: '◐ Fair', color: '#F97316', width: '50%' },
        { label: '◑ Good', color: '#EAB308', width: '75%' },
        { label: '✓ Strong', color: '#22C55E', width: '100%' },
    ];

    const level = levels[score] || levels[0];
    bar.style.width = level.width;
    bar.style.backgroundColor = level.color;
    text.textContent = level.label;
    text.style.color = level.color;

    checkPasswordMatch();
}

function checkPasswordMatch() {
    const pw = document.getElementById('password');
    const cpw = document.getElementById('confirmPassword');
    const ok = document.getElementById('ok-confirmPassword');
    const err = document.getElementById('err-confirmPassword');
    if (!pw || !cpw || !ok || !err) return;

    if (!cpw.value) {
        ok.style.display = 'none';
        err.style.display = 'none';
        return;
    }

    if (pw.value === cpw.value) {
        ok.style.display = 'block';
        err.style.display = 'none';
    } else {
        ok.style.display = 'none';
        err.style.display = 'block';
        err.textContent = 'Passwords do not match.';
    }
}

function togglePassword(inputId, btn) {
    const input = document.getElementById(inputId);
    if (!input) return;
    input.type = input.type === 'password' ? 'text' : 'password';
    btn.textContent = input.type === 'password' ? '👁' : '🙈';
}

/* ====================================================
   STEPPER
==================================================== */
function showStep(n) {
    document.querySelectorAll('.step-content')
        .forEach(el => el.classList.remove('active'));
    document.getElementById('step' + n).classList.add('active');
    updateStepper(n);
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

function updateStepper(n) {
    for (let i = 1; i <= 5; i++) {
        const dot = document.getElementById('stepDot' + i);
        if (!dot) continue;
        dot.classList.remove('active', 'done');
        if (i < n) {
            dot.classList.add('done');
            dot.querySelector('.step-circle').textContent = '✓';
        }
        if (i === n) dot.classList.add('active');
        if (i > n) dot.querySelector('.step-circle').textContent = i;
    }
    const fills = { 1: '0%', 2: '25%', 3: '50%', 4: '75%', 5: '100%' };
    const fill = document.getElementById('stepperFill');
    if (fill) fill.style.width = fills[n] || '0%';
}

function goPrev(from) { showStep(from - 1); }

function showErr(id, show) {
    const el = document.getElementById(id);
    if (el) el.style.display = show ? 'block' : 'none';
}

/* ====================================================
   STEP 1 — VALIDATE + POST
==================================================== */
function submitStep1() {
    hideServerError();
    let ok = true;

    if (!selectedPlan) { showErr('err-plan', true); ok = false; }
    else showErr('err-plan', false);

    const fn = document.getElementById('firstName').value.trim();
    const ln = document.getElementById('lastName').value.trim();
    const id = document.getElementById('idNumber').value.trim();
    const dob = document.getElementById('dob').value;
    const em = document.getElementById('email').value.trim();
    const ph = document.getElementById('phone').value.trim();

    showErr('err-firstName', !fn); if (!fn) ok = false;
    showErr('err-lastName', !ln); if (!ln) ok = false;

    const idOk = id.length === 13 && parseIdNumber(id);
    showErr('err-id', !idOk); if (!idOk) ok = false;

    showErr('err-dob', !dob); if (!dob) ok = false;
    showErr('err-gender', !selectedGender); if (!selectedGender) ok = false;

    const emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em);
    showErr('err-email', !emailOk); if (!emailOk) ok = false;

    showErr('err-phone', !ph); if (!ph) ok = false;

    const pw = document.getElementById('password').value;
    const cpw = document.getElementById('confirmPassword').value;

    if (!isStrongPassword(pw)) {
        showErr('err-password', true); ok = false;
    } else {
        showErr('err-password', false);
    }

    if (!cpw) {
        showErr('err-confirmPassword', true);
        document.getElementById('err-confirmPassword').textContent =
            'Please confirm your password.';
        ok = false;
    } else if (pw !== cpw) {
        showErr('err-confirmPassword', true);
        document.getElementById('err-confirmPassword').textContent =
            'Passwords do not match.';
        ok = false;
    } else {
        showErr('err-confirmPassword', false);
    }

    if (!ok) return;

    memberData = {
        firstName: fn,
        lastName: ln,
        idNumber: id,
        dateOfBirth: dob,
        gender: selectedGender,
        email: em,
        phone: ph,
        plan: selectedPlan,
        passwordHash: pw
    };

    const btn = document.querySelector('#step1 .btn-next');
    btn.textContent = 'Sending code...';
    btn.disabled = true;

    fetch('/Forms/SubmitPersonalInfo', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getToken()
        },
        body: JSON.stringify(memberData)
    })
        .then(res => res.json())
        .then(data => {
            btn.textContent = 'Continue to Email Verification →';
            btn.disabled = false;
            if (data.success) {
                document.getElementById('emailDisplay').textContent = em;
                document.getElementById('emailDisplay2').textContent = em;
                showStep(2);
                startResendTimer();
            } else {
                showServerError(data.message || 'Something went wrong.');
            }
        })
        .catch(() => {
            btn.textContent = 'Continue to Email Verification →';
            btn.disabled = false;
            showServerError('Connection error. Please try again.');
        });
}

/* ====================================================
   OTP HELPERS
==================================================== */
function otpMove(current, nextId) {
    current.value = current.value.replace(/\D/g, '');
    current.classList.toggle('filled', current.value !== '');
    if (current.value && nextId) document.getElementById(nextId).focus();
}

function otpBack(e, current, prevId) {
    if (e.key === 'Backspace' && !current.value && prevId)
        document.getElementById(prevId).focus();
}

function getEnteredOtp() {
    return ['otp1', 'otp2', 'otp3', 'otp4', 'otp5', 'otp6']
        .map(id => document.getElementById(id).value).join('');
}

/* ====================================================
   STEP 2 — VERIFY OTP
==================================================== */
function submitStep2() {
    const entered = getEnteredOtp();
    const errEl = document.getElementById('err-otp');
    const okEl = document.getElementById('ok-otp');
    errEl.style.display = 'none';
    okEl.style.display = 'none';

    if (entered.length < 6) {
        errEl.style.display = 'block';
        errEl.textContent = 'Please enter all 6 digits.';
        return;
    }

    const btn = document.querySelector('#step2 .btn-next');
    btn.textContent = 'Verifying...';
    btn.disabled = true;

    fetch('/Forms/VerifyOtp', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getToken()
        },
        body: JSON.stringify({ enteredOtp: entered })
    })
        .then(res => res.json())
        .then(data => {
            btn.textContent = 'Verify & Continue →';
            btn.disabled = false;
            if (data.success) {
                okEl.style.display = 'block';
                setTimeout(() => showStep(3), 900);
            } else {
                errEl.style.display = 'block';
                errEl.textContent = data.message || 'Incorrect code.';
            }
        })
        .catch(() => {
            btn.textContent = 'Verify & Continue →';
            btn.disabled = false;
            errEl.style.display = 'block';
            errEl.textContent = 'Connection error.';
        });
}

/* ====================================================
   RESEND OTP
==================================================== */
function resendOtp() {
    ['otp1', 'otp2', 'otp3', 'otp4', 'otp5', 'otp6'].forEach(id => {
        const el = document.getElementById(id);
        el.value = '';
        el.classList.remove('filled');
    });
    document.getElementById('otp1').focus();

    fetch('/Forms/ResendOtp', {
        method: 'POST',
        headers: { 'RequestVerificationToken': getToken() }
    })
        .then(res => res.json())
        .then(data => {
            const msg = document.getElementById('resend-msg');
            if (msg) {
                msg.textContent = data.message;
                msg.style.display = 'block';
                setTimeout(() => msg.style.display = 'none', 3000);
            }
            startResendTimer();
        });
}

function startResendTimer() {
    let secs = 60;
    const timerEl = document.getElementById('resendTimer');
    timerEl.textContent = ` (${secs}s)`;
    if (resendInterval) clearInterval(resendInterval);
    resendInterval = setInterval(() => {
        secs--;
        timerEl.textContent = secs > 0 ? ` (${secs}s)` : '';
        if (secs <= 0) clearInterval(resendInterval);
    }, 1000);
}

/* ====================================================
   STEP 3 — ACCEPT TERMS
==================================================== */
function submitStep3() {
    const tc1 = document.getElementById('tc1').checked;
    const tc2 = document.getElementById('tc2').checked;
    const tc3 = document.getElementById('tc3').checked;

    if (!tc1 || !tc2 || !tc3) {
        document.getElementById('err-tc').style.display = 'block';
        return;
    }
    document.getElementById('err-tc').style.display = 'none';

    const btn = document.querySelector('#step3 .btn-next');
    btn.textContent = 'Saving...';
    btn.disabled = true;

    fetch('/Forms/AcceptTerms', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getToken()
        },
        body: JSON.stringify({ tc1, tc2, tc3 })
    })
        .then(res => res.json())
        .then(data => {
            btn.textContent = 'Accept & Proceed to Payment →';
            btn.disabled = false;
            if (data.success) { showStep(4); }
            else { showServerError(data.message || 'Failed. Please try again.'); }
        })
        .catch(() => {
            btn.textContent = 'Accept & Proceed to Payment →';
            btn.disabled = false;
            showServerError('Connection error.');
        });
}

/* ====================================================
   STEP 4 — PAYSTACK PAYMENT
==================================================== */
function submitStep4() {
    hideServerError();

    // Must select a payment method first
    if (!selectedPayMethod) {
        showErr('err-paymethod', true);
        return;
    }
    showErr('err-paymethod', false);

    const btn = document.querySelector('#step4 .btn-next');
    btn.textContent = 'Initializing payment...';
    btn.disabled = true;

    // Step 1 — get reference + keys from server
    fetch('/Payment/Initialize', {
        method: 'POST',
        headers: { 'RequestVerificationToken': getToken() }
    })
        .then(res => res.json())
        .then(data => {
            if (!data.success) {
                showServerError(data.message || 'Could not initialize payment.');
                btn.textContent = 'Confirm Payment & Complete Registration →';
                btn.disabled = false;
                return;
            }

            // Step 2 — launch Paystack popup
            const handler = PaystackPop.setup({
                key: data.publicKey,
                email: data.email,
                amount: data.amount,      // in kobo/cents
                currency: data.currency,    // ZAR
                ref: data.reference,
                firstname: data.firstName,
                lastname: data.lastName,
                label: 'GymHub Membership',

                onClose: function () {
                    btn.textContent = 'Confirm Payment & Complete Registration →';
                    btn.disabled = false;
                    showServerError('Payment window closed. Please try again.');
                },

                callback: function (response) {
                    // Step 3 — verify on server
                    btn.textContent = 'Verifying payment...';
                    btn.disabled = true;

                    fetch('/Payment/Verify', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': getToken()
                        },
                        body: JSON.stringify({ reference: response.reference })
                    })
                        .then(res => res.json())
                        .then(result => {
                            btn.textContent = 'Confirm Payment & Complete Registration →';
                            btn.disabled = false;

                            if (result.success) {
                                generatedMemberId = result.memberNumber;
                                finaliseConfirmation(result);
                                showStep(5);
                                setTimeout(() => {
                                    const ring = document.getElementById('successRing');
                                    if (ring) ring.style.strokeDashoffset = '0';
                                }, 300);
                                setTimeout(generateQR, 600);
                            } else {
                                showServerError(
                                    result.message || 'Payment verification failed.');
                            }
                        })
                        .catch(() => {
                            btn.textContent = 'Confirm Payment & Complete Registration →';
                            btn.disabled = false;
                            showServerError('Connection error during verification.');
                        });
                }
            });

            handler.openIframe();
        })
        .catch(() => {
            btn.textContent = 'Confirm Payment & Complete Registration →';
            btn.disabled = false;
            showServerError('Connection error. Please try again.');
        });
}

/* ====================================================
   POPULATE CONFIRMATION
==================================================== */
function finaliseConfirmation(data) {
    const p = planData[selectedPlan] || { label: '—' };
    const set = (id, val) => {
        const el = document.getElementById(id);
        if (el) el.textContent = val;
    };

    set('confirmName', (memberData.firstName || 'MEMBER').toUpperCase());
    set('conf-name', `${memberData.firstName} ${memberData.lastName}`.trim());
    set('conf-email', memberData.email);
    set('conf-plan', p.label);
    set('conf-payment',
        selectedPayMethod === 'card' ? '💳 Card (Paystack)' :
            selectedPayMethod === 'eft' ? '🏦 EFT Bank Transfer' :
                selectedPayMethod === 'debit' ? '📋 Debit Order' : '—');
    set('conf-id', generatedMemberId);
    set('sum-memberid', generatedMemberId);
    set('qr-member-id', generatedMemberId);

    const wrap = document.getElementById('sum-memberid-wrap');
    const ph = document.getElementById('sum-memberid-placeholder');
    if (wrap) wrap.style.display = 'block';
    if (ph) ph.style.display = 'none';
}

/* ====================================================
   QR CODE GENERATION
==================================================== */
function generateQR() {
    const wrap = document.getElementById('qrCodeWrap');
    if (!wrap) return;
    wrap.innerHTML = '';

    const qrContent = [
        'GYMHUB MEMBER',
        'ID: ' + generatedMemberId,
        'Name: ' + `${memberData.firstName} ${memberData.lastName}`.trim(),
        'Plan: ' + (planData[selectedPlan] ? planData[selectedPlan].label : ''),
        'Email: ' + memberData.email,
        'Issued: ' + new Date().toLocaleDateString('en-ZA')
    ].join(' | ');

    new QRCode(wrap, {
        text: qrContent,
        width: 180,
        height: 180,
        colorDark: '#0A1628',
        colorLight: '#FFFFFF',
        correctLevel: QRCode.CorrectLevel.H
    });
}

/* ====================================================
   DOWNLOAD QR CODE
==================================================== */
function downloadQR() {
    const canvas = document.querySelector('#qrCodeWrap canvas');
    if (!canvas) {
        alert('QR code not ready yet. Please wait a moment.');
        return;
    }
    const link = document.createElement('a');
    link.download = 'GymHub-' + generatedMemberId + '.png';
    link.href = canvas.toDataURL('image/png');
    link.click();
}