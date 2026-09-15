namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class auditLogTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AuditLogs",
                c => new
                {
                    Id = c.Long(nullable: false, identity: true),
                    TableName = c.String(maxLength: 20),
                    RecordId = c.String(),
                    Action = c.String(),
                    OldValue = c.String(),
                    NewValue = c.String(),
                    UserId = c.String(maxLength: 36),
                    DateLogged = c.DateTime(nullable: false),
                })
                .PrimaryKey(t => t.Id)
                .Index(t => new { t.UserId, t.TableName });

            DropColumn("dbo.Products", "ModifiedBy");
            DropColumn("dbo.Products", "ModifiedAt");
            DropColumn("dbo.Products", "CreatedBy");
            DropColumn("dbo.Products", "CreatedAt");
        }

        public override void Down()
        {
            AddColumn("dbo.Products", "CreatedAt", c => c.DateTime(nullable: false));
            AddColumn("dbo.Products", "CreatedBy", c => c.String(maxLength: 100));
            AddColumn("dbo.Products", "ModifiedAt", c => c.DateTime());
            AddColumn("dbo.Products", "ModifiedBy", c => c.String(maxLength: 100));
            DropIndex("dbo.AuditLogs", new[] { "UserId", "TableName" });
            DropTable("dbo.AuditLogs");
        }
    }
}
