var memberInfo = {
    gender: 'Male'
};

function getToken() {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
}

function loadFitnessSection() {
    fetch('/Dashboard/GetFitnessProfile')
        .then(function (r) { return r.json(); })
        .then(function (data) {
            if (!data.success) return;

            if (!data.hasProfile) {
                document.getElementById('fitness-setup-panel').style.display = 'block';
                document.getElementById('fitness-progress-panel').style.display = 'none';
                document.getElementById('fitness-no-profile-msg').style.display = 'block';
            } else {
                document.getElementById('fitness-setup-panel').style.display = 'block';
                document.getElementById('fitness-progress-panel').style.display = 'block';
                document.getElementById('fitness-no-profile-msg').style.display = 'none';
                renderFitnessProfile(data);
                renderBMIGauge(data.bmi);
                renderProgressChart(data.chartData);
                renderProgressLogs(data.chartData);
            }
        })
        .catch(function () { console.error('[GymHub] Could not load fitness profile.'); });
}

/* ==================================================
   RENDER PROFILE DATA INTO THE UI
================================================== */
function renderFitnessProfile(data) {
    setEl('fit-bmi-value', data.bmi);
    setEl('fit-bmi-category', data.bmiCategory);
    setEl('fit-bmr-value', data.bmr + ' kcal');
    setEl('fit-tdee-value', data.tdee + ' kcal');
    setEl('fit-goal-value', data.goalType);
    setEl('fit-projected-date', data.projectedGoalDate);
    setEl('fit-weeks-completed', data.weeksCompleted + ' / ' + data.projectedWeeks + ' weeks');
    setEl('fit-weight-change', (parseFloat(data.totalWeightChange) > 0 ? '+' : '') + data.totalWeightChange + ' kg');

    var wtEl = document.getElementById('fit-weight-change');
    if (wtEl) {
        var change = parseFloat(data.totalWeightChange);
        var isLoss = data.goalType === 'Weight Loss';
        wtEl.style.color = (isLoss ? change < 0 : change > 0) ? '#22C55E' : '#EF4444';
    }

    var bar = document.getElementById('fit-progress-bar');
    var badge = document.getElementById('fit-progress-badge');
    if (bar) bar.style.width = data.progressPct + '%';
    if (badge) badge.textContent = data.progressPct + '% complete';

    var trackBadge = document.getElementById('fit-on-track-badge');
    if (trackBadge) {
        trackBadge.textContent = data.isOnTrack ? '✅ On Track' : '⚠️ Needs Attention';
        trackBadge.style.color = data.isOnTrack ? '#22C55E' : '#F59E0B';
        trackBadge.style.fontWeight = '700';
        trackBadge.style.fontSize = '13px';
    }

    setVal('fit-height', data.heightCm);
    setVal('fit-weight', data.weightKg);
    setVal('fit-bodyfat', data.bodyFatPercent);
    setVal('fit-activity', data.activityLevel);
    setVal('fit-goal', data.goalType);
    setVal('fit-target-weight', data.targetWeightKg);
    setVal('fit-target-bodyfat', data.targetBodyFatPercent);
}

function setEl(id, val) { var el = document.getElementById(id); if (el) el.textContent = val; }
function setVal(id, val) { var el = document.getElementById(id); if (el) el.value = val; }

/* ==================================================
   BMI COLOUR HELPER
================================================== */
function getBmiColor(bmi) {
    var b = parseFloat(bmi);
    if (b < 18.5) return '#3B82F6';
    if (b < 25) return '#22C55E';
    if (b < 30) return '#F59E0B';
    return '#EF4444';
}

