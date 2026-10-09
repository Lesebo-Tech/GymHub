namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddWorkoutSessionLog : DbMigration
    {
        public override void Up()
        {
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
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.WorkoutSessionLogs", "WorkoutProgramId", "dbo.WorkoutPrograms");
            DropForeignKey("dbo.WorkoutSessionLogs", "TrainerId", "dbo.Trainers");
            DropForeignKey("dbo.WorkoutSessionLogs", "MemberId", "dbo.MemberForms");
            DropIndex("dbo.WorkoutSessionLogs", new[] { "TrainerId" });
            DropIndex("dbo.WorkoutSessionLogs", new[] { "WorkoutProgramId" });
            DropIndex("dbo.WorkoutSessionLogs", new[] { "MemberId" });
            DropTable("dbo.WorkoutSessionLogs");
        }
    }
}
