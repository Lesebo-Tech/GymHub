/* ====================================================
   FITNESS GOALS & PROGRESS  —  fitness.js  (single file)
   ==================================================== */

/* --------------------------------------------------
   GLOBALS
-------------------------------------------------- */
var memberInfo = window.memberInfo || { gender: 'Male' };

function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

/* --------------------------------------------------
   SMALL DOM HELPERS
-------------------------------------------------- */
function setEl(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = (val !== null && val !== undefined) ? val : '—';
}

function setVal(id, val) {
    var el = document.getElementById(id);
    if (el) el.value = (val !== null && val !== undefined) ? val : '';
}

function showError(id, msg) {
    var el = document.getElementById(id);
    if (!el) return;
    el.textContent = msg;
    el.style.display = 'block';
}

function hideMsg(id) {
    var el = document.getElementById(id);
    if (el) el.style.display = 'none';
}

function showSuccess(id, html) {
    var el = document.getElementById(id);
    if (!el) return;
    el.innerHTML = html;
    el.style.display = 'block';
}

/* --------------------------------------------------
   BMI COLOUR HELPER
-------------------------------------------------- */
function getBmiColor(bmi) {
    var b = parseFloat(bmi);
    if (b < 18.5) return '#3B82F6';
    if (b < 25) return '#22C55E';
    if (b < 30) return '#F59E0B';
    return '#EF4444';
}

function getBmiCategory(bmi) {
    var b = parseFloat(bmi);
    if (b < 18.5) return 'Underweight';
    if (b < 25) return 'Normal';
    if (b < 30) return 'Overweight';
    return 'Obese';
}

/* --------------------------------------------------
   LOAD FITNESS SECTION  (entry point)
-------------------------------------------------- */
function loadFitnessSection() {
    fetch('/Dashboard/GetFitnessProfile')
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        })
        .then(function (data) {
            if (!data.success) return;

            var setupPanel = document.getElementById('fitness-setup-panel');
            var progressPanel = document.getElementById('fitness-progress-panel');
            var noProfileMsg = document.getElementById('fitness-no-profile-msg');

            if (!data.hasProfile) {
                if (setupPanel) setupPanel.style.display = 'block';
                if (progressPanel) progressPanel.style.display = 'none';
                if (noProfileMsg) noProfileMsg.style.display = 'block';
            } else {
                if (setupPanel) setupPanel.style.display = 'block';
                if (progressPanel) progressPanel.style.display = 'block';
                if (noProfileMsg) noProfileMsg.style.display = 'none';

                renderFitnessProfile(data);
                renderBMIGauge(data.bmi);
                renderProgressChart(data.chartData);
                renderProgressLogs(data.chartData);
            }
        })
        .catch(function (err) {
            console.error('[GymHub] Could not load fitness profile.', err);
        });
}

/* --------------------------------------------------
   RENDER PROFILE DATA INTO THE UI
-------------------------------------------------- */
function renderFitnessProfile(data) {
    setEl('fit-bmi-value', data.bmi);
    setEl('fit-bmi-category', data.bmiCategory);
    setEl('fit-bmr-value', data.bmr + ' kcal');
    setEl('fit-tdee-value', data.tdee + ' kcal');
    setEl('fit-goal-value', data.goalType);
    setEl('fit-projected-date', data.projectedGoalDate);
    setEl('fit-weeks-completed', data.weeksCompleted + ' / ' + data.projectedWeeks + ' weeks');

    var weightChange = parseFloat(data.totalWeightChange) || 0;
    var weightStr = (weightChange > 0 ? '+' : '') + data.totalWeightChange + ' kg';
    setEl('fit-weight-change', weightStr);

    var wtEl = document.getElementById('fit-weight-change');
    if (wtEl) {
        var isLoss = data.goalType === 'Weight Loss';
        wtEl.style.color = (isLoss ? weightChange < 0 : weightChange > 0) ? '#22C55E' : '#EF4444';
    }

    var bar = document.getElementById('fit-progress-bar');
    var badge = document.getElementById('fit-progress-badge');
    if (bar) bar.style.width = (data.progressPct || 0) + '%';
    if (badge) badge.textContent = (data.progressPct || 0) + '% complete';

    var trackBadge = document.getElementById('fit-on-track-badge');
    if (trackBadge) {
        trackBadge.textContent = data.isOnTrack ? '✅ On Track' : '⚠️ Needs Attention';
        trackBadge.style.color = data.isOnTrack ? '#22C55E' : '#F59E0B';
        trackBadge.style.fontWeight = '700';
        trackBadge.style.fontSize = '13px';
    }

    /* Pre-fill the profile form */
    setVal('fit-height', data.heightCm);
    setVal('fit-weight', data.weightKg);
    setVal('fit-bodyfat', data.bodyFatPercent);
    setVal('fit-activity', data.activityLevel);
    setVal('fit-goal', data.goalType);
    setVal('fit-target-weight', data.targetWeightKg);
    setVal('fit-target-bodyfat', data.targetBodyFatPercent);
}

