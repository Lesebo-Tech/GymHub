using Newtonsoft.Json;
using SwiftyS26.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace SwiftyS26.Controllers
{
    public class WorkoutController : Controller
    {
        private readonly GymHubDB db = new GymHubDB();

        // ── AUTH HELPERS ──────────────────────────────────────────────
        private int GetCurrentMemberId()
        {
            var id = Session["LoggedInMember"];
            return id != null ? (int)id : 0;
        }

        private bool IsLoggedIn()
        {
            return Session["LoggedInMember"] != null;
        }

        // ── GET: /Workout ─────────────────────────────────────────────
        public ActionResult Index()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            int memberId = GetCurrentMemberId();

            var categories = db.WorkoutCategories
                .Include(c => c.WorkoutPlans)
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            var recentSessions = db.WorkoutSessions
                .Include(s => s.Plan.Category)
                .Where(s => s.MemberId == memberId)
                .OrderByDescending(s => s.StartedAt)
                .Take(5)
                .ToList();

            int totalWorkouts = db.WorkoutSessions
                .Count(s => s.MemberId == memberId && s.Status == "Completed");

            int streak = CalculateStreak(memberId);

            var badges = db.UserBadges
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.EarnedAt)
                .Take(6)
                .ToList();

            ViewBag.Categories = categories;
            ViewBag.RecentSessions = recentSessions;
            ViewBag.TotalWorkouts = totalWorkouts;
            ViewBag.Streak = streak;
            ViewBag.Badges = badges;

            return View();
        }

        // ── GET: /Workout/Category/{slug} ─────────────────────────────
        public ActionResult Category(string slug)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            var category = db.WorkoutCategories
                .FirstOrDefault(c => c.Slug == slug && c.IsActive);

            if (category == null)
                return HttpNotFound();

            var plans = db.WorkoutPlans
                .Where(p => p.CategoryId == category.CategoryId && p.IsActive)
                .ToList();

            ViewBag.Category = category;
            ViewBag.Plans = plans;

            return View();
        }

        // ── GET: /Workout/Start/{planId} ──────────────────────────────
        public ActionResult Start(int planId)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            var plan = db.WorkoutPlans
                .Include(p => p.Category)
                .Include(p => p.PlanExercises.Select(pe => pe.Exercise))
                .FirstOrDefault(p => p.PlanId == planId && p.IsActive);

            if (plan == null)
                return HttpNotFound();

            var exercises = plan.PlanExercises
                .OrderBy(pe => pe.OrderIndex)
                .Select(pe => new
                {
                    planExerciseId = pe.PlanExerciseId,
                    exerciseId = pe.ExerciseId,
                    orderIndex = pe.OrderIndex,
                    name = pe.Exercise.Name,
                    instructions = pe.Exercise.Instructions,
                    muscleGroup = pe.Exercise.MuscleGroup,
                    mediaUrl = pe.Exercise.MediaUrl,
                    mediaType = pe.Exercise.MediaType,
                    reps = pe.Reps,
                    sets = pe.Sets,
                    durationSeconds = pe.DurationSeconds,
                    restSeconds = pe.RestSeconds,
                    notes = pe.Notes,
                    isTimeBased = pe.DurationSeconds.HasValue && pe.DurationSeconds.Value > 0
                })
                .ToList();

            ViewBag.PlanId = plan.PlanId;
            ViewBag.PlanName = plan.Name;
            ViewBag.PlanDescription = plan.Description;
            ViewBag.PlanDifficulty = plan.Difficulty;
            ViewBag.PlanMinutes = plan.EstimatedMinutes;
            ViewBag.CategoryName = plan.Category != null ? plan.Category.Name : "";
            ViewBag.CategoryIconClass = plan.Category != null ? plan.Category.IconClass : "";
            ViewBag.CategoryColor = plan.Category != null ? plan.Category.ColorAccent : "#3B82F6";
            ViewBag.ExercisesJson = JsonConvert.SerializeObject(exercises);
            ViewBag.MemberId = GetCurrentMemberId();

            return View();
        }

        // ── POST: /Workout/SaveSession ────────────────────────────────
        //
        // NO parameter on this method — this is intentional and critical.
        //
        // When the JS sends Content-Type: text/plain, MVC5 skips all value
        // providers and leaves Request.InputStream untouched. We read it
        // manually with StreamReader and deserialize with Newtonsoft.Json.
        //
        // If you add (SaveSessionDto dto) back as a parameter, MVC5's binding
        // pipeline will attempt to read the stream and leave it at EOF — your
        // StreamReader then gets an empty string and dto comes back null.
        //
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SaveSession()
        {
            if (!IsLoggedIn())
                return Json(new { success = false, message = "Session expired. Please log in again." });

            int memberId = GetCurrentMemberId();

            // ── Read raw JSON body ────────────────────────────────────
            SaveSessionDto dto;
            try
            {
                Request.InputStream.Seek(0, SeekOrigin.Begin);
                string body = new StreamReader(Request.InputStream).ReadToEnd();

                System.Diagnostics.Debug.WriteLine("[SaveSession] body: " + body);

                if (string.IsNullOrWhiteSpace(body))
                    return Json(new { success = false, message = "Empty request body." });

                dto = JsonConvert.DeserializeObject<SaveSessionDto>(body);

                if (dto == null)
                    return Json(new { success = false, message = "Could not parse session data." });

                if (dto.PlanId == 0)
                    return Json(new { success = false, message = "PlanId missing." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SaveSession] parse error: " + ex.Message);
                return Json(new { success = false, message = "Parse error: " + ex.Message });
            }

            // ── Save to database ──────────────────────────────────────
            try
            {
                var session = new WorkoutSession
                {
                    MemberId = memberId,
                    PlanId = dto.PlanId,
                    StartedAt = DateTime.Now.AddSeconds(-dto.TotalDurationSeconds),
                    CompletedAt = dto.Status == "Completed" ? DateTime.Now : (DateTime?)null,
                    Status = dto.Status,
                    TotalDurationSeconds = dto.TotalDurationSeconds,
                    ExercisesCompleted = dto.Exercises != null ? dto.Exercises.Count(e => !e.Skipped) : 0,
                    CaloriesBurned = EstimateCalories(dto.TotalDurationSeconds)
                };

                db.WorkoutSessions.Add(session);
                db.SaveChanges();  // session.SessionId is now populated

                System.Diagnostics.Debug.WriteLine("[SaveSession] saved SessionId=" + session.SessionId);

                if (dto.Exercises != null && dto.Exercises.Any())
                {
                    foreach (var ex in dto.Exercises)
                    {
                        db.SessionExerciseLogs.Add(new SessionExerciseLog
                        {
                            SessionId = session.SessionId,
                            ExerciseId = ex.ExerciseId,
                            OrderIndex = ex.OrderIndex,
                            RepsCompleted = ex.RepsCompleted,
                            SetsCompleted = ex.SetsCompleted,
                            DurationSeconds = ex.DurationSeconds,
                            Skipped = ex.Skipped,
                            LoggedAt = DateTime.Now
                        });
                    }
                    db.SaveChanges();
                }

                var newBadges = new List<UserBadge>();
                if (dto.Status == "Completed")
                    newBadges = AwardBadges(memberId);

                return Json(new
                {
                    success = true,
                    sessionId = session.SessionId,
                    badges = newBadges.Select(b => new
                    {
                        badgeName = b.BadgeName,
                        iconClass = b.IconClass,
                        description = b.Description
                    })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SaveSession] db error: " + ex.Message + "\n" + ex.StackTrace);
                return Json(new { success = false, message = "Database error: " + ex.Message });
            }
        }

        // ── GET: /Workout/Summary ─────────────────────────────────────
        public ActionResult Summary(int sessionId)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            int memberId = GetCurrentMemberId();

            var session = db.WorkoutSessions
                .FirstOrDefault(s => s.SessionId == sessionId && s.MemberId == memberId);

            if (session == null)
                return HttpNotFound();

            var plan = db.WorkoutPlans.Find(session.PlanId);
            var category = plan != null ? db.WorkoutCategories.Find(plan.CategoryId) : null;

            var logs = db.SessionExerciseLogs
                .Where(l => l.SessionId == sessionId)
                .OrderBy(l => l.OrderIndex)
                .ToList();

            foreach (var log in logs)
                log.Exercise = db.Exercises.Find(log.ExerciseId);

            int totalWorkouts = db.WorkoutSessions
                .Count(s => s.MemberId == memberId && s.Status == "Completed");

            int streak = CalculateStreak(memberId);

            var badges = db.UserBadges
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.EarnedAt)
                .Take(6)
                .ToList();

            ViewBag.PlanName = plan != null ? plan.Name : "Workout";
            ViewBag.PlanId = session.PlanId;
            ViewBag.CategoryName = category != null ? category.Name : "";
            ViewBag.CategoryColor = category != null ? category.ColorAccent : "#3B82F6";

            var vm = new WorkoutSummaryVm
            {
                Session = session,
                Logs = logs,
                NewBadges = badges,
                TotalWorkouts = totalWorkouts,
                CurrentStreak = streak
            };

            return View(vm);
        }

        // ── GET: /Workout/History ─────────────────────────────────────
        public ActionResult History(int page = 1)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login", "Forms");

            int memberId = GetCurrentMemberId();
            const int pageSize = 10;

            var allSessions = db.WorkoutSessions
                .Where(s => s.MemberId == memberId)
                .OrderByDescending(s => s.StartedAt)
                .ToList();

            int total = allSessions.Count;
            var sessions = allSessions
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var sessionRows = sessions.Select(s =>
            {
                var plan = db.WorkoutPlans.Find(s.PlanId);
                var category = plan != null ? db.WorkoutCategories.Find(plan.CategoryId) : null;
                return new SessionRowVm
                {
                    SessionId = s.SessionId,
                    PlanName = plan != null ? plan.Name : "—",
                    CategoryName = category != null ? category.Name : "—",
                    CategoryColor = category != null ? category.ColorAccent : "#CBD5E1",
                    StartedAt = s.StartedAt.ToString("dd MMM yyyy"),
                    DurationSecs = s.TotalDurationSeconds,
                    Status = s.Status
                };
            }).ToList();

            var chartSessions = db.WorkoutSessions
                .Where(s => s.MemberId == memberId && s.Status == "Completed")
                .OrderByDescending(s => s.StartedAt)
                .Take(7)
                .ToList()
                .OrderBy(s => s.StartedAt)
                .Select(s => new
                {
                    date = s.StartedAt.ToString("dd MMM"),
                    duration = s.TotalDurationSeconds / 60
                })
                .ToList();

            int totalCompleted = db.WorkoutSessions
                .Count(s => s.MemberId == memberId && s.Status == "Completed");

            int totalMinutes = db.WorkoutSessions
                .Where(s => s.MemberId == memberId && s.Status == "Completed")
                .ToList()
                .Sum(s => s.TotalDurationSeconds / 60);

            int streak = CalculateStreak(memberId);

            ViewBag.SessionRows = sessionRows;
            ViewBag.TotalSessions = total;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.ChartDataJson = JsonConvert.SerializeObject(chartSessions);
            ViewBag.TotalCompleted = totalCompleted;
            ViewBag.TotalMinutes = totalMinutes;
            ViewBag.Streak = streak;

            return View();
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────

        private static int EstimateCalories(int durationSeconds)
        {
            return (int)(durationSeconds / 60.0 * 8);
        }

        private int CalculateStreak(int memberId)
        {
            var dates = db.WorkoutSessions
                .Where(s => s.MemberId == memberId && s.Status == "Completed")
                .OrderByDescending(s => s.StartedAt)
                .ToList()
                .Select(s => s.StartedAt.Date)
                .Distinct()
                .ToList();

            if (!dates.Any()) return 0;

            int streak = 0;
            DateTime check = DateTime.Today;

            foreach (var d in dates)
            {
                if (d == check || d == check.AddDays(-1))
                {
                    streak++;
                    check = d;
                }
                else break;
            }

            return streak;
        }

        private List<UserBadge> AwardBadges(int memberId)
        {
            var awarded = new List<UserBadge>();
            var existing = db.UserBadges
                .Where(b => b.MemberId == memberId)
                .Select(b => b.BadgeType)
                .ToList();

            int totalDone = db.WorkoutSessions
                .Count(s => s.MemberId == memberId && s.Status == "Completed");

            int streak = CalculateStreak(memberId);

            var toAward = new List<Tuple<string, string, string, string>>();

            if (totalDone == 1 && !existing.Contains("FirstWorkout"))
                toAward.Add(Tuple.Create("FirstWorkout", "First Sweat", "Completed your very first workout!", "fas fa-fire"));
            if (totalDone >= 5 && !existing.Contains("Five"))
                toAward.Add(Tuple.Create("Five", "High Five", "Completed 5 workouts!", "fas fa-hand-peace"));
            if (totalDone >= 10 && !existing.Contains("Ten"))
                toAward.Add(Tuple.Create("Ten", "Perfect 10", "10 workouts done. You are on fire!", "fas fa-medal"));
            if (totalDone >= 25 && !existing.Contains("TwentyFive"))
                toAward.Add(Tuple.Create("TwentyFive", "Quarter Century", "25 workouts completed!", "fas fa-trophy"));
            if (streak >= 3 && !existing.Contains("Streak3"))
                toAward.Add(Tuple.Create("Streak3", "On a Roll", "3-day workout streak!", "fas fa-bolt"));
            if (streak >= 7 && !existing.Contains("Streak7"))
                toAward.Add(Tuple.Create("Streak7", "Week Warrior", "7-day streak! Incredible!", "fas fa-crown"));

            foreach (var item in toAward)
            {
                var badge = new UserBadge
                {
                    MemberId = memberId,
                    BadgeType = item.Item1,
                    BadgeName = item.Item2,
                    Description = item.Item3,
                    IconClass = item.Item4,
                    EarnedAt = DateTime.Now
                };
                db.UserBadges.Add(badge);
                awarded.Add(badge);
            }

            if (awarded.Any())
            {
                db.SaveChanges();

                foreach (var badge in awarded)
                {
                    try
                    {
                        new NotificationService(db).Notify(
                            "Member", memberId,
                            "Badge Earned!",
                            $"{badge.BadgeName} — {badge.Description}",
                            "Success",
                            "/Workout/History");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NOTIFY] {ex.Message}"); }
                }
            }

            return awarded;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}