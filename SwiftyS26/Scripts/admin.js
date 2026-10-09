/* ====================================================
   GYMHUB ADMIN DASHBOARD — admin.js
==================================================== */
console.log('GymHub Admin Dashboard loaded');

/* ====================================================
   ANTI FORGERY TOKEN HELPER
==================================================== */
function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}