namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class pass : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.MemberForms", "FailedLoginAttempts", c => c.Int(nullable: false));
            AddColumn("dbo.MemberForms", "LockoutEnd", c => c.DateTime());
            AddColumn("dbo.MemberForms", "LastLoginAt", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.MemberForms", "LastLoginAt");
            DropColumn("dbo.MemberForms", "LockoutEnd");
            DropColumn("dbo.MemberForms", "FailedLoginAttempts");
        }
    }
}
