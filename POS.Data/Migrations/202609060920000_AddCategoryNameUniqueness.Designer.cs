namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddCategoryNameUniqueness : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddCategoryNameUniqueness));
        string IMigrationMetadata.Id => "202609060920000_AddCategoryNameUniqueness";
        string IMigrationMetadata.Source => null;
        // SQL-only computed key/index; the EF model is unchanged.
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