/* ==================================================
   BMI SPEEDOMETER GAUGE (SVG semicircle)
================================================== */
function renderBMIGauge(bmi) {
    var container = document.getElementById('fit-bmi-gauge');
    if (!container) return;

    var b = parseFloat(bmi) || 0;

    // Map BMI 10–45 onto -90deg to +90deg sweep
    var minBmi = 10, maxBmi = 45;
    var pct = Math.min(Math.max((b - minBmi) / (maxBmi - minBmi), 0), 1);
    var angleDeg = -90 + pct * 180;
    var angleRad = (angleDeg * Math.PI) / 180;

    var cx = 120, cy = 110, r = 80;
    var needleLen = 68;
    var nx = cx + needleLen * Math.cos(angleRad);
    var ny = cy + needleLen * Math.sin(angleRad);

    // 4 coloured arc zones
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
            '" stroke="' + z.color + '" stroke-width="18" fill="none" stroke-linecap="butt" opacity="0.9"/>';
    }).join('');

    // Scale tick marks at 10, 18.5, 25, 30, 45
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
            '" text-anchor="middle" dominant-baseline="middle" font-size="8" fill="#94A3B8">' + t.label + '</text>';
    }).join('');

    var cat = b < 18.5 ? 'Underweight' : b < 25 ? 'Normal' : b < 30 ? 'Overweight' : 'Obese';
    var color = getBmiColor(b);

    container.innerHTML =
        '<svg viewBox="0 0 240 135" style="width:100%;max-width:240px;">' +
        // grey background arc
        '<path d="' + arcPath(-90, 90, r) + '" stroke="#E2E8F0" stroke-width="18" fill="none"/>' +
        arcsSVG +
        ticksSVG +
        // needle
        '<line x1="' + cx + '" y1="' + cy +
        '" x2="' + nx.toFixed(1) + '" y2="' + ny.toFixed(1) +
        '" stroke="#1E293B" stroke-width="3" stroke-linecap="round"/>' +
        '<circle cx="' + cx + '" cy="' + cy + '" r="6" fill="#1E293B"/>' +
        // centre BMI number
        '<text x="' + cx + '" y="' + (cy + 20) +
        '" text-anchor="middle" font-family="Bebas Neue,sans-serif" font-size="24" fill="' + color + '">' +
        (b > 0 ? b : '—') + '</text>' +
        '</svg>' +
        // category label below gauge
        '<div style="font-size:13px;font-weight:700;color:' + color + ';margin-top:2px;text-align:center;">' +
        (b > 0 ? cat : 'Save your profile to see BMI') +
        '</div>' +
        // colour legend dots
        '<div style="display:flex;gap:6px;margin-top:10px;flex-wrap:wrap;justify-content:center;">' +
        zones.map(function (z) {
            return '<span style="font-size:10px;font-weight:700;color:' + z.color +
                ';background:' + z.color + '18;padding:2px 8px;border-radius:50px;">' +
                z.label + '</span>';
        }).join('') +
        '</div>';
}

/* ==================================================
   LIVE BMI PREVIEW — also updates gauge in real time
================================================== */
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
        var cat = bmi < 18.5 ? 'Underweight' : bmi < 25 ? 'Normal' : bmi < 30 ? 'Overweight' : 'Obese';
        preview.textContent = 'BMI: ' + bmi + ' (' + cat + ')';
        preview.style.color = getBmiColor(bmi);

        // Update gauge live while typing
        renderBMIGauge(bmi);

        if (bodyFat > 0 && recDiv) {
            var gender = (typeof memberInfo !== 'undefined' && memberInfo.gender) ? memberInfo.gender : 'Male';
            fetch('/Dashboard/GetRecommendedGoal?bmi=' + bmi + '&bodyFat=' + bodyFat + '&gender=' + encodeURIComponent(gender))
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (d.success) {
                        recDiv.innerHTML = '💡 <strong>Recommended goal:</strong> ' + d.recommended + ' — ' + d.tip;
                        recDiv.style.display = 'block';
                        var goalSelect = document.getElementById('fit-goal');
                        if (goalSelect && !goalSelect.dataset.manuallyChanged) goalSelect.value = d.recommended;
                    }
                });
        }
    } else {
        preview.textContent = '';
        if (recDiv) recDiv.style.display = 'none';
    }
}

