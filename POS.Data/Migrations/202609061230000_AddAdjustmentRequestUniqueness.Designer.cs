namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class AddAdjustmentRequestUniqueness : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddAdjustmentRequestUniqueness));
        string IMigrationMetadata.Id => "202609061230000_AddAdjustmentRequestUniqueness";
        string IMigrationMetadata.Source => null;
        // SQL-only filtered index; the EF model is unchanged.
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
