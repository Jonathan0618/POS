namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPurchaseReturns : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PurchaseReturnLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PurchaseReturnId = c.Int(nullable: false),
                        GoodsReceiptLineId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.GoodsReceiptLines", t => t.GoodsReceiptLineId)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseReturns", t => t.PurchaseReturnId, cascadeDelete: true)
                .Index(t => t.PurchaseReturnId)
                .Index(t => t.GoodsReceiptLineId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.PurchaseReturns",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ReturnNumber = c.String(nullable: false, maxLength: 30),
                        GoodsReceiptId = c.Int(nullable: false),
                        SupplierId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 250),
                        ReturnedUtc = c.DateTime(nullable: false),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.GoodsReceipts", t => t.GoodsReceiptId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.ReturnNumber, unique: true)
                .Index(t => t.GoodsReceiptId)
                .Index(t => t.SupplierId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PurchaseReturnLines", "PurchaseReturnId", "dbo.PurchaseReturns");
            DropForeignKey("dbo.PurchaseReturns", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.PurchaseReturns", "GoodsReceiptId", "dbo.GoodsReceipts");
            DropForeignKey("dbo.PurchaseReturnLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseReturnLines", "GoodsReceiptLineId", "dbo.GoodsReceiptLines");
            DropIndex("dbo.PurchaseReturns", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseReturns", new[] { "GoodsReceiptId" });
            DropIndex("dbo.PurchaseReturns", new[] { "ReturnNumber" });
            DropIndex("dbo.PurchaseReturnLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseReturnLines", new[] { "GoodsReceiptLineId" });
            DropIndex("dbo.PurchaseReturnLines", new[] { "PurchaseReturnId" });
            DropTable("dbo.PurchaseReturns");
            DropTable("dbo.PurchaseReturnLines");
        }
    }
}
