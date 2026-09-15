namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAuditRegisterAttribution : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AuditLogs", "RegisterStationId", c => c.Int());
            AddColumn("dbo.AuditLogs", "RegisterCode", c => c.String(maxLength: 30));
            CreateIndex("dbo.AuditLogs", new[] { "RegisterStationId", "DateLogged" });
        }
        
        public override void Down()
        {
            DropIndex("dbo.AuditLogs", new[] { "RegisterStationId", "DateLogged" });
            DropColumn("dbo.AuditLogs", "RegisterCode");
            DropColumn("dbo.AuditLogs", "RegisterStationId");
        }
    }
}
