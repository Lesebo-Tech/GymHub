/* ====================================================

   OTP BOX HELPERS

==================================================== */

function otpMove(current, nextId) {

    current.value = current.value.replace(/\D/g, '');

    current.classList.toggle('filled', current.value !== '');

    if (current.value && nextId)

        document.getElementById(nextId).focus();

    collectOtp();

}

function otpBack(e, current, prevId) {

    if (e.key === 'Backspace' && !current.value && prevId)

        document.getElementById(prevId).focus();

}

function collectOtp() {

    const val = ['otp1', 'otp2', 'otp3', 'otp4', 'otp5', 'otp6']

        .map(id => document.getElementById(id).value)

        .join('');

    document.getElementById('otpHidden').value = val;

}

/* ====================================================

   PASSWORD TOGGLE

==================================================== */

function togglePass(inputId, toggleId) {

    const input = document.getElementById(inputId);

    const toggle = document.getElementById(toggleId);

    if (input.type === 'password') {

        input.type = 'text';

        toggle.textContent = '🙈';

    } else {

        input.type = 'password';

        toggle.textContent = '👁';

    }

}

/* ====================================================

   DISABLE BUTTON ON SUBMIT

==================================================== */

document.getElementById('resetBtn').addEventListener('click', function () {

    collectOtp();

    const form = document.querySelector('form');

    if (form.checkValidity()) {

        this.textContent = 'Resetting…';

        this.disabled = true;

        form.submit();

    }

});
