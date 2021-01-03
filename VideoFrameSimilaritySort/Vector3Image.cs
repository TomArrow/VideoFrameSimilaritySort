using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VideoFrameSimilaritySort
{
    class Vector3Image
    {
        public Vector3[] imageData;
        public int originalStride = 0;
        public int width = 0, height = 0;
        public PixelFormat pixelFormat;

        public Vector3Image(ByteImage inputImage)
        {

            byte[] inputImageData = inputImage.imageData;
            width = inputImage.width;
            height = inputImage.height;
            originalStride = inputImage.stride;
            PixelFormat pixelFormat = inputImage.pixelFormat;

            imageData = new Vector3[width*height];
            int strideHere = 0;
            int pixelOffset;
            int offsetHere;

            int pixelMultiplier = 3;
            if(inputImage.pixelFormat == PixelFormat.Format32bppArgb)
            {
                pixelMultiplier = 4;
            }

            for (int y = 0; y < height; y++)
            {
                strideHere = originalStride * y;
                for (int x = 0; x < width; x++) // 4 bc RGBA
                {
                    pixelOffset = x * pixelMultiplier;
                    offsetHere = strideHere + pixelOffset;

                    imageData[y * width + x].X = inputImageData[offsetHere];
                    imageData[y * width + x].Y = inputImageData[offsetHere+1];
                    imageData[y * width + x].Z = inputImageData[offsetHere+2];
                }
            }

        }

        public int Length
        {
            get { return imageData.Length; }
        }

        public Vector3 this[int index]
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