/* --------------------------------------------------
   BMI SPEEDOMETER GAUGE  (SVG semicircle)
-------------------------------------------------- */
function renderBMIGauge(bmi) {
    var container = document.getElementById('fit-bmi-gauge');
    if (!container) return;

    var b = parseFloat(bmi) || 0;
    var minBmi = 10, maxBmi = 45;
    var pct = Math.min(Math.max((b - minBmi) / (maxBmi - minBmi), 0), 1);
    var angleDeg = -90 + pct * 180;
    var angleRad = (angleDeg * Math.PI) / 180;

    var cx = 120, cy = 110, r = 80, needleLen = 68;
    var nx = cx + needleLen * Math.cos(angleRad);
    var ny = cy + needleLen * Math.sin(angleRad);

    var zones = [
        { label: 'Underweight', color: '#3B82F6', from: -90, to: -54 },
        { label: 'Normal', color: '#22C55E', from: -54, to: 0 },
        { label: 'Overweight', color: '#F59E0B', from: 0, to: 36 },
        { label: 'Obese', color: '#EF4444', from: 36, to: 90 }
    ];

    function arcPath(startDeg, endDeg, radius) {
        var s = (startDeg * Math.PI) / 180;
        var e = (endDeg * Math.PI) / 180;
        var x1 = cx + radius * Math.cos(s), y1 = cy + radius * Math.sin(s);
        var x2 = cx + radius * Math.cos(e), y2 = cy + radius * Math.sin(e);
        var large = (endDeg - startDeg) > 180 ? 1 : 0;
        return 'M ' + x1.toFixed(2) + ' ' + y1.toFixed(2) +
            ' A ' + radius + ' ' + radius + ' 0 ' + large + ' 1 ' +
            x2.toFixed(2) + ' ' + y2.toFixed(2);
    }

    var arcsSVG = zones.map(function (z) {
        return '<path d="' + arcPath(z.from, z.to, r) +
            '" stroke="' + z.color + '" stroke-width="18" fill="none"' +
            ' stroke-linecap="butt" opacity="0.9"/>';
    }).join('');

    var ticks = [
        { bmi: 10, label: '10' },
        { bmi: 18.5, label: '18.5' },
        { bmi: 25, label: '25' },
        { bmi: 30, label: '30' },
        { bmi: 45, label: '45' }
    ];
    var ticksSVG = ticks.map(function (t) {
        var tp = Math.min(Math.max((t.bmi - minBmi) / (maxBmi - minBmi), 0), 1);
        var ta = (-90 + tp * 180) * Math.PI / 180;
        var tx = cx + (r + 14) * Math.cos(ta);
        var ty = cy + (r + 14) * Math.sin(ta);
        return '<text x="' + tx.toFixed(1) + '" y="' + ty.toFixed(1) +
            '" text-anchor="middle" dominant-baseline="middle"' +
            ' font-size="8" fill="#94A3B8">' + t.label + '</text>';
    }).join('');

    var cat = getBmiCategory(b);
    var color = getBmiColor(b);

    container.innerHTML =
        '<svg viewBox="0 0 240 135" style="width:100%;max-width:240px;">' +
        '<path d="' + arcPath(-90, 90, r) + '" stroke="#E2E8F0" stroke-width="18" fill="none"/>' +
        arcsSVG + ticksSVG +
        '<line x1="' + cx + '" y1="' + cy +
        '" x2="' + nx.toFixed(1) + '" y2="' + ny.toFixed(1) +
        '" stroke="#1E293B" stroke-width="3" stroke-linecap="round"/>' +
        '<circle cx="' + cx + '" cy="' + cy + '" r="6" fill="#1E293B"/>' +
        '<text x="' + cx + '" y="' + (cy + 20) +
        '" text-anchor="middle" font-family="Bebas Neue,sans-serif"' +
        ' font-size="24" fill="' + color + '">' + (b > 0 ? b : '—') + '</text>' +
        '</svg>' +
        '<div style="font-size:13px;font-weight:700;color:' + color +
        ';margin-top:2px;text-align:center;">' +
        (b > 0 ? cat : 'Save your profile to see BMI') + '</div>' +
        '<div style="display:flex;gap:6px;margin-top:10px;flex-wrap:wrap;justify-content:center;">' +
        zones.map(function (z) {
            return '<span style="font-size:10px;font-weight:700;color:' + z.color +
                ';background:' + z.color + '18;padding:2px 8px;border-radius:50px;">' +
                z.label + '</span>';
        }).join('') + '</div>';
}

