namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FullOperationsFoundation : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CashierShifts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        RegisterStationId = c.Int(nullable: false),
                        CashierUserId = c.String(nullable: false, maxLength: 128),
                        Status = c.Int(nullable: false),
                        OpenedUtc = c.DateTime(nullable: false),
                        ClosedUtc = c.DateTime(),
                        OpeningCash = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExpectedCash = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CountedCash = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Variance = c.Decimal(nullable: false, precision: 18, scale: 2),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.RegisterStations", t => t.RegisterStationId, cascadeDelete: true)
                .Index(t => t.RegisterStationId);
            
            CreateTable(
                "dbo.CashMovements",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        CashierShiftId = c.Int(nullable: false),
                        MovementType = c.Int(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Reason = c.String(nullable: false, maxLength: 250),
                        UserId = c.String(nullable: false, maxLength: 128),
                        CreatedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.CashierShifts", t => t.CashierShiftId, cascadeDelete: true)
                .Index(t => t.CashierShiftId);
            
            CreateTable(
                "dbo.RegisterStations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 30),
                        Name = c.String(nullable: false, maxLength: 100),
                        PrinterName = c.String(maxLength: 200),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Code, unique: true);
            
            CreateTable(
                "dbo.Customers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 30),
                        Name = c.String(nullable: false, maxLength: 150),
                        Phone = c.String(maxLength: 50),
                        Email = c.String(maxLength: 100),
                        Address = c.String(maxLength: 300),
                        TaxIdentifier = c.String(maxLength: 50),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Code, unique: true);
            
            CreateTable(
                "dbo.GoodsReceiptLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        GoodsReceiptId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.GoodsReceipts", t => t.GoodsReceiptId, cascadeDelete: true)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .Index(t => t.GoodsReceiptId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.GoodsReceipts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ReceiptNumber = c.String(nullable: false, maxLength: 30),
                        SupplierId = c.Int(nullable: false),
                        PurchaseOrderId = c.Int(),
                        SupplierReference = c.String(maxLength: 50),
                        Status = c.Int(nullable: false),
                        ReceivedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.ReceiptNumber, unique: true);
            
            CreateTable(
                "dbo.InventoryBalances",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        QuantityOnHand = c.Int(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => t.ProductId, unique: true);
            
            CreateTable(
                "dbo.NumberSequences",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DocumentType = c.String(nullable: false, maxLength: 30),
                        Prefix = c.String(nullable: false, maxLength: 20),
                        NextNumber = c.Long(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.DocumentType, unique: true);
            
            CreateTable(
                "dbo.Payments",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        SaleId = c.Int(nullable: false),
                        TenderType = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExternalReference = c.String(maxLength: 100),
                        CreatedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Sales", t => t.SaleId)
                .Index(t => t.SaleId);
            
            CreateTable(
                "dbo.Sales",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SaleDate = c.DateTime(nullable: false),
                        ReceiptNumber = c.String(maxLength: 30),
                        Status = c.Int(nullable: false),
                        RegisterStationId = c.Int(),
                        CashierShiftId = c.Int(),
                        CustomerId = c.Int(),
                        CashierUserId = c.String(maxLength: 128),
                        Subtotal = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DiscountAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TaxAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        RoundingAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CashReceived = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Change = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.CashierShifts", t => t.CashierShiftId)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .ForeignKey("dbo.RegisterStations", t => t.RegisterStationId)
                .Index(t => t.RegisterStationId)
                .Index(t => t.CashierShiftId)
                .Index(t => t.CustomerId);
            
            CreateTable(
                "dbo.SaleItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SaleId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        ProductName = c.String(maxLength: 150),
                        Sku = c.String(maxLength: 50),
                        Barcode = c.String(maxLength: 50),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Subtotal = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CostPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DiscountAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TaxRate = c.Decimal(nullable: false, precision: 9, scale: 6),
                        TaxAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.Sales", t => t.SaleId)
                .Index(t => t.SaleId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.PurchaseOrderLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PurchaseOrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        OrderedQuantity = c.Int(nullable: false),
                        ReceivedQuantity = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.PurchaseOrders", t => t.PurchaseOrderId, cascadeDelete: true)
                .Index(t => t.PurchaseOrderId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.PurchaseOrders",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OrderNumber = c.String(nullable: false, maxLength: 30),
                        SupplierId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedUtc = c.DateTime(nullable: false),
                        OrderedUtc = c.DateTime(),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId, cascadeDelete: true)
                .Index(t => t.OrderNumber, unique: true)
                .Index(t => t.SupplierId);
            
            CreateTable(
                "dbo.Suppliers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 30),
                        Name = c.String(nullable: false, maxLength: 150),
                        ContactName = c.String(maxLength: 100),
                        Phone = c.String(maxLength: 50),
                        Email = c.String(maxLength: 100),
                        Address = c.String(maxLength: 300),
                        TaxIdentifier = c.String(maxLength: 50),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Code, unique: true);
            
            CreateTable(
                "dbo.SupplierProducts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SupplierId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        SupplierSku = c.String(maxLength: 50),
                        DefaultCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        LeadTimeDays = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId, cascadeDelete: true)
                .Index(t => new { t.SupplierId, t.ProductId }, unique: true);
            
            CreateTable(
                "dbo.RefundPayments",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        SaleReturnId = c.Int(nullable: false),
                        TenderType = c.Int(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExternalReference = c.String(maxLength: 100),
                        CreatedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.SaleReturns", t => t.SaleReturnId, cascadeDelete: true)
                .Index(t => t.SaleReturnId);
            
            CreateTable(
                "dbo.SaleReturns",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ReturnNumber = c.String(nullable: false, maxLength: 30),
                        SaleId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        Reason = c.String(nullable: false, maxLength: 250),
                        CreatedByUserId = c.String(maxLength: 128),
                        ApprovedByUserId = c.String(maxLength: 128),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CreatedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Sales", t => t.SaleId, cascadeDelete: true)
                .Index(t => t.ReturnNumber, unique: true)
                .Index(t => t.SaleId);
            
            CreateTable(
                "dbo.SaleReturnLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SaleReturnId = c.Int(nullable: false),
                        SaleItemId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        RefundAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Disposition = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.SaleItems", t => t.SaleItemId)
                .ForeignKey("dbo.SaleReturns", t => t.SaleReturnId, cascadeDelete: true)
                .Index(t => t.SaleReturnId)
                .Index(t => t.SaleItemId);
            
            CreateTable(
                "dbo.StockCountLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StockCountId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        ExpectedQuantity = c.Int(nullable: false),
                        CountedQuantity = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.StockCounts", t => t.StockCountId, cascadeDelete: true)
                .Index(t => t.StockCountId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.StockCounts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Status = c.Int(nullable: false),
                        CreatedByUserId = c.String(maxLength: 128),
                        CreatedUtc = c.DateTime(nullable: false),
                        PostedUtc = c.DateTime(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.StockMovements",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        MovementType = c.Int(nullable: false),
                        QuantityDelta = c.Int(nullable: false),
                        ReferenceType = c.String(maxLength: 40),
                        ReferenceId = c.String(maxLength: 50),
                        Reason = c.String(nullable: false, maxLength: 250),
                        UserId = c.String(maxLength: 128),
                        CreatedUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .Index(t => new { t.ProductId, t.CreatedUtc });
            
            CreateTable(
                "dbo.StoreSettings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StoreName = c.String(nullable: false, maxLength: 150),
                        Address = c.String(maxLength: 300),
                        Phone = c.String(maxLength: 50),
                        Email = c.String(maxLength: 100),
                        TaxIdentifier = c.String(maxLength: 50),
                        CurrencyCode = c.String(nullable: false, maxLength: 3),
                        TimeZoneId = c.String(nullable: false, maxLength: 100),
                        ReceiptFooter = c.String(maxLength: 500),
                        DefaultTaxRateId = c.Int(nullable: false),
                        AllowNegativeStock = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.TaxRates",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Rate = c.Decimal(nullable: false, precision: 9, scale: 6),
                        IsInclusive = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        EffectiveFromUtc = c.DateTime(nullable: false),
                        EffectiveToUtc = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.Products", "Sku", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.StockCountLines", "StockCountId", "dbo.StockCounts");
            DropForeignKey("dbo.StockCountLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.SaleReturns", "SaleId", "dbo.Sales");
            DropForeignKey("dbo.RefundPayments", "SaleReturnId", "dbo.SaleReturns");
            DropForeignKey("dbo.SaleReturnLines", "SaleReturnId", "dbo.SaleReturns");
            DropForeignKey("dbo.SaleReturnLines", "SaleItemId", "dbo.SaleItems");
            DropForeignKey("dbo.PurchaseOrders", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierProducts", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseOrderLines", "PurchaseOrderId", "dbo.PurchaseOrders");
            DropForeignKey("dbo.PurchaseOrderLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Payments", "SaleId", "dbo.Sales");
            DropForeignKey("dbo.SaleItems", "SaleId", "dbo.Sales");
            DropForeignKey("dbo.SaleItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Sales", "RegisterStationId", "dbo.RegisterStations");
            DropForeignKey("dbo.Sales", "CustomerId", "dbo.Customers");
            DropForeignKey("dbo.Sales", "CashierShiftId", "dbo.CashierShifts");
            DropForeignKey("dbo.InventoryBalances", "ProductId", "dbo.Products");
            DropForeignKey("dbo.GoodsReceiptLines", "ProductId", "dbo.Products");
            DropForeignKey("dbo.GoodsReceiptLines", "GoodsReceiptId", "dbo.GoodsReceipts");
            DropForeignKey("dbo.CashierShifts", "RegisterStationId", "dbo.RegisterStations");
            DropForeignKey("dbo.CashMovements", "CashierShiftId", "dbo.CashierShifts");
            DropIndex("dbo.StockMovements", new[] { "ProductId", "CreatedUtc" });
            DropIndex("dbo.StockCountLines", new[] { "ProductId" });
            DropIndex("dbo.StockCountLines", new[] { "StockCountId" });
            DropIndex("dbo.SaleReturnLines", new[] { "SaleItemId" });
            DropIndex("dbo.SaleReturnLines", new[] { "SaleReturnId" });
            DropIndex("dbo.SaleReturns", new[] { "SaleId" });
            DropIndex("dbo.SaleReturns", new[] { "ReturnNumber" });
            DropIndex("dbo.RefundPayments", new[] { "SaleReturnId" });
            DropIndex("dbo.SupplierProducts", new[] { "SupplierId", "ProductId" });
            DropIndex("dbo.Suppliers", new[] { "Code" });
            DropIndex("dbo.PurchaseOrders", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseOrders", new[] { "OrderNumber" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "ProductId" });
            DropIndex("dbo.PurchaseOrderLines", new[] { "PurchaseOrderId" });
            DropIndex("dbo.SaleItems", new[] { "ProductId" });
            DropIndex("dbo.SaleItems", new[] { "SaleId" });
            DropIndex("dbo.Sales", new[] { "CustomerId" });
            DropIndex("dbo.Sales", new[] { "CashierShiftId" });
            DropIndex("dbo.Sales", new[] { "RegisterStationId" });
            DropIndex("dbo.Payments", new[] { "SaleId" });
            DropIndex("dbo.NumberSequences", new[] { "DocumentType" });
            DropIndex("dbo.InventoryBalances", new[] { "ProductId" });
            DropIndex("dbo.GoodsReceipts", new[] { "ReceiptNumber" });
            DropIndex("dbo.GoodsReceiptLines", new[] { "ProductId" });
            DropIndex("dbo.GoodsReceiptLines", new[] { "GoodsReceiptId" });
            DropIndex("dbo.Customers", new[] { "Code" });
            DropIndex("dbo.RegisterStations", new[] { "Code" });
            DropIndex("dbo.CashMovements", new[] { "CashierShiftId" });
            DropIndex("dbo.CashierShifts", new[] { "RegisterStationId" });
            DropColumn("dbo.Products", "Sku");
            DropTable("dbo.TaxRates");
            DropTable("dbo.StoreSettings");
            DropTable("dbo.StockMovements");
            DropTable("dbo.StockCounts");
            DropTable("dbo.StockCountLines");
            DropTable("dbo.SaleReturnLines");
            DropTable("dbo.SaleReturns");
            DropTable("dbo.RefundPayments");
            DropTable("dbo.SupplierProducts");
            DropTable("dbo.Suppliers");
            DropTable("dbo.PurchaseOrders");
            DropTable("dbo.PurchaseOrderLines");
            DropTable("dbo.SaleItems");
            DropTable("dbo.Sales");
            DropTable("dbo.Payments");
            DropTable("dbo.NumberSequences");
            DropTable("dbo.InventoryBalances");
            DropTable("dbo.GoodsReceipts");
            DropTable("dbo.GoodsReceiptLines");
            DropTable("dbo.Customers");
            DropTable("dbo.RegisterStations");
            DropTable("dbo.CashMovements");
            DropTable("dbo.CashierShifts");
        }
    }
}
