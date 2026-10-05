using POS.Forms;
using System;
using POS.Composition;
using POS.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace POS
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                DatabaseMigrationService.ApplyPendingMigrations();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "The database could not be updated. The application will close.\n\n" +
                    exception.Message,
                    "Database Migration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var composition = new ApplicationCompositionRoot();
            while (true)
            {
                using (var signIn = composition.CreateSignIn())
                {
                    if (signIn.ShowDialog() != DialogResult.OK)
                        return;
                }

                using (var main = composition.CreateMainForm())
                {
                    Application.Run(main);
                    if (!main.LogoutRequested)
                        return;
                }
            }
        }
    }
}
