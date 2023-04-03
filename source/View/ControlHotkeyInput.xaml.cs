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

namespace FnSync.View
{
    /// <summary>
    /// Interaction logic for ControlHotkeyInput.xaml
    /// </summary>

    public class Hotkey
    {
        public readonly static Hotkey Empty = new();

        public Key Key { get; }

        public ModifierKeys Modifiers { get; }

        public bool ModifierCtrl => Modifiers.HasFlag(ModifierKeys.Control);
        public bool ModifierShift => Modifiers.HasFlag(ModifierKeys.Shift);
        public bool ModifierAlt => Modifiers.HasFlag(ModifierKeys.Alt);
        public bool ModifierWin => Modifiers.HasFlag(ModifierKeys.Windows);

        private Hotkey()
        {
            this.Key = Key.None;
            this.Modifiers = ModifierKeys.None;
        }

        public Hotkey(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public Hotkey(string text)
        {
            ModifierKeys modifiers = new();
            Key key = Key.None;

            string[] keySequences = text.Split(new char[] { '+' });

            foreach (string k in keySequences)
            {
                string keyTrimmed = k.Trim();
                if (k.Equals("ctrl", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Control;
                }
                else if (k.Equals("shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Shift;
                }
                else if (k.Equals("alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Alt;
                }
                else if (k.Equals("win", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModifierKeys.Windows;
                }
                else
                {
                    key = (Key)(new KeyConverter().ConvertFromString(keyTrimmed) ?? Key.None);
                }
            }

            /*
            if (key == Key.None)
            {
                throw new Exception($"Invalid key combination {text}");
            }
            */

            this.Key = key;
            this.Modifiers = modifiers;
        }

        public override string ToString()
        {
            StringBuilder ret = new();

            if (ModifierCtrl)
            {
                ret.Append("Ctrl + ");
            }

            if (ModifierShift)
            {
                ret.Append("Shift + ");
            }

            if (ModifierAlt)
            {
                ret.Append("Alt + ");
            }

            if (ModifierWin)
            {
                ret.Append("Win + ");
            }

            ret.Append(Key.ToString());

            return ret.ToString();
        }
    }

    public partial class ControlHotkeyInput : UserControl
    {
        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.Register(
                nameof(Hotkey),
                typeof(Hotkey),
                typeof(ControlHotkeyInput),
                new FrameworkPropertyMetadata(
                    default(Hotkey),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
                )
            );

        public Hotkey Hotkey
        {
            get => (Hotkey)GetValue(HotkeyProperty);
            set => SetValue(HotkeyProperty, value);
        }

        public ControlHotkeyInput()
        {
            InitializeComponent();
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            ModifierKeys modifiers = Keyboard.Modifiers;
            Key key = e.Key;

            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (modifiers == ModifierKeys.None &&
                (key == Key.Delete || key == Key.Back || key == Key.Escape))
            {
                Hotkey = Hotkey.Empty;
                return;
            }

            if (key == Key.LeftCtrl ||
                key == Key.RightCtrl ||
                key == Key.LeftAlt ||
                key == Key.RightAlt ||
                key == Key.LeftShift ||
                key == Key.RightShift ||
                key == Key.LWin ||
                key == Key.RWin ||
                key == Key.Clear ||
                key == Key.OemClear ||
                key == Key.Apps)
            {
                return;
            }

            // Update the value
            Hotkey = new Hotkey(key, modifiers);
        }
    }
}
