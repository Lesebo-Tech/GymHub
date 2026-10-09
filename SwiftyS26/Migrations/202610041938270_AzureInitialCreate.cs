namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AzureInitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Attendances",
                c => new
                    {
                        AttendanceId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        MemberNumber = c.String(maxLength: 20),
                        MemberName = c.String(maxLength: 100),
                        Plan = c.String(maxLength: 20),
                        CheckInTime = c.DateTime(nullable: false),
                        CheckOutTime = c.DateTime(),
                        IpAddress = c.String(maxLength: 45),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.AttendanceId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.MemberForms",
                c => new
                    {
                        MemberId = c.Int(nullable: false, identity: true),
                        MemberNumber = c.String(maxLength: 20),
                        QrCodeBase64 = c.String(),
                        FirstName = c.String(nullable: false, maxLength: 50),
                        LastName = c.String(nullable: false, maxLength: 50),
                        IdNumber = c.String(nullable: false, maxLength: 13),
                        DateOfBirth = c.DateTime(nullable: false),
                        Gender = c.String(nullable: false, maxLength: 20),
                        Phone = c.String(nullable: false, maxLength: 15),
                        Email = c.String(nullable: false, maxLength: 100),
                        PasswordHash = c.String(maxLength: 255),
                        OtpCode = c.String(maxLength: 6),
                        OtpGeneratedAt = c.DateTime(),
                        IsEmailVerified = c.Boolean(nullable: false),
                        AcceptedTerms = c.Boolean(nullable: false),
                        AcceptedAgeConfirmation = c.Boolean(nullable: false),
                        AcceptedPrivacyPolicy = c.Boolean(nullable: false),
                        TermsAcceptedAt = c.DateTime(),
                        Plan = c.String(nullable: false, maxLength: 20),
                        MonthlyFee = c.Decimal(nullable: false, precision: 18, scale: 2),
                        RegisteredAt = c.DateTime(nullable: false),
                        Status = c.String(maxLength: 20),
                        IpAddress = c.String(maxLength: 45),
                        FailedLoginAttempts = c.Int(nullable: false),
                        LockoutEnd = c.DateTime(),
                        LastLoginAt = c.DateTime(),
                        MembershipExpiry = c.DateTime(),
                        ReferralCode = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.MemberId);
            
            CreateTable(
                "dbo.CafeProducts",
                c => new
                    {
                        CafeProductId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 300),
                        Emoji = c.String(maxLength: 10),
                        ImagePath = c.String(maxLength: 200),
                        Category = c.String(nullable: false, maxLength: 50),
                        PointsCost = c.Int(nullable: false),
                        RandValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsActive = c.Boolean(nullable: false),
                        InStock = c.Boolean(nullable: false),
                        DailyRedemptionLimit = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.CafeProductId);
            
            CreateTable(
                "dbo.CafeRedemptions",
                c => new
                    {
                        CafeRedemptionId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        CafeProductId = c.Int(nullable: false),
                        PointsSpent = c.Int(nullable: false),
                        RandValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CollectionCode = c.String(maxLength: 10),
                        Status = c.String(maxLength: 20),
                        RedeemedAt = c.DateTime(nullable: false),
                        CollectedAt = c.DateTime(),
                        Notes = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.CafeRedemptionId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.CafeProducts", t => t.CafeProductId, cascadeDelete: true)
                .Index(t => t.MemberId)
                .Index(t => t.CafeProductId);
            
            CreateTable(
                "dbo.ComplaintTickets",
                c => new
                    {
                        ComplaintId = c.Int(nullable: false, identity: true),
                        ReferenceNumber = c.String(nullable: false, maxLength: 20),
                        MemberId = c.Int(nullable: false),
                        Category = c.String(nullable: false, maxLength: 30),
                        Details = c.String(nullable: false, maxLength: 2000),
                        EvidenceFileName = c.String(maxLength: 260),
                        EvidenceFilePath = c.String(maxLength: 300),
                        Status = c.String(nullable: false, maxLength: 30),
                        StaffComments = c.String(maxLength: 2000),
                        ResolutionDetails = c.String(maxLength: 2000),
                        HandledByStaffId = c.Int(),
                        IpAddress = c.String(maxLength: 45),
                        SubmittedAt = c.DateTime(nullable: false),
                        LastUpdatedAt = c.DateTime(),
                        ClosedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.ComplaintId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.DailyProgressNotes",
                c => new
                    {
                        DailyProgressNoteId = c.Int(nullable: false, identity: true),
                        WorkoutSessionLogId = c.Int(nullable: false),
                        MemberId = c.Int(nullable: false),
                        TrainerId = c.Int(nullable: false),
                        TrainerDescription = c.String(nullable: false, maxLength: 500),
                        SavedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.DailyProgressNoteId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.WorkoutSessionLogs", t => t.WorkoutSessionLogId, cascadeDelete: true)
                .Index(t => t.WorkoutSessionLogId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.WorkoutSessionLogs",
                c => new
                    {
                        WorkoutSessionLogId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        WorkoutProgramId = c.Int(nullable: false),
                        TrainerId = c.Int(nullable: false),
                        Status = c.String(nullable: false, maxLength: 20),
                        SessionDate = c.DateTime(nullable: false),
                        ActualDurationSeconds = c.Int(nullable: false),
                        ExercisesSummary = c.String(),
                        Notes = c.String(maxLength: 500),
                        LoggedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.WorkoutSessionLogId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .ForeignKey("dbo.Trainers", t => t.TrainerId)
                .ForeignKey("dbo.WorkoutPrograms", t => t.WorkoutProgramId)
                .Index(t => t.MemberId)
                .Index(t => t.WorkoutProgramId)
                .Index(t => t.TrainerId);
            
            CreateTable(
                "dbo.Trainers",
                c => new
                    {
                        TrainerId = c.Int(nullable: false, identity: true),
                        TrainerCode = c.String(),
                        FullName = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false),
                        Gender = c.String(),
                        DateOfBirth = c.DateTime(),
                        Specialization = c.String(),
                        ExperienceLevel = c.String(),
                        Qualification = c.String(),
                        EmploymentType = c.String(),
                        MaxCapacity = c.Int(nullable: false),
                        PasswordHash = c.String(),
                        MustChangePassword = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        Role = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TrainerId);
            
            CreateTable(
                "dbo.TrainerAvailabilities",
                c => new
                    {
                        AvailabilityId = c.Int(nullable: false, identity: true),
                        TrainerId = c.Int(nullable: false),
                        DayOfWeek = c.String(),
                        StartTime = c.String(),
                        EndTime = c.String(),
                        IsAvailable = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.AvailabilityId)
                .ForeignKey("dbo.Trainers", t => t.TrainerId, cascadeDelete: true)
                .Index(t => t.TrainerId);
            
            CreateTable(
                "dbo.MemberProgresses",
                c => new
                    {
                        ProgressId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        TrainerId = c.Int(nullable: false),
                        Weight = c.Double(nullable: false),
                        Notes = c.String(),
                        RecordedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ProgressId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.Trainers", t => t.TrainerId, cascadeDelete: true)
                .Index(t => t.MemberId)
                .Index(t => t.TrainerId);
            
            CreateTable(
                "dbo.MemberTrainers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        TrainerId = c.Int(nullable: false),
                        AssignedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.Trainers", t => t.TrainerId, cascadeDelete: true)
                .Index(t => t.MemberId)
                .Index(t => t.TrainerId);
            
            CreateTable(
                "dbo.TrainingSessions",
                c => new
                    {
                        SessionId = c.Int(nullable: false, identity: true),
                        TrainerId = c.Int(nullable: false),
                        MemberId = c.Int(nullable: false),
                        SessionDate = c.DateTime(nullable: false),
                        TimeSlot = c.String(),
                        SessionType = c.String(),
                        Notes = c.String(),
                        Duration = c.String(),
                        Status = c.String(),
                    })
                .PrimaryKey(t => t.SessionId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.Trainers", t => t.TrainerId, cascadeDelete: true)
                .Index(t => t.TrainerId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.WorkoutPrograms",
                c => new
                    {
                        WorkoutProgramId = c.Int(nullable: false, identity: true),
                        ProgramName = c.String(nullable: false),
                        Description = c.String(),
                        TrainerId = c.Int(nullable: false),
                        MemberId = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(),
                        CreatedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.WorkoutProgramId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .ForeignKey("dbo.Trainers", t => t.TrainerId, cascadeDelete: true)
                .Index(t => t.TrainerId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.ProgramExercises",
                c => new
                    {
                        ProgramExerciseId = c.Int(nullable: false, identity: true),
                        ExerciseName = c.String(nullable: false),
                        Sets = c.Int(nullable: false),
                        Reps = c.Int(nullable: false),
                        DurationMinutes = c.Int(nullable: false),
                        WorkoutProgramId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ProgramExerciseId)
                .ForeignKey("dbo.WorkoutPrograms", t => t.WorkoutProgramId, cascadeDelete: true)
                .Index(t => t.WorkoutProgramId);
            
            CreateTable(
                "dbo.DeliveryItems",
                c => new
                    {
                        DeliveryItemId = c.Int(nullable: false, identity: true),
                        SupplierDeliveryId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        ExpectedQty = c.Int(nullable: false),
                        ReceivedQty = c.Int(nullable: false),
                        GoodQty = c.Int(nullable: false),
                        DamagedQty = c.Int(nullable: false),
                        MissingQty = c.Int(nullable: false),
                        ExcessQty = c.Int(nullable: false),
                        Condition = c.String(nullable: false, maxLength: 20),
                        DamageType = c.String(maxLength: 40),
                        DamageSeverity = c.String(maxLength: 20),
                        InspectionNotes = c.String(maxLength: 1000),
                        EvidenceImagePath = c.String(maxLength: 255),
                        IsFlagged = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.DeliveryItemId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.SupplierDeliveries", t => t.SupplierDeliveryId, cascadeDelete: true)
                .Index(t => t.SupplierDeliveryId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        ProductId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 150),
                        Description = c.String(maxLength: 1000),
                        Category = c.String(nullable: false, maxLength: 30),
                        ImagePath = c.String(maxLength: 255),
                        NormalPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        QuantityOnHand = c.Int(nullable: false),
                        ReorderLevel = c.Int(nullable: false),
                        ExpiryDate = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        SKU = c.String(maxLength: 30),
                        ProductQrToken = c.String(maxLength: 100),
                        ProductQrBase64 = c.String(),
                        ProductQrGeneratedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.ProductId);
            
            CreateTable(
                "dbo.Promotions",
                c => new
                    {
                        PromotionId = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        DiscountPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedByStoreStaffId = c.Int(nullable: false),
                        CreatedByAdmin = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.PromotionId)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.SupplierDeliveries",
                c => new
                    {
                        SupplierDeliveryId = c.Int(nullable: false, identity: true),
                        PurchaseOrderId = c.Int(nullable: false),
                        InspectionReference = c.String(nullable: false, maxLength: 25),
                        DeliveryNoteNumber = c.String(nullable: false, maxLength: 80),
                        DeliveredAt = c.DateTime(nullable: false),
                        CourierDriver = c.String(maxLength: 100),
                        VehicleRegistration = c.String(maxLength: 30),
                        NumberOfPackages = c.Int(nullable: false),
                        PackagingCondition = c.String(nullable: false, maxLength: 20),
                        SealCondition = c.String(nullable: false, maxLength: 20),
                        SignsOfTampering = c.Boolean(nullable: false),
                        DeliveryNoteReceived = c.Boolean(nullable: false),
                        SupplierInvoiceReceived = c.Boolean(nullable: false),
                        GeneralNotes = c.String(maxLength: 1000),
                        OverallResult = c.String(nullable: false, maxLength: 30),
                        FinalInspectionNotes = c.String(maxLength: 1000),
                        InspectionStatus = c.String(nullable: false, maxLength: 20),
                        InspectedAt = c.DateTime(nullable: false),
                        InspectedByStoreStaffId = c.Int(),
                        InspectedByAdminId = c.Int(),
                    })
                .PrimaryKey(t => t.SupplierDeliveryId)
                .ForeignKey("dbo.PurchaseOrders", t => t.PurchaseOrderId)
                .Index(t => t.PurchaseOrderId);
            
            CreateTable(
                "dbo.PurchaseOrders",
                c => new
                    {
                        PurchaseOrderId = c.Int(nullable: false, identity: true),
                        ReferenceNumber = c.String(maxLength: 20),
                        SupplierId = c.Int(nullable: false),
                        Status = c.String(nullable: false, maxLength: 25),
                        TotalValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        OrderPurpose = c.String(nullable: false, maxLength: 100),
                        InternalNotes = c.String(maxLength: 500),
                        Priority = c.String(nullable: false, maxLength: 15),
                        RequestedDeliveryDate = c.DateTime(nullable: false),
                        RequiredAdminApproval = c.Boolean(nullable: false),
                        CreatedByStoreStaffId = c.Int(),
                        CreatedAt = c.DateTime(nullable: false),
                        ApprovedByAdminId = c.Int(),
                        ApprovedAt = c.DateTime(),
                        AdminRejectionReason = c.String(maxLength: 300),
                        SentToSupplierAt = c.DateTime(),
                        ConfirmedAt = c.DateTime(),
                        CompletedAt = c.DateTime(),
                        CompletedByAdminId = c.Int(),
                        ReceivingQrToken = c.String(maxLength: 100),
                        ReceivingQrBase64 = c.String(),
                        ReceivingQrGeneratedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.PurchaseOrderId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.SupplierId);
            
            CreateTable(
                "dbo.PurchaseOrderItems",
                c => new
                    {
                        PurchaseOrderItemId = c.Int(nullable: false, identity: true),
                        PurchaseOrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        QuantityOrdered = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.PurchaseOrderItemId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseOrders", t => t.PurchaseOrderId, cascadeDelete: true)
                .Index(t => t.PurchaseOrderId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.Suppliers",
                c => new
                    {
                        SupplierId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 150),
                        ContactPerson = c.String(maxLength: 100),
                        Email = c.String(maxLength: 150),
                        PhoneNumber = c.String(maxLength: 20),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.SupplierId);
            
            CreateTable(
                "dbo.Equipments",
                c => new
                    {
                        EquipmentId = c.Int(nullable: false, identity: true),
                        EquipmentCode = c.String(maxLength: 20),
                        QrCodeBase64 = c.String(),
                        Name = c.String(nullable: false, maxLength: 100),
                        Category = c.String(maxLength: 50),
                        Brand = c.String(maxLength: 80),
                        Model = c.String(maxLength: 80),
                        SerialNumber = c.String(maxLength: 100),
                        PurchaseDate = c.DateTime(),
                        PurchaseCost = c.Decimal(precision: 18, scale: 2),
                        VendorName = c.String(maxLength: 100),
                        VendorContact = c.String(maxLength: 100),
                        WarrantyExpiryDate = c.DateTime(),
                        GymArea = c.String(maxLength: 50),
                        Status = c.String(maxLength: 20),
                        RegisteredAt = c.DateTime(nullable: false),
                        RetiredAt = c.DateTime(),
                        RetirementReason = c.String(maxLength: 500),
                        PendingInspectionTechnicianId = c.Int(),
                        PendingInspectionAssignedDate = c.DateTime(),
                        PendingMaintenanceTechnicianId = c.Int(),
                        PendingMaintenanceAssignedDate = c.DateTime(),
                        LastInspectionDate = c.DateTime(),
                        InspectionIntervalDays = c.Int(nullable: false),
                        NextInspectionDueDate = c.DateTime(),
                        LastMaintenanceDate = c.DateTime(),
                        MaintenanceIntervalDays = c.Int(nullable: false),
                        NextMaintenanceDueDate = c.DateTime(),
                        IsDeleted = c.Boolean(nullable: false),
                        DeletedAt = c.DateTime(),
                        ImagePath = c.String(maxLength: 255),
                        Notes = c.String(maxLength: 1000),
                    })
                .PrimaryKey(t => t.EquipmentId)
                .ForeignKey("dbo.Technicians", t => t.PendingInspectionTechnicianId)
                .ForeignKey("dbo.Technicians", t => t.PendingMaintenanceTechnicianId)
                .Index(t => t.PendingInspectionTechnicianId)
                .Index(t => t.PendingMaintenanceTechnicianId);
            
            CreateTable(
                "dbo.Technicians",
                c => new
                    {
                        TechnicianId = c.Int(nullable: false, identity: true),
                        TechnicianCode = c.String(),
                        FullName = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        PhoneNumber = c.String(nullable: false),
                        Gender = c.String(),
                        DateOfBirth = c.DateTime(),
                        Specialization = c.String(),
                        CertificationLevel = c.String(),
                        EmploymentType = c.String(),
                        PasswordHash = c.String(),
                        MustChangePassword = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        Role = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.TechnicianId);
            
            CreateTable(
                "dbo.EWallets",
                c => new
                    {
                        EWalletId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        Balance = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CreatedAt = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.EWalletId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.Exercises",
                c => new
                    {
                        ExerciseId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 1000),
                        Instructions = c.String(maxLength: 1000),
                        MediaUrl = c.String(maxLength: 255),
                        MediaType = c.String(maxLength: 20),
                        MuscleGroup = c.String(maxLength: 50),
                        Equipment = c.String(maxLength: 100),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ExerciseId);
            
            CreateTable(
                "dbo.WorkoutPlanExercises",
                c => new
                    {
                        PlanExerciseId = c.Int(nullable: false, identity: true),
                        PlanId = c.Int(nullable: false),
                        ExerciseId = c.Int(nullable: false),
                        OrderIndex = c.Int(nullable: false),
                        Reps = c.Int(),
                        Sets = c.Int(),
                        DurationSeconds = c.Int(),
                        RestSeconds = c.Int(nullable: false),
                        Notes = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.PlanExerciseId)
                .ForeignKey("dbo.Exercises", t => t.ExerciseId, cascadeDelete: true)
                .ForeignKey("dbo.WorkoutPlans", t => t.PlanId, cascadeDelete: true)
                .Index(t => new { t.PlanId, t.OrderIndex }, unique: true)
                .Index(t => t.ExerciseId);
            
            CreateTable(
                "dbo.WorkoutPlans",
                c => new
                    {
                        PlanId = c.Int(nullable: false, identity: true),
                        CategoryId = c.Int(nullable: false),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 500),
                        Difficulty = c.String(maxLength: 20),
                        EstimatedMinutes = c.Int(nullable: false),
                        Equipment = c.String(maxLength: 100),
                        ThumbnailUrl = c.String(maxLength: 255),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.PlanId)
                .ForeignKey("dbo.WorkoutCategories", t => t.CategoryId, cascadeDelete: true)
                .Index(t => t.CategoryId);
            
            CreateTable(
                "dbo.WorkoutCategories",
                c => new
                    {
                        CategoryId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 50),
                        Slug = c.String(maxLength: 20),
                        Description = c.String(maxLength: 200),
                        IconClass = c.String(maxLength: 100),
                        ColorAccent = c.String(maxLength: 20),
                        IsActive = c.Boolean(nullable: false),
                        DisplayOrder = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CategoryId);
            
            CreateTable(
                "dbo.Feedbacks",
                c => new
                    {
                        FeedbackId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        Category = c.String(nullable: false, maxLength: 30),
                        Comments = c.String(nullable: false, maxLength: 1000),
                        Rating = c.Int(nullable: false),
                        IpAddress = c.String(maxLength: 45),
                        SubmittedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.FeedbackId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.FitnessProfiles",
                c => new
                    {
                        FitnessProfileId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        HeightCm = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WeightKg = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BodyFatPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Age = c.Int(nullable: false),
                        Gender = c.String(),
                        BMI = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BMICategory = c.String(),
                        BMR = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TDEE = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ActivityLevel = c.String(),
                        GoalType = c.String(),
                        TargetWeightKg = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TargetBodyFatPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ProjectedWeeks = c.Int(nullable: false),
                        ProjectedGoalDate = c.DateTime(),
                        IsOnTrack = c.Boolean(nullable: false),
                        AlertsSentCount = c.Int(nullable: false),
                        LastAlertSentAt = c.DateTime(),
                        CreatedAt = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                        DietaryPreference = c.String(),
                        Allergies = c.String(),
                    })
                .PrimaryKey(t => t.FitnessProfileId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.FitnessProgressLogs",
                c => new
                    {
                        ProgressLogId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        WeightKg = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BodyFatPercent = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WorkoutsCompleted = c.Int(nullable: false),
                        Notes = c.String(),
                        BMI = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WeightChangeSinceStart = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BodyFatChangeSinceStart = c.Decimal(nullable: false, precision: 18, scale: 2),
                        IsUnderperforming = c.Boolean(nullable: false),
                        WeekNumber = c.Int(nullable: false),
                        LoggedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ProgressLogId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.FoodPortions",
                c => new
                    {
                        PortionId = c.Int(nullable: false, identity: true),
                        FoodId = c.Int(nullable: false),
                        PortionName = c.String(nullable: false, maxLength: 50),
                        WeightGrams = c.Int(nullable: false),
                        Calories = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Protein = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Carbs = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Fat = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Fibre = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.PortionId)
                .ForeignKey("dbo.Foods", t => t.FoodId, cascadeDelete: true)
                .Index(t => t.FoodId);
            
            CreateTable(
                "dbo.Foods",
                c => new
                    {
                        FoodId = c.Int(nullable: false, identity: true),
                        FoodName = c.String(nullable: false, maxLength: 100),
                        Category = c.String(maxLength: 50),
                        VisionLabelAliases = c.String(maxLength: 300),
                    })
                .PrimaryKey(t => t.FoodId);
            
            CreateTable(
                "dbo.FreezeRequests",
                c => new
                    {
                        FreezeRequestId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        MemberNumber = c.String(nullable: false, maxLength: 20),
                        FreezeStart = c.DateTime(nullable: false),
                        FreezeEnd = c.DateTime(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 500),
                        Status = c.String(nullable: false, maxLength: 20),
                        RequestedAt = c.DateTime(nullable: false),
                        ProcessedAt = c.DateTime(),
                        AdminNotes = c.String(maxLength: 1000),
                        OriginalExpiry = c.DateTime(),
                        ExtendedExpiry = c.DateTime(),
                    })
                .PrimaryKey(t => t.FreezeRequestId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.Inspections",
                c => new
                    {
                        InspectionId = c.Int(nullable: false, identity: true),
                        EquipmentId = c.Int(nullable: false),
                        TechnicianId = c.Int(),
                        InspectionDate = c.DateTime(nullable: false),
                        Result = c.String(nullable: false, maxLength: 10),
                        Findings = c.String(maxLength: 1000),
                        ActionTaken = c.String(maxLength: 500),
                        RepairId = c.Int(),
                        InspectionType = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.InspectionId)
                .ForeignKey("dbo.Equipments", t => t.EquipmentId, cascadeDelete: true)
                .ForeignKey("dbo.Repairs", t => t.RepairId)
                .ForeignKey("dbo.Technicians", t => t.TechnicianId)
                .Index(t => t.EquipmentId)
                .Index(t => t.TechnicianId)
                .Index(t => t.RepairId);
            
            CreateTable(
                "dbo.Repairs",
                c => new
                    {
                        RepairId = c.Int(nullable: false, identity: true),
                        EquipmentId = c.Int(nullable: false),
                        InspectionId = c.Int(),
                        TechnicianId = c.Int(),
                        ReportedDate = c.DateTime(nullable: false),
                        FaultDescription = c.String(nullable: false, maxLength: 500),
                        RepairStatus = c.String(maxLength: 20),
                        DiagnosisNotes = c.String(maxLength: 1000),
                        PartsUsed = c.String(maxLength: 500),
                        RepairCost = c.Decimal(precision: 18, scale: 2),
                        ResolvedDate = c.DateTime(),
                        ReportedBySource = c.String(maxLength: 20),
                        ReportedByMemberId = c.Int(),
                        Category = c.String(maxLength: 50),
                        Severity = c.String(maxLength: 10),
                        ReferenceNumber = c.String(maxLength: 20),
                        PhotoPath = c.String(maxLength: 255),
                        VendorId = c.Int(),
                        MaintenanceId = c.Int(),
                        ApprovalRejectionReason = c.String(maxLength: 500),
                        VerificationTechnicianId = c.Int(),
                        VerificationAssignedDate = c.DateTime(),
                    })
                .PrimaryKey(t => t.RepairId)
                .ForeignKey("dbo.Equipments", t => t.EquipmentId, cascadeDelete: true)
                .ForeignKey("dbo.Inspections", t => t.InspectionId)
                .ForeignKey("dbo.Maintenances", t => t.MaintenanceId)
                .ForeignKey("dbo.MemberForms", t => t.ReportedByMemberId)
                .ForeignKey("dbo.Technicians", t => t.TechnicianId)
                .ForeignKey("dbo.Vendors", t => t.VendorId)
                .ForeignKey("dbo.Technicians", t => t.VerificationTechnicianId)
                .Index(t => t.EquipmentId)
                .Index(t => t.InspectionId)
                .Index(t => t.TechnicianId)
                .Index(t => t.ReportedByMemberId)
                .Index(t => t.VendorId)
                .Index(t => t.MaintenanceId)
                .Index(t => t.VerificationTechnicianId);
            
            CreateTable(
                "dbo.Maintenances",
                c => new
                    {
                        MaintenanceId = c.Int(nullable: false, identity: true),
                        EquipmentId = c.Int(nullable: false),
                        TechnicianId = c.Int(),
                        MaintenanceDate = c.DateTime(nullable: false),
                        Result = c.String(nullable: false, maxLength: 20),
                        TasksPerformed = c.String(maxLength: 1000),
                        PartsReplaced = c.String(maxLength: 500),
                        Cost = c.Decimal(precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.MaintenanceId)
                .ForeignKey("dbo.Equipments", t => t.EquipmentId, cascadeDelete: true)
                .ForeignKey("dbo.Technicians", t => t.TechnicianId)
                .Index(t => t.EquipmentId)
                .Index(t => t.TechnicianId);
            
            CreateTable(
                "dbo.Vendors",
                c => new
                    {
                        VendorId = c.Int(nullable: false, identity: true),
                        VendorCode = c.String(),
                        CompanyName = c.String(nullable: false),
                        ContactPerson = c.String(),
                        Phone = c.String(nullable: false),
                        Email = c.String(),
                        ServiceType = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.VendorId);
            
            CreateTable(
                "dbo.InventoryMovements",
                c => new
                    {
                        InventoryMovementId = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        QuantityChange = c.Int(nullable: false),
                        MovementType = c.String(nullable: false, maxLength: 40),
                        ReferenceType = c.String(maxLength: 40),
                        ReferenceId = c.Int(),
                        ReferenceNumber = c.String(maxLength: 30),
                        Notes = c.String(maxLength: 500),
                        PerformedBy = c.String(maxLength: 80),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.InventoryMovementId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.LockerAccessOverrides",
                c => new
                    {
                        LockerAccessOverrideId = c.Int(nullable: false, identity: true),
                        LockerId = c.Int(nullable: false),
                        ReservationId = c.Int(),
                        MemberId = c.Int(nullable: false),
                        LockerFaultId = c.Int(nullable: false),
                        PerformedByAdminId = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 500),
                        Acknowledgement = c.String(maxLength: 200),
                        PerformedAt = c.DateTime(nullable: false),
                        IpAddress = c.String(maxLength: 45),
                        MemberNotified = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.LockerAccessOverrideId)
                .ForeignKey("dbo.Lockers", t => t.LockerId)
                .ForeignKey("dbo.LockerFaults", t => t.LockerFaultId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.LockerId)
                .Index(t => t.MemberId)
                .Index(t => t.LockerFaultId);
            
            CreateTable(
                "dbo.Lockers",
                c => new
                    {
                        LockerId = c.Int(nullable: false, identity: true),
                        LockerNumber = c.String(nullable: false, maxLength: 10),
                        Location = c.String(maxLength: 100),
                        IsOccupied = c.Boolean(nullable: false),
                        OperationalStatus = c.String(nullable: false, maxLength: 20),
                        Gender = c.String(nullable: false, maxLength: 10),
                    })
                .PrimaryKey(t => t.LockerId);
            
            CreateTable(
                "dbo.LockerFaults",
                c => new
                    {
                        LockerFaultId = c.Int(nullable: false, identity: true),
                        LockerId = c.Int(nullable: false),
                        ReferenceNumber = c.String(maxLength: 20),
                        FaultType = c.String(nullable: false, maxLength: 30),
                        ReportedSeverity = c.String(maxLength: 20),
                        Severity = c.String(maxLength: 20),
                        Status = c.String(nullable: false, maxLength: 20),
                        ReportedDate = c.DateTime(nullable: false),
                        BelongingsInside = c.Boolean(nullable: false),
                        TriagedDate = c.DateTime(),
                        TriagedByAdminId = c.Int(),
                        TriageNotes = c.String(maxLength: 500),
                        TechnicianId = c.Int(),
                        AssignedDate = c.DateTime(),
                        StartedDate = c.DateTime(),
                        ResolvedDate = c.DateTime(),
                        ResolutionNotes = c.String(maxLength: 1000),
                        PartsUsed = c.String(maxLength: 500),
                        RepairCost = c.Decimal(precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.LockerFaultId)
                .ForeignKey("dbo.Lockers", t => t.LockerId)
                .ForeignKey("dbo.Technicians", t => t.TechnicianId)
                .Index(t => t.LockerId)
                .Index(t => t.TechnicianId);
            
            CreateTable(
                "dbo.LockerFaultReports",
                c => new
                    {
                        LockerFaultReportId = c.Int(nullable: false, identity: true),
                        LockerFaultId = c.Int(nullable: false),
                        MemberId = c.Int(),
                        ReportedBySource = c.String(nullable: false, maxLength: 20),
                        Description = c.String(nullable: false, maxLength: 1000),
                        ReportedSeverity = c.String(maxLength: 20),
                        PhotoPath = c.String(maxLength: 255),
                        WasHoldingLocker = c.Boolean(nullable: false),
                        ReportedDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.LockerFaultReportId)
                .ForeignKey("dbo.LockerFaults", t => t.LockerFaultId, cascadeDelete: true)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.LockerFaultId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.LockerReservations",
                c => new
                    {
                        ReservationId = c.Int(nullable: false, identity: true),
                        LockerId = c.Int(nullable: false),
                        MemberId = c.Int(nullable: false),
                        AccessCodeHash = c.String(maxLength: 200),
                        ReservedAt = c.DateTime(nullable: false),
                        EndedAt = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        FailedAttempts = c.Int(nullable: false),
                        LockedUntil = c.DateTime(),
                        HasBeenUnlocked = c.Boolean(nullable: false),
                        LockoutCount = c.Int(nullable: false),
                        WasForceReleased = c.Boolean(nullable: false),
                        ForceReleaseReason = c.String(maxLength: 300),
                    })
                .PrimaryKey(t => t.ReservationId)
                .ForeignKey("dbo.Lockers", t => t.LockerId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.LockerId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.LoyaltyAccounts",
                c => new
                    {
                        LoyaltyAccountId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        CurrentPoints = c.Int(nullable: false),
                        TotalPointsEarned = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        LastUpdated = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.LoyaltyAccountId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.MealLogItems",
                c => new
                    {
                        MealLogItemId = c.Int(nullable: false, identity: true),
                        MealLogId = c.Int(nullable: false),
                        FoodId = c.Int(),
                        FoodName = c.String(nullable: false, maxLength: 100),
                        PortionName = c.String(maxLength: 50),
                        WeightGrams = c.Int(nullable: false),
                        Calories = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Protein = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Carbs = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Fat = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Fibre = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.MealLogItemId)
                .ForeignKey("dbo.Foods", t => t.FoodId)
                .ForeignKey("dbo.MealLogs", t => t.MealLogId, cascadeDelete: true)
                .Index(t => t.MealLogId)
                .Index(t => t.FoodId);
            
            CreateTable(
                "dbo.MealLogs",
                c => new
                    {
                        MealLogId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        MealType = c.String(maxLength: 20),
                        PhotoUrl = c.String(maxLength: 300),
                        LoggedAt = c.DateTime(nullable: false),
                        TotalCalories = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalProtein = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalCarbs = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalFat = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalFibre = c.Decimal(nullable: false, precision: 18, scale: 2),
                        GoalTypeAtLogTime = c.String(maxLength: 30),
                        GoalVerdict = c.String(maxLength: 20),
                        GoalVerdictMessage = c.String(maxLength: 300),
                    })
                .PrimaryKey(t => t.MealLogId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.MealPlanItems",
                c => new
                    {
                        MealPlanItemId = c.Int(nullable: false, identity: true),
                        MealPlanId = c.Int(nullable: false),
                        DayNumber = c.Int(nullable: false),
                        RecipeId = c.Int(nullable: false),
                        MealTime = c.String(nullable: false, maxLength: 20),
                    })
                .PrimaryKey(t => t.MealPlanItemId)
                .ForeignKey("dbo.MealPlans", t => t.MealPlanId, cascadeDelete: true)
                .ForeignKey("dbo.Recipes", t => t.RecipeId)
                .Index(t => t.MealPlanId)
                .Index(t => t.RecipeId);
            
            CreateTable(
                "dbo.MealPlans",
                c => new
                    {
                        MealPlanId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        TargetCalories = c.Int(nullable: false),
                        TargetProtein = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TargetCarbs = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TargetFat = c.Decimal(nullable: false, precision: 18, scale: 2),
                        GoalType = c.String(maxLength: 30),
                        GeneratedAt = c.DateTime(nullable: false),
                        IsAccepted = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.MealPlanId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.Recipes",
                c => new
                    {
                        RecipeId = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 150),
                        MealTime = c.String(nullable: false, maxLength: 20),
                        Calories = c.Int(nullable: false),
                        Protein = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Carbs = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Fat = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PortionDescription = c.String(maxLength: 200),
                        DietaryTags = c.String(maxLength: 100),
                        Allergens = c.String(maxLength: 100),
                        Description = c.String(maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.RecipeId);
            
            CreateTable(
                "dbo.Notifications",
                c => new
                    {
                        NotificationId = c.Int(nullable: false, identity: true),
                        RecipientType = c.String(nullable: false, maxLength: 20),
                        RecipientId = c.Int(nullable: false),
                        Title = c.String(nullable: false, maxLength: 150),
                        Message = c.String(nullable: false, maxLength: 500),
                        Type = c.String(maxLength: 20),
                        LinkUrl = c.String(maxLength: 200),
                        IsRead = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.NotificationId);
            
            CreateTable(
                "dbo.Payments",
                c => new
                    {
                        PaymentId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        Reference = c.String(maxLength: 100),
                        TransactionId = c.String(maxLength: 100),
                        PaymentDate = c.DateTime(nullable: false),
                        Description = c.String(maxLength: 200),
                        Method = c.String(maxLength: 50),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Status = c.String(maxLength: 20),
                    })
                .PrimaryKey(t => t.PaymentId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.PointsTransactions",
                c => new
                    {
                        PointsTransactionId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        TransactionType = c.String(nullable: false, maxLength: 20),
                        Points = c.Int(nullable: false),
                        Description = c.String(maxLength: 200),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.PointsTransactionId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.Referrals",
                c => new
                    {
                        ReferralId = c.Int(nullable: false, identity: true),
                        ReferrerId = c.Int(nullable: false),
                        ReferralCodeUsed = c.String(nullable: false, maxLength: 20),
                        ReferredMemberId = c.Int(nullable: false),
                        Status = c.String(nullable: false, maxLength: 20),
                        RewardPoints = c.Int(nullable: false),
                        RegisteredAt = c.DateTime(nullable: false),
                        RewardedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.ReferralId)
                .ForeignKey("dbo.MemberForms", t => t.ReferredMemberId)
                .ForeignKey("dbo.MemberForms", t => t.ReferrerId)
                .Index(t => t.ReferrerId)
                .Index(t => t.ReferredMemberId);
            
            CreateTable(
                "dbo.RefundRequests",
                c => new
                    {
                        RefundRequestId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        MemberNumber = c.String(maxLength: 20),
                        RefundType = c.String(maxLength: 50),
                        RefundAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Status = c.String(maxLength: 20),
                        RequestedAt = c.DateTime(nullable: false),
                        ProcessedAt = c.DateTime(),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.RefundRequestId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.RepairAssignments",
                c => new
                    {
                        RepairAssignmentId = c.Int(nullable: false, identity: true),
                        RepairId = c.Int(nullable: false),
                        AssigneeType = c.String(nullable: false, maxLength: 20),
                        TechnicianId = c.Int(),
                        VendorId = c.Int(),
                        AssignedByAdminId = c.Int(nullable: false),
                        AssignedDate = c.DateTime(nullable: false),
                        DueDate = c.DateTime(),
                        Priority = c.String(maxLength: 20),
                        AssignmentNotes = c.String(maxLength: 500),
                        AssignmentStatus = c.String(nullable: false, maxLength: 20),
                        AcceptedDate = c.DateTime(),
                        StartedDate = c.DateTime(),
                        CompletedDate = c.DateTime(),
                        DeclineReason = c.String(maxLength: 500),
                        EscalationReason = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.RepairAssignmentId)
                .ForeignKey("dbo.Repairs", t => t.RepairId, cascadeDelete: true)
                .ForeignKey("dbo.Technicians", t => t.TechnicianId)
                .ForeignKey("dbo.Vendors", t => t.VendorId)
                .Index(t => t.RepairId)
                .Index(t => t.TechnicianId)
                .Index(t => t.VendorId);
            
            CreateTable(
                "dbo.RepairRequests",
                c => new
                    {
                        RepairRequestId = c.Int(nullable: false, identity: true),
                        ReferenceNumber = c.String(maxLength: 20),
                        EquipmentCode = c.String(nullable: false, maxLength: 20),
                        Category = c.String(nullable: false, maxLength: 50),
                        Severity = c.String(nullable: false, maxLength: 20),
                        Description = c.String(nullable: false, maxLength: 1000),
                        PhotoUrl = c.String(maxLength: 500),
                        Status = c.String(maxLength: 20),
                        AssignedTechnicianId = c.Int(),
                        AssignedAt = c.DateTime(),
                        ReportedAt = c.DateTime(nullable: false),
                        ResolvedAt = c.DateTime(),
                        ReportedBy = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.RepairRequestId);
            
            CreateTable(
                "dbo.ReturnRequests",
                c => new
                    {
                        ReturnRequestId = c.Int(nullable: false, identity: true),
                        ReferenceNumber = c.String(maxLength: 20),
                        StoreOrderItemId = c.Int(nullable: false),
                        ReturnQrTokenHash = c.String(maxLength: 64),
                        ReturnQrBase64 = c.String(),
                        ReturnQrGeneratedAt = c.DateTime(),
                        MemberId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 30),
                        Description = c.String(maxLength: 1000),
                        RequestedResolution = c.String(maxLength: 20),
                        ExchangeType = c.String(maxLength: 30),
                        ExchangeProductId = c.Int(),
                        ExchangeQuantity = c.Int(),
                        ReturnCreditAmount = c.Decimal(precision: 18, scale: 2),
                        ExchangeUnitPrice = c.Decimal(precision: 18, scale: 2),
                        ExchangeTotalAmount = c.Decimal(precision: 18, scale: 2),
                        ExchangePriceDifference = c.Decimal(precision: 18, scale: 2),
                        Status = c.String(nullable: false, maxLength: 30),
                        RequestedAt = c.DateTime(nullable: false),
                        ItemReceivedAt = c.DateTime(),
                        QuantityPresented = c.Int(),
                        StaffReviewedAt = c.DateTime(),
                        StaffReviewedByStoreStaffId = c.Int(),
                        ItemCondition = c.String(maxLength: 30),
                        PackagingCondition = c.String(maxLength: 30),
                        AccessoriesComplete = c.Boolean(),
                        ProductMatchesPurchase = c.Boolean(),
                        FaultVerification = c.String(maxLength: 30),
                        FaultCause = c.String(maxLength: 40),
                        StaffReviewNotes = c.String(maxLength: 500),
                        StaffRecommendation = c.String(maxLength: 20),
                        StaffRecommendedResolution = c.String(maxLength: 20),
                        InspectionEvidencePath = c.String(maxLength: 255),
                        ItemDisposition = c.String(maxLength: 30),
                        DecidedAt = c.DateTime(),
                        DecidedByAdminId = c.Int(),
                        AdminDecisionReason = c.String(maxLength: 500),
                        ApprovedResolution = c.String(maxLength: 20),
                        WasRestocked = c.Boolean(nullable: false),
                        RefundAmount = c.Decimal(precision: 18, scale: 2),
                        PaystackRefundReference = c.String(maxLength: 100),
                        RefundProcessedAt = c.DateTime(),
                        ExchangeQuantityReserved = c.Int(),
                        ExchangeReservedAt = c.DateTime(),
                        ExchangeReservationExpiresAt = c.DateTime(),
                        ExchangeReservationReleasedAt = c.DateTime(),
                        ExchangeCancellationReason = c.String(maxLength: 500),
                        AdditionalPaymentAmount = c.Decimal(precision: 18, scale: 2),
                        ExchangePaymentReference = c.String(maxLength: 100),
                        ExchangePaymentCompletedAt = c.DateTime(),
                        ExchangeRefundAmount = c.Decimal(precision: 18, scale: 2),
                        ExchangeRefundReference = c.String(maxLength: 100),
                        ExchangeCompletedAt = c.DateTime(),
                        ExchangeCompletedByStoreStaffId = c.Int(),
                        ExchangeSignaturePath = c.String(maxLength: 255),
                    })
                .PrimaryKey(t => t.ReturnRequestId)
                .ForeignKey("dbo.Products", t => t.ExchangeProductId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .ForeignKey("dbo.StoreOrderItems", t => t.StoreOrderItemId)
                .Index(t => t.StoreOrderItemId)
                .Index(t => t.MemberId)
                .Index(t => t.ExchangeProductId);
            
            CreateTable(
                "dbo.StoreOrderItems",
                c => new
                    {
                        StoreOrderItemId = c.Int(nullable: false, identity: true),
                        StoreOrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitPriceAtPurchase = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.StoreOrderItemId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.StoreOrders", t => t.StoreOrderId, cascadeDelete: true)
                .Index(t => t.StoreOrderId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.StoreOrders",
                c => new
                    {
                        StoreOrderId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        ReferenceNumber = c.String(nullable: false, maxLength: 20),
                        Subtotal = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DiscountAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        FinalTotal = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaystackReference = c.String(maxLength: 100),
                        Status = c.String(nullable: false, maxLength: 30),
                        CreatedAt = c.DateTime(nullable: false),
                        PaidAt = c.DateTime(),
                        ReadyAt = c.DateTime(),
                        PickupDeadline = c.DateTime(),
                        CollectedAt = c.DateTime(),
                        PickupSignaturePath = c.String(maxLength: 255),
                        CollectedByStaffId = c.Int(),
                        FirstReminderSentAt = c.DateTime(),
                        FinalReminderSentAt = c.DateTime(),
                        OrderQrBase64 = c.String(),
                        ReadyQrToken = c.String(maxLength: 100),
                        ReadyQrGeneratedAt = c.DateTime(),
                        ReadyQrUsedAt = c.DateTime(),
                        UncollectedAt = c.DateTime(),
                        StockReleasedAt = c.DateTime(),
                        IsPickupStockReserved = c.Boolean(nullable: false),
                        UncollectedResolutionStatus = c.String(maxLength: 40),
                        UncollectedResolutionNotes = c.String(maxLength: 500),
                        UncollectedResolvedBy = c.String(maxLength: 80),
                        UncollectedResolvedAt = c.DateTime(),
                        DeadlineExtensionCount = c.Int(nullable: false),
                        PreviousPickupDeadline = c.DateTime(),
                        UncollectedRefundAmount = c.Decimal(precision: 18, scale: 2),
                        UncollectedRefundReference = c.String(maxLength: 100),
                        UncollectedRefundedAt = c.DateTime(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.StoreOrderId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.StoreOrderCollectionEvents",
                c => new
                    {
                        StoreOrderCollectionEventId = c.Int(nullable: false, identity: true),
                        StoreOrderId = c.Int(nullable: false),
                        EventType = c.String(nullable: false, maxLength: 50),
                        Description = c.String(nullable: false, maxLength: 500),
                        PerformedByRole = c.String(maxLength: 30),
                        PerformedBy = c.String(maxLength: 80),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StoreOrderCollectionEventId)
                .ForeignKey("dbo.StoreOrders", t => t.StoreOrderId)
                .Index(t => t.StoreOrderId);
            
            CreateTable(
                "dbo.SessionExerciseLogs",
                c => new
                    {
                        LogId = c.Int(nullable: false, identity: true),
                        SessionId = c.Int(nullable: false),
                        ExerciseId = c.Int(nullable: false),
                        OrderIndex = c.Int(nullable: false),
                        RepsCompleted = c.Int(nullable: false),
                        SetsCompleted = c.Int(nullable: false),
                        DurationSeconds = c.Int(nullable: false),
                        Skipped = c.Boolean(nullable: false),
                        LoggedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.LogId)
                .ForeignKey("dbo.Exercises", t => t.ExerciseId)
                .ForeignKey("dbo.WorkoutSessions", t => t.SessionId, cascadeDelete: true)
                .Index(t => t.SessionId)
                .Index(t => t.ExerciseId);
            
            CreateTable(
                "dbo.WorkoutSessions",
                c => new
                    {
                        SessionId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        PlanId = c.Int(nullable: false),
                        StartedAt = c.DateTime(nullable: false),
                        CompletedAt = c.DateTime(),
                        Status = c.String(maxLength: 20),
                        TotalDurationSeconds = c.Int(nullable: false),
                        ExercisesCompleted = c.Int(nullable: false),
                        CaloriesBurned = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.SessionId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .ForeignKey("dbo.WorkoutPlans", t => t.PlanId)
                .Index(t => t.MemberId)
                .Index(t => t.PlanId);
            
            CreateTable(
                "dbo.ShoppingCartItems",
                c => new
                    {
                        ShoppingCartItemId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        AddedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.ShoppingCartItemId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => t.MemberId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.StoreStaffs",
                c => new
                    {
                        StoreStaffId = c.Int(nullable: false, identity: true),
                        FullName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 150),
                        PhoneNumber = c.String(maxLength: 20),
                        Gender = c.String(maxLength: 10),
                        DateOfBirth = c.DateTime(),
                        StoreStaffCode = c.String(maxLength: 20),
                        PasswordHash = c.String(maxLength: 200),
                        MustChangePassword = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        Role = c.String(maxLength: 20),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.StoreStaffId);
            
            CreateTable(
                "dbo.SupplierProducts",
                c => new
                    {
                        SupplierProductId = c.Int(nullable: false, identity: true),
                        SupplierId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        CurrentUnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        EstimatedDeliveryDays = c.Int(nullable: false),
                        IsPreferredSupplier = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        PriceUpdatedAt = c.DateTime(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.SupplierProductId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => new { t.SupplierId, t.ProductId }, unique: true);
            
            CreateTable(
                "dbo.SupplierReturns",
                c => new
                    {
                        SupplierReturnId = c.Int(nullable: false, identity: true),
                        ReferenceNumber = c.String(nullable: false, maxLength: 20),
                        DeliveryItemId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 30),
                        RequestedResolution = c.String(nullable: false, maxLength: 30),
                        Notes = c.String(maxLength: 500),
                        EstimatedReturnValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        RequiresAdminApproval = c.Boolean(nullable: false),
                        Status = c.String(nullable: false, maxLength: 30),
                        CreatedByStoreStaffId = c.Int(),
                        CreatedByAdminId = c.Int(),
                        CreatedAt = c.DateTime(nullable: false),
                        ApprovedByAdminId = c.Int(),
                        ApprovedAt = c.DateTime(),
                        RejectionReason = c.String(maxLength: 300),
                        ResolvedAt = c.DateTime(),
                        Resolution = c.String(maxLength: 20),
                        ResolutionNotes = c.String(maxLength: 300),
                    })
                .PrimaryKey(t => t.SupplierReturnId)
                .ForeignKey("dbo.DeliveryItems", t => t.DeliveryItemId)
                .Index(t => t.DeliveryItemId);
            
            CreateTable(
                "dbo.UserBadges",
                c => new
                    {
                        BadgeId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        BadgeType = c.String(nullable: false, maxLength: 50),
                        BadgeName = c.String(maxLength: 100),
                        Description = c.String(maxLength: 255),
                        IconClass = c.String(maxLength: 100),
                        EarnedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.BadgeId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.WalletTransactions",
                c => new
                    {
                        WalletTransactionId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        TransactionType = c.String(nullable: false, maxLength: 30),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Description = c.String(maxLength: 200),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.WalletTransactionId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
            CreateTable(
                "dbo.WeeklyProgressLogs",
                c => new
                    {
                        WeeklyProgressLogId = c.Int(nullable: false, identity: true),
                        MemberId = c.Int(nullable: false),
                        WeekNumber = c.Int(nullable: false),
                        LoggedDate = c.DateTime(nullable: false),
                        WeightKg = c.Decimal(nullable: false, precision: 5, scale: 2),
                        BodyFatPercent = c.Decimal(nullable: false, precision: 5, scale: 2),
                        WorkoutsThisWeek = c.Int(nullable: false),
                        Notes = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.WeeklyProgressLogId)
                .ForeignKey("dbo.MemberForms", t => t.MemberId, cascadeDelete: true)
                .Index(t => t.MemberId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.WeeklyProgressLogs", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.WalletTransactions", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.UserBadges", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.SupplierReturns", "DeliveryItemId", "dbo.DeliveryItems");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ShoppingCartItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ShoppingCartItems", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.SessionExerciseLogs", "SessionId", "dbo.WorkoutSessions");
            DropForeignKey("dbo.WorkoutSessions", "PlanId", "dbo.WorkoutPlans");
            DropForeignKey("dbo.WorkoutSessions", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.SessionExerciseLogs", "ExerciseId", "dbo.Exercises");
            DropForeignKey("dbo.ReturnRequests", "StoreOrderItemId", "dbo.StoreOrderItems");
            DropForeignKey("dbo.StoreOrderItems", "StoreOrderId", "dbo.StoreOrders");
            DropForeignKey("dbo.StoreOrders", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.StoreOrderCollectionEvents", "StoreOrderId", "dbo.StoreOrders");
            DropForeignKey("dbo.StoreOrderItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ReturnRequests", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.ReturnRequests", "ExchangeProductId", "dbo.Products");
            DropForeignKey("dbo.RepairAssignments", "VendorId", "dbo.Vendors");
            DropForeignKey("dbo.RepairAssignments", "TechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.RepairAssignments", "RepairId", "dbo.Repairs");
            DropForeignKey("dbo.RefundRequests", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Referrals", "ReferrerId", "dbo.MemberForms");
            DropForeignKey("dbo.Referrals", "ReferredMemberId", "dbo.MemberForms");
            DropForeignKey("dbo.PointsTransactions", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Payments", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.MealPlanItems", "RecipeId", "dbo.Recipes");
            DropForeignKey("dbo.MealPlanItems", "MealPlanId", "dbo.MealPlans");
            DropForeignKey("dbo.MealPlans", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.MealLogs", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.MealLogItems", "MealLogId", "dbo.MealLogs");
            DropForeignKey("dbo.MealLogItems", "FoodId", "dbo.Foods");
            DropForeignKey("dbo.LoyaltyAccounts", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.LockerAccessOverrides", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.LockerAccessOverrides", "LockerFaultId", "dbo.LockerFaults");
            DropForeignKey("dbo.LockerAccessOverrides", "LockerId", "dbo.Lockers");
            DropForeignKey("dbo.LockerReservations", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.LockerReservations", "LockerId", "dbo.Lockers");
            DropForeignKey("dbo.LockerFaults", "TechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.LockerFaultReports", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.LockerFaultReports", "LockerFaultId", "dbo.LockerFaults");
            DropForeignKey("dbo.LockerFaults", "LockerId", "dbo.Lockers");
            DropForeignKey("dbo.InventoryMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Inspections", "TechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.Inspections", "RepairId", "dbo.Repairs");
            DropForeignKey("dbo.Repairs", "VerificationTechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.Repairs", "VendorId", "dbo.Vendors");
            DropForeignKey("dbo.Repairs", "TechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.Repairs", "ReportedByMemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Repairs", "MaintenanceId", "dbo.Maintenances");
            DropForeignKey("dbo.Maintenances", "TechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.Maintenances", "EquipmentId", "dbo.Equipments");
            DropForeignKey("dbo.Repairs", "InspectionId", "dbo.Inspections");
            DropForeignKey("dbo.Repairs", "EquipmentId", "dbo.Equipments");
            DropForeignKey("dbo.Inspections", "EquipmentId", "dbo.Equipments");
            DropForeignKey("dbo.FreezeRequests", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.FoodPortions", "FoodId", "dbo.Foods");
            DropForeignKey("dbo.FitnessProgressLogs", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.FitnessProfiles", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Feedbacks", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.WorkoutPlanExercises", "PlanId", "dbo.WorkoutPlans");
            DropForeignKey("dbo.WorkoutPlans", "CategoryId", "dbo.WorkoutCategories");
            DropForeignKey("dbo.WorkoutPlanExercises", "ExerciseId", "dbo.Exercises");
            DropForeignKey("dbo.EWallets", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Equipments", "PendingMaintenanceTechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.Equipments", "PendingInspectionTechnicianId", "dbo.Technicians");
            DropForeignKey("dbo.DeliveryItems", "SupplierDeliveryId", "dbo.SupplierDeliveries");
            DropForeignKey("dbo.SupplierDeliveries", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.PurchaseOrderItems", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrderItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.DeliveryItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Promotions", "ProductId", "dbo.Products");
            DropForeignKey("dbo.DailyProgressNotes", "WorkoutSessionLogId", "dbo.WorkoutSessionLogs");
            DropForeignKey("dbo.WorkoutSessionLogs", "WorkoutProgramId", "dbo.WorkoutPrograms");
            DropForeignKey("dbo.WorkoutSessionLogs", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.WorkoutPrograms", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.ProgramExercises", "WorkoutProgramId", "dbo.WorkoutPrograms");
            DropForeignKey("dbo.WorkoutPrograms", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.TrainingSessions", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.TrainingSessions", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.MemberTrainers", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.MemberTrainers", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.MemberProgresses", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.MemberProgresses", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.TrainerAvailabilities", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.WorkoutSessionLogs", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.DailyProgressNotes", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.ComplaintTickets", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.CafeRedemptions", "CafeProductId", "dbo.CafeProducts");
            DropForeignKey("dbo.CafeRedemptions", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Attendances", "MemberId", "dbo.MemberForms");
            DropIndex("dbo.WeeklyProgressLogs", new[] { "MemberId" });
            DropIndex("dbo.WalletTransactions", new[] { "MemberId" });
            DropIndex("dbo.UserBadges", new[] { "MemberId" });
            DropIndex("dbo.SupplierReturns", new[] { "DeliveryItemId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId", "ProductId" });
            DropIndex("dbo.ShoppingCartItems", new[] { "ProductId" });
            DropIndex("dbo.ShoppingCartItems", new[] { "MemberId" });
            DropIndex("dbo.WorkoutSessions", new[] { "PlanId" });
            DropIndex("dbo.WorkoutSessions", new[] { "MemberId" });
            DropIndex("dbo.SessionExerciseLogs", new[] { "ExerciseId" });
            DropIndex("dbo.SessionExerciseLogs", new[] { "SessionId" });
            DropIndex("dbo.StoreOrderCollectionEvents", new[] { "StoreOrderId" });
            DropIndex("dbo.StoreOrders", new[] { "MemberId" });
            DropIndex("dbo.StoreOrderItems", new[] { "ProductId" });
            DropIndex("dbo.StoreOrderItems", new[] { "StoreOrderId" });
            DropIndex("dbo.ReturnRequests", new[] { "ExchangeProductId" });
            DropIndex("dbo.ReturnRequests", new[] { "MemberId" });
            DropIndex("dbo.ReturnRequests", new[] { "StoreOrderItemId" });
            DropIndex("dbo.RepairAssignments", new[] { "VendorId" });
            DropIndex("dbo.RepairAssignments", new[] { "TechnicianId" });
            DropIndex("dbo.RepairAssignments", new[] { "RepairId" });
            DropIndex("dbo.RefundRequests", new[] { "MemberId" });
            DropIndex("dbo.Referrals", new[] { "ReferredMemberId" });
            DropIndex("dbo.Referrals", new[] { "ReferrerId" });
            DropIndex("dbo.PointsTransactions", new[] { "MemberId" });
            DropIndex("dbo.Payments", new[] { "MemberId" });
            DropIndex("dbo.MealPlans", new[] { "MemberId" });
            DropIndex("dbo.MealPlanItems", new[] { "RecipeId" });
            DropIndex("dbo.MealPlanItems", new[] { "MealPlanId" });
            DropIndex("dbo.MealLogs", new[] { "MemberId" });
            DropIndex("dbo.MealLogItems", new[] { "FoodId" });
            DropIndex("dbo.MealLogItems", new[] { "MealLogId" });
            DropIndex("dbo.LoyaltyAccounts", new[] { "MemberId" });
            DropIndex("dbo.LockerReservations", new[] { "MemberId" });
            DropIndex("dbo.LockerReservations", new[] { "LockerId" });
            DropIndex("dbo.LockerFaultReports", new[] { "MemberId" });
            DropIndex("dbo.LockerFaultReports", new[] { "LockerFaultId" });
            DropIndex("dbo.LockerFaults", new[] { "TechnicianId" });
            DropIndex("dbo.LockerFaults", new[] { "LockerId" });
            DropIndex("dbo.LockerAccessOverrides", new[] { "LockerFaultId" });
            DropIndex("dbo.LockerAccessOverrides", new[] { "MemberId" });
            DropIndex("dbo.LockerAccessOverrides", new[] { "LockerId" });
            DropIndex("dbo.InventoryMovements", new[] { "ProductId" });
            DropIndex("dbo.Maintenances", new[] { "TechnicianId" });
            DropIndex("dbo.Maintenances", new[] { "EquipmentId" });
            DropIndex("dbo.Repairs", new[] { "VerificationTechnicianId" });
            DropIndex("dbo.Repairs", new[] { "MaintenanceId" });
            DropIndex("dbo.Repairs", new[] { "VendorId" });
            DropIndex("dbo.Repairs", new[] { "ReportedByMemberId" });
            DropIndex("dbo.Repairs", new[] { "TechnicianId" });
            DropIndex("dbo.Repairs", new[] { "InspectionId" });
            DropIndex("dbo.Repairs", new[] { "EquipmentId" });
            DropIndex("dbo.Inspections", new[] { "RepairId" });
            DropIndex("dbo.Inspections", new[] { "TechnicianId" });
            DropIndex("dbo.Inspections", new[] { "EquipmentId" });
            DropIndex("dbo.FreezeRequests", new[] { "MemberId" });
            DropIndex("dbo.FoodPortions", new[] { "FoodId" });
            DropIndex("dbo.FitnessProgressLogs", new[] { "MemberId" });
            DropIndex("dbo.FitnessProfiles", new[] { "MemberId" });
            DropIndex("dbo.Feedbacks", new[] { "MemberId" });
            DropIndex("dbo.WorkoutPlans", new[] { "CategoryId" });
            DropIndex("dbo.WorkoutPlanExercises", new[] { "ExerciseId" });
            DropIndex("dbo.WorkoutPlanExercises", new[] { "PlanId", "OrderIndex" });
            DropIndex("dbo.EWallets", new[] { "MemberId" });
            DropIndex("dbo.Equipments", new[] { "PendingMaintenanceTechnicianId" });
            DropIndex("dbo.Equipments", new[] { "PendingInspectionTechnicianId" });
            DropIndex("dbo.PurchaseOrderItems", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderItems", new[] { "PurchaseOrderId" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.SupplierDeliveries", new[] { "PurchaseOrderId" });
            DropIndex("dbo.Promotions", new[] { "ProductId" });
            DropIndex("dbo.DeliveryItems", new[] { "ProductId" });
            DropIndex("dbo.DeliveryItems", new[] { "SupplierDeliveryId" });
            DropIndex("dbo.ProgramExercises", new[] { "WorkoutProgramId" });
            DropIndex("dbo.WorkoutPrograms", new[] { "MemberId" });
            DropIndex("dbo.WorkoutPrograms", new[] { "TrainerId" });
            DropIndex("dbo.TrainingSessions", new[] { "MemberId" });
            DropIndex("dbo.TrainingSessions", new[] { "TrainerId" });
            DropIndex("dbo.MemberTrainers", new[] { "TrainerId" });
            DropIndex("dbo.MemberTrainers", new[] { "MemberId" });
            DropIndex("dbo.MemberProgresses", new[] { "TrainerId" });
            DropIndex("dbo.MemberProgresses", new[] { "MemberId" });
            DropIndex("dbo.TrainerAvailabilities", new[] { "TrainerId" });
            DropIndex("dbo.WorkoutSessionLogs", new[] { "TrainerId" });
            DropIndex("dbo.WorkoutSessionLogs", new[] { "WorkoutProgramId" });
            DropIndex("dbo.WorkoutSessionLogs", new[] { "MemberId" });
            DropIndex("dbo.DailyProgressNotes", new[] { "MemberId" });
            DropIndex("dbo.DailyProgressNotes", new[] { "WorkoutSessionLogId" });
            DropIndex("dbo.ComplaintTickets", new[] { "MemberId" });
            DropIndex("dbo.CafeRedemptions", new[] { "CafeProductId" });
            DropIndex("dbo.CafeRedemptions", new[] { "MemberId" });
            DropIndex("dbo.Attendances", new[] { "MemberId" });
            DropTable("dbo.WeeklyProgressLogs");
            DropTable("dbo.WalletTransactions");
            DropTable("dbo.UserBadges");
            DropTable("dbo.SupplierReturns");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.StoreStaffs");
            DropTable("dbo.ShoppingCartItems");
            DropTable("dbo.WorkoutSessions");
            DropTable("dbo.SessionExerciseLogs");
            DropTable("dbo.StoreOrderCollectionEvents");
            DropTable("dbo.StoreOrders");
            DropTable("dbo.StoreOrderItems");
            DropTable("dbo.ReturnRequests");
            DropTable("dbo.RepairRequests");
            DropTable("dbo.RepairAssignments");
            DropTable("dbo.RefundRequests");
            DropTable("dbo.Referrals");
            DropTable("dbo.PointsTransactions");
            DropTable("dbo.Payments");
            DropTable("dbo.Notifications");
            DropTable("dbo.Recipes");
            DropTable("dbo.MealPlans");
            DropTable("dbo.MealPlanItems");
            DropTable("dbo.MealLogs");
            DropTable("dbo.MealLogItems");
            DropTable("dbo.LoyaltyAccounts");
            DropTable("dbo.LockerReservations");
            DropTable("dbo.LockerFaultReports");
            DropTable("dbo.LockerFaults");
            DropTable("dbo.Lockers");
            DropTable("dbo.LockerAccessOverrides");
            DropTable("dbo.InventoryMovements");
            DropTable("dbo.Vendors");
            DropTable("dbo.Maintenances");
            DropTable("dbo.Repairs");
            DropTable("dbo.Inspections");
            DropTable("dbo.FreezeRequests");
            DropTable("dbo.Foods");
            DropTable("dbo.FoodPortions");
            DropTable("dbo.FitnessProgressLogs");
            DropTable("dbo.FitnessProfiles");
            DropTable("dbo.Feedbacks");
            DropTable("dbo.WorkoutCategories");
            DropTable("dbo.WorkoutPlans");
            DropTable("dbo.WorkoutPlanExercises");
            DropTable("dbo.Exercises");
            DropTable("dbo.EWallets");
            DropTable("dbo.Technicians");
            DropTable("dbo.Equipments");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrderItems");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.SupplierDeliveries");
            DropTable("dbo.Promotions");
            DropTable("dbo.Products");
            DropTable("dbo.DeliveryItems");
            DropTable("dbo.ProgramExercises");
            DropTable("dbo.WorkoutPrograms");
            DropTable("dbo.TrainingSessions");
            DropTable("dbo.MemberTrainers");
            DropTable("dbo.MemberProgresses");
            DropTable("dbo.TrainerAvailabilities");
            DropTable("dbo.Trainers");
            DropTable("dbo.WorkoutSessionLogs");
            DropTable("dbo.DailyProgressNotes");
            DropTable("dbo.ComplaintTickets");
            DropTable("dbo.CafeRedemptions");
            DropTable("dbo.CafeProducts");
            DropTable("dbo.MemberForms");
            DropTable("dbo.Attendances");
        }
    }
}