/* --------------------------------------------------
   LIVE BMI PREVIEW  (updates gauge in real time)
-------------------------------------------------- */
function updateBMIPreview() {
    var height = parseFloat(document.getElementById('fit-height').value) || 0;
    var weight = parseFloat(document.getElementById('fit-weight').value) || 0;
    var bodyFat = parseFloat(document.getElementById('fit-bodyfat').value) || 0;
    var preview = document.getElementById('fit-bmi-preview');
    var recDiv = document.getElementById('fit-goal-recommendation');
    if (!preview) return;

    if (height > 0 && weight > 0) {
        var hm = height / 100;
        var bmi = (weight / (hm * hm)).toFixed(1);
        var cat = getBmiCategory(bmi);

        preview.textContent = 'BMI: ' + bmi + ' (' + cat + ')';
        preview.style.color = getBmiColor(bmi);

        renderBMIGauge(bmi);

        if (bodyFat > 0 && recDiv) {
            var gender = (typeof memberInfo !== 'undefined' && memberInfo.gender)
                ? memberInfo.gender : 'Male';
            fetch('/Dashboard/GetRecommendedGoal?bmi=' + bmi +
                '&bodyFat=' + bodyFat + '&gender=' + encodeURIComponent(gender))
                .then(function (r) {
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    return r.json();
                })
                .then(function (d) {
                    if (d.success) {
                        recDiv.innerHTML = '💡 <strong>Recommended goal:</strong> ' +
                            d.recommended + ' — ' + d.tip;
                        recDiv.style.display = 'block';
                        var goalSelect = document.getElementById('fit-goal');
                        if (goalSelect && !goalSelect.dataset.manuallyChanged) {
                            goalSelect.value = d.recommended;
                        }
                    }
                })
                .catch(function () { /* silent — recommendation is non-critical */ });
        }
    } else {
        preview.textContent = '';
        if (recDiv) recDiv.style.display = 'none';
    }
}

