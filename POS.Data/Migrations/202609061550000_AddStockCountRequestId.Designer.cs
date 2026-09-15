namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddStockCountRequestId : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddStockCountRequestId));
        string IMigrationMetadata.Id => "202609061550000_AddStockCountRequestId";
        string IMigrationMetadata.Source => null;
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
