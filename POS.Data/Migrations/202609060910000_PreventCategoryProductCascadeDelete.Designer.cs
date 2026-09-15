namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;

    public partial class PreventCategoryProductCascadeDelete : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(PreventCategoryProductCascadeDelete));
        string IMigrationMetadata.Id => "202609060910000_PreventCategoryProductCascadeDelete";
        string IMigrationMetadata.Source => null;
        string IMigrationMetadata.Target => Resources.GetString("Target");
    }
}