/* --------------------------------------------------
   SAVE FITNESS PROFILE
-------------------------------------------------- */
function saveFitnessProfile() {
    var btn = document.getElementById('fit-save-btn');
    var height = parseFloat(document.getElementById('fit-height').value) || 0;
    var weight = parseFloat(document.getElementById('fit-weight').value) || 0;
    var bodyFat = parseFloat(document.getElementById('fit-bodyfat').value) || 0;
    var activity = document.getElementById('fit-activity').value;
    var goal = document.getElementById('fit-goal').value;
    var targetWt = parseFloat(document.getElementById('fit-target-weight').value) || 0;
    var targetFat = parseFloat(document.getElementById('fit-target-bodyfat').value) || 0;

    hideMsg('fit-form-error');
    hideMsg('fit-form-success');

    if (!height || !weight || !bodyFat || !activity || !goal || !targetWt || !targetFat) {
        showError('fit-form-error', 'Please fill in all fields.');
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-pulse"></i> Saving…';

    fetch('/Dashboard/SaveFitnessProfile', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            heightCm: height,
            weightKg: weight,
            bodyFatPercent: bodyFat,
            activityLevel: activity,
            goalType: goal,
            targetWeightKg: targetWt,
            targetBodyFatPercent: targetFat,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        })
        .then(function (data) {
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-save"></i> Save Profile';

            if (data.success) {
                showSuccess('fit-form-success',
                    '✅ ' + data.message +
                    '<br/><small>BMI: ' + data.bmi + ' (' + data.bmiCategory +
                    ') | BMR: ' + data.bmr + ' kcal/day | TDEE: ' + data.tdee + ' kcal/day</small>');
                setTimeout(loadFitnessSection, 800);
            } else {
                showError('fit-form-error', data.message || 'Could not save profile.');
            }
        })
        .catch(function (err) {
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-save"></i> Save Profile';
            showError('fit-form-error', 'Connection error. Please try again. (' + err.message + ')');
            console.error('[GymHub] SaveFitnessProfile failed:', err);
        });
}

/* --------------------------------------------------
   LOG WEEKLY PROGRESS
-------------------------------------------------- */
function logWeeklyProgress() {
    var btn = document.getElementById('fit-log-btn');
    var weight = parseFloat(document.getElementById('fit-log-weight').value) || 0;
    var bodyFat = parseFloat(document.getElementById('fit-log-bodyfat').value) || 0;
    var workouts = parseInt(document.getElementById('fit-log-workouts').value) || 0;
    var notes = document.getElementById('fit-log-notes').value;

    hideMsg('fit-log-error');
    hideMsg('fit-log-success');

    if (!weight || !bodyFat) {
        showError('fit-log-error', 'Weight and body fat % are required.');
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner fa-pulse"></i> Logging…';

    fetch('/Dashboard/LogWeeklyProgress', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            weightKg: weight,
            bodyFatPercent: bodyFat,
            workoutsCompleted: workouts,
            notes: notes,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        })
        .then(function (data) {
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-save"></i> Log This Week';

            if (data.success) {
                showSuccess('fit-log-success', data.message);
                document.getElementById('fit-log-weight').value = '';
                document.getElementById('fit-log-bodyfat').value = '';
                document.getElementById('fit-log-workouts').value = '';
                document.getElementById('fit-log-notes').value = '';
                setTimeout(loadFitnessSection, 800);
            } else {
                showError('fit-log-error', data.message || 'Could not log progress.');
            }
        })
        .catch(function (err) {
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-save"></i> Log This Week';
            showError('fit-log-error', 'Connection error. Please try again. (' + err.message + ')');
            console.error('[GymHub] LogWeeklyProgress failed:', err);
        });
}

