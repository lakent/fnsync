using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FnSync
{
    public class UserControlExtension : UserControl
    {
        public UserControlExtension()
        {
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, __) =>
            {
                //LoadDataContext();
                Window window = Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += (___, ____) => OnClosing();
                }
            };
        }

        protected virtual void LoadDataContext()
        {

        }

        private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is UserControlExtension uce))
                return;

            uce.LoadDataContext();
        }

        protected virtual void OnClosing()
        {

        }
    }

}
