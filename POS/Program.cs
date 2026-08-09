using POS.Forms;
using System;
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

            using (var signIn = new SignIn())
            {
                if (signIn.ShowDialog() == DialogResult.OK)
                {
                    Application.Run(new mainfrm());
                }
            }
        }
    }
}