/* ==================================================
   SAVE FITNESS PROFILE
================================================== */
function saveFitnessProfile() {
    var btn = document.getElementById('fit-save-btn');
    var errEl = document.getElementById('fit-form-error');
    var succEl = document.getElementById('fit-form-success');
    var height = parseFloat(document.getElementById('fit-height').value) || 0;
    var weight = parseFloat(document.getElementById('fit-weight').value) || 0;
    var bodyFat = parseFloat(document.getElementById('fit-bodyfat').value) || 0;
    var activity = document.getElementById('fit-activity').value;
    var goal = document.getElementById('fit-goal').value;
    var targetWt = parseFloat(document.getElementById('fit-target-weight').value) || 0;
    var targetFat = parseFloat(document.getElementById('fit-target-bodyfat').value) || 0;

    errEl.style.display = 'none'; succEl.style.display = 'none';

    if (!height || !weight || !bodyFat || !activity || !goal || !targetWt || !targetFat) {
        errEl.textContent = 'Please fill in all fields.';
        errEl.style.display = 'block'; return;
    }

    btn.disabled = true; btn.textContent = '⏳ Saving...';

    fetch('/Dashboard/SaveFitnessProfile', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            heightCm: height, weightKg: weight, bodyFatPercent: bodyFat,
            activityLevel: activity, goalType: goal,
            targetWeightKg: targetWt, targetBodyFatPercent: targetFat,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            btn.disabled = false; btn.textContent = '💾 Save Profile';
            if (data.success) {
                succEl.innerHTML = '✅ ' + data.message +
                    '<br/><small>BMI: ' + data.bmi + ' (' + data.bmiCategory + ') | BMR: ' + data.bmr + ' kcal/day | TDEE: ' + data.tdee + ' kcal/day</small>';
                succEl.style.display = 'block';
                setTimeout(loadFitnessSection, 800);
            } else {
                errEl.textContent = data.message; errEl.style.display = 'block';
            }
        })
        .catch(function () {
            btn.disabled = false; btn.textContent = '💾 Save Profile';
            errEl.textContent = 'Connection error.'; errEl.style.display = 'block';
        });
}

/* ==================================================
   LOG WEEKLY PROGRESS
================================================== */
function logWeeklyProgress() {
    var btn = document.getElementById('fit-log-btn');
    var errEl = document.getElementById('fit-log-error');
    var succEl = document.getElementById('fit-log-success');
    var weight = parseFloat(document.getElementById('fit-log-weight').value) || 0;
    var bodyFat = parseFloat(document.getElementById('fit-log-bodyfat').value) || 0;
    var workouts = parseInt(document.getElementById('fit-log-workouts').value) || 0;
    var notes = document.getElementById('fit-log-notes').value;

    errEl.style.display = 'none'; succEl.style.display = 'none';
    if (!weight || !bodyFat) {
        errEl.textContent = 'Weight and body fat % are required.';
        errEl.style.display = 'block'; return;
    }

    btn.disabled = true; btn.textContent = '⏳ Logging...';
    fetch('/Dashboard/LogWeeklyProgress', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            weightKg: weight, bodyFatPercent: bodyFat,
            workoutsCompleted: workouts, notes: notes,
            __RequestVerificationToken: getToken()
        }).toString()
    })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            btn.disabled = false; btn.textContent = '📊 Log This Week';
            if (data.success) {
                succEl.textContent = data.message; succEl.style.display = 'block';
                document.getElementById('fit-log-weight').value = '';
                document.getElementById('fit-log-bodyfat').value = '';
                document.getElementById('fit-log-workouts').value = '';
                document.getElementById('fit-log-notes').value = '';
                setTimeout(loadFitnessSection, 800);
            } else {
                errEl.textContent = data.message; errEl.style.display = 'block';
            }
        })
        .catch(function () {
            btn.disabled = false; btn.textContent = '📊 Log This Week';
            errEl.textContent = 'Connection error.'; errEl.style.display = 'block';
        });
}

