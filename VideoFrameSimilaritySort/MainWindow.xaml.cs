using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
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
using Spectrogram;

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

            //int maxThreads = 

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
                //Vector3Image currentFrameData;

                double[] frameDifferences = new double[frameCount];

                int maxWorkers = 0;
                int maxCompletionPortThreads = 0;
                ThreadPool.GetMaxThreads(out maxWorkers, out maxCompletionPortThreads);

                progress.Report("Starting processing of "+frameCount+" frames with up to "+ maxWorkers+"(workers)/"+maxCompletionPortThreads + "(IO) ...");
#if DEBUG
                try
                {
#endif

                    for (int currentIndex = 0; currentIndex < frameCount; currentIndex++)
                    {// not to confuse with current frame. We're just tracking our progress here.


                        double smallestDifference = double.PositiveInfinity;
                        int smallestDifferenceFrame = -1;

                        //currentFrameData = new Vector3Image(loadedVideo[currentFrame]);
                        //ParallelOptions options = new ParallelOptions();
                        //options.MaxDegreeOfParallelism = 4;

                        double smallestDifferenceImpreciseOpt = double.PositiveInfinity;
                        // Go through all frames, comparing them to the current frame
                        Parallel.For(0, frameCount, (compareFrame) =>
                        { 
                            //File.WriteAllText(compareFrame + ".txt", ""); // debug


                            frameDifferences[compareFrame] = double.PositiveInfinity;
                            if (compareFrame == currentFrame) return; // No need to compare to itself.
                            if (alreadyUsedFrames[compareFrame] == true) return; // No need to go through already used frames

                            double thisFrameDifference = 0;
                            Vector3 thisFrameRGBDifference = new Vector3() { X = 0, Y = 0, Z = 0 };
                            Vector3 currentFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };
                            Vector3 compareFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };


                            // Calculate difference
                            int baseIndex;
                            //int pixelOffsetBase;
                            for (int y = 0; y < height; y++)
                            {
                                //pixelOffsetBase = y * width;
                                for (int x = 0; x < width; x++)
                                {
                                    baseIndex = stride * y + x * channelMultiplier;
                                    currentFramePixel.X = loadedVideo[currentFrame].imageData[baseIndex];
                                    currentFramePixel.Y = loadedVideo[currentFrame].imageData[baseIndex + 1];
                                    currentFramePixel.Z = loadedVideo[currentFrame].imageData[baseIndex + 2];
                                    compareFramePixel.X = loadedVideo[compareFrame].imageData[baseIndex];
                                    compareFramePixel.Y = loadedVideo[compareFrame].imageData[baseIndex + 1];
                                    compareFramePixel.Z = loadedVideo[compareFrame].imageData[baseIndex + 2];
                                    thisFrameRGBDifference += Vector3.Abs(currentFramePixel - compareFramePixel);
                                    //thisFrameRGBDifference += Vector3.Abs(currentFrameData.imageData[pixelOffsetBase+x]-compareFramePixel);
                                    //thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex] - loadedVideo[compareFrame].imageData[baseIndex]);
                                    //thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 1] - loadedVideo[compareFrame].imageData[baseIndex + 1]);
                                    //thisFrameDifference += Math.Abs(loadedVideo[currentFrame].imageData[baseIndex + 2] - loadedVideo[compareFrame].imageData[baseIndex + 2]);
                                }
                                //if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break; // fast skip for very different frames. Since this is multithreaded, this might not always be correct in the sense of always having the right number in smallestDifference, but might work as optimization.
                                thisFrameDifference = thisFrameRGBDifference.X + thisFrameRGBDifference.Y + thisFrameRGBDifference.Z;
                                if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break; // fast skip for very different frames. Since this is multithreaded, this might not always be correct in the sense of always having the right number in smallestDifference, but might work as optimization.
                            }
                            thisFrameDifference = thisFrameRGBDifference.X + thisFrameRGBDifference.Y + thisFrameRGBDifference.Z;
                            frameDifferences[compareFrame] = thisFrameDifference / pixelCountX3;
                            if (frameDifferences[compareFrame] < smallestDifferenceImpreciseOpt)
                            {
                                smallestDifferenceImpreciseOpt = frameDifferences[compareFrame];
                            }

                            //if (compareFrame % 1000 == 0)
                            //{
                            //    progress.Report("Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is "+currentFrame+" comparing to "+compareFrame);
                            //}
                        });



                        for (int compareFrame = 0; compareFrame < frameCount; compareFrame++)
                        {
                            if (frameDifferences[compareFrame] < smallestDifference)
                            {
                                smallestDifference = frameDifferences[compareFrame];
                                smallestDifferenceFrame = compareFrame;
                            }
                        }

                        /*for (int compareFrame = 0; compareFrame < frameCount; compareFrame++)
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
                            if (frameDifferences[compareFrame] < smallestDifference)
                            {
                                smallestDifference = frameDifferences[compareFrame];
                                smallestDifferenceFrame = compareFrame;
                            }
                        }*/





                        progress.Report("Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is " + currentFrame);

                        if (smallestDifferenceFrame != -1)
                        {
                            sortedIndizi[currentIndex] = smallestDifferenceFrame;
                            currentFrame = smallestDifferenceFrame;
                            alreadyUsedFrames[smallestDifferenceFrame] = true;
                        }
                    }
#if DEBUG
                }
                catch (Exception e)
                {
                    MessageBox.Show("le error: "+e.Message);
                }
