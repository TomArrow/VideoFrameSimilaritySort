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
using Accord.Audio;
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

        ByteImage[] loadedVideo;
        bool videoIsLoaded = false;
        int[] orderedVideo;
        bool videoIsOrdered = false;
        Accord.Math.Rational frameRate = 24;

        private async Task processVideo()
        {
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;
            await Task.Run(() =>
            {
                int frameCount = loadedVideo.Length;
                int[] sortedIndizi = new int[loadedVideo.Length];
                bool[] alreadyUsedFrames = new bool[loadedVideo.Length];
                alreadyUsedFrames[0] = true;

                int width = loadedVideo[0].width;
                int height = loadedVideo[0].height;
                int pixelCount = width * height;
                int pixelCountX3 = width * height*3;
                int stride = loadedVideo[0].stride;
                int channelMultiplier = 3;

                if(loadedVideo[0].pixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                {
                    channelMultiplier = 4;
                }

                Console.WriteLine(loadedVideo[0].pixelFormat);

                int currentFrame = 0;

                double[] frameDifferences = new double[frameCount];

                for (int currentIndex = 0; currentIndex<frameCount;currentIndex++) {// not to confuse with current frame. We're just tracking our progress here.

                    double smallestDifferenceImpreciseOpt = double.PositiveInfinity;

                    // Go through all frames, comparing them to the current frame
                    Parallel.For(0, frameCount, (compareFrame) =>
                    {
                        frameDifferences[compareFrame] = double.PositiveInfinity;
                        if (compareFrame == currentFrame) return; // No need to compare to itself.
                        if (alreadyUsedFrames[compareFrame] == true) return; // No need to go through already used frames

                        double thisFrameDifference = 0;
                        // Calculate difference
                        int baseIndex;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                baseIndex = stride * y + x * channelMultiplier;
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex] - loadedVideo[compareFrame].imageData[baseIndex]);
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 1] - loadedVideo[compareFrame].imageData[baseIndex + 1]);
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 2] - loadedVideo[compareFrame].imageData[baseIndex + 2]);
                            }
                            if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break; // fast skip for very different frames. Since this is multithreaded, this might not always be correct in the sense of always having the right number in smallestDifference, but might work as optimization.
                        }
                        frameDifferences[compareFrame] = thisFrameDifference / pixelCountX3;
                        if (frameDifferences[compareFrame] < smallestDifferenceImpreciseOpt)
                        {
                            smallestDifferenceImpreciseOpt = frameDifferences[compareFrame];
                        }
                        /*
                        if (compareFrame % 1000 == 0)
                        {
                            progress.Report("Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is "+currentFrame+" comparing to "+compareFrame);
                        }*/
                    });
                    /*
                    for (int compareFrame = 0; compareFrame < frameCount; compareFrame++)
                    {
                        frameDifferences[compareFrame] = double.PositiveInfinity;
                        if (compareFrame == currentFrame) continue; // No need to compare to itself.
                        if (alreadyUsedFrames[compareFrame] == true) continue; // No need to go through already used frames

                        double thisFrameDifference = 0;
                        // Calculate difference
                        int baseIndex;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                baseIndex = stride * y + x*channelMultiplier;
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex] - loadedVideo[compareFrame].imageData[baseIndex]);
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 1] - loadedVideo[compareFrame].imageData[baseIndex + 1]);
                                thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 2] - loadedVideo[compareFrame].imageData[baseIndex + 2]);
                            }
                            if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break; // fast skip for very different frames. Since this is multithreaded, this might not always be correct in the sense of always having the right number in smallestDifference, but might work as optimization.
                        }
                        frameDifferences[compareFrame] = thisFrameDifference / pixelCountX3;
                        if (frameDifferences[compareFrame] < smallestDifferenceImpreciseOpt)
                        {
                            smallestDifferenceImpreciseOpt = frameDifferences[compareFrame];
                        }
                    }*/

                    double smallestDifference = double.PositiveInfinity;
                    int smallestDifferenceFrame = -1;

                    for (int compareFrame = 0; compareFrame < frameCount; compareFrame++)
                    {
                        if (frameDifferences[compareFrame] < smallestDifference)
                        {
                            smallestDifference = frameDifferences[compareFrame];
                            smallestDifferenceFrame = compareFrame;
                        }
                    }


                        

                    progress.Report("Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is " + currentFrame);

                    if(smallestDifferenceFrame != -1)
                    {
                        sortedIndizi[currentIndex] = smallestDifferenceFrame;
                        currentFrame = smallestDifferenceFrame;
                        alreadyUsedFrames[smallestDifferenceFrame] = true;
                    }
                }



                orderedVideo = sortedIndizi;
            });
            status_txt.Text = "Completed.";
            videoIsLoaded = true;
            loadVideo_button.IsEnabled = true;
            processVideo_button.IsEnabled = true;
            saveSortedFrameList_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = true;
        }

        private async Task loadVideoAsync(string path)
        {
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;
            await Task.Run(() =>
            {
                long frameCount;
                try
                {

                    VideoFileReader reader = new VideoFileReader();
                    reader.Open(path);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */

                    frameCount = reader.FrameCount;

                    int currentFrame = 0;

                    loadedVideo = new ByteImage[frameCount];
                    frameRate = reader.FrameRate;


                    while (true)
                    {

                        using (Bitmap videoFrame = reader.ReadVideoFrame())
                        {
                            if (videoFrame == null)
                                break;

                            loadedVideo[currentFrame] = Helpers.BitmapToByteArray(videoFrame);

                            if(currentFrame % 1000 == 0)
                            {
                                progress.Report("Loading video: "+currentFrame+"/"+frameCount+" frames");
                            }


                            currentFrame++;
                            // process the frame here

                        }
                    }

                    reader.Close();

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

            });
            status_txt.Text = "Completed.";
            videoIsLoaded = true;
            processVideo_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = false;
            saveSortedFrameList_button.IsEnabled = false;

        }

        

        private async Task saveVideoAsync(string path)
        {
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;
            await Task.Run(() =>
            {
                long frameCount;
                try
                {

                    VideoFileWriter writer = new VideoFileWriter();
                    writer.Open(path,loadedVideo[0].width, loadedVideo[0].height,frameRate,VideoCodec.FFV1);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */

                    frameCount = orderedVideo.Length;

                    int currentFrame = 0;

                    int frameToWrite = 0;
                    for(int i = 0; i < frameCount; i++)
                    {
                        frameToWrite = orderedVideo[i];
                        writer.WriteVideoFrame(Helpers.ByteArrayToBitmap(loadedVideo[frameToWrite]));
                        if (currentFrame % 1000 == 0)
                        {
                            progress.Report("Saving video: " + i + "/" + frameCount + " frames");
                        }
                    }

                    writer.Close();

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

            });
            status_txt.Text = "Completed.";
            videoIsLoaded = true;
            processVideo_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = false;
            saveSortedFrameList_button.IsEnabled = false;

        }



        private async Task processAudioAsync(string path,string outputPath)
        {
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;
            await Task.Run(() =>
            {
                
                

            });
            status_txt.Text = "Completed.";

        }

        private void loadVideo_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {

                processVideo_button.IsEnabled = false;
                loadVideoAsync(ofd.FileName);

            }
        }

        private void processVideo_button_Click(object sender, RoutedEventArgs e)
        {
            processVideo_button.IsEnabled = false;
            loadVideo_button.IsEnabled = false;
            saveSortedFrameList_button.IsEnabled = false;
            saveSortedVideo_button.IsEnabled = false;
            processVideo();
        }

        private void saveSortedVideo_button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == true)
            {

                saveVideoAsync(sfd.FileName);

            }
        }

        private void saveSortedFrameList_button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == true)
            {

                File.WriteAllText(sfd.FileName, string.Join("\n", orderedVideo));

            }
        }

        private void processWAVFile_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {

                SaveFileDialog sfd = new SaveFileDialog();
                if (sfd.ShowDialog() == true)
                {


                    processAudioAsync(ofd.FileName,sfd.FileName);

                }

            }
        }
    }
}