/* --------------------------------------------------
   RENDER PROGRESS CHART
   Works with 0, 1, or many entries
-------------------------------------------------- */
function renderProgressChart(chartData) {
    var container = document.getElementById('fit-chart-wrap');
    if (!container) return;

    if (!chartData || chartData.length === 0) {
        container.innerHTML =
            '<div style="text-align:center;padding:32px;color:#94A3B8;font-size:13px;">' +
            '📊 Log your first week above to start tracking your progress.</div>';
        return;
    }

    var w = 500, h = 160, pad = 40;
    var weights = chartData.map(function (d) { return d.weight; });
    var minW = Math.min.apply(null, weights) - 3;
    var maxW = Math.max.apply(null, weights) + 3;
    var weeks = chartData.length;

    function xPos(i) {
        return weeks === 1 ? w / 2 : pad + (i / (weeks - 1)) * (w - pad * 2);
    }
    function yPos(val) {
        return h - pad - ((val - minW) / (maxW - minW)) * (h - pad * 2);
    }

    var lineSVG = '';
    if (weeks > 1) {
        var pts = chartData.map(function (d, i) {
            return xPos(i).toFixed(1) + ',' + yPos(d.weight).toFixed(1);
        }).join(' ');
        lineSVG = '<polyline points="' + pts +
            '" fill="none" stroke="#3B82F6" stroke-width="2.5" stroke-linejoin="round"/>';
    }

    var dots = chartData.map(function (d, i) {
        var cx = xPos(i).toFixed(1);
        var cy = yPos(d.weight).toFixed(1);
        var col = d.isUnderperforming ? '#EF4444' : '#3B82F6';
        return '<circle cx="' + cx + '" cy="' + cy + '" r="5" fill="' + col + '"/>' +
            '<text x="' + cx + '" y="' + (parseFloat(cy) - 10) +
            '" text-anchor="middle" fill="#475569" font-size="11" font-weight="bold">' +
            d.weight + ' kg</text>';
    }).join('');

    var labels = chartData.map(function (d, i) {
        return '<text x="' + xPos(i).toFixed(1) + '" y="' + (h - 6) +
            '" text-anchor="middle" fill="#94A3B8" font-size="10">W' + d.week + '</text>';
    }).join('');

    var hint = weeks === 1
        ? '<text x="' + (w / 2) + '" y="' + (h - pad - 28) +
        '" text-anchor="middle" fill="#CBD5E1" font-size="11">' +
        'Log week 2 to see your trend line</text>'
        : '';

    container.innerHTML =
        '<svg viewBox="0 0 ' + w + ' ' + h +
        '" style="width:100%;overflow:visible;">' +
        lineSVG + dots + labels + hint + '</svg>';
}

/* --------------------------------------------------
   RENDER PROGRESS LOG HISTORY
-------------------------------------------------- */
function renderProgressLogs(chartData) {
    var el = document.getElementById('fit-log-history');
    if (!el) return;

    if (!chartData || chartData.length === 0) {
        el.innerHTML =
            '<div style="text-align:center;padding:24px;color:#94A3B8;font-size:13px;">' +
            'No progress logs yet. Start logging above!</div>';
        return;
    }

    el.innerHTML = chartData.slice().reverse().map(function (d) {
        var icon = d.isUnderperforming ? '⚠️' : '✅';
        var change = parseFloat(d.weightChange) || 0;
        var changeStr = (change > 0 ? '+' : '') + d.weightChange + ' kg';
        var changeCol = change < 0 ? '#22C55E' : '#EF4444';

        return '<div class="log-row" style="display:flex;align-items:center;gap:12px;' +
            'padding:10px 0;border-bottom:1px solid #E2E8F0;">' +
            '<div style="font-size:18px;">' + icon + '</div>' +
            '<div style="flex:1;">' +
            '<div style="font-size:13px;font-weight:600;color:#1E293B;">Week ' +
            d.week + ' — ' + d.weight + ' kg | ' + d.bodyFat + '% body fat</div>' +
            '<div style="font-size:11px;color:#64748B;margin-top:2px;">' +
            d.loggedAt + ' · ' + d.workouts + ' workouts</div>' +
            '</div>' +
            '<div style="font-family:\'Bebas Neue\',sans-serif;font-size:16px;color:' +
            changeCol + ';">' + changeStr + '</div></div>';
    }).join('');
}

/* --------------------------------------------------
   BOOT
-------------------------------------------------- */
document.addEventListener('DOMContentLoaded', function () {
    loadFitnessSection();
});