#endif

                orderedVideo = sortedIndizi;
            });
            status_txt.Text = "Completed processing.";
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
            int tooFewFramesDelivered = 0;

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

                    // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                    if(currentFrame < frameCount)
                    {
                        tooFewFramesDelivered = (int)frameCount-currentFrame;
                        Array.Resize<ByteImage>(ref loadedVideo, currentFrame );
                    }

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

            });
            if (tooFewFramesDelivered > 0)
            {

                status_txt.Text = "Completed loading video. Video reader delivered " + tooFewFramesDelivered + " fewer frame(s) than it announced. Consider redoing the input video.";
            }
            else
            {
                status_txt.Text = "Completed loading video.";
            }
            videoIsLoaded = true;
            processVideo_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = false;
            saveSortedFrameList_button.IsEnabled = false;
            audio2spectrumLoadAudio_button.IsEnabled = true;
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
            status_txt.Text = "Completed saving video.";
            videoIsLoaded = true;
            processVideo_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = true;
            saveSortedFrameList_button.IsEnabled = true;

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

        private void audio2spectrumLoadAudio_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {

                audio2spectrumSaveSpectrumVideo_button.IsEnabled = false;
                audio2spectrum_loadAudioAsync(ofd.FileName);

            }
        }

        private void audio2spectrumSaveSpectrumVideo_button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == true)
            {

                audio2spectrum_saveVideoAsync(sfd.FileName);

            }
        }

        int audio2spectrum_sampleRate = 0;
        double[] audio2spectrum_samples;
        bool audio2spectrum_audioIsLoaded = false;
        private async Task audio2spectrum_loadAudioAsync(string path)
        {
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;

            bool errored = false;
            await Task.Run(() =>
            {

                try
                {
                    (audio2spectrum_sampleRate, audio2spectrum_samples) = WavFile.ReadMono(path);


                }
                catch (Exception e)
                {
                    errored = true;
                    MessageBox.Show(e.Message);
                }

            });
            if (!errored)
            {

                status_txt.Text = "Audio2Spectrum: Completed loading audio.";
                audio2spectrum_audioIsLoaded = true;
                audio2spectrumLoadAudio_button.IsEnabled = true;
                audio2spectrumSaveSpectrumVideo_button.IsEnabled = true;
            }
        }

        private async Task audio2spectrum_saveVideoAsync(string path)
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


                    double samplesPerFrame = ((double)audio2spectrum_sampleRate) * frameRate.Denominator / frameRate.Numerator;
                    double totalFrameCount = Math.Ceiling(audio2spectrum_samples.Length / samplesPerFrame);

                    int roundedSamplesPerFrame = (int)Math.Ceiling(samplesPerFrame);

                    int outputWidth = 50; 
                    int outputHeight = 50;
                    double samplesPerPixel = samplesPerFrame / outputWidth;

                    // Now find closest fft size (take next highest)
                    int fftSize =(int) Math.Pow(2,Math.Ceiling(Math.Log(samplesPerPixel,2.0)));


                    progress.Report("Audio2Spectrum: Loading spectrogram library");
                    Spectrogram.Spectrogram spec = new Spectrogram.Spectrogram(audio2spectrum_sampleRate, fftSize: 2048, stepSize: 50);

                    
                    spec.SetFixedWidth(outputWidth);
                    //outputWidth = spec.Width;

                    progress.Report("Audio2Spectrum: Initializing video writer");
                    VideoFileWriter writer = new VideoFileWriter();
                    writer.Open(path, outputWidth, outputHeight, frameRate, VideoCodec.FFV1);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */


                    frameCount = (long)totalFrameCount;

                    // Enlarge the array to make sure we don't end up accessing nonexisting samples. We make it a tiny bit bigger than maybe necessary, just to be safe. (To be honest, I am just too lazy to calculate the precise number we need)
                    if((long)Math.Ceiling(frameCount  * samplesPerFrame) > audio2spectrum_samples.Length)
                    {
                        progress.Report("Audio2Spectrum: Resizing array");
                        Array.Resize<double>(ref audio2spectrum_samples, (int)Math.Ceiling(frameCount * samplesPerFrame));
                    }

                    double[] frameSampleBuffer = new double[roundedSamplesPerFrame];

                    int currentFrame = 0;
                    long currentStartSample = 0;

                    progress.Report("Audio2Spectrum: Starting video generation");

                    Bitmap tmp;
                    for (int i = 0; i < frameCount; i++)
                    {
                        currentStartSample = (long) Math.Floor(i * samplesPerFrame);
                        Array.Copy(audio2spectrum_samples, currentStartSample, frameSampleBuffer, 0, roundedSamplesPerFrame); // No need to worry about accessing too much at the end, since we resized the array before

                        spec.Add(frameSampleBuffer);
                        tmp = spec.GetBitmapMel(dB: true,melBinCount: outputHeight);
#if DEBUG
                        Console.WriteLine(tmp.Width+"x"+tmp.Height);
#endif
                        writer.WriteVideoFrame(tmp);
                        if (currentFrame % 1000 == 0)
                        {
                            progress.Report("Audio2Spectrum: Saving video: " + i + "/" + frameCount + " frames");
                        }
                    }

                    writer.Close();

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }

            });
            status_txt.Text = "Audio2Spectrum: Completed saving video.";
            videoIsLoaded = true;

        }

    }
}
