namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCategoryActiveState : DbMigration
    {
        public override void Up()
        {
            // Preserve availability of every existing category.
            AddColumn("dbo.Categories", "IsActive", c => c.Boolean(nullable: false, defaultValue: true));
        }

        public override void Down()
        {
            // Rolling back removes category lifecycle state; export it before rollback.
            DropColumn("dbo.Categories", "IsActive");
        }
    }
}
