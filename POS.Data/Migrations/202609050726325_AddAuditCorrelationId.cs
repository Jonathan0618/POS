namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddAuditCorrelationId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AuditLogs", "CorrelationId", c => c.Guid());
            CreateIndex("dbo.AuditLogs", "CorrelationId");
        }
        
        public override void Down()
        {
            DropIndex("dbo.AuditLogs", new[] { "CorrelationId" });
            DropColumn("dbo.AuditLogs", "CorrelationId");
        }
    }
}
