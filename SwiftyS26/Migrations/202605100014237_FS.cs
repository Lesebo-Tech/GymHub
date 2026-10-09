namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FS : DbMigration
    {
        public override void Up()
        {
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
            DropIndex("dbo.WeeklyProgressLogs", new[] { "MemberId" });
            DropTable("dbo.WeeklyProgressLogs");
        }
    }
}
