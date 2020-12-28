using System;
using System.Collections.Generic;
using System.Drawing;
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
using Accord.Video.FFMPEG;
using Microsoft.Win32;

namespace VideoFrameSimilaritySort
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //int[,] a = new int[1811939328, 2];
        }

        private void loadVideo(string path)
        {
            try
            {

                VideoFileReader reader = new VideoFileReader();
                reader.Open(path);

                Console.WriteLine("width:  " + reader.Width);
                Console.WriteLine("height: " + reader.Height);
                Console.WriteLine("fps:    " + reader.FrameRate);
                Console.WriteLine("codec:  " + reader.CodecName);

                while (true) {
                    using (Bitmap videoFrame = reader.ReadVideoFrame())
                    {
                        if (videoFrame == null)
                            break;

                            // process the frame here
                       
                    }
                }

                reader.Close();

            } catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void loadVideo_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                loadVideo(ofd.FileName);
            }
        }
    }
}
