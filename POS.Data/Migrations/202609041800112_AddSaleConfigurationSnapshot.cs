namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSaleConfigurationSnapshot : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Sales", "CurrencyCode", c => c.String(maxLength: 3));
            AddColumn("dbo.Sales", "TaxName", c => c.String(maxLength: 100));
            AddColumn("dbo.Sales", "TaxInclusive", c => c.Boolean(nullable: false));
            AddColumn("dbo.Sales", "MoneyDecimalPlaces", c => c.Int(nullable: false, defaultValue: 2));
            AddColumn("dbo.Sales", "RoundingMethod", c => c.String(maxLength: 40, defaultValue: "AwayFromZero"));
            AddColumn("dbo.StoreSettings", "MoneyDecimalPlaces", c => c.Int(nullable: false, defaultValue: 2));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StoreSettings", "MoneyDecimalPlaces");
            DropColumn("dbo.Sales", "RoundingMethod");
            DropColumn("dbo.Sales", "MoneyDecimalPlaces");
            DropColumn("dbo.Sales", "TaxInclusive");
            DropColumn("dbo.Sales", "TaxName");
            DropColumn("dbo.Sales", "CurrencyCode");
        }
    }
}
