using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : AdonisWindow
    {
        public static WindowMain CurrentWindow { get; private set; } = null;

        public static void NewOne()
        {
            NewOne(null);
        }

        public static void NewOne(string id)
        {
            App.FakeDispatcher.Invoke(() =>
            {
                if (CurrentWindow == null)
                {
                    new WindowMain(id).Show();
                }
                else
                {
                    _ = CurrentWindow.Activate();
                }

                return null;
            });
        }

        public WindowMain() : this(null) { }

        public WindowMain(string id)
        {
            CurrentWindow = this;
            this.DataContext = new ViewModel.WindowMain.ViewModel(id);
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CurrentWindow = null;
            if (this.DataContext is IDisposable Disposable)
            {
                Disposable.Dispose();
            }
        }
    }
}
