namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateMemberTrainer : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.MemberForms", "MemberTrainer_Id", "dbo.MemberTrainers");
            DropIndex("dbo.MemberForms", new[] { "MemberTrainer_Id" });
            CreateIndex("dbo.MemberTrainers", "MemberId");
            AddForeignKey("dbo.MemberTrainers", "MemberId", "dbo.MemberForms", "MemberId", cascadeDelete: true);
            DropColumn("dbo.MemberForms", "MemberTrainer_Id");
        }
        
        public override void Down()
        {
            AddColumn("dbo.MemberForms", "MemberTrainer_Id", c => c.Int());
            DropForeignKey("dbo.MemberTrainers", "MemberId", "dbo.MemberForms");
            DropIndex("dbo.MemberTrainers", new[] { "MemberId" });
            CreateIndex("dbo.MemberForms", "MemberTrainer_Id");
            AddForeignKey("dbo.MemberForms", "MemberTrainer_Id", "dbo.MemberTrainers", "Id");
        }
    }
}
