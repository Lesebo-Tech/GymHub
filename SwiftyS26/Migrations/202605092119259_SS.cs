namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SS : DbMigration
    {
        public override void Up()
        {
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.DailyProgressNotes", "WorkoutSessionLogId", "dbo.WorkoutSessionLogs");
            DropForeignKey("dbo.DailyProgressNotes", "MemberId", "dbo.MemberForms");
            DropIndex("dbo.DailyProgressNotes", new[] { "MemberId" });
            DropIndex("dbo.DailyProgressNotes", new[] { "WorkoutSessionLogId" });
            DropTable("dbo.DailyProgressNotes");
        }
    }
}
