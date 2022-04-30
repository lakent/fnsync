using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    /// Interaction logic for ControlConnectionByQR.xaml
    /// </summary>
    public partial class ControlConnectionByQR : UserControlExtension
    {
        public ControlConnectionByQR()
        {
            InitializeComponent();
            RefreshQrCode();
        }

        private string GetAdditionalIPs()
        {
            string[] ips = MainConfig.Config.AdditionalIPs.Split(new char[] { ';', '|' });
            StringBuilder Builder = new StringBuilder();

            foreach (string ip in ips)
            {
                string ipTrimed = ip.Trim();
                Builder.Append(ipTrimed).Append("|");
            }

            if (Builder.EndsWith("|"))
            {
                Builder.Remove(Builder.Length - 1, 1);
                Builder.Insert(0, "|");
            }

            return Builder.ToString();
        }

        private void RefreshQrCode()
        {
            string token = Guid.NewGuid().ToString();
            PhoneListener.Singleton.Code = token;

            JObject helloJson = HandShake.MakeHelloJson(
                false,
                token,
                null
            );

            helloJson["ips"] = Utils.GetAllInterface(false) + GetAdditionalIPs();
            /*
            helloJson["i"] = Utils.GetAllInterface(false);
            helloJson["l"] = Utils.GetAllInterface(true);
            */

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(
                helloJson.ToString(Newtonsoft.Json.Formatting.None),
                QRCodeGenerator.ECCLevel.L
            );
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);

            using (MemoryStream memory = new MemoryStream())
            {
                qrCodeImage.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                QRCode.Source = bitmapimage;
            }
        }

        private void QRCode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RefreshQrCode();
        }
    }
}
