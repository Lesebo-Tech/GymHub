/* ====================================================
   GYMHUB EQUIPMENT FAULT REPORT — equipment-scanner.js
   Reuses the jsQR + getUserMedia pattern from scanner.js,
   pointed at /Equipment/VerifyEquipmentQr instead of member QR.
==================================================== */

var eqVideoStream = null;
var eqScanInterval = null;
var eqScanCooldown = false;
var selectedPhotoFile = null;

/* ====================================================
   ANTI FORGERY TOKEN
==================================================== */
function getEqToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

/* ====================================================
   SEARCH / FILTER
==================================================== */
function filterEquipment() {
    var query = document.getElementById('eqSearchInput').value.trim().toLowerCase();
    var cards = document.querySelectorAll('.eq-card');
    var anyVisible = false;

    cards.forEach(function (card) {
        var match = card.getAttribute('data-search').indexOf(query) !== -1;
        card.style.display = match ? '' : 'none';
        if (match) anyVisible = true;
    });

    // Hide empty area headings if all their cards are filtered out
    document.querySelectorAll('.eq-grid').forEach(function (grid) {
        var visibleCards = grid.querySelectorAll('.eq-card:not([style*="display: none"])');
        var heading = grid.previousElementSibling;
        var show = visibleCards.length > 0;
        grid.style.display = show ? '' : 'none';
        if (heading && heading.classList.contains('eq-area-heading')) {
            heading.style.display = show ? '' : 'none';
        }
    });

    var noResults = document.getElementById('eqNoResults');
    if (noResults) noResults.style.display = (query && !anyVisible) ? 'block' : 'none';
}

/* ====================================================
   CAMERA — OPEN / CLOSE
==================================================== */
function openCamera() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        alert('Camera not supported on this browser. Please search for the equipment manually instead.');
        return;
    }

    document.getElementById('eqCamOverlay').style.display = 'flex';

    navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
        .then(function (stream) {
            eqVideoStream = stream;
            var video = document.getElementById('eqCamVideo');
            video.srcObject = stream;
            video.play();
            eqScanInterval = setInterval(scanEqFrame, 400);
        })
        .catch(function (err) {
            console.error('Camera error:', err);
            alert('Could not access the camera. Please search for the equipment manually instead.');
            closeCamera();
        });
}

function closeCamera() {
    if (eqVideoStream) {
        eqVideoStream.getTracks().forEach(function (t) { t.stop(); });
        eqVideoStream = null;
    }
    if (eqScanInterval) {
        clearInterval(eqScanInterval);
        eqScanInterval = null;
    }
    document.getElementById('eqCamOverlay').style.display = 'none';
    eqScanCooldown = false;
}

/* ====================================================
   SCAN FRAME
==================================================== */
function scanEqFrame() {
    if (eqScanCooldown) return;

    var video = document.getElementById('eqCamVideo');
    var canvas = document.getElementById('eqCamCanvas');
    if (!video || video.readyState !== video.HAVE_ENOUGH_DATA) return;

    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    var ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
    if (typeof jsQR === 'undefined') return;

    var code = jsQR(imageData.data, imageData.width, imageData.height, { inversionAttempts: 'dontInvert' });

    if (code && code.data) {
        eqScanCooldown = true;
        verifyEquipmentQr(code.data);
    }
}

/* ====================================================
   VERIFY QR — sends to EquipmentController
==================================================== */
function verifyEquipmentQr(qrData) {
    fetch('/Equipment/VerifyEquipmentQr', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': getEqToken()
        },
        body: 'qrData=' + encodeURIComponent(qrData)
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            closeCamera();
            if (data.success) {
                openReportModal(data.equipmentId, data.name, data.category, data.gymArea, data.statusDisplay);
            } else {
                alert(data.message || 'Could not recognise that QR code.');
            }
        })
        .catch(function () {
            closeCamera();
            alert('Connection error while verifying the QR code. Please try again.');
        });
}

/* ====================================================
   REPORT MODAL — OPEN / CLOSE
==================================================== */
function openReportModal(id, name, category, area, statusDisplay) {
    document.getElementById('reportModal').dataset.equipmentId = id;
    document.getElementById('roName').textContent = name || '—';
    document.getElementById('roCategory').textContent = category || '—';
    document.getElementById('roArea').textContent = area || '—';
    document.getElementById('roStatus').textContent = statusDisplay || '—';

    // Reset form
    document.getElementById('faultCategory').value = '';
    document.getElementById('faultDescription').value = '';
    document.getElementById('sevMinor').checked = true;
    document.getElementById('reportFormError').style.display = 'none';
    document.getElementById('photoPreview').style.display = 'none';
    document.getElementById('photoCapture').value = '';
    document.getElementById('photoUpload').value = '';
    selectedPhotoFile = null;

    document.getElementById('reportFormView').style.display = 'block';
    document.getElementById('reportSuccessView').style.display = 'none';
    var btn = document.getElementById('reportSubmitBtn');
    btn.disabled = false;
    btn.textContent = 'Submit Report';

    document.getElementById('reportModal').style.display = 'flex';
}

function closeReportModal() {
    document.getElementById('reportModal').style.display = 'none';
}

document.addEventListener('DOMContentLoaded', function () {
    var overlay = document.getElementById('reportModal');
    if (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === this) closeReportModal();
        });
    }
});

/* ====================================================
   PHOTO SELECT / PREVIEW
==================================================== */
function handlePhotoSelect(input) {
    var file = input.files && input.files[0];
    if (!file) return;

    selectedPhotoFile = file;

    var reader = new FileReader();
    reader.onload = function (e) {
        var preview = document.getElementById('photoPreview');
        preview.src = e.target.result;
        preview.style.display = 'block';
    };
    reader.readAsDataURL(file);
}

/* ====================================================
   SUBMIT REPORT
==================================================== */
function submitReport() {
    var equipmentId = document.getElementById('reportModal').dataset.equipmentId;
    var category = document.getElementById('faultCategory').value;
    var severity = document.querySelector('input[name="severity"]:checked').value;
    var description = document.getElementById('faultDescription').value.trim();
    var errorBox = document.getElementById('reportFormError');

    if (!category) {
        errorBox.textContent = 'Please select a fault category.';
        errorBox.style.display = 'block';
        return;
    }
    if (description.length < 10) {
        errorBox.textContent = 'Please describe the issue (at least 10 characters).';
        errorBox.style.display = 'block';
        return;
    }
    errorBox.style.display = 'none';

    var btn = document.getElementById('reportSubmitBtn');
    btn.disabled = true;
    btn.textContent = 'Submitting…';

    var formData = new FormData();
    formData.append('equipmentId', equipmentId);
    formData.append('category', category);
    formData.append('severity', severity);
    formData.append('faultDescription', description);
    formData.append('__RequestVerificationToken', getEqToken());
    if (selectedPhotoFile) formData.append('photoFile', selectedPhotoFile);

    fetch('/Dashboard/ReportFault', {
        method: 'POST',
        body: formData
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (data.success) {
                document.getElementById('reportFormView').style.display = 'none';
                document.getElementById('successMessage').textContent = data.message;
                document.getElementById('successRef').textContent = 'Reference: ' + data.referenceNumber;
                document.getElementById('reportSuccessView').style.display = 'block';
            } else {
                errorBox.textContent = data.message || 'Something went wrong. Please try again.';
                errorBox.style.display = 'block';
                btn.disabled = false;
                btn.textContent = 'Submit Report';
            }
        })
        .catch(function () {
            errorBox.textContent = 'Connection error. Please try again.';
            errorBox.style.display = 'block';
            btn.disabled = false;
            btn.textContent = 'Submit Report';
        });
}