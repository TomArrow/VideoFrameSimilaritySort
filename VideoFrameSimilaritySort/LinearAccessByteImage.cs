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
        public byte[] imageData;
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
            int vectorSize = Vector<short>.Count; // bc thats what were gonna be using for the calculations

            int pixelCount = width * height * 3;
            int pixelCountDivisibleByVectorSize = (int)(vectorSize * Math.Ceiling((double)pixelCount / (double)vectorSize));

            imageData = new byte[pixelCountDivisibleByVectorSize]; // We're not actually going to be using the extra pixels for anything useful, it's just to avoid memory overflow when reading from the array

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
                    imageData[linearHere + x * 3] = imageDataA[strideHere + x * channelMultiplier];
                    imageData[linearHere + x * 3+1] = imageDataA[strideHere + x * channelMultiplier+1];
                    imageData[linearHere + x * 3+2] = imageDataA[strideHere + x * channelMultiplier+2];
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
                    output[strideHere + x * channelMultiplier] = imageData[linearHere + x * 3];
                    output[strideHere + x * channelMultiplier + 1] = imageData[linearHere + x * 3 + 1];
                    output[strideHere + x * channelMultiplier + 2] = imageData[linearHere + x * 3 + 2]; 
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

        public byte this[int index]
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
