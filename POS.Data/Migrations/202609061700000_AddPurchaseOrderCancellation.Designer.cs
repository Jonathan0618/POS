namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddPurchaseOrderCancellation : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddPurchaseOrderCancellation));
        string IMigrationMetadata.Id => "202609061700000_AddPurchaseOrderCancellation";
        string IMigrationMetadata.Source => null;
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
