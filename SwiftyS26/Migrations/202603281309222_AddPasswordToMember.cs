updanamespace SwiftyS26.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPasswordToMember : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.MemberForms", "PasswordHash", c => c.String(nullable: false, maxLength: 255));
        }
        
        public override void Down()
        {
            DropColumn("dbo.MemberForms", "PasswordHash");
        }
    }
}
