/* ====================================================

   TOGGLE PASSWORD VISIBILITY

==================================================== */

function togglePassword() {

    const input = document.getElementById('loginPassword');

    const toggle = document.getElementById('pwToggle');

    if (input.type === 'password') {

        input.type = 'text';

        toggle.textContent = '🙈';

    } else {

        input.type = 'password';

        toggle.textContent = '👁';

    }

}

/* ====================================================

   DISABLE BUTTON ON SUBMIT — PREVENT DOUBLE CLICK

==================================================== */

document.querySelector('.btn-login').addEventListener('click', function () {

    const form = document.querySelector('form');

    if (form.checkValidity()) {

        this.textContent = 'Signing in…';

        this.disabled = true;

        form.submit();

    }

});
