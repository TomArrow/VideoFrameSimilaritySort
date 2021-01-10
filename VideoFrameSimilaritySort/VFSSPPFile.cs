using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VideoFrameSimilaritySort
{
    // VFSSPP stands for Video Frame Similairy Sort Pre-Processed
    // It's a file format to convert a video file to, basically, to be able to read it in the cross-platform CLI version without needing to rely on the Windows-only ffmpeg library
    class VFSSPPFile
    {
        // Careful changing this. This is the header file format.
        public struct Header
        {
            public UInt32 width;
            public UInt32 height;
            public UInt64 imageCount;
            public UInt64 singleImageByteSize;
            public UInt32 firstImageByteOffset;
        };

        enum OpenMode { READ, WRITE};

        OpenMode openMode;

        public const int firstImageByteOffset = 1000; // We just set that fixed, but we still write it to the header in case it ever changes.

        ~VFSSPPFile()
        {
            if(openMode == OpenMode.READ)
            {
                reader.Close();
                reader.Dispose();
            } else if (openMode == OpenMode.WRITE)
            {
                if (!isFinalized)
                {
                    writer.Seek(0, SeekOrigin.Begin);
                    writer.Write(StructToByteArray(_header)); // Dump the header in case it was changed, like when expected framecount wasnt delivered.
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        // Common things:

        public Header _header;
        
        //
        //
        //
        // READ CODE
        //
        //
        //
        BinaryReader reader;
        public VFSSPPFile(string inputFile)
        {
            openMode = OpenMode.READ;
            reader = new BinaryReader(File.Open(inputFile, FileMode.Open));
            _header = ReadStruct<Header>(reader);

            reader.BaseStream.Seek(_header.firstImageByteOffset,SeekOrigin.Begin);
        }

        UInt64 readFrames = 0;

        public LinearAccessByteImage readImage()
        {

            if (openMode == OpenMode.WRITE)
            {
                throw new Exception("Attempting to read in write mode.");
            }
            if(readFrames == _header.imageCount)
            {

                // Already read all there is
                return null;
            }

            LinearAccessByteImage result =  new LinearAccessByteImage(reader.ReadBytes((int)_header.singleImageByteSize),(int)_header.width*3,(int)_header.width,(int)_header.height,3);

            readFrames++;

            return result;
        }

        //
        //
        //
        // WRITE CODE
        //
        //
        //
        // Always call finalize when you're done. Don't trust the destructor, just in case!
        BinaryWriter writer;
        bool isFinalized;
        public VFSSPPFile(string outputFile, int width, int height, long imageCount, long singleImageByteSize)
        {
            openMode = OpenMode.WRITE;
            _header = new Header();
            _header.width = (uint)width;
            _header.height = (uint)height;
            _header.imageCount = (UInt64)imageCount;
            _header.singleImageByteSize = (UInt64)singleImageByteSize;
            _header.firstImageByteOffset = firstImageByteOffset;

            writer = new BinaryWriter(File.Open(outputFile,FileMode.Create));

            writer.Write(StructToByteArray(_header));

            writer.Seek(firstImageByteOffset,SeekOrigin.Begin);

        }

        public void writeImage(LinearAccessByteImage inputImage)
        {
            if (isFinalized)
            {
                throw new Exception("Attempting to change finalized file.");
            }
            if (openMode == OpenMode.READ)
            {
                throw new Exception("Attempting to write in read mode.");
            }
            byte[] buffer = new byte[_header.singleImageByteSize];
            for(int y = 0; y < _header.height; y++)
            {
                for(int x=0; x < _header.width; x++)
                {
                    buffer[y * _header.width * 3 + x * 3] = (byte)(128+inputImage.imageData[y * _header.width * 3 + x * 3]);
                    buffer[y * _header.width * 3 + x * 3+1] = (byte)(128+inputImage.imageData[y * _header.width * 3 + x * 3+1]);
                    buffer[y * _header.width * 3 + x * 3+2] = (byte)(128+inputImage.imageData[y * _header.width * 3 + x * 3+2]);
                }
            }
            writer.Write(buffer);

        }

        public void amendFrameCount(long correctFrameCount)
        {
            if (isFinalized)
            {
                throw new Exception("Attempting to change finalized file.");
            }
            if (openMode == OpenMode.READ)
            {
                throw new Exception("Attempting to write in read mode.");
            }
            if (openMode == OpenMode.WRITE)
            {
                _header.imageCount = (UInt64)correctFrameCount;
            }
            
        }

        public void finalizeFile()
        {
            writer.Seek(0, SeekOrigin.Begin);
            writer.Write(StructToByteArray(_header)); // Dump the header in case it was changed, like when expected framecount wasnt delivered.
            writer.Close();
            writer.Dispose();
            isFinalized = true;
        }




        private static T ReadStruct<T>(BinaryReader binary_reader) where T : struct
        {
            Byte[] buffer = new Byte[Marshal.SizeOf(typeof(T))];
            binary_reader.Read(buffer, 0, buffer.Count());

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            T result = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return result;
        }

        private static byte[] StructToByteArray(Header header)
        {
            try
            {
                // This function copies the structure data into a byte[] 

                //Set the buffer to the correct size 
                byte[] buffer = new byte[Marshal.SizeOf(header)];

                //Allocate the buffer to memory and pin it so that GC cannot use the 
                //space (Disable GC) 
                GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                // copy the struct into int byte[] mem alloc 
                Marshal.StructureToPtr(header, h.AddrOfPinnedObject(), false);

                h.Free(); //Allow GC to do its job 

                return buffer; // return the byte[]. After all that's why we are here 
                               // right. 
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
