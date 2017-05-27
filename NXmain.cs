using System;
using System.Collections.Generic;
using System.Text;
using NXOpen;
using NXOpenUI;
using NXOpen.UF;
using NXOpen.Features;
using NXOpen.GeometricUtilities;
using System.Windows.Forms;
using MapWindows;

namespace ProcessCompare
{
    public class NXMain
    {
        // class members
        private static Session theSession;
        private static UI theUI;
        public static bool isDisposeCalled;
        public static int Main(string[] args)
        {
            int retValue = 0;
            try
            {
                theSession = Session.GetSession();
                theUI = UI.GetUI();
                Part workPart = theSession.Parts.Work;
                Part displayPart = theSession.Parts.Display;
                string ButtonName = args[0];
                if (ButtonName.Equals("MapDimension"))
                {
                    MapCreate newmap = new MapCreate();
                    newmap.Show();
                    //Form1 newform = new Form1();
                    //newform.Show();
                }
            }
            catch (NXOpen.NXException error)
            {
                // ---- Enter your exception handling code here -----
                MessageBox.Show(error.Message, " 错 误 ", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
            return retValue;
        }
    }
}