namespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class GymHubUpdate : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.MemberForms", "AzureFaceId");
        }
        
        public override void Down()
        {
            AddColumn("dbo.MemberForms", "AzureFaceId", c => c.String(maxLength: 100));
        }
    }
}
