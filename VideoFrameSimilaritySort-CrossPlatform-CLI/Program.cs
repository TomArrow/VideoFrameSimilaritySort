using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace VideoFrameSimilaritySort_CrossPlatform_CLI
{
    class Program
    {


        static bool videoIsLoaded = false;
        static int[] orderedVideo;
        static bool videoIsOrdered = false;

        static LinearAccessByteImage[] loadedVideo;

        // Statistics
        static float[] fps;
        static float highestFPS;
        static float lowestFPS;
        static float[] smallestDifferences;
        static float smallestSmallestDifference;
        static float highestSmallestDifference;

        static async Task Main(string[] args)
        {

#if DEBUG
            args = new string[1] {"testateshsrhrsrsdh.vfsspp"};
#endif

            if (args.Length == 0)
            {
                Console.WriteLine("Need to provide a VFSSPP file as an argument.");
            } else
            {
                string inputFilename = args[0];
                if (Path.GetExtension(inputFilename).ToLower() != ".vfsspp" || !File.Exists(inputFilename))
                {
                    Console.WriteLine("Invalid argument"+ inputFilename + ". Please provide an existing .vfsspp file as argument.");
                }
                else
                {
                    string outputTxtFileName;
                    int overWriteProtectionIndex = 0;
                    while(File.Exists(outputTxtFileName = Path.GetDirectoryName(Path.GetFullPath(inputFilename)) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(inputFilename) +"_"+overWriteProtectionIndex+ ".txt"))
                    {
                        overWriteProtectionIndex++;
                    }

                    readVFSSPPFile(inputFilename);

                    Console.WriteLine("VFSSPP file was loaded: " + inputFileHeaders.width+"x" + +inputFileHeaders.height+", " + +inputFileHeaders.imageCount+" images, " + +inputFileHeaders.singleImageByteSize +" single image byte size, " + inputFileHeaders.firstImageByteOffset+" offset");
                    Console.WriteLine("Press any key to process.");
                    Console.ReadKey();

                    await processVideo();

                    Console.WriteLine("Writing ordered frames to "+ outputTxtFileName);

                    File.WriteAllText(outputTxtFileName, string.Join("\n", orderedVideo));
                }
            }

            Console.WriteLine("Press any key to blah blah blah");
            Console.ReadKey();
        }

        static VFSSPPFile.Header inputFileHeaders;

        private static void readVFSSPPFile(string path)
        {
            VFSSPPFile inputFile = new VFSSPPFile(path);
            loadedVideo = new LinearAccessByteImage[inputFile._header.imageCount];

            inputFileHeaders = inputFile._header;

            long index= 0;
            LinearAccessByteImage tmp;
            while((tmp = inputFile.readImage() )!= null)
            {
                loadedVideo[index++] = tmp;
            }
        }

        struct ProcessingProgressReport
        {
            public string message;
            public int currentIndex;
            public bool drawStats; // whether to draw stats

            public ProcessingProgressReport(string messageA = "", int currentIndexA = 0, bool drawStatsA = false)
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


        private static async Task processVideo()
        {

            //int maxThreads = 

            var progressHandler = new Progress<ProcessingProgressReport>(value =>
            {
                //Console.WriteLine(value.message);
                Console.Write("\r"+value.message);
                //status_txt.Text = value.message;
                //if (value.drawStats) drawStats(value.currentIndex);
            });
            Console.WriteLine("Commencing processing.");
            Console.WriteLine("Yada yada blah blah.");
            Console.WriteLine();
            Console.WriteLine();

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

                                // Danger: XOR fuckery
                                //tmpDiff = currentFramePixel - compareFramePixel;
                                //(tmpDiff ^ (tmpDiff >> 31)) - (tmpDiff >> 31);
                                // /Danger XOR fuckery over


                                donePixelsPerVectorElement += 2;

                                if (donePixelsPerVectorElement >= (maxDifferencesPerVector - 2))
                                {

                                    for (a = 0; a < elementsPerVector; a++)
                                    {
                                        thisFrameDifference += thisFrameRGBDifference[a];
                                    }
                                    donePixelsPerVectorElement = 0;
                                    if (thisFrameDifference / pixelCountX3 > smallestDifferenceImpreciseOpt) break;
                                    thisFrameRGBDifference = new Vector<short>(0);
                                }
                                /*for(a=0; a < elementsPerVector; a++)
                                {
                                    currentFramePixel.Item[]
                                }*/
                            }
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


                        //Stats
                        fps[currentIndex] = 1 / ((float)stopWatch.ElapsedTicks / (float)Stopwatch.Frequency);
                        if (fps[currentIndex] < lowestFPS) lowestFPS = fps[currentIndex];
                        if (fps[currentIndex] > highestFPS) highestFPS = fps[currentIndex];
                        smallestDifferences[currentIndex] = (float)smallestDifference;
                        if (smallestDifference < smallestSmallestDifference) smallestSmallestDifference = smallestDifferences[currentIndex];
                        if (smallestDifference > highestSmallestDifference && smallestDifference != double.PositiveInfinity) highestSmallestDifference = smallestDifferences[currentIndex];
                        progressReport.drawStats = currentIndex % 100 == 0; // Only after 100 processed frames draw stats, to not have a notable performance impact
                        stopWatch.Restart();

                        // Status update
                        progressReport.message = "Processing: " + currentIndex + "/" + frameCount + " ordered frames.";//+" Current frame is " + currentFrame + ", last smallest difference was " + smallestDifference;
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
                    Console.WriteLine("le error: " + e.Message);
                }
#endif
                progressReport.message = "Processing finished";
                progressReport.drawStats = true;
                progressReport.currentIndex = frameCount - 1;
                progress.Report(progressReport);
                orderedVideo = sortedIndizi;
            });

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Finished processing");
        }
    }
}
