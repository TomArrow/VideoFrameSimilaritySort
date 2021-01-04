using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VideoFrameSimilaritySort
{
    class LinearAccessByteImage
    {
        public sbyte[] imageData;
        public int stride;
        public int width, height;
        public PixelFormat pixelFormat;
        public PixelFormat originalPixelFormat;
        public int originalStride;

        public LinearAccessByteImage(byte[] imageDataA, int strideA, int widthA, int heightA, PixelFormat pixelFormatA)
        {
            originalStride = strideA;
            width = widthA;
            height = heightA;
            pixelFormat = PixelFormat.Format24bppRgb;
            originalPixelFormat = pixelFormatA;
            int vectorCountForMultiplication = Vector<short>.Count*2; // bc thats what were gonna be using for the calculations. 2 bc we need to use widen to go from byte to short and that creates 2 vectors.

            int pixelCount = width * height * 3;
            int pixelCountDivisibleByVectorSize = (int)(vectorCountForMultiplication * Math.Ceiling((double)pixelCount / (double)vectorCountForMultiplication));

            imageData = new sbyte[pixelCountDivisibleByVectorSize]; // We're not actually going to be using the extra pixels for anything useful, it's just to avoid memory overflow when reading from the array

            int channelMultiplier = 3;
            if (pixelFormatA == PixelFormat.Format32bppArgb)
            {
                channelMultiplier = 4;
            }

            int strideHere,linearHere;
            for (int y = 0; y < height; y++)
            {
                strideHere = y * strideA;
                linearHere = y * width*3;
                for(int x = 0; x < width; x++)
                {
                    imageData[linearHere + x * 3] = (sbyte)( imageDataA[strideHere + x * channelMultiplier]-128);
                    imageData[linearHere + x * 3+1] = (sbyte)(imageDataA[strideHere + x * channelMultiplier+1]-128);
                    imageData[linearHere + x * 3+2] = (sbyte)(imageDataA[strideHere + x * channelMultiplier+2]-128);
                }
            }

            stride = width;

            //imageData = imageDataA;
        }

        public byte[] getOriginalDataReconstruction()
        {

            int pixelCount = width * height *3;

            //int widthStrideDifference = stride - width;

            int channelMultiplier = 3;
            if (originalPixelFormat == PixelFormat.Format32bppArgb)
            {
                channelMultiplier = 4;
            }
            byte[] output = new byte[height * originalStride];

            int strideHere, linearHere;
            for (int y = 0; y < height; y++)
            {
                strideHere = y * originalStride;
                linearHere = y * width*3;
                for (int x = 0; x < width; x++)
                {
                    output[strideHere + x * channelMultiplier] = (byte)( imageData[linearHere + x * 3]+128);
                    output[strideHere + x * channelMultiplier + 1] = (byte)(imageData[linearHere + x * 3 + 1] + 128);
                    output[strideHere + x * channelMultiplier + 2] = (byte)(imageData[linearHere + x * 3 + 2] + 128); 
                    if(channelMultiplier == 4)
                    {
                        output[strideHere + x * channelMultiplier + 3] = (byte)255;
                    }
                }
            }
            return output;
        }

        public int Length
        {
            get { return imageData.Length; }
        }

        public sbyte this[int index]
        {
            get
            {
                return imageData[index];
            }

            set
            {
                imageData[index] = value;
            }
        }
    }
}
