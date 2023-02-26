using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FnSync
{
    public abstract class UserControlExtension : UserControl
    {
        public UserControlExtension()
        {
            DataContextChanged += OnDataContextChanged;

#if DEBUG
            // https://stackoverflow.com/a/21317011/1968839
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
#endif
                Loaded += (_, __) =>
                {
                    //LoadDataContext();
                    Window window = Window.GetWindow(this);
                    if (window != null)
                    {
                        window.Closing += (___, ____) => OnClosing();
                    }
                };
#if DEBUG
            }
#endif
        }

        protected virtual void OnDataContextLoaded()
        {

        }

        private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not UserControlExtension uce)
                return;

            uce.OnDataContextLoaded();
        }

        public virtual void OnShow()
        {

        }

        protected virtual void OnClosing()
        {

        }

    }
}
