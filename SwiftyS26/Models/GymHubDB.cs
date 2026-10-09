
using Org.BouncyCastle.Asn1;
using System.Data.Entity;

namespace SwiftyS26.Models
{
    public class GymHubDB : DbContext
    {
        public GymHubDB() : base("GymHubDB") { }

        // ── EXISTING TABLES ───────────────────────────────────────────
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MemberForm> Members { get; set; }
        public DbSet<RefundRequest> RefundRequests { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<FreezeRequest> FreezeRequests { get; set; }
        public DbSet<LoyaltyAccount> LoyaltyAccounts { get; set; }
        public DbSet<PointsTransaction> PointsTransactions { get; set; }
        public DbSet<EWallet> EWallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<FitnessProfile> FitnessProfiles { get; set; }
        public DbSet<FitnessProgressLog> FitnessProgressLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        // ── UC14: Café ────────────────────────────────────────────────
        public DbSet<CafeProduct> CafeProducts { get; set; }
        public DbSet<CafeRedemption> CafeRedemptions { get; set; }

        // ── WORKOUT FEATURE ───────────────────────────────────────────
        public DbSet<WorkoutCategory> WorkoutCategories { get; set; }
        public DbSet<WorkoutPlan> WorkoutPlans { get; set; }
        public DbSet<Exercise> Exercises { get; set; }
        public DbSet<WorkoutPlanExercise> WorkoutPlanExercises { get; set; }
        public DbSet<WorkoutSession> WorkoutSessions { get; set; }
        public DbSet<SessionExerciseLog> SessionExerciseLogs { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }

        // ── TRAINER FEATURE ───────────────────────────────────────────
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<MemberTrainer> MemberTrainers { get; set; }
        public DbSet<WorkoutProgram> WorkoutPrograms { get; set; }
        public DbSet<ProgramExercise> ProgramExercises { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<MemberProgress> MemberProgresses { get; set; }
        public DbSet<TrainerAvailability> TrainerAvailabilities { get; set; }
        public DbSet<DailyProgressNote> DailyProgressNotes { get; set; }
        public DbSet<WeeklyProgressLog> WeeklyProgressLogs { get; set; }

        // ── ✅ NEW: Workout Session Logs (member start/complete/absent) ─
        public DbSet<WorkoutSessionLog> WorkoutSessionLogs { get; set; }

        // ── TECHNICIAN (Equipment Management) ─────────────────────────
        public DbSet<Technician> Technicians { get; set; }

        // ── EQUIPMENT MANAGEMENT ───────────────────────────────────────
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<Inspection> Inspections { get; set; }
        public DbSet<RepairRequest> RepairRequests { get; set; }
        public DbSet<Repair> Repairs { get; set; }
        // ── COMPLAINTS FEATURE ────────────────────────────────────────
        public DbSet<ComplaintTicket> ComplaintTickets { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        // ── REFERRAL FEATURE ────────────────────────────────────────────
        public DbSet<Referral> Referrals { get; set; }

        // ── SMART LOCKER RESERVATION ─────────────────────────────────────
        public DbSet<Locker> Lockers { get; set; }
        public DbSet<LockerReservation> LockerReservations { get; set; }
        public DbSet<RepairAssignment> RepairAssignments { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Maintenance> Maintenances { get; set; }

        public DbSet<LockerFault> LockerFaults { get; set; }
        public DbSet<LockerFaultReport> LockerFaultReports { get; set; }
        public DbSet<LockerAccessOverride> LockerAccessOverrides { get; set; }
        // ── ADD these DbSets to GymHubDB.cs ────────────────────────────
        public DbSet<Product> Products { get; set; }
        public DbSet<StoreStaff> StoreStaffs { get; set; }
        public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
        public DbSet<StoreOrder> StoreOrders { get; set; }
        public DbSet<StoreOrderItem> StoreOrderItems { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<SupplierDelivery> SupplierDeliveries { get; set; }
        public DbSet<DeliveryItem> DeliveryItems { get; set; }
        public DbSet<SupplierReturn> SupplierReturns { get; set; }
        public DbSet<Promotion> Promotions { get; set; }

        public DbSet<Food> Foods { get; set; }

        public DbSet<FoodPortion> FoodPortions { get; set; }

        public DbSet<MealLog> MealLogs { get; set; }

        public DbSet<MealLogItem> MealLogItems { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<MealPlan> MealPlans { get; set; }
        public DbSet<MealPlanItem> MealPlanItems { get; set; }
        public DbSet<StoreOrderCollectionEvent> StoreOrderCollectionEvents { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Existing constraints ──────────────────────────────────
            modelBuilder.Entity<WorkoutPlanExercise>()
                .HasIndex(x => new { x.PlanId, x.OrderIndex })
                .IsUnique();

            modelBuilder.Entity<WorkoutSession>()
                .HasRequired(s => s.Member)
                .WithMany()
                .HasForeignKey(s => s.MemberId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<WorkoutSession>()
                .HasRequired(s => s.Plan)
                .WithMany()
                .HasForeignKey(s => s.PlanId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SessionExerciseLog>()
                .HasRequired(l => l.Session)
                .WithMany(s => s.ExerciseLogs)
                .HasForeignKey(l => l.SessionId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<SessionExerciseLog>()
                .HasRequired(l => l.Exercise)
                .WithMany()
                .HasForeignKey(l => l.ExerciseId)
                .WillCascadeOnDelete(false);

            // ── ✅ NEW: WorkoutSessionLog cascade config ───────────────
            modelBuilder.Entity<WorkoutSessionLog>()
                .HasRequired(l => l.MemberForm)
                .WithMany()
                .HasForeignKey(l => l.MemberId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<WorkoutSessionLog>()
                .HasRequired(l => l.WorkoutProgram)
                .WithMany()
                .HasForeignKey(l => l.WorkoutProgramId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<WorkoutSessionLog>()
                .HasRequired(l => l.Trainer)
                .WithMany()
                .HasForeignKey(l => l.TrainerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<WeeklyProgressLog>()
                .Property(l => l.WeightKg)
                .HasPrecision(5, 2);

            modelBuilder.Entity<WeeklyProgressLog>()
                .Property(l => l.BodyFatPercent)
                .HasPrecision(5, 2);
            // ── Complaints: cascade config ────────────────────────────────
            modelBuilder.Entity<ComplaintTicket>()
                .HasRequired(c => c.Member)
                .WithMany()
                .HasForeignKey(c => c.MemberId)
                .WillCascadeOnDelete(false);
            // ── Feedback: cascade config ───────────────────────────────────
            modelBuilder.Entity<Feedback>()
                .HasRequired(f => f.Member)
                .WithMany()
                .HasForeignKey(f => f.MemberId)
                .WillCascadeOnDelete(false);
            // ── Referral: cascade config (two FKs to MemberForm — both must be non-cascading) ──
            modelBuilder.Entity<Referral>()
                .HasRequired(r => r.Referrer)
                .WithMany()
                .HasForeignKey(r => r.ReferrerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Referral>()
                .HasRequired(r => r.ReferredMember)
                .WithMany()
                .HasForeignKey(r => r.ReferredMemberId)
                .WillCascadeOnDelete(false);
            // ── Smart Locker Reservation cascade config ───────────────────────
            modelBuilder.Entity<LockerReservation>()
                .HasRequired(r => r.Locker)
                .WithMany(l => l.Reservations)
                .HasForeignKey(r => r.LockerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LockerReservation>()
                .HasRequired(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .WillCascadeOnDelete(false);
            // Repair → Inspection (the failed inspection that originally created this repair)
            modelBuilder.Entity<Repair>()
                .HasOptional(r => r.Inspection)
                .WithMany()
                .HasForeignKey(r => r.InspectionId);

            // Inspection → Repair (the repair this post-repair inspection is verifying)
            modelBuilder.Entity<Inspection>()
                .HasOptional(i => i.Repair)
                .WithMany()
                .HasForeignKey(i => i.RepairId);

            // ── Locker faults: cascade config ─────────────────────────────
            modelBuilder.Entity<LockerFault>()
                .HasRequired(f => f.Locker)
                .WithMany(l => l.Faults)
                .HasForeignKey(f => f.LockerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LockerFaultReport>()
                .HasRequired(r => r.LockerFault)
                .WithMany(f => f.Reports)
                .HasForeignKey(r => r.LockerFaultId)
                .WillCascadeOnDelete(true);   // reports belong to their fault

            modelBuilder.Entity<LockerFaultReport>()
                .HasOptional(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .WillCascadeOnDelete(false);

            // ── Access overrides: audit records, never cascade-deleted ────
            modelBuilder.Entity<LockerAccessOverride>()
                .HasRequired(o => o.Locker)
                .WithMany()
                .HasForeignKey(o => o.LockerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LockerAccessOverride>()
                .HasRequired(o => o.Member)
                .WithMany()
                .HasForeignKey(o => o.MemberId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<LockerAccessOverride>()
                .HasRequired(o => o.LockerFault)
                .WithMany()
                .HasForeignKey(o => o.LockerFaultId)
                .WillCascadeOnDelete(false);
            // ── ADD to OnModelCreating ──────────────────────────────────────
            modelBuilder.Entity<ShoppingCartItem>()
                .HasRequired(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ShoppingCartItem>()
                .HasRequired(c => c.Member)
                .WithMany()
                .HasForeignKey(c => c.MemberId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<StoreOrderItem>()
                .HasRequired(i => i.StoreOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.StoreOrderId)
                .WillCascadeOnDelete(true); // items belong to their order

            modelBuilder.Entity<StoreOrderItem>()
                .HasRequired(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<StoreOrder>()
                .HasRequired(o => o.Member)
                .WithMany()
                .HasForeignKey(o => o.MemberId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<ReturnRequest>()
                .HasRequired(r => r.StoreOrderItem)
                .WithMany()
                .HasForeignKey(r => r.StoreOrderItemId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ReturnRequest>()
                .HasRequired(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MemberId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<PurchaseOrderItem>()
                .HasRequired(i => i.PurchaseOrder)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PurchaseOrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PurchaseOrderItem>()
                .HasRequired(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseOrder>()
                .HasRequired(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Supplier)
                .WithMany()
                .HasForeignKey(sp => sp.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierProduct>()
                .HasRequired(sp => sp.Product)
                .WithMany()
                .HasForeignKey(sp => sp.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierProduct>()
                .HasIndex(sp => new { sp.SupplierId, sp.ProductId })
                .IsUnique();
            modelBuilder.Entity<DeliveryItem>()
                .HasRequired(d => d.SupplierDelivery)
                .WithMany(s => s.Items)
                .HasForeignKey(d => d.SupplierDeliveryId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<DeliveryItem>()
                .HasRequired(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SupplierDelivery>()
                .HasRequired(s => s.PurchaseOrder)
                .WithMany()
                .HasForeignKey(s => s.PurchaseOrderId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<SupplierReturn>()
                .HasRequired(r => r.DeliveryItem)
                .WithMany()
                .HasForeignKey(r => r.DeliveryItemId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<Promotion>()
                .HasRequired(p => p.Product)
                .WithMany(prod => prod.Promotions)
                .HasForeignKey(p => p.ProductId)
                .WillCascadeOnDelete(true);
            // Food -> FoodPortions
            modelBuilder.Entity<Food>()
                .HasMany(f => f.Portions)
                .WithRequired(p => p.Food)
                .HasForeignKey(p => p.FoodId)
                .WillCascadeOnDelete(true);

            // Member -> MealLogs
            modelBuilder.Entity<MealLog>()
                .HasRequired(m => m.Member)
                .WithMany()
                .HasForeignKey(m => m.MemberId)
                .WillCascadeOnDelete(false);

            // MealLog -> MealLogItems
            modelBuilder.Entity<MealLog>()
                .HasMany(m => m.Items)
                .WithRequired(i => i.MealLog)
                .HasForeignKey(i => i.MealLogId)
                .WillCascadeOnDelete(true);

            // MealLogItem -> Food
            // FoodId is nullable, so the relationship is optional
            modelBuilder.Entity<MealLogItem>()
                .HasOptional(i => i.Food)
                .WithMany()
                .HasForeignKey(i => i.FoodId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MealPlanItem>()
                .HasRequired(i => i.MealPlan)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.MealPlanId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<MealPlanItem>()
                .HasRequired(i => i.Recipe)
                .WithMany()
                .HasForeignKey(i => i.RecipeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MealPlan>()
                .HasRequired(p => p.Member)
                .WithMany()
                .HasForeignKey(p => p.MemberId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<StoreOrderCollectionEvent>()
    .HasRequired(e => e.StoreOrder)
    .WithMany(o => o.CollectionEvents)
    .HasForeignKey(e => e.StoreOrderId)
    .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryMovement>()
                .HasRequired(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .WillCascadeOnDelete(false);

        }
    }
}
