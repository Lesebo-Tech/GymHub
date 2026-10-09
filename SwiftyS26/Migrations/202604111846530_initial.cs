namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initial : DbMigration
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
                        ResetOtpCode = c.String(maxLength: 6),
                        ResetOtpGeneratedAt = c.DateTime(),
                        MembershipExpiry = c.DateTime(),
                    })
                .PrimaryKey(t => t.MemberId);
            
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.RefundRequests", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Payments", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.FreezeRequests", "MemberId", "dbo.MemberForms");
            DropForeignKey("dbo.Attendances", "MemberId", "dbo.MemberForms");
            DropIndex("dbo.RefundRequests", new[] { "MemberId" });
            DropIndex("dbo.Payments", new[] { "MemberId" });
            DropIndex("dbo.FreezeRequests", new[] { "MemberId" });
            DropIndex("dbo.Attendances", new[] { "MemberId" });
            DropTable("dbo.RefundRequests");
            DropTable("dbo.Payments");
            DropTable("dbo.FreezeRequests");
            DropTable("dbo.MemberForms");
            DropTable("dbo.Attendances");
        }
    }
}
