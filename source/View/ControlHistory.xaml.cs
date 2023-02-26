using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FnSync
{
    /// <summary>
    /// Interaction logic for ControlHistory.xaml
    /// </summary>
    public partial class ControlHistory : UserControlExtension
    {
        private SavedPhones.HistoryReader? Reader = null;

        public ControlHistory()
        {
            InitializeComponent();
        }

        protected override void OnDataContextLoaded()
        {
            HistoryDateChoose.ItemsSource = null;
            Reader = null;

            if (DataContext is SavedPhones.Phone phone)
            {
                Reader = new SavedPhones.HistoryReader(phone.Id);

                List<String> list = Reader.AllDates;
                HistoryDateChoose.ItemsSource = list;

                if (list.Any())
                {
                    HistoryDateChoose.SelectedIndex = list.Count - 1;
                    DeleteHistory.IsEnabled = true;

                    return;
                }
            }

            HistoryBox.Document.Blocks.Clear();
            HistoryBox.AppendText((string)FindResource("NoHistory"));
            DeleteHistory.IsEnabled = false;
        }

        private Paragraph MakeParagraphForNotification(JObject ln)
        {
            string time = DateTimeOffset.FromUnixTimeMilliseconds(ln.OptLong("time", 0))
                .LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

            string appName = ln.OptString("appname") ?? ln.OptString("pkgname") ?? "(Unknown)";

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(
                new Bold(new Run(
                    $"{time} {appName}\n"
                ))
            );
            paragraph.Inlines.Add(
                new Run(
                    $"{ln.OptString("title", "")}\n" +
                    $"{ln.OptString("text", "")}"
                )
            );
            paragraph.Tag = ln;

            return paragraph;
        }

        private Paragraph MakeParagraphForTextCast(JObject ln)
        {
            string time = DateTimeOffset.FromUnixTimeMilliseconds(ln.OptLong("time", 0))
                .LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(
                new Bold(new Run(
                    $"{time} {Casting.TEXT_RECEIVED}\n"
                ))
            );
            paragraph.Inlines.Add(
                new Run(
                    $"{ln.OptString("text", "")}"
                )
            );
            paragraph.Tag = ln;

            return paragraph;
        }

        private void HistoryDateChoose_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;

            if (Reader == null || HistoryDateChoose.SelectedItem is not string selectedDate)
            {
                return;
            }

            FlowDocument flowDocument = new();

            try
            {
                Reader.SetCurrent(selectedDate);
                JObject? line;
                while ((line = Reader.ReadLine()) != null)
                {
                    Paragraph paragraph;

                    switch (line.OptString(PhoneClient.MSG_TYPE_KEY))
                    {
                        case Casting.MSG_TYPE_TEXT_CAST:
                            paragraph = MakeParagraphForTextCast(line);
                            break;

                        case PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION:
                            paragraph = MakeParagraphForNotification(line);
                            break;

                        default:
                            goto case PhoneMessageCenter.MSG_TYPE_NEW_NOTIFICATION;
                    }

                    flowDocument.Blocks.Add(paragraph);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                Reader.Dispose();
            }

            HistoryBox.Document = flowDocument;
            HistoryBox.ScrollToEnd();
        }

        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDateChoose.DataContext == null)
            {
                return;
            }

            if (MessageBox.Show(
                    (string)FindResource("ConfirmDeletion"),
                    (string)FindResource("Delete"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                ) == MessageBoxResult.Yes)
            {
                if (Reader != null)
                {
                    Reader.Clear();
                    RefreshHistory_Click(null!, null!);
                }
            }
        }

        private void RefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            OnDataContextLoaded();
        }
    }

    static class RichTextBoxExtension
    {
        public static object? GetSelectedHistory(this RichTextBox box)
        {
            if (!box.Selection.IsEmpty)
            {
                List<JObject> ret = new();

                Paragraph begin = box.Selection.Start.Paragraph;
                Paragraph end = box.Selection.End.Paragraph;

                if (begin == null || end == null)
                {
                    return null;
                }

                foreach (Block block in box.Document.Blocks)
                {
                    if (ret.Any() || block == begin)
                    {
                        if (block.Tag is JObject json)
                        {
                            ret.Add(json);
                        }
                    }

                    if (block == end)
                    {
                        break;
                    }
                }

                if (ret.Any())
                {
                    return ret;
                }
                else
                {
                    return null;
                }
            }
            else if (box.CaretPosition.Paragraph != null)
            {
                return box.CaretPosition.Paragraph.Tag;
            }
            else
            {
                return null;
            }
        }
    }

    class MarkAsImportantCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private readonly RichTextBox box;

        public MarkAsImportantCommand(RichTextBox box)
        {
            this.box = box;
        }

        public bool CanExecute(object? parameter)
        {
            return !box.Selection.IsEmpty || box.CaretPosition.Paragraph != null;
        }

        public void Execute(object? parameter)
        {
            box.GetSelectedHistory();
        }
    }

}
