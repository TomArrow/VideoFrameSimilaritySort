using System;
using System.Collections.Generic;
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

            long time1=0, time2=0;
            long lastTime = 0;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Do this 100 times
            for(int e = 0; e < 100; e++) { 

                // Do 1 million of each
                for (int i = 0; i < 1000000; i++)
                {
                    Vector.Widen(blah, out widenTarget1, out widenTarget2);
                    result1 += Vector.Dot<int>(widenTarget1, Vector<int>.One) + Vector.Dot<int>(widenTarget2, Vector<int>.One);
                }
                time1 += watch.ElapsedMilliseconds-lastTime;
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

            Console.ReadKey();
        }
    }
}
