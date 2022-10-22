using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace FnSync
{
    public partial class ControlFolderList : UserControlExtension
    {
        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                "SelectedItem",
                typeof(object),
                typeof(ControlFolderList)
            );

        public ControlFolderList()
        {
            InitializeComponent();
        }

        protected override void OnClosing()
        {
        }

        private void ItemMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem item = sender as TreeViewItem;
            if (item != null)
            {
                item.IsSelected = true;
                e.Handled = true;
            }
        }

        private void FolderList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedItem = e.NewValue;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (!(sender is TreeViewItem item))
            {
                return;
            }

            item.BringIntoView();
            e.Handled = true;
        }
    }
}
