/**
 * workout-session.js — SwiftyS26 Gym App
 *
 * CRITICAL FIX — saveSession() now uses fetch() with contentType 'text/plain':
 *
 *   $.ajax({ contentType: 'application/json' }) caused MVC5's
 *   JsonValueProviderFactory to consume Request.InputStream BEFORE the
 *   SaveSession action body ran. The StreamReader then read an empty string,
 *   dto was null, nothing was saved, and the button stayed on "Saving…".
 *
 *   fetch() with Content-Type: text/plain bypasses the value provider
 *   entirely — the stream is untouched and the controller reads the full
 *   JSON payload correctly.
 */

; (function () {
    'use strict';

    // Guard: constants must be defined by the inline <script> in the view
    if (typeof EXERCISES === 'undefined' || typeof PLAN_ID === 'undefined') {
        console.error('[WorkoutSession] Required constants not defined. Check view script order.');
        return;
    }

    // ── STATE ─────────────────────────────────────────────────────────
    var state = {
        exercises: EXERCISES,
        currentIdx: 0,
        currentSetsDone: 0,
        isRunning: false,
        sessionStarted: false,
        elapsedSeconds: 0,
        elapsedTimer: null,
        exTimer: null,
        restTimer: null,
        remainingSeconds: 0,
        sessionLog: [],
        skippedCount: 0
    };

    // ── DOM REFS ──────────────────────────────────────────────────────
    function byId(id) { return document.getElementById(id); }

    var el = {
        screens: document.querySelectorAll('.session-screen'),
        progressFill: byId('progressFill'),
        elapsedDisplay: byId('elapsedDisplay'),
        exNum: byId('exNum'),
        exTotal: byId('exTotal'),
        exMediaImg: byId('exMediaImg'),
        exMuscleTag: byId('exMuscleTag'),
        exName: byId('exName'),
        exMetricRow: byId('exMetricRow'),
        exInstructions: byId('exInstructions'),
        setDots: byId('setDots'),
        btnStartPause: byId('btnStartPause'),
        btnIcon: byId('btnIcon'),
        btnLabel: byId('btnLabel'),
        btnNextSet: byId('btnNextSet'),
        btnSkip: byId('btnSkip'),
        restCountdown: byId('restCountdown'),
        restRing: byId('restRing'),
        restNextName: byId('restNextName'),
        exercisePreview: byId('exercisePreview'),
        totalExCount: byId('totalExCount'),
        cDuration: byId('cDuration'),
        cExercises: byId('cExercises'),
        cCalories: byId('cCalories'),
        btnViewSummary: byId('btnViewSummary'),
        badgeAwards: byId('badgeAwards'),
        abandonModal: byId('abandonModal')
    };

    var REST_RING_CIRCUMFERENCE = 2 * Math.PI * 54; // r=54 matches SVG viewBox

    // ── INIT ──────────────────────────────────────────────────────────
    function init() {
        if (!state.exercises || state.exercises.length === 0) {
            console.warn('[WorkoutSession] No exercises for this plan.');
            return;
        }
        el.totalExCount.textContent = state.exercises.length;
        buildExercisePreview();
        setupRestRing();
    }

    function buildExercisePreview() {
        el.exercisePreview.innerHTML = '';
        state.exercises.forEach(function (ex, i) {
            var row = document.createElement('div');
            row.className = 'preview-row';

            var metric;
            if (ex.isTimeBased && ex.durationSeconds) {
                metric = formatTime(ex.durationSeconds);
            } else if (ex.reps && ex.sets) {
                metric = ex.reps + ' reps \u00D7 ' + ex.sets + ' sets';
            } else {
                metric = '\u2014';
            }

            row.innerHTML =
                '<span class="preview-num">' + (i + 1) + '</span>' +
                '<span class="preview-name">' + (ex.name || '') + '</span>' +
                '<span class="preview-metric">' + metric + '</span>';

            el.exercisePreview.appendChild(row);
        });
    }

    function setupRestRing() {
        if (el.restRing) {
            el.restRing.style.strokeDasharray = REST_RING_CIRCUMFERENCE;
            el.restRing.style.strokeDashoffset = 0;
        }
    }

    // ── SCREEN MANAGEMENT ─────────────────────────────────────────────
    function showScreen(id) {
        el.screens.forEach(function (s) { s.classList.remove('active'); });
        var target = byId(id);
        if (target) target.classList.add('active');
    }

    // ── WORKOUT START ─────────────────────────────────────────────────
    window.startWorkout = function () {
        state.sessionStarted = true;
        startElapsedTimer();
        loadExercise(0);
        showScreen('screenExercise');
    };

    // ── LOAD EXERCISE ─────────────────────────────────────────────────
    function loadExercise(idx) {
        if (idx >= state.exercises.length) {
            finishWorkout();
            return;
        }

        clearExTimer();

        state.currentIdx = idx;
        state.currentSetsDone = 0;
        state.isRunning = false;

        var ex = state.exercises[idx];
        var totalSets = ex.sets || 1;

        el.progressFill.style.width = ((idx / state.exercises.length) * 100) + '%';
        el.exNum.textContent = idx + 1;
        el.exTotal.textContent = state.exercises.length;
        el.exMediaImg.src = ex.mediaUrl || '';
        el.exMediaImg.alt = ex.name || '';
        el.exMuscleTag.textContent = ex.muscleGroup || '';
        el.exName.textContent = ex.name || '';
        el.exInstructions.textContent = ex.instructions || ex.description || '';

        renderMetricRow(ex);
        renderSetDots(totalSets);
        setRunningUI(false);

        el.btnNextSet.style.display = ex.isTimeBased ? 'none' : 'flex';

        if (!state.sessionLog[idx]) {
            state.sessionLog[idx] = {
                exerciseId: ex.exerciseId,
                orderIndex: ex.orderIndex,
                repsCompleted: 0,
                setsCompleted: 0,
                durationSeconds: 0,
                skipped: false
            };
        }
    }

    // ── METRIC ROW ────────────────────────────────────────────────────
    function renderMetricRow(ex) {
        el.exMetricRow.innerHTML = '';

        if (ex.isTimeBased) {
            var totalSec = ex.durationSeconds || 0;
            var circumference = 2 * Math.PI * 40;

            el.exMetricRow.innerHTML =
                '<div class="timer-ring-wrap">' +
                '<svg class="timer-ring" viewBox="0 0 100 100" width="110" height="110">' +
                '<circle class="timer-ring-bg" cx="50" cy="50" r="40"/>' +
                '<circle class="timer-ring-fill" cx="50" cy="50" r="40"' +
                ' id="exTimerRing"' +
                ' style="stroke-dasharray:' + circumference + ';stroke-dashoffset:0"/>' +
                '</svg>' +
                '<div class="timer-num">' +
                '<span id="exTimerNum">' + totalSec + '</span>' +
                '<small class="timer-sub">sec</small>' +
                '</div>' +
                '</div>';

            state.remainingSeconds = totalSec;

        } else {
            var reps = ex.reps || 0;
            var sets = ex.sets || 1;

            el.exMetricRow.innerHTML =
                '<div class="metric-badge">' +
                '<div class="metric-num">' + reps + '</div>' +
                '<div class="metric-lbl">Reps</div>' +
                '</div>' +
                '<div class="metric-badge">' +
                '<div class="metric-num" id="currentSetDisplay">1</div>' +
                '<div class="metric-lbl">of ' + sets + ' Sets</div>' +
                '</div>';
        }
    }

    function renderSetDots(total) {
        el.setDots.innerHTML = '';
        for (var i = 0; i < total; i++) {
            var dot = document.createElement('div');
            dot.className = 'set-dot';
            dot.id = 'setDot_' + i;
            el.setDots.appendChild(dot);
        }
    }

    // ── START / PAUSE ─────────────────────────────────────────────────
    window.toggleStartPause = function () {
        var ex = state.exercises[state.currentIdx];

        if (!state.isRunning) {
            state.isRunning = true;
            setRunningUI(true);
            if (ex.isTimeBased) runExerciseTimer(ex);
        } else {
            state.isRunning = false;
            setRunningUI(false);
            clearExTimer();
        }
    };

    function setRunningUI(running) {
        el.btnIcon.className = running ? 'fas fa-pause' : 'fas fa-play';
        el.btnLabel.textContent = running
            ? 'Pause'
            : (state.currentSetsDone === 0 ? 'Start' : 'Resume');
    }

    // ── EXERCISE TIMER (duration-based) ───────────────────────────────
    function runExerciseTimer(ex) {
        var totalSec = ex.durationSeconds || 0;
        var circumference = 2 * Math.PI * 40;
        var remaining = state.remainingSeconds;

        function tick() {
            if (!state.isRunning) return;

            remaining--;
            state.remainingSeconds = remaining;

            var numEl = byId('exTimerNum');
            var ringEl = byId('exTimerRing');

            if (numEl) numEl.textContent = remaining;
            if (ringEl) {
                var pct = remaining / totalSec;
                ringEl.style.strokeDashoffset = circumference * (1 - pct);
            }

            if (remaining <= 0) {
                clearExTimer();
                logTimedExercise(ex, totalSec);
                beginRest(ex);
            } else {
                state.exTimer = setTimeout(tick, 1000);
            }
        }

        state.exTimer = setTimeout(tick, 1000);
    }

    function clearExTimer() {
        if (state.exTimer) { clearTimeout(state.exTimer); state.exTimer = null; }
    }

    // ── COMPLETE A SET (reps-based) ───────────────────────────────────
    window.completeSet = function () {
        if (!state.isRunning) return;

        var ex = state.exercises[state.currentIdx];
        var total = ex.sets || 1;

        state.currentSetsDone++;

        var dot = byId('setDot_' + (state.currentSetsDone - 1));
        if (dot) dot.classList.add('completed');

        var sd = byId('currentSetDisplay');
        if (sd && state.currentSetsDone < total)
            sd.textContent = state.currentSetsDone + 1;

        var logEntry = state.sessionLog[state.currentIdx];
        if (logEntry) {
            logEntry.repsCompleted += ex.reps || 0;
            logEntry.setsCompleted = state.currentSetsDone;
        }

        if (state.currentSetsDone >= total) {
            state.isRunning = false;
            setRunningUI(false);
            clearExTimer();
            beginRest(ex);
        }
    };

    function logTimedExercise(ex, durationSeconds) {
        var logEntry = state.sessionLog[state.currentIdx];
        if (logEntry) {
            logEntry.durationSeconds += durationSeconds;
            logEntry.setsCompleted = 1;
        }
    }

    // ── SKIP ──────────────────────────────────────────────────────────
    window.skipExercise = function () {
        clearExTimer();
        state.isRunning = false;

        var idx = state.currentIdx;
        var ex = state.exercises[idx];

        if (!state.sessionLog[idx]) {
            state.sessionLog[idx] = {
                exerciseId: ex.exerciseId,
                orderIndex: ex.orderIndex,
                repsCompleted: 0,
                setsCompleted: 0,
                durationSeconds: 0,
                skipped: false
            };
        }

        state.sessionLog[idx].skipped = true;
        state.skippedCount++;
        beginRest(ex);
    };

    // ── REST ──────────────────────────────────────────────────────────
    function beginRest(ex) {
        var restSec = ex.restSeconds || 30;
        var nextIdx = state.currentIdx + 1;
        var nextName = nextIdx < state.exercises.length
            ? state.exercises[nextIdx].name
            : 'Workout Complete!';

        el.restNextName.textContent = nextName;
        el.restCountdown.textContent = restSec;
        el.restRing.style.strokeDashoffset = 0;

        showScreen('screenRest');
        runRestTimer(restSec, nextIdx);
    }

    function runRestTimer(totalSec, nextIdx) {
        var remaining = totalSec;

        function tick() {
            if (remaining <= 0) {
                clearRestTimer();
                afterRest(nextIdx);
                return;
            }

            remaining--;
            el.restCountdown.textContent = remaining;

            var pct = remaining / totalSec;
            el.restRing.style.strokeDashoffset = REST_RING_CIRCUMFERENCE * (1 - pct);

            if (remaining <= 0) {
                clearRestTimer();
                afterRest(nextIdx);
            } else {
                state.restTimer = setTimeout(tick, 1000);
            }
        }

        state.restTimer = setTimeout(tick, 1000);
    }

    function clearRestTimer() {
        if (state.restTimer) { clearTimeout(state.restTimer); state.restTimer = null; }
    }

    window.skipRest = function () {
        clearRestTimer();
        afterRest(state.currentIdx + 1);
    };

    function afterRest(nextIdx) {
        if (nextIdx >= state.exercises.length) {
            finishWorkout();
        } else {
            loadExercise(nextIdx);
            showScreen('screenExercise');
        }
    }

    // ── ELAPSED TIMER ─────────────────────────────────────────────────
    function startElapsedTimer() {
        state.elapsedTimer = setInterval(function () {
            state.elapsedSeconds++;
            el.elapsedDisplay.textContent = formatTime(state.elapsedSeconds);
        }, 1000);
    }

    function stopElapsedTimer() {
        if (state.elapsedTimer) { clearInterval(state.elapsedTimer); state.elapsedTimer = null; }
    }

    // ── SUMMARY BUTTON HELPERS ────────────────────────────────────────
    function disableSummaryBtn() {
        if (!el.btnViewSummary) return;
        el.btnViewSummary.removeAttribute('href');
        el.btnViewSummary.style.opacity = '0.5';
        el.btnViewSummary.style.pointerEvents = 'none';
        el.btnViewSummary.textContent = 'Saving\u2026';
    }

    function enableSummaryBtn(sessionId) {
        if (!el.btnViewSummary) return;
        el.btnViewSummary.href = SUMMARY_URL + '?sessionId=' + sessionId;
        el.btnViewSummary.style.opacity = '';
        el.btnViewSummary.style.pointerEvents = '';
        el.btnViewSummary.innerHTML =
            'View Full Summary <i class="fas fa-arrow-right"></i>';
    }

    function fallbackSummaryBtn() {
        if (!el.btnViewSummary) return;
        el.btnViewSummary.href = HUB_URL;
        el.btnViewSummary.style.opacity = '';
        el.btnViewSummary.style.pointerEvents = '';
        el.btnViewSummary.innerHTML = 'Back to Hub';
    }

    // ── FINISH ────────────────────────────────────────────────────────
    function finishWorkout() {
        stopElapsedTimer();
        el.progressFill.style.width = '100%';
        showScreen('screenComplete');

        var dur = state.elapsedSeconds;
        var exDone = state.sessionLog.filter(function (e) { return e && !e.skipped; }).length;
        var calories = Math.round(dur / 60 * 8);

        el.cDuration.textContent = formatTime(dur);
        el.cExercises.textContent = exDone;
        el.cCalories.textContent = calories;

        disableSummaryBtn();
        saveSession('Completed');
    }

    // ── ABANDON ───────────────────────────────────────────────────────
    window.confirmAbandon = function () {
        if (!state.sessionStarted) {
            window.location.href = HUB_URL;
            return;
        }
        el.abandonModal.style.display = 'flex';
    };

    window.closeAbandonModal = function () {
        el.abandonModal.style.display = 'none';
    };

    window.abandonWorkout = function () {
        el.abandonModal.style.display = 'none';
        stopElapsedTimer();
        clearExTimer();
        clearRestTimer();
        state.isRunning = false;
        showScreen('screenComplete');

        var dur = state.elapsedSeconds;
        el.cDuration.textContent = formatTime(dur);
        el.cExercises.textContent = state.sessionLog
            .filter(function (e) { return e && !e.skipped && e.setsCompleted > 0; }).length;
        el.cCalories.textContent = Math.round(dur / 60 * 8);

        disableSummaryBtn();
        saveSession('Abandoned');
    };

    // ── SAVE SESSION ──────────────────────────────────────────────────
    //
    // Uses fetch() with Content-Type: text/plain.
    //
    // WHY NOT $.ajax with application/json:
    //   MVC5's JsonValueProviderFactory activates when it sees
    //   Content-Type: application/json and reads Request.InputStream
    //   during model binding — before your action body executes.
    //   By the time SaveSession() calls StreamReader.ReadToEnd(),
    //   the stream is at EOF and returns "". dto is null. Nothing saves.
    //
    // WHY text/plain works:
    //   MVC5 ignores text/plain bodies during binding. The stream is
    //   untouched. SaveSession() reads the full JSON payload correctly.
    //
    function saveSession(status) {
        var payload = {
            planId: PLAN_ID,
            totalDurationSeconds: state.elapsedSeconds,
            status: status,
            exercises: state.sessionLog.filter(Boolean)
        };

        var jsonBody = JSON.stringify(payload);
        console.log('[WorkoutSession] POSTing to', SAVE_URL, jsonBody);

        fetch(SAVE_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'text/plain' },
            body: jsonBody
        })
            .then(function (response) {
                if (!response.ok)
                    throw new Error('HTTP ' + response.status + ' ' + response.statusText);
                return response.json();
            })
            .then(function (data) {
                console.log('[WorkoutSession] SaveSession response:', data);
                if (data.success) {
                    enableSummaryBtn(data.sessionId);
                    if (data.badges && data.badges.length > 0)
                        showBadges(data.badges);
                } else {
                    console.warn('[SaveSession] Server returned success=false:', data.message);
                    fallbackSummaryBtn();
                }
            })
            .catch(function (err) {
                console.error('[SaveSession] fetch failed:', err);
                fallbackSummaryBtn();
            });
    }

    // ── BADGES ────────────────────────────────────────────────────────
    function showBadges(badges) {
        el.badgeAwards.style.display = 'flex';
        badges.forEach(function (b) {
            var chip = document.createElement('div');
            chip.className = 'badge-item earned';
            chip.innerHTML =
                '<i class="' + b.iconClass + '"></i>' +
                '<span>' + b.badgeName + '</span>';
            el.badgeAwards.appendChild(chip);
        });
    }

    // ── UTIL ──────────────────────────────────────────────────────────
    function formatTime(seconds) {
        var m = Math.floor(seconds / 60);
        var s = seconds % 60;
        return m + ':' + (s < 10 ? '0' + s : s);
    }

    // ── BOOT ──────────────────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', init);

})();