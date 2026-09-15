namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddProductIdentifierUniqueness : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddProductIdentifierUniqueness));

        string IMigrationMetadata.Id => "202609051800000_AddProductIdentifierUniqueness";
        string IMigrationMetadata.Source => null;
        // This SQL-only migration retains the preceding EF model snapshot.
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
