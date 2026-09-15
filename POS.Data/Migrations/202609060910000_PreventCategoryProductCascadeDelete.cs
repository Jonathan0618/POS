namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class PreventCategoryProductCascadeDelete : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            AddForeignKey("dbo.Products", "CategoryId", "dbo.Categories", "Id", cascadeDelete: false);
        }

        public override void Down()
        {
            // Rollback restores the previous cascading behavior; prefer forward recovery.
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            AddForeignKey("dbo.Products", "CategoryId", "dbo.Categories", "Id", cascadeDelete: true);
        }
    }
}