/* ==================================================
   RENDER PROGRESS CHART — works with 0, 1, or many entries
================================================== */
function renderProgressChart(chartData) {
    var container = document.getElementById('fit-chart-wrap');
    if (!container) return;

    // No entries yet
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

    // For a single point, centre it horizontally
    function xPos(i) { return weeks === 1 ? w / 2 : pad + (i / (weeks - 1)) * (w - pad * 2); }
    function yPos(val) { return h - pad - ((val - minW) / (maxW - minW)) * (h - pad * 2); }

    // Only draw the line when there are 2+ points
    var lineSVG = '';
    if (weeks > 1) {
        var points = chartData.map(function (d, i) {
            return xPos(i).toFixed(1) + ',' + yPos(d.weight).toFixed(1);
        }).join(' ');
        lineSVG = '<polyline points="' + points + '" fill="none" stroke="#3B82F6" stroke-width="2.5" stroke-linejoin="round"/>';
    }

    var dots = chartData.map(function (d, i) {
        var cx = xPos(i).toFixed(1);
        var cy = yPos(d.weight).toFixed(1);
        var col = d.isUnderperforming ? '#EF4444' : '#3B82F6';
        return '<circle cx="' + cx + '" cy="' + cy + '" r="5" fill="' + col + '"/>' +
            '<text x="' + cx + '" y="' + (parseFloat(cy) - 10) +
            '" text-anchor="middle" fill="#475569" font-size="11" font-weight="bold">' + d.weight + ' kg</text>';
    }).join('');

    var labels = chartData.map(function (d, i) {
        return '<text x="' + xPos(i).toFixed(1) + '" y="' + (h - 6) +
            '" text-anchor="middle" fill="#94A3B8" font-size="10">W' + d.week + '</text>';
    }).join('');

    // Helpful hint when only 1 entry
    var hint = weeks === 1
        ? '<text x="' + (w / 2) + '" y="' + (h - pad - 28) +
        '" text-anchor="middle" fill="#CBD5E1" font-size="11">Log week 2 to see your trend line</text>'
        : '';

    container.innerHTML =
        '<svg viewBox="0 0 ' + w + ' ' + h + '" style="width:100%;overflow:visible;">' +
        lineSVG + dots + labels + hint +
        '</svg>';
}

/* ==================================================
   RENDER PROGRESS LOG HISTORY
================================================== */
function renderProgressLogs(chartData) {
    var el = document.getElementById('fit-log-history');
    if (!el) return;

    if (!chartData || chartData.length === 0) {
        el.innerHTML = '<div class="loading-msg">No progress logs yet. Start logging above!</div>';
        return;
    }

    el.innerHTML = chartData.slice().reverse().map(function (d) {
        var icon = d.isUnderperforming ? '⚠️' : '✅';
        var changeStr = (d.weightChange > 0 ? '+' : '') + d.weightChange + ' kg';
        var changeColor = d.weightChange < 0 ? '#22C55E' : '#EF4444';
        return '<div class="log-row">' +
            '<div class="log-icon">' + icon + '</div>' +
            '<div class="log-info">' +
            '<div class="log-title">Week ' + d.week + ' — ' + d.weight + ' kg | ' + d.bodyFat + '% body fat</div>' +
            '<div class="log-meta">' + d.loggedAt + ' · ' + d.workouts + ' workouts</div>' +
            '</div>' +
            '<div class="log-change" style="color:' + changeColor + ';">' + changeStr + '</div>' +
            '</div>';
    }).join('');
}

/* ==================================================
   BOOT
================================================== */
document.addEventListener('DOMContentLoaded', function () {
    loadFitnessSection();
});