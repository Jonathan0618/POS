namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddCategoryActiveState : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddCategoryActiveState));
        string IMigrationMetadata.Id => "202609060900000_AddCategoryActiveState";
        string IMigrationMetadata.Source => null;
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
