using Accord.Video.FFMPEG;
using Microsoft.Win32;
using Spectrogram;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Vector = System.Numerics.Vector;

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

        LinearAccessByteImage[] loadedVideo;
        bool videoIsLoaded = false;
        int[] orderedVideo;
        bool videoIsOrdered = false;
        Accord.Math.Rational frameRate = 24;

        // Statistics
        float[] fps;
        float highestFPS;
        float lowestFPS;
        float[] smallestDifferences;
        float smallestSmallestDifference;
        float highestSmallestDifference;

        struct ProcessingProgressReport
        {
            public string message;
            public int currentIndex;
            public bool drawStats; // whether to draw stats

            public ProcessingProgressReport(string messageA = "", int currentIndexA = 0, bool drawStatsA=false)
            {
                drawStats = drawStatsA;
                currentIndex = currentIndexA;
                message = messageA;
            }

            public static implicit operator string(ProcessingProgressReport d) => d.message;
            public static implicit operator ProcessingProgressReport(string b) => new ProcessingProgressReport(b);
            public override string ToString()
            {
                return message;
            }
        }

        private void drawStats(int maxIndex)
        {
            int imageWidth = (int)stats_img_container.ActualWidth;
            int imageHeight = (int)stats_img_container.ActualHeight;

            if (imageWidth < 5 || imageHeight < 5) return; // avoid crashes and shit

            // We flip imageHeight and imageWidth because it's more efficient to work on rows than on columns. We later rotate the image into the proper position
            ByteImage statsImage = Helpers.BitmapToByteArray(new Bitmap(imageHeight, imageWidth, System.Drawing.Imaging.PixelFormat.Format24bppRgb));

            int stride = statsImage.stride;
            int strideHere;

            double verticalScaleFactorFps = highestFPS == 0 ? 0: (imageHeight-1)/highestFPS;
            double verticalScaleFactorSmallestDifference = highestSmallestDifference == 0 ? 1: (imageHeight-1) / highestSmallestDifference;

            double indiziPerPixel = (double)maxIndex / (double)imageWidth;
            int indiziPerPixelRounded = (int)Math.Ceiling(indiziPerPixel);
            int pixelValueAddedPerValue = (int)Math.Max(1,Math.Ceiling(255.0 / indiziPerPixel));

            float[] currentColumnValuesFps = new float[indiziPerPixelRounded];
            float[] currentColumnValuesSmallestDifference = new float[indiziPerPixelRounded];

            double averageFpsForColumnCounter;
            double averageSmallestDifferenceForColumnCounter;

            int rangeStart = 0;
            int yPosition;
            double yPositionUnrounded;

            // Remember, x/y in the actual Bitmap data are flipped! Hence having the outer for be X makes more sense
            for(int x = 0; x < imageWidth; x++)
            {
                strideHere = x * stride;
                rangeStart = (int)Math.Floor((indiziPerPixel * (double)x));

                Array.Copy(fps, rangeStart, currentColumnValuesFps, 0, Math.Min(indiziPerPixelRounded,fps.Length-rangeStart));
                Array.Sort(currentColumnValuesFps);
                Array.Copy(smallestDifferences, rangeStart, currentColumnValuesSmallestDifference, 0, Math.Min(indiziPerPixelRounded, smallestDifferences.Length-rangeStart));
                Array.Sort(currentColumnValuesSmallestDifference);

                averageFpsForColumnCounter = 0;
                averageSmallestDifferenceForColumnCounter = 0;
                for (int i = 0; i < indiziPerPixelRounded; i++) // fps
                {
                    yPositionUnrounded = currentColumnValuesFps[i] * verticalScaleFactorFps;
                    yPosition = (int)yPositionUnrounded;
                    averageFpsForColumnCounter += yPositionUnrounded;
                    statsImage.imageData[strideHere + yPosition * 3+2] = statsImage.imageData[strideHere + yPosition * 3 + 2] == 255 ? (byte)255 : (byte)Math.Min(255,statsImage.imageData[strideHere + yPosition * 3+2] + (byte)pixelValueAddedPerValue);
                }
                yPosition = averageFpsForColumnCounter == 0 ? 0: (int)(averageFpsForColumnCounter / (double)indiziPerPixelRounded);
                statsImage.imageData[strideHere + yPosition * 3 + 2] = 255;
                for (int i = 0; i < indiziPerPixelRounded; i++) // Smallest differences
                {
                    yPositionUnrounded = currentColumnValuesSmallestDifference[i] * verticalScaleFactorSmallestDifference;
                    yPosition = (int)yPositionUnrounded;
                    averageSmallestDifferenceForColumnCounter += yPositionUnrounded;
                    statsImage.imageData[strideHere + yPosition * 3+1] = statsImage.imageData[strideHere + yPosition * 3 + 1] == 255 ? (byte)255 : (byte)Math.Min(255,statsImage.imageData[strideHere + yPosition * 3+1] + (byte)pixelValueAddedPerValue);
                }
                yPosition = averageSmallestDifferenceForColumnCounter == 0 ? 0 : (int)(averageSmallestDifferenceForColumnCounter / (double)indiziPerPixelRounded);
                statsImage.imageData[strideHere + yPosition * 3 + 1] = 255;
                for (int y = 0; y < imageHeight; y++) // Apply gamma to make it more visible
                {
                    statsImage.imageData[strideHere + y * 3 + 1] = (byte) Math.Min(255, 255*Math.Pow(((double)statsImage.imageData[strideHere + y * 3 + 1]/255),1.0/3.0));
                    statsImage.imageData[strideHere + y * 3 + 2] = (byte) Math.Min(255, 255*Math.Pow(((double)statsImage.imageData[strideHere + y * 3 + 2]/255),1.0/3.0));
                }
            }
            Bitmap statsImageBitmap = Helpers.ByteArrayToBitmap(statsImage);
            statsImageBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);

            int padding = 2;
            if(imageWidth > 200 && imageHeight > 100) { 

                Graphics g = Graphics.FromImage(statsImageBitmap);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                string fpsString = "Max FPS: " + highestFPS;
                string lowestDifferenceString = "Biggest difference: " + highestSmallestDifference;

                SizeF fpsStringSize = g.MeasureString(fpsString, new Font("Tahoma", 8));
                SizeF lowestDifferenceStringSize = g.MeasureString(lowestDifferenceString, new Font("Tahoma", 8));
                if((fpsStringSize.Width + padding )< imageWidth && (fpsStringSize.Height + padding) < imageHeight)
                {

                    RectangleF rectf = new RectangleF(padding, padding, fpsStringSize.Width, fpsStringSize.Height);
                    g.DrawString(fpsString, new Font("Tahoma", 8), System.Drawing.Brushes.Red, rectf);
                }
                if((lowestDifferenceStringSize.Width + padding) < imageWidth && (fpsStringSize.Height + padding*2 + lowestDifferenceStringSize.Height) < imageHeight)
                {

                    RectangleF rectf = new RectangleF(padding, lowestDifferenceStringSize.Height+padding*2, lowestDifferenceStringSize.Width, lowestDifferenceStringSize.Height);
                    g.DrawString(lowestDifferenceString, new Font("Tahoma", 8), System.Drawing.Brushes.Green, rectf);
                }

                g.Flush();
            }


            stats_img.Source = Helpers.BitmapToImageSource(statsImageBitmap);

        }

        private async Task processVideoFirstFrameNoBackFrame() {


            var progressHandler = new Progress<ProcessingProgressReport>(value =>
            {
                status_txt.Text = value.message;
                if (value.drawStats) drawStats(value.currentIndex);
            });
            var progress = progressHandler as IProgress<ProcessingProgressReport>;
            await Task.Run(() =>
            {
                int frameCount = loadedVideo.Length;
                int[] sortedIndizi = new int[loadedVideo.Length];
                bool[] alreadyUsedFrames = new bool[loadedVideo.Length];
                alreadyUsedFrames[0] = true;

                int width = loadedVideo[0].width;
                int height = loadedVideo[0].height;
                int pixelCount = width * height;
                int pixelCountX3 = width * height * 3;
                int stride = loadedVideo[0].stride;
                int channelMultiplier = 3;


                // Stats
                Stopwatch stopWatch = new Stopwatch();
                ProcessingProgressReport progressReport = new ProcessingProgressReport();
                fps = new float[loadedVideo.Length];
                smallestDifferences = new float[loadedVideo.Length];
                lowestFPS = float.PositiveInfinity;
                highestFPS = 0;
                smallestSmallestDifference = float.PositiveInfinity;
                highestSmallestDifference = 0;

                // Vectorization related stuff
                int elementsPerVector = Vector<short>.Count;
                int elementsPerTwoVectors = 2 * elementsPerVector;
                int maxDifferencesPerVector = short.MaxValue / 255; // How often can we add a difference (assuming unlikely case of 255 each time) to a vector until it overflows? Then we need to flush.
                int oneFrameTotalLength = loadedVideo[0].imageData.Length;

                if (loadedVideo[0].pixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
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


                progress.Report("Starting processing of " + frameCount + " frames with up to " + maxWorkers + "(workers)/" + maxCompletionPortThreads + "(IO) ...");
#if DEBUG
                try
                {
#endif

                stopWatch.Start();

                bool comparisonsDone = false;

                for (int currentIndex = 0; currentIndex < frameCount; currentIndex++)
                {// not to confuse with current frame. We're just tracking our progress here.


                    double smallestDifference = double.PositiveInfinity;
                    int smallestDifferenceFrame = -1;


                    //currentFrameData = new Vector3Image(loadedVideo[currentFrame]);
                    //ParallelOptions options = new ParallelOptions();
                    //options.MaxDegreeOfParallelism = 4;

                    double smallestDifferenceImpreciseOpt = double.PositiveInfinity;


                    if (!comparisonsDone) { 
                        // Go through all frames, comparing them to the current frame
                        Parallel.For(0, frameCount, (compareFrame) =>
                        {
                            //File.WriteAllText(compareFrame + ".txt", ""); // debug


                            frameDifferences[compareFrame] = double.PositiveInfinity;
                            if (compareFrame == currentFrame) return; // No need to compare to itself.
                            //if (alreadyUsedFrames[compareFrame] == true) return; // No need to go through already used frames

                            double thisFrameDifference = 0;
                            /*Vector3 thisFrameRGBDifference = new Vector3() { X = 0, Y = 0, Z = 0 };
                            Vector3 currentFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };
                            Vector3 compareFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };*/
                            Vector<short> thisFrameRGBDifference = new Vector<short>(0);
                            Vector<short> currentFramePixel = new Vector<short>();
                            Vector<short> currentFramePixel2 = new Vector<short>();
                            Vector<short> compareFramePixel = new Vector<short>();
                            Vector<short> compareFramePixel2 = new Vector<short>();

                            //Vector<short> tmpDiff = new Vector<short>();


                            // Calculate difference
                            int baseIndex;
                            //int pixelOffsetBase;

                            int i, a;
                            int donePixelsPerVectorElement = 0;

                            for (i = 0; i < oneFrameTotalLength; i += elementsPerTwoVectors)
                            {

                                Vector.Widen(new Vector<sbyte>(loadedVideo[currentFrame].imageData, i), out currentFramePixel, out currentFramePixel2);
                                Vector.Widen(new Vector<sbyte>(loadedVideo[compareFrame].imageData, i), out compareFramePixel, out compareFramePixel2);
                                thisFrameRGBDifference += Vector.Abs<short>(currentFramePixel - compareFramePixel);
                                thisFrameRGBDifference += Vector.Abs<short>(currentFramePixel2 - compareFramePixel2);

                                donePixelsPerVectorElement += 2;

                                if (donePixelsPerVectorElement >= (maxDifferencesPerVector - 2))
                                {

                                    for (a = 0; a < elementsPerVector; a++)
                                    {
                                        thisFrameDifference += thisFrameRGBDifference[a];
                                    }
                                    donePixelsPerVectorElement = 0;
                                    //if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break; // Not doing this in first frame compare mode bc we're comparing ALL frames fully and then sorting.
                                    thisFrameRGBDifference = new Vector<short>(0);
                                }
                            }

                            if (donePixelsPerVectorElement > 0)
                            {
                                for (a = 0; a < elementsPerVector; a++)
                                {
                                    thisFrameDifference += thisFrameRGBDifference[a];
                                }
                            }
                            frameDifferences[compareFrame] = thisFrameDifference / pixelCountX3;

                            if (frameDifferences[compareFrame] < smallestDifferenceImpreciseOpt)
                            {
                                smallestDifferenceImpreciseOpt = frameDifferences[compareFrame];
                            }

                        });
                        comparisonsDone = true;
                    }



                    for (int compareFrame = 0; compareFrame < frameCount; compareFrame++)
                    {
                        if (frameDifferences[compareFrame] < smallestDifference && alreadyUsedFrames[compareFrame] != true && compareFrame != currentFrame)
                        {
                            smallestDifference = frameDifferences[compareFrame];
                            smallestDifferenceFrame = compareFrame;
                        }
                    }


                    //Stats
                    fps[currentIndex] = 1 / ((float)stopWatch.ElapsedTicks / (float)Stopwatch.Frequency);
                    if (fps[currentIndex] < lowestFPS) lowestFPS = fps[currentIndex];
                    if (fps[currentIndex] > highestFPS) highestFPS = fps[currentIndex];
                    smallestDifferences[currentIndex] = (float)smallestDifference;
                    if (smallestDifference < smallestSmallestDifference) smallestSmallestDifference = smallestDifferences[currentIndex];
                    if (smallestDifference > highestSmallestDifference && smallestDifference != double.PositiveInfinity) highestSmallestDifference = smallestDifferences[currentIndex];
                    progressReport.drawStats = currentIndex % 10000 == 0; // Only after 100 processed frames draw stats, to not have a notable performance impact
                    stopWatch.Restart();

                    // Status update
                    progressReport.message = "Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is " + currentFrame + ", last smallest difference was " + smallestDifference;
                    progressReport.currentIndex = currentIndex;
                    progress.Report(progressReport);

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
                progressReport.message = "Processing finished";
                progressReport.drawStats = true;
                progressReport.currentIndex = frameCount - 1;
                progress.Report(progressReport);
                orderedVideo = sortedIndizi;
            });
            status_txt.Text = "Completed processing.";
            videoIsLoaded = true;
            loadVideo_button.IsEnabled = true;
            processVideo_button.IsEnabled = true;
            saveSortedFrameList_button.IsEnabled = true;
            saveSortedVideo_button.IsEnabled = true;
        }

        private async Task processVideo(int backFrames=0,bool firstFrameCompareOnly = false)
        {

            // We have a quicker solution for you bucko!
            if(firstFrameCompareOnly && backFrames == 0)
            {
                await processVideoFirstFrameNoBackFrame();
            }
            //int maxThreads = 

            var progressHandler = new Progress<ProcessingProgressReport>(value =>
            {
                status_txt.Text = value.message;
                if (value.drawStats) drawStats(value.currentIndex);
            });
            var progress = progressHandler as IProgress<ProcessingProgressReport>;
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


                // Stats
                Stopwatch stopWatch = new Stopwatch();
                ProcessingProgressReport progressReport = new ProcessingProgressReport();
                fps = new float[loadedVideo.Length];
                smallestDifferences = new float[loadedVideo.Length];
                lowestFPS = float.PositiveInfinity;
                highestFPS = 0;
                smallestSmallestDifference = float.PositiveInfinity;
                highestSmallestDifference = 0;

                // Vectorization related stuff
                int elementsPerVector = Vector<short>.Count;
                int elementsPerTwoVectors = 2* elementsPerVector;
                int maxDifferencesPerVector = short.MaxValue/255; // How often can we add a difference (assuming unlikely case of 255 each time) to a vector until it overflows? Then we need to flush.
                int oneFrameTotalLength = loadedVideo[0].imageData.Length;

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

                int[] currentFrames = new int[backFrames + 1];
                int[] currentFramesActual = new int[backFrames + 1];
                for(int i = 0; i < currentFrames.Length; i++)
                {
                    currentFrames[i] = -1; // backframes marked with -1 will just be ignored.
                }

                progress.Report("Starting processing of " + frameCount + " frames with up to " + maxWorkers + "(workers)/" + maxCompletionPortThreads + "(IO) ...");
#if DEBUG
                try
                {
#endif

                stopWatch.Start();
                for (int currentIndex = 0; currentIndex < frameCount; currentIndex++)
                {// not to confuse with current frame. We're just tracking our progress here.


                    double smallestDifference = double.PositiveInfinity;
                    int smallestDifferenceFrame = -1;

                    for (int i = currentFrames.Length-1; i > 0; i--) // Move all previous currentframes by 1.
                    {
                        currentFrames[i] = currentFrames[i-1]; 
                    }
                    currentFrames[0] = currentFrame; // Then set first one to current frame.
                    currentFramesActual = (int[])currentFrames.Clone();
                    if (firstFrameCompareOnly)
                    {
                        currentFramesActual[0] = 0; // We need this separate second array because we want to potentially keep the back frame comparison, but also allow to always have the first frame be always 0 without propagating to the "back" positions
                    }

                    int compareFrameCount = Math.Min(currentIndex+1,currentFramesActual.Length); // Tells how many are being compared, so we can divide properly.
                    int pixelCountX3TimesCompareFrameCount = pixelCountX3 * compareFrameCount;
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
                        /*Vector3 thisFrameRGBDifference = new Vector3() { X = 0, Y = 0, Z = 0 };
                        Vector3 currentFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };
                        Vector3 compareFramePixel = new Vector3() { X = 0, Y = 0, Z = 0 };*/
                        Vector<short> thisFrameRGBDifference = new Vector<short>(0);
                        Vector<short> currentFramePixel = new Vector<short>();
                        Vector<short> currentFramePixel2 = new Vector<short>();
                        Vector<short> compareFramePixel = new Vector<short>();
                        Vector<short> compareFramePixel2 = new Vector<short>();

                        //Vector<short> tmpDiff = new Vector<short>();


                        // Calculate difference
                        int baseIndex;
                        //int pixelOffsetBase;

                        int i, a;
                        int donePixelsPerVectorElement = 0;

                        foreach(int currentFrameTmp in currentFramesActual) { // This is now a loop because we want to compare to more than just the current frame, also past ones.

                            if (currentFrameTmp == -1) continue; // Let's say we compare to 5 past frames, but it's only 2 frames into the video. Then the array will have empty places. Those just get -1 assigned as start value.

                            for (i = 0; i < oneFrameTotalLength; i += elementsPerTwoVectors)
                            {

                                Vector.Widen(new Vector<sbyte>(loadedVideo[currentFrameTmp].imageData, i), out currentFramePixel, out currentFramePixel2);
                                Vector.Widen(new Vector<sbyte>(loadedVideo[compareFrame].imageData, i), out compareFramePixel, out compareFramePixel2);
                                thisFrameRGBDifference += Vector.Abs<short>(currentFramePixel - compareFramePixel);
                                thisFrameRGBDifference += Vector.Abs<short>(currentFramePixel2 - compareFramePixel2);

                                // Danger: XOR fuckery
                                //tmpDiff = currentFramePixel - compareFramePixel;
                                //(tmpDiff ^ (tmpDiff >> 31)) - (tmpDiff >> 31);
                                // /Danger XOR fuckery over


                                donePixelsPerVectorElement += 2;

                                if(donePixelsPerVectorElement >= (maxDifferencesPerVector - 2))
                                {

                                    for(a = 0; a < elementsPerVector; a++)
                                    {
                                        thisFrameDifference += thisFrameRGBDifference[a];
                                    }
                                    donePixelsPerVectorElement = 0;
                                    //if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break;
                                    if (thisFrameDifference / pixelCountX3TimesCompareFrameCount > smallestDifferenceImpreciseOpt) goto loopBroken;
                                    thisFrameRGBDifference = new Vector<short>(0);
                                }
                                /*for(a=0; a < elementsPerVector; a++)
                                {
                                    currentFramePixel.Item[]
                                }*/
                            }
                        }
                        loopBroken:

                        if (donePixelsPerVectorElement > 0)
                        {
                            for (a = 0; a < elementsPerVector; a++)
                            {
                                thisFrameDifference += thisFrameRGBDifference[a];
                            }
                        }
                        /*
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
                        thisFrameDifference = thisFrameRGBDifference.X + thisFrameRGBDifference.Y + thisFrameRGBDifference.Z;*/
                        frameDifferences[compareFrame] = thisFrameDifference / pixelCountX3TimesCompareFrameCount;
                        
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


                    //Stats
                    fps[currentIndex] = 1/((float)stopWatch.ElapsedTicks / (float)Stopwatch.Frequency);
                    if (fps[currentIndex] < lowestFPS) lowestFPS = fps[currentIndex];
                    if (fps[currentIndex] > highestFPS) highestFPS = fps[currentIndex];
                    smallestDifferences[currentIndex] = (float)smallestDifference;
                    if (smallestDifference < smallestSmallestDifference) smallestSmallestDifference = smallestDifferences[currentIndex];
                    if (smallestDifference > highestSmallestDifference && smallestDifference != double.PositiveInfinity) highestSmallestDifference = smallestDifferences[currentIndex];
                    progressReport.drawStats = currentIndex % 100 == 0; // Only after 100 processed frames draw stats, to not have a notable performance impact
                    stopWatch.Restart();

                    // Status update
                    progressReport.message = "Processing: " + currentIndex + "/" + frameCount + " ordered frames. Current frame is " + currentFrame + ", last smallest difference was " + smallestDifference;
                    progressReport.currentIndex = currentIndex;
                    progress.Report(progressReport);

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
                progressReport.message = "Processing finished";
                progressReport.drawStats = true;
                progressReport.currentIndex = frameCount-1;
                progress.Report(progressReport);
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

            bool failed = false;

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

                    loadedVideo = new LinearAccessByteImage[frameCount];
                    frameRate = reader.FrameRate;


                    while (true)
                    {

                        using (Bitmap videoFrame = reader.ReadVideoFrame())
                        {
                            if (videoFrame == null)
                                break;

                            loadedVideo[currentFrame] = Helpers.BitmapToLinearAccessByteArray(videoFrame);

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
                        Array.Resize<LinearAccessByteImage>(ref loadedVideo, currentFrame );
                    }

                }
                catch (Exception e)
                {
                    failed = true;
                    MessageBox.Show(e.Message);
                }

            });
            if (failed)
            {
                return;
            }
            if (tooFewFramesDelivered > 0)
            {

                status_txt.Text = "Completed loading video. Video reader delivered " + tooFewFramesDelivered + " fewer frame(s) than it announced. Consider redoing the input video.";
            }
            else
            {
                status_txt.Text = "Completed loading video.";
            }
            videoIsLoaded = true;
            replaceFirstFrame_button.IsEnabled = true;
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
            int backFrames = 0;
            int.TryParse(txtBackFrames.Text,out backFrames);
            processVideo(backFrames,firstFrameCompareOnly_check.IsChecked == true);
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
        Int16[] audio2spectrum_samples;
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
                    (audio2spectrum_sampleRate, audio2spectrum_samples) = WavFileInt16Mod.ReadMono(path);


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
                    /*if((long)Math.Ceiling(frameCount  * samplesPerFrame) > audio2spectrum_samples.Length)
                    {
                        progress.Report("Audio2Spectrum: Resizing array");
                        Array.Resize<double>(ref audio2spectrum_samples, (int)Math.Ceiling(frameCount * samplesPerFrame));
                    }*/

                    double[] frameSampleBuffer = new double[roundedSamplesPerFrame];

                    int currentFrame = 0;
                    long currentStartSample = 0;

                    progress.Report("Audio2Spectrum: Starting video generation");

                    Bitmap tmp;
                    for (int i = 0; i < frameCount; i++)
                    {
                        currentStartSample = (long) Math.Floor(i * samplesPerFrame);

                        // Doing this branching here now because the resizing the array first was just way way too slow and memory hungry
                        if(currentStartSample >= audio2spectrum_samples.Length) // Even the first sample is already outside the bounds, just make empty array.
                        {
                            frameSampleBuffer = new double[roundedSamplesPerFrame];
                        } else if((currentStartSample+(roundedSamplesPerFrame-1)) > (audio2spectrum_samples.Length-1)) // Copy as many samples as possible
                        {
                            long difference = (currentStartSample + (roundedSamplesPerFrame - 1)) - (audio2spectrum_samples.Length - 1);
                            frameSampleBuffer = new double[roundedSamplesPerFrame];
                            Array.Copy(audio2spectrum_samples, currentStartSample, frameSampleBuffer, 0, roundedSamplesPerFrame- difference);
                        }
                        else
                        {
                            Array.Copy(audio2spectrum_samples, currentStartSample, frameSampleBuffer, 0, roundedSamplesPerFrame); 
                        }

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

        private void vectorSizeCheck_button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("byte: " +Vector<byte>.Count+
                ", sbyte: " +Vector<sbyte>.Count+
                ", int:"+ Vector<int>.Count + 
                ", float:" + Vector<float>.Count + 
                ", double:" + Vector<double>.Count + 
                ", long:" + Vector<long>.Count + 
                ", short:" + Vector<short>.Count);
        }

        private void randomTest_button_Click(object sender, RoutedEventArgs e)
        {
            Vector<short> blah = new Vector<short>(short.MaxValue);
            int calc = Vector<short>.Count * short.MaxValue;
            int sum1 = Vector.Dot<short>(blah, Vector<short>.One);

            Vector<int> widenTarget1, widenTarget2;

            Vector.Widen(blah, out widenTarget1, out widenTarget2);
            int sum2 = Vector.Dot<int>(widenTarget1,Vector<int>.One) + Vector.Dot<int>(widenTarget2, Vector<int>.One);

            MessageBox.Show("Calculated: "+calc+", summed (short): "+sum1+", summed (Widen,int):"+sum2);

            // Test performance
            long result1 = 0, result2 = 0;
            int vectorLength = Vector<short>.Count;

            long time1 = 0, time2 = 0;
            long lastTime = 0;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Do this 100 times
            for (int v = 0; v < 100; v++)
            {

                // Do 1 million of each
                for (int i = 0; i < 1000000; i++)
                {
                    Vector.Widen(blah, out widenTarget1, out widenTarget2);
                    result1 += Vector.Dot<int>(widenTarget1, Vector<int>.One) + Vector.Dot<int>(widenTarget2, Vector<int>.One);
                }
                time1 += watch.ElapsedMilliseconds - lastTime;
                lastTime = watch.ElapsedMilliseconds;


                for (int i = 0; i < 1000000; i++)
                {
                    for (int a = 0; a < vectorLength; a++)
                    {
                        result2 += blah[a];
                    }
                }
                time2 += watch.ElapsedMilliseconds - lastTime;
                lastTime = watch.ElapsedMilliseconds;
            }

            MessageBox.Show("100 million operations in milliseconds: Time (widen & dot): "+time1+", Time (iterate via for): "+time2);


        }

        private void vfssppCreate_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Choose source video file";
            if (ofd.ShowDialog() == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Video Frame Similarity Sort Pre-Process File (*.vfsspp)|*.vfsspp";
                sfd.Title = "Choose destination VFSSPP file";
                if (sfd.ShowDialog() == true)
                {

                    vfssppCreate(ofd.FileName, sfd.FileName);

                }

            }
        }

        private async Task vfssppCreate(string pathSource, string pathDestination)
        {
            vfssppExport_button.IsEnabled = false;
            var progressHandler = new Progress<string>(value =>
            {
                status_txt.Text = value;
            });
            var progress = progressHandler as IProgress<string>;
            int tooFewFramesDelivered = 0;

            bool failed = false;

            await Task.Run(() =>
            {
                long frameCount;
                long actualFrameCount;
                try
                {


                    VideoFileReader reader = new VideoFileReader();
                    reader.Open(pathSource);

                    /*
                    Console.WriteLine("width:  " + reader.Width);
                    Console.WriteLine("height: " + reader.Height);
                    Console.WriteLine("fps:    " + reader.FrameRate);
                    Console.WriteLine("codec:  " + reader.CodecName);
                    Console.WriteLine("length:  " + reader.FrameCount);
                    */

                    actualFrameCount= frameCount = reader.FrameCount;

                    int currentFrame = 0;

                    LinearAccessByteImage loadedFrame;
                    frameRate = reader.FrameRate;

                    VFSSPPFile vfssppWriter = new VFSSPPFile(pathDestination,reader.Width,reader.Height,reader.FrameCount,reader.Width*reader.Height*3);


                    while (true)
                    {

                        using (Bitmap videoFrame = reader.ReadVideoFrame())
                        {
                            if (videoFrame == null)
                                break;

                            loadedFrame = Helpers.BitmapToLinearAccessByteArray(videoFrame);


                            vfssppWriter.writeImage(loadedFrame);

                            if (currentFrame % 1000 == 0)
                            {
                                progress.Report("Converting video to VFSSPP: " + currentFrame + "/" + frameCount + " frames");
                            }


                            currentFrame++;
                            // process the frame here

                        }
                    }

                    reader.Close();

                    // If the video delivered less frames than it promised (can happen for whatever reason) then we chip off the last parts of the array
                    if (currentFrame < frameCount)
                    {
                        tooFewFramesDelivered = (int)frameCount - currentFrame;
                        actualFrameCount= currentFrame;
                        vfssppWriter.amendFrameCount(actualFrameCount);
                    }
                    vfssppWriter.finalizeFile();

                }
                catch (Exception e)
                {
                    failed = true;
                    MessageBox.Show(e.Message);
                }

            });
            if (failed)
            {
                return;
            }
            if (tooFewFramesDelivered > 0)
            {

                status_txt.Text = "Completed converting video to VFSSPP. Video reader delivered " + tooFewFramesDelivered + " fewer frame(s) than it announced. Consider redoing the input video.";
            }
            else
            {
                status_txt.Text = "Completed converting video to VFSSPP.";
            }
            vfssppExport_button.IsEnabled = true;
        }

        private void replaceFirstFrame(string filename)
        {
            if(loadedVideo == null || loadedVideo.Length == 0)
            {
                MessageBox.Show("Error replacing frame: No proper reference video loaded yet?");
                return;
            }
            LinearAccessByteImage referenceFrame = loadedVideo[0];
            LinearAccessByteImage loadedFrame = null;
            try
            {
                loadedFrame = Helpers.BitmapToLinearAccessByteArray((Bitmap)Bitmap.FromFile(filename));
            } catch (Exception e)
            {
                MessageBox.Show("Error while loading replacement frame: "+e.Message);
                return;
            }
            if(loadedFrame.width == referenceFrame.width && loadedFrame.width == referenceFrame.width && loadedFrame.pixelFormat == referenceFrame.pixelFormat)
            {
                loadedVideo[0] = loadedFrame;
                status_txt.Text = "Successfully replaced first frame with loaded image.";
            } else
            {
                MessageBox.Show("First frame can only be replaced with a compatible frame: Same width, height and pixel format.");
            }

        }

        private void ReplaceFirstFrame_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {

                replaceFirstFrame(ofd.FileName);

            }
        }
    }
}
