using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class TrainerController : Controller
    {
        private GymHubDB db = new GymHubDB();

        private bool IsTrainerLoggedIn() => Session["LoggedInTrainer"] != null;

        private Trainer GetLoggedInTrainer()
        {
            if (!IsTrainerLoggedIn()) return null;
            int id = (int)Session["LoggedInTrainer"];
            return db.Trainers.Find(id);
        }

        // ============================================================
        // DASHBOARD
        // ============================================================
        public ActionResult Dashboard()
        {
            if (!IsTrainerLoggedIn())
                return RedirectToAction("Login", "Admin");

            var trainer = GetLoggedInTrainer();
            if (trainer == null)
                return RedirectToAction("Login", "Admin");

            if (trainer.MustChangePassword)
                return RedirectToAction("ChangePassword");

            int trainerId = trainer.TrainerId;

            var assignedMembers = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Include(mt => mt.Member)
                .ToList();

            var upcomingSessions = db.TrainingSessions
                .Where(s => s.TrainerId == trainerId && s.Status == "Scheduled" && s.SessionDate >= DateTime.Today)
                .Include(s => s.MemberForm)
                .OrderBy(s => s.SessionDate)
                .Take(10)
                .ToList();

            var workoutPrograms = db.WorkoutPrograms
                .Where(w => w.TrainerId == trainerId)
                .Include(w => w.ProgramExercises)
                .ToList();

            var progressLogs = db.MemberProgresses
                .Where(p => p.TrainerId == trainerId)
                .Include(p => p.MemberForm)
                .OrderByDescending(p => p.RecordedDate)
                .Take(20)
                .ToList();

            var availability = db.TrainerAvailabilities
                .Where(a => a.TrainerId == trainerId)
                .ToList();

            var memberSessionLogs = db.WorkoutSessionLogs
           .Where(l => l.TrainerId == trainerId)
           .Include(l => l.MemberForm)
           .Include(l => l.WorkoutProgram)
           .OrderByDescending(l => l.SessionDate)
           .Take(30)
           .ToList();

            ViewBag.TrainerName = trainer.FullName;
            ViewBag.TrainerCode = trainer.TrainerCode;
            ViewBag.TrainerRole = trainer.Role;
            ViewBag.Specialization = trainer.Specialization;
            ViewBag.EmploymentType = trainer.EmploymentType;
            ViewBag.ExperienceLevel = trainer.ExperienceLevel;
            ViewBag.TotalSessions = upcomingSessions.Count;
            ViewBag.TotalPrograms = workoutPrograms.Count;


            // Then add it to the VM:
            var vm = new TrainerDashboardViewModel
            {
                AssignedMembers = assignedMembers,
                UpcomingSessions = upcomingSessions,
                WorkoutPrograms = workoutPrograms,
                ProgressLogs = progressLogs,
                Availability = availability,
                MemberSessionLogs = memberSessionLogs   // ← ADD THIS
            };

            return View(vm);
        }

        // ============================================================
        // ASSIGNED MEMBERS
        // ============================================================
        public ActionResult MyMembers()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var members = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Include(mt => mt.Member)
                .ToList();

            ViewBag.TrainerName = trainer.FullName;
            return View(members);
        }

        // ============================================================
        // SCHEDULE — VIEW
        // ============================================================
        public ActionResult Schedule()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;
            var sessions = db.TrainingSessions
                .Where(s => s.TrainerId == trainerId)
                .Include(s => s.MemberForm)
                .OrderBy(s => s.SessionDate)
                .ToList();

            var availability = db.TrainerAvailabilities
                .Where(a => a.TrainerId == trainerId)
                .ToList();

            ViewBag.TrainerName = trainer.FullName;
            ViewBag.Sessions = sessions;
            ViewBag.Availability = availability;

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Select(mt => mt.MemberId)
                .ToList();
            ViewBag.Members = db.Members
                .Where(m => memberIds.Contains(m.MemberId))
                .ToList();

            return View(sessions);
        }

        // ============================================================
        // SCHEDULE — BOOK SESSION (GET)
        // ============================================================
        public ActionResult BookSession()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Select(mt => mt.MemberId)
                .ToList();

            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName");

            ViewBag.SessionTypes = new SelectList(new[] { "Strength", "Cardio", "HIIT", "Flexibility", "Mixed" });
            ViewBag.TimeSlots = new SelectList(new[] { "06:00", "07:00", "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00", "19:00", "20:00" });
            ViewBag.Durations = new SelectList(new[] { "30 min", "45 min", "60 min", "90 min" });

            return View(new TrainingSession { TrainerId = trainerId, SessionDate = DateTime.Today, Status = "Scheduled" });
        }

        // ============================================================
        // SCHEDULE — BOOK SESSION (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookSession(TrainingSession model)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            model.TrainerId = trainer.TrainerId;
            model.Status = "Scheduled";

            if (ModelState.IsValid)
            {
                db.TrainingSessions.Add(model);
                db.SaveChanges();
                TempData["Success"] = "Session booked successfully.";
                return RedirectToAction("Schedule");
            }

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainer.TrainerId)
                .Select(mt => mt.MemberId).ToList();
            ViewBag.Members = new SelectList(db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(), "MemberId", "FullName");
            ViewBag.SessionTypes = new SelectList(new[] { "Strength", "Cardio", "HIIT", "Flexibility", "Mixed" });
            ViewBag.TimeSlots = new SelectList(new[] { "06:00", "07:00", "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00", "18:00", "19:00", "20:00" });
            ViewBag.Durations = new SelectList(new[] { "30 min", "45 min", "60 min", "90 min" });
            return View(model);
        }

        // ============================================================
        // SCHEDULE — UPDATE SESSION STATUS
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSessionStatus(int sessionId, string status)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();

            var session = db.TrainingSessions.Find(sessionId);
            if (session != null && session.TrainerId == trainer.TrainerId)
            {
                session.Status = status;
                db.SaveChanges();
                TempData["Success"] = $"Session marked as {status}.";
            }
            return RedirectToAction("Schedule");
        }

        // ============================================================
        // AVAILABILITY — GET
        // ============================================================
        public ActionResult Availability()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var availability = db.TrainerAvailabilities
                .Where(a => a.TrainerId == trainerId)
                .ToList();

            ViewBag.TrainerName = trainer.FullName;
            return View(availability);
        }

        // ============================================================
        // AVAILABILITY — SAVE (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveAvailability(List<TrainerAvailability> availabilities)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var existing = db.TrainerAvailabilities.Where(a => a.TrainerId == trainerId).ToList();
            db.TrainerAvailabilities.RemoveRange(existing);

            if (availabilities != null)
            {
                foreach (var a in availabilities)
                {
                    a.TrainerId = trainerId;
                    db.TrainerAvailabilities.Add(a);
                }
            }

            db.SaveChanges();
            TempData["Success"] = "Availability updated.";
            return RedirectToAction("Availability");
        }

        // ============================================================
        // WORKOUT PROGRAMS — LIST
        // ============================================================
        public ActionResult WorkoutPrograms()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var programs = db.WorkoutPrograms
                .Where(w => w.TrainerId == trainerId)
                .Include(w => w.ProgramExercises)
                .Include(w => w.MemberForm)
                .ToList();

            ViewBag.TrainerName = trainer.FullName;
            ViewBag.TrainerRole = trainer.Role;
            ViewBag.TrainerCode = trainer.TrainerCode;
            return View(programs);
        }

        // ============================================================
        // WORKOUT PROGRAMS — CREATE (GET)
        // ============================================================
        public ActionResult CreateWorkoutProgram()
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Select(mt => mt.MemberId).ToList();
            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName");

            return View(new WorkoutProgram { TrainerId = trainerId, CreatedDate = DateTime.Today });
        }

        // ============================================================
        // WORKOUT PROGRAMS — CREATE (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateWorkoutProgram(WorkoutProgram model)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            model.TrainerId = trainer.TrainerId;

            if (ModelState.IsValid)
            {
                db.WorkoutPrograms.Add(model);
                db.SaveChanges();
                TempData["Success"] = "Workout program created. Now add exercises.";
                return RedirectToAction("AddExercises", new { programId = model.WorkoutProgramId });
            }

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainer.TrainerId)
                .Select(mt => mt.MemberId).ToList();
            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName");
            return View(model);
        }


        // ────────────────────────────────────────────────────────────────────────────
        // REPLACE these two actions in TrainerController.cs
        // ────────────────────────────────────────────────────────────────────────────

        // ── 1. AddExercises GET ──────────────────────────────────────────────────────
        //    todayLog now only blocks the "Mark Done" button for TODAY.
        //    The Add Exercise form is always available regardless.

        public ActionResult AddExercises(int programId)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");

            var program = db.WorkoutPrograms
                .Include(w => w.ProgramExercises)
                .Include(w => w.MemberForm)
                .FirstOrDefault(w => w.WorkoutProgramId == programId);

            if (program == null) return HttpNotFound();

            var fitnessProfile = db.FitnessProfiles
                .FirstOrDefault(f => f.MemberId == program.MemberId);

            // Only check for TODAY's log — so the Mark Done button is hidden if today is done.
            // Adding exercises is always allowed; they go into the program for future sessions.
            var todayLog = db.WorkoutSessionLogs
                .FirstOrDefault(l =>
                    l.WorkoutProgramId == programId &&
                    l.SessionDate == DateTime.Today);

            var trainer = GetLoggedInTrainer();
            ViewBag.TrainerName = trainer.FullName;
            ViewBag.TrainerRole = trainer.Role;
            ViewBag.TrainerCode = trainer.TrainerCode;
            ViewBag.Program = program;
            ViewBag.FitnessProfile = fitnessProfile;
            ViewBag.TodayLog = todayLog;   // null = not done yet; non-null = already logged today

            return View(new ProgramExercise { WorkoutProgramId = programId });
        }


        // ── 2. AddExercise POST ──────────────────────────────────────────────────────
        //    Exercises are ALWAYS added to the program (no date restriction).
        //    They will show up in the NEXT session's "Today's Session" panel
        //    once the current day's log exists.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddExercise(ProgramExercise model)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");

            if (ModelState.IsValid)
            {
                db.ProgramExercises.Add(model);
                db.SaveChanges();
                TempData["Success"] = $"{model.ExerciseName} added to the program.";
            }

            return RedirectToAction("AddExercises", new { programId = model.WorkoutProgramId });
        }


        // ── 3. MarkDayDone POST ──────────────────────────────────────────────────────
        //    Snapshots ONLY the exercises that existed BEFORE this moment.
        //    Any exercises added AFTER marking done are NOT included in today's log —
        //    they will appear fresh the next time "Mark Day Done" is available.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkDayDone(int workoutProgramId)
        {
            if (!IsTrainerLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var program = db.WorkoutPrograms
                .Include(w => w.ProgramExercises)
                .FirstOrDefault(w => w.WorkoutProgramId == workoutProgramId
                                  && w.TrainerId == trainerId);

            if (program == null)
                return Json(new { success = false, message = "Program not found." });

            // Block duplicate log for the same program on the same day
            bool alreadyLogged = db.WorkoutSessionLogs.Any(l =>
                l.WorkoutProgramId == workoutProgramId &&
                l.SessionDate == DateTime.Today);

            if (alreadyLogged)
                return Json(new { success = false, message = "Today's session is already marked as done." });

            // ── KEY: snapshot only the exercises that exist RIGHT NOW ──────────────
            // If the trainer adds more exercises after clicking "Mark Done" tomorrow,
            // they won't bleed back into today's completed record.
            var exercises = program.ProgramExercises?.ToList() ?? new List<ProgramExercise>();
            string summary = string.Join(", ", exercises.Select(e => e.ExerciseName));
            int totalSeconds = exercises.Sum(e => e.DurationMinutes) * 60;

            var log = new WorkoutSessionLog
            {
                MemberId = program.MemberId,
                WorkoutProgramId = workoutProgramId,
                TrainerId = trainerId,
                Status = "Completed",
                SessionDate = DateTime.Today,
                ActualDurationSeconds = totalSeconds,
                ExercisesSummary = summary,   // frozen snapshot of today's exercises
                Notes = "Marked done by trainer.",
                LoggedAt = DateTime.Now
            };

            db.WorkoutSessionLogs.Add(log);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                logId = log.WorkoutSessionLogId,
                message = "Day marked as done and saved to Progress Tracking!"
            });
        }

        // ============================================================
        // WORKOUT PROGRAMS — DELETE EXERCISE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteExercise(int exerciseId, int programId)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");

            var exercise = db.ProgramExercises.Find(exerciseId);
            if (exercise != null)
            {
                db.ProgramExercises.Remove(exercise);
                db.SaveChanges();
            }
            return RedirectToAction("AddExercises", new { programId });
        }

        // ============================================================
        // WORKOUT PROGRAMS — GET MEMBER FITNESS PROFILE (AJAX)
        // ============================================================
        public ActionResult GetMemberFitnessProfile(int memberId)
        {
            var profile = db.FitnessProfiles.FirstOrDefault(f => f.MemberId == memberId);
            if (profile == null)
                return Json(new { found = false }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                found = true,
                goalType = profile.GoalType,
                bmi = profile.BMI,
                weightKg = profile.WeightKg,
                bodyFat = profile.BodyFatPercent,
                activityLevel = profile.ActivityLevel
            }, JsonRequestBehavior.AllowGet);
        }



        // ============================================================
        // MEMBER PROGRESS — LOG (GET)
        // ============================================================
        public ActionResult LogProgress(int? memberId)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Select(mt => mt.MemberId).ToList();

            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName", memberId);

            return View(new MemberProgress
            {
                TrainerId = trainerId,
                MemberId = memberId ?? 0,
                RecordedDate = DateTime.Today
            });
        }

        // ============================================================
        // MEMBER PROGRESS — LOG (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogProgress(MemberProgress model)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            model.TrainerId = trainer.TrainerId;

            if (ModelState.IsValid)
            {
                db.MemberProgresses.Add(model);
                db.SaveChanges();
                TempData["Success"] = "Progress logged successfully.";
                return RedirectToAction("MemberProgress", new { memberId = model.MemberId });
            }

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainer.TrainerId)
                .Select(mt => mt.MemberId).ToList();
            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName", model.MemberId);
            return View(model);
        }



        // ────────────────────────────────────────────────────────────
        // 4.  REPLACE the existing MemberProgress GET action
        // ────────────────────────────────────────────────────────────

        public ActionResult MemberProgress(int? memberId)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");
            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            ViewBag.TrainerName = trainer.FullName;
            ViewBag.TrainerRole = trainer.Role;
            ViewBag.TrainerCode = trainer.TrainerCode;

            var memberIds = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Select(mt => mt.MemberId).ToList();

            ViewBag.Members = new SelectList(
                db.Members.Where(m => memberIds.Contains(m.MemberId)).ToList(),
                "MemberId", "FullName", memberId);

            ViewBag.SelectedMemberId = memberId;

            List<WorkoutSessionLog> sessionLogs = new List<WorkoutSessionLog>();
            List<DailyProgressNote> progressNotes = new List<DailyProgressNote>();

            if (memberId.HasValue)
            {
                sessionLogs = db.WorkoutSessionLogs
                    .Where(l => l.MemberId == memberId.Value && l.TrainerId == trainerId)
                    .Include(l => l.MemberForm)
                    .Include(l => l.WorkoutProgram)
                    .OrderBy(l => l.SessionDate)
                    .ToList();

                var logIds = sessionLogs.Select(l => l.WorkoutSessionLogId).ToList();
                progressNotes = db.DailyProgressNotes
                    .Where(n => logIds.Contains(n.WorkoutSessionLogId))
                    .ToList();
            }

            ViewBag.SessionLogs = sessionLogs;
            ViewBag.ProgressNotes = progressNotes;

            // Keep existing MemberProgress model for any legacy usage
            var query = db.MemberProgresses
                .Where(p => p.TrainerId == trainerId)
                .Include(p => p.MemberForm)
                .AsQueryable();

            if (memberId.HasValue)
                query = query.Where(p => p.MemberId == memberId.Value);

            var logs = query.OrderByDescending(p => p.RecordedDate).ToList();
            return View(logs);
        }

        

        // ────────────────────────────────────────────────────────────
        // 6.  NEW ACTION — Trainer saves a daily description (locks on save)
        // ────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveProgressNote(int workoutSessionLogId, string description)
        {
            if (!IsTrainerLoggedIn())
                return Json(new { success = false, message = "Unauthorized" });

            var trainer = GetLoggedInTrainer();

            if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < 5)
                return Json(new { success = false, message = "Description must be at least 5 characters." });

            var log = db.WorkoutSessionLogs.Find(workoutSessionLogId);
            if (log == null || log.TrainerId != trainer.TrainerId)
                return Json(new { success = false, message = "Session log not found." });

            // Check if a note already exists — if so, it's locked
            bool exists = db.DailyProgressNotes.Any(n => n.WorkoutSessionLogId == workoutSessionLogId);
            if (exists)
                return Json(new { success = false, message = "This description has already been saved and is locked." });

            db.DailyProgressNotes.Add(new DailyProgressNote
            {
                WorkoutSessionLogId = workoutSessionLogId,
                MemberId = log.MemberId,
                TrainerId = trainer.TrainerId,
                TrainerDescription = description.Trim(),
                SavedAt = DateTime.Now
            });

            db.SaveChanges();

            return Json(new { success = true, message = "Description saved and locked." });
        }

        // ────────────────────────────────────────────────────────────
        // 7.  NEW ACTION — Auto-save at midnight (call via a scheduled
        //     task / Hangfire / Windows Task Scheduler hitting this URL,
        //     or call it from Application_Start with a timer).
        //     Route: GET /Trainer/AutoSaveMidnight?secret=YOUR_SECRET
        // ────────────────────────────────────────────────────────────

        public ActionResult AutoSaveMidnight(string secret)
        {
            // Simple shared-secret guard — set "AutoSaveSecret" in Web.config AppSettings
            string expectedSecret = System.Configuration.ConfigurationManager.AppSettings["AutoSaveSecret"];
            if (string.IsNullOrWhiteSpace(expectedSecret) || secret != expectedSecret)
                return new HttpStatusCodeResult(403, "Forbidden");

            var yesterday = DateTime.Today.AddDays(-1);

            // Find all active workout programs that had no session log for yesterday
            var activePrograms = db.WorkoutPrograms
                .Include(w => w.ProgramExercises)
                .Where(w => w.StartDate <= yesterday && yesterday < System.Data.Entity.DbFunctions.AddDays(w.StartDate, 7))
                .ToList();

            int autoSaved = 0;

            foreach (var program in activePrograms)
            {
                bool alreadyLogged = db.WorkoutSessionLogs.Any(l =>
                    l.WorkoutProgramId == program.WorkoutProgramId &&
                    l.SessionDate == yesterday);

                if (alreadyLogged) continue;

                // Also skip if member was marked absent (that's already a log)
                // — the above check covers both Completed and Absent since both create logs

                var exercises = program.ProgramExercises?.ToList() ?? new List<ProgramExercise>();
                string summary = string.Join(", ", exercises.Select(e => e.ExerciseName));
                int totalSeconds = exercises.Sum(e => e.DurationMinutes) * 60;

                db.WorkoutSessionLogs.Add(new WorkoutSessionLog
                {
                    MemberId = program.MemberId,
                    WorkoutProgramId = program.WorkoutProgramId,
                    TrainerId = program.TrainerId,
                    Status = "Completed",
                    SessionDate = yesterday,
                    ActualDurationSeconds = totalSeconds,
                    ExercisesSummary = summary,
                    Notes = "[Auto] Auto-saved at midnight — trainer did not mark done.",
                    LoggedAt = DateTime.Now
                });

                autoSaved++;
            }

            if (autoSaved > 0)
                db.SaveChanges();

            return Content($"Auto-save complete. {autoSaved} session(s) auto-logged for {yesterday:dd MMM yyyy}.");
        }

        // ────────────────────────────────────────────────────────────
        // 8.  ADMIN: GET endpoint to view all member progress notes
        //     Add this to your AdminController (or wherever admins live)
        // ────────────────────────────────────────────────────────────


            public ActionResult MemberProgressAdmin(int? memberId)
            {
                 // (admin auth check here)
 
                var allMembers = db.Members.OrderBy(m => m.FullName).ToList();
                ViewBag.Members = new SelectList(allMembers, "MemberId", "FullName", memberId);
                ViewBag.SelectedMemberId = memberId;
 
                List<WorkoutSessionLog> sessionLogs = new List<WorkoutSessionLog>();
                 List<DailyProgressNote> progressNotes = new List<DailyProgressNote>();
 
                if (memberId.HasValue)
                {
                     sessionLogs = db.WorkoutSessionLogs
                    .Where(l => l.MemberId == memberId.Value)
                    .Include(l => l.MemberForm)
                    .Include(l => l.WorkoutProgram)
                    .OrderBy(l => l.SessionDate)
                    .ToList();
 
                     var logIds = sessionLogs.Select(l => l.WorkoutSessionLogId).ToList();
                     progressNotes = db.DailyProgressNotes
                    .Where(n => logIds.Contains(n.WorkoutSessionLogId))
                    .ToList();
                }
 
                ViewBag.SessionLogs = sessionLogs;
                ViewBag.ProgressNotes = progressNotes;
 
                return View("MemberProgressAdmin"); // create a view similar to MemberProgress.cshtml
            }



        // ============================================================
        // CHANGE PASSWORD — GET
        // ============================================================
        public ActionResult ChangePassword()
        {
            if (!IsTrainerLoggedIn())
                return RedirectToAction("Login", "Admin");

            return View(new ChangePasswordViewModel());
        }

        // ============================================================
        // CHANGE PASSWORD — POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!IsTrainerLoggedIn())
                return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            var trainer = GetLoggedInTrainer();
            if (trainer == null)
                return RedirectToAction("Login", "Admin");

            string hashedCurrent = HashPassword(model.CurrentPassword);
            if (trainer.PasswordHash != hashedCurrent)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            string hashedNew = HashPassword(model.NewPassword);
            if (hashedNew == trainer.PasswordHash)
            {
                ModelState.AddModelError("NewPassword", "New password must be different from your current password.");
                return View(model);
            }

            trainer.PasswordHash = hashedNew;
            trainer.MustChangePassword = false;
            db.SaveChanges();

            TempData["Success"] = "Password changed successfully. Welcome to GymHub!";
            return RedirectToAction("Dashboard");
        }

        // ============================================================
        // LOGOUT
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Remove("LoggedInTrainer");
            Session.Remove("LoggedInTrainerName");
            Session.Remove("LoggedInTrainerCode");
            Session.Remove("LoggedInTrainerRole");
            return RedirectToAction("Index", "Home");
        }

        // ============================================================
        // HELPER
        // ============================================================
        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var sb = new System.Text.StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public ActionResult MemberWeeklyProgress(int? memberId)
        {
            if (!IsTrainerLoggedIn()) return RedirectToAction("Login", "Admin");

            var trainer = GetLoggedInTrainer();
            int trainerId = trainer.TrainerId;

            ViewBag.TrainerName = trainer.FullName;
            ViewBag.TrainerRole = trainer.Role;
            ViewBag.TrainerCode = trainer.TrainerCode;

            // Only show members assigned to THIS trainer
            var myMembers = db.MemberTrainers
                .Where(mt => mt.TrainerId == trainerId)
                .Include(mt => mt.Member)
                .OrderBy(mt => mt.Member.FirstName)
                .ToList();

            ViewBag.Members = new SelectList(
                myMembers.Select(mt => new {
                    MemberId = mt.MemberId,
                    Name = mt.Member.FirstName + " " + mt.Member.LastName
                }),
                "MemberId", "Name", memberId);

            ViewBag.SelectedMemberId = memberId;

            List<WeeklyProgressLog> logs = new List<WeeklyProgressLog>();
            MemberForm selectedMember = null;
            FitnessProfile fitnessProfile = null;

            if (memberId.HasValue)
            {
                // Security: confirm this member belongs to the logged-in trainer
                bool isAssigned = myMembers.Any(mt => mt.MemberId == memberId.Value);
                if (!isAssigned) return new HttpStatusCodeResult(403, "Forbidden");

                selectedMember = db.Members.Find(memberId.Value);
                fitnessProfile = db.FitnessProfiles
                    .FirstOrDefault(f => f.MemberId == memberId.Value);

                logs = db.WeeklyProgressLogs
                    .Where(l => l.MemberId == memberId.Value)
                    .OrderBy(l => l.WeekNumber)
                    .ToList();
            }

            ViewBag.SelectedMember = selectedMember;
            ViewBag.FitnessProfile = fitnessProfile;

            return View(logs);
        }



        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

    }
}