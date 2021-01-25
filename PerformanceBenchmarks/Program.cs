using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceBenchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // VectorStuff();
            Branching();

            Console.ReadKey();
        }

        static private void Branching()
        {

            int maxInnerSize = 40;
            int outerSize = 100000;
            int randomRegenerateCountPerInnerSize = 10;
            int testHowOften = 100;
            long sum = 0; // We don't really need the sum, it's just a random thing we measure basically so he's got something to do
            long time1 = 0, time2 = 0;
            long lastTime = 0;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            StringBuilder csvOutput = new StringBuilder("innerSize,timeFastSkip,timeNoBranch,ratio\r\n");


            Random rnd = new Random();
            for (int innerSize = 1; innerSize < maxInnerSize; innerSize++) { 


                for(int k=0;k< randomRegenerateCountPerInnerSize; k++) { 

                    short[][] numbersToAdd = new short[outerSize][];

                    int breakAt = 0;
                    for(int i=0;i< outerSize;i++)
                    {
                        numbersToAdd[i] = new short[innerSize];
                        breakAt = rnd.Next(0, innerSize);
                        for(int a = 0; a < breakAt; a++)
                        {
                            numbersToAdd[i][a] = (short)rnd.Next(1, short.MaxValue);
                        }
                    }
                    // To sum up: We know that once we reach a 0 in the short array, all other values are 0, so we can implement a fast skip. I want to see if the fast skip is faster than just going through the rest of the array, due to branching.

                    //Console.WriteLine("Branching data prepared. Starting test.");
                    time1 = 0;
                    time2 = 0;
                    watch.Restart();



                    for (int i = 0; i < testHowOften; i++)
                    {
                        sum = 0;
                        // Fast skip version
                        watch.Restart();
                        for (int a = 0; a < outerSize; a++)
                        {
                            for(int b=0; b < innerSize; b++)
                            {
                                sum += numbersToAdd[a][b];
                                if(numbersToAdd[a][b] == 0)
                                {
                                    break;
                                }
                            }
                        }
                        time1 += watch.ElapsedTicks;

                        sum = 0;
                        // No branch version
                        watch.Restart();
                        for (int a = 0; a < outerSize; a++)
                        {
                            for (int b = 0; b < innerSize; b++)
                            {
                                sum += numbersToAdd[a][b];
                            }
                        }
                        time2 += watch.ElapsedTicks;
                    }

                    csvOutput.Append(innerSize+","+time1+","+time2+","+((float)time1/(float)time2)+"\r\n");

                    Console.WriteLine(innerSize+" inSz: (fast skip): " + time1 + ", (no branching): " + time2+", ratio: "+ ((float)time1 / (float)time2));
                }
            }

            File.WriteAllLines("test.csv",new string[1] { csvOutput.ToString() });
        }

        static private void VectorStuff()
        {
            Vector<short> blah = new Vector<short>(short.MaxValue);
            int calc = Vector<short>.Count * short.MaxValue;
            int sum1 = Vector.Dot<short>(blah, Vector<short>.One);

            Vector<int> widenTarget1, widenTarget2;

            Vector.Widen(blah, out widenTarget1, out widenTarget2);
            int sum2 = Vector.Dot<int>(widenTarget1, Vector<int>.One) + Vector.Dot<int>(widenTarget2, Vector<int>.One);

            Console.WriteLine("Calculated: " + calc + ", summed (short): " + sum1 + ", summed (Widen,int):" + sum2);

            // Test performance

            long result1 = 0, result2 = 0;
            int vectorLength = Vector<short>.Count;

            long time1 = 0, time2 = 0;
            long lastTime = 0;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Do this 100 times
            for (int e = 0; e < 100; e++)
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

            Console.WriteLine("100 million operations in milliseconds: Time (widen & dot): " + time1 + ", Time (iterate via for): " + time2);

        }
    }
}
