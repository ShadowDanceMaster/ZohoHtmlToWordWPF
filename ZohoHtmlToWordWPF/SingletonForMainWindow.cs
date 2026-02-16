using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ZohoHtmlToWordWPF
{
    internal class SingletonForMainWindow
    {
        static MainWindow mw;
        private SingletonForMainWindow()
        {
            mw = Application.Current.MainWindow as MainWindow;
        }
        public static MainWindow GetInstance()
        {
            if (mw is null)
            {
                new SingletonForMainWindow();
            }
            return mw;
        }
    }
}
