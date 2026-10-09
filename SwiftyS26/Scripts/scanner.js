/* ====================================================
   GYMHUB QR SCANNER — scanner.js
==================================================== */

var videoStream = null;
var scanInterval = null;
var scanCooldown = false;
var scanCount = 0;

/* ====================================================
   ANTI FORGERY TOKEN
==================================================== */
function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

/* ====================================================
   CAMERA — START
==================================================== */
function startCamera() {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        alert('Camera not supported on this browser. Please use the manual Member ID input below.');
        return;
    }

    navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
        .then(function (stream) {
            videoStream = stream;
            var video = document.getElementById('cameraFeed');
            video.srcObject = stream;
            video.play();

            document.getElementById('startBtn').style.display = 'none';
            document.getElementById('stopBtn').style.display = 'block';

            // Start scanning frames
            scanInterval = setInterval(scanFrame, 500);
        })
        .catch(function (err) {
            console.error('Camera error:', err);
            alert('Could not access camera. Please use the manual Member ID input below.');
        });
}

/* ====================================================
   CAMERA — STOP
==================================================== */
function stopCamera() {
    if (videoStream) {
        videoStream.getTracks().forEach(function (t) { t.stop(); });
        videoStream = null;
    }
    if (scanInterval) {
        clearInterval(scanInterval);
        scanInterval = null;
    }
    document.getElementById('startBtn').style.display = 'block';
    document.getElementById('stopBtn').style.display = 'none';
}

/* ====================================================
   SCAN FRAME — reads QR from camera
==================================================== */
function scanFrame() {
    if (scanCooldown) return;

    var video = document.getElementById('cameraFeed');
    var canvas = document.getElementById('scanCanvas');

    if (!video || video.readyState !== video.HAVE_ENOUGH_DATA) return;

    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;

    var ctx = canvas.getContext('2d');
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    var imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);

    if (typeof jsQR === 'undefined') return;

    var code = jsQR(imageData.data, imageData.width, imageData.height, {
        inversionAttempts: 'dontInvert'
    });

    if (code && code.data) {
        scanCooldown = true;
        verifyQrData(code.data);
    }
}

/* ====================================================
   VERIFY QR DATA — sends to C# controller
==================================================== */
function verifyQrData(qrData) {
    fetch('/Admin/VerifyQr', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': getToken()
        },
        body: 'qrData=' + encodeURIComponent(qrData)
    })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            showResult(data);
        })
        .catch(function (err) {
            console.error('Verify error:', err);
            showResult({ success: false, message: 'Connection error. Please try again.' });
        });
}

/* ====================================================
   MANUAL MEMBER ID VERIFICATION
   Builds the same QR content string the controller expects
==================================================== */
function verifyManualId() {
    var input = document.getElementById('manualMemberId');
    var memberId = input.value.trim().toUpperCase();

    if (!memberId) {
        input.focus();
        input.style.borderColor = '#DC2626';
        setTimeout(function () { input.style.borderColor = ''; }, 1500);
        return;
    }

    // Validate format: GH-XXXXXX
    if (!/^GH-\d{6}$/.test(memberId)) {
        input.style.borderColor = '#DC2626';
        showDenied('Invalid format. Please enter a valid Member ID (e.g. GH-987805)', null, null);
        return;
    }

    input.style.borderColor = '';

    // Build the QR content string the controller parses
    // Format: "GYMHUB MEMBER | ID: GH-XXXXXX | Name: ..."
    var fakeQrContent = 'GYMHUB MEMBER | ID: ' + memberId + ' | Name: Manual Entry | Plan: N/A | Email: N/A | Issued: Manual';

    verifyQrData(fakeQrContent);
}

/* ====================================================
   SHOW RESULT
==================================================== */
function showResult(data) {
    document.getElementById('resultWaiting').style.display = 'none';
    document.getElementById('resultSuccess').style.display = 'none';
    document.getElementById('resultDenied').style.display = 'none';

    if (data.success) {
        // Access granted
        document.getElementById('successName').textContent = data.memberName || '—';
        document.getElementById('successId').textContent = data.memberNumber || '—';
        document.getElementById('successPlan').textContent = data.plan || '—';
        document.getElementById('resultSuccess').style.display = 'block';
        addToHistory(true, data.memberName, data.memberNumber);
    } else {
        // Access denied
        showDenied(data.message, data.memberName, data.memberNumber);
    }
}

function showDenied(message, memberName, memberNumber) {
    document.getElementById('resultWaiting').style.display = 'none';
    document.getElementById('resultSuccess').style.display = 'none';

    document.getElementById('deniedMsg').textContent = message || 'Access denied.';

    var card = document.getElementById('deniedCard');
    if (memberName || memberNumber) {
        card.style.display = 'block';
        document.getElementById('deniedName').textContent = memberName || '—';
        document.getElementById('deniedId').textContent = memberNumber || '—';
    } else {
        card.style.display = 'none';
    }

    document.getElementById('resultDenied').style.display = 'block';
    addToHistory(false, memberName, memberNumber || '—');
}

/* ====================================================
   RESET SCANNER
==================================================== */
function resetScanner() {
    document.getElementById('resultWaiting').style.display = 'block';
    document.getElementById('resultSuccess').style.display = 'none';
    document.getElementById('resultDenied').style.display = 'none';

    // Clear manual input
    var input = document.getElementById('manualMemberId');
    if (input) input.value = '';

    // Allow scanning again after 1 second
    setTimeout(function () { scanCooldown = false; }, 1000);
}

/* ====================================================
   SCAN HISTORY
==================================================== */
function addToHistory(granted, memberName, memberId) {
    var historyEl = document.getElementById('scanHistory');
    var emptyEl = historyEl.querySelector('.history-empty');
    if (emptyEl) emptyEl.remove();

    var now = new Date();
    var time = now.getHours().toString().padStart(2, '0') + ':' +
        now.getMinutes().toString().padStart(2, '0');

    var item = document.createElement('div');
    item.className = 'history-item ' + (granted ? 'granted' : 'denied');
    item.innerHTML =
        '<div class="history-dot"></div>' +
        '<div class="history-name">' + (memberName || memberId || 'Unknown') + '</div>' +
        '<div class="history-time">' + time + ' — ' + (granted ? 'Granted' : 'Denied') + '</div>';

    historyEl.insertBefore(item, historyEl.firstChild);

    // Keep only last 10
    var items = historyEl.querySelectorAll('.history-item');
    if (items.length > 10) items[items.length - 1].remove();

    scanCount++;
}

/* ====================================================
   INIT
==================================================== */
document.addEventListener('DOMContentLoaded', function () {
    console.log('GymHub Scanner loaded');
});