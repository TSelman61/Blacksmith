using Blacksmith.Compressions;
using Blacksmith.Enums;
using Blacksmith.FileTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

// not Super Mario Odyssey!
// Odyssey uses Oodle compression

namespace Blacksmith.Games
{
    public class Odyssey
    {
        #region Structs
        public struct HeaderBlock
        {
            public long Identifier { get; internal set; }
            public short Version { get; internal set; }
            public Compression Compression { get; internal set; }
            // skip 4 bytes
            public int BlockCount { get; internal set; }
        }

        public struct BlockIndex
        {
            public int UncompressedSize { get; internal set; } // both are uint, but who cares?
            public int CompressedSize { get; internal set; }
        }

        public struct DataChunk
        {
            public int Checksum { get; internal set; }
            public byte[] Data { get; internal set; }
        }

        public struct DatafileHeader
        {
            public int ResourceType { get; internal set; }
            public int FileSize { get; internal set; }
            public int FileNameSize { get; internal set; }
            public char[] FileName { get; internal set; }
        }

        public struct TopMip
        {
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            // skip 8 bytes
            public DXT DXTType { get; internal set; }
            // skip 4 bytes
            public int Mipmaps { get; internal set; }
        }

        public struct TextureMap
        {
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            // skip 8 bytes
            public DXT DXTType { get; internal set; }
            // skip 8 bytes
            public int MipmapCount { get; internal set; }
            // skip 130 bytes
            public int DataSize { get; internal set; }
            public byte[] Data { get; internal set; }
        }
        #endregion

        #region Raw Files
        /// <summary>
        /// Reads a file extracted from the forge file and writes all its decompressed data chunks to a file
        /// </summary>
        /// <param name="fileName"></param>
        public static bool ReadFile(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            List<byte[]> combinedData = new List<byte[]>();
            using (Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    long[] identifierOffsets = Helpers.LocateRawDataIdentifier(reader);
                    long x = identifierOffsets[1];

                    // skip to this Raw Data Block's offset
                    reader.BaseStream.Seek(x, SeekOrigin.Begin);

                    // Header Block
                    HeaderBlock header = new HeaderBlock
                    {
                        Identifier = reader.ReadInt64(),
                        Version = reader.ReadInt16(),
                        Compression = (Compression)Enum.ToObject(typeof(Compression), reader.ReadByte())
                    };

                    // skip 4 bytes
                    reader.BaseStream.Seek(4, SeekOrigin.Current);

                    // finish reading the Header Block
                    header.BlockCount = reader.ReadInt32();

                    // Block Indices
                    BlockIndex[] indices = new BlockIndex[header.BlockCount];
                    for (int i = 0; i < header.BlockCount; i++)
                    {
                        indices[i] = new BlockIndex
                        {
                            UncompressedSize = reader.ReadInt32(),
                            CompressedSize = reader.ReadInt32()
                        };
                    }

                    // Data Chunks
                    DataChunk[] chunks = new DataChunk[header.BlockCount];
                    for (int i = 0; i < header.BlockCount; i++)
                    {
                        chunks[i] = new DataChunk
                        {
                            Checksum = reader.ReadInt32(),
                            Data = reader.ReadBytes(indices[i].CompressedSize)
                        };

                        // if the compressedSize and uncompressedSize do not match, decompress data
                        // otherwise, the data was not ever compressed
                        byte[] decompressed = indices[i].CompressedSize == indices[i].UncompressedSize ?
                            chunks[i].Data :
                            Oodle.Decompress(chunks[i].Data, indices[i].CompressedSize, indices[i].UncompressedSize);

                        // add decompressed data to combinedData
                        combinedData.Add(decompressed);
                    }
                }
            }

            // write all decompressed data chunks (stored in combinedData) to a combined file
            Helpers.WriteToTempFile($"{fileName}.dec", combinedData.ToArray());

            return true;
        }

        public static byte[] ReadFile(byte[] rawData)
        {
            List<byte> combinedData = new List<byte>();
            using (Stream stream = new MemoryStream(rawData))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    long[] identifierOffsets = Helpers.LocateRawDataIdentifier(reader);
                    long x = identifierOffsets[1];

                    // skip to this Raw Data Block's offset
                    reader.BaseStream.Seek(x, SeekOrigin.Begin);

                    // Header Block
                    HeaderBlock header = new HeaderBlock
                    {
                        Identifier = reader.ReadInt64(),
                        Version = reader.ReadInt16(),
                        Compression = (Compression)Enum.ToObject(typeof(Compression), reader.ReadByte())
                    };

                    // skip 4 bytes
                    reader.BaseStream.Seek(4, SeekOrigin.Current);

                    // finish reading the Header Block
                    header.BlockCount = reader.ReadInt32();

                    // Block Indices
                    BlockIndex[] indices = new BlockIndex[header.BlockCount];
                    for (int i = 0; i < header.BlockCount; i++)
                    {
                        indices[i] = new BlockIndex
                        {
                            UncompressedSize = reader.ReadInt32(),
                            CompressedSize = reader.ReadInt32()
                        };
                    }

                    // Data Chunks
                    DataChunk[] chunks = new DataChunk[header.BlockCount];
                    for (int i = 0; i < header.BlockCount; i++)
                    {
                        chunks[i] = new DataChunk
                        {
                            Checksum = reader.ReadInt32(),
                            Data = reader.ReadBytes(indices[i].CompressedSize)
                        };

                        // if the compressedSize and uncompressedSize do not match, decompress data
                        // otherwise, the data was not ever compressed
                        byte[] decompressed = indices[i].CompressedSize == indices[i].UncompressedSize ?
                            chunks[i].Data :
                            Oodle.Decompress(chunks[i].Data, indices[i].CompressedSize, indices[i].UncompressedSize);

                        // add decompressed data to combinedData
                        combinedData.AddRange(decompressed);
                    }
                }
            }

            using (Stream stream = new MemoryStream(combinedData.ToArray()))
            {
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }
        #endregion

        #region Datafile
        public static void ReadDatafile(string fileName)
        {
            using (Stream stream = new FileStream($"{fileName}.dec", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    DatafileHeader header = new DatafileHeader
                    {
                        ResourceType = reader.ReadInt32(),
                        FileSize = reader.ReadInt32(),
                        FileNameSize = reader.ReadInt32()
                    };
                    header.FileName = reader.ReadChars(header.FileNameSize);
                }
            }
        }
        #endregion

        #region Textures
        public static void ExtractTextureMap(string fileName, Forge forge, Action conversionCompleted)
        {
            using (Stream stream = new FileStream($"{fileName}.dec", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    DatafileHeader header = new DatafileHeader
                    {
                        ResourceType = reader.ReadInt32(),
                        FileSize = reader.ReadInt32(),
                        FileNameSize = reader.ReadInt32()
                    };
                    header.FileName = reader.ReadChars(header.FileNameSize);

                    // ignore the 2 bytes, file ID, and resource type
                    reader.BaseStream.Seek(14, SeekOrigin.Current);

                    // toppmip 0
                    TopMip mip0 = new TopMip
                    {
                        Width = reader.ReadInt32(),
                        Height = reader.ReadInt32()
                    };
                    reader.BaseStream.Seek(8, SeekOrigin.Current);
                    mip0.DXTType = DXTExtensions.GetDXT(reader.ReadInt32());
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mip0.Mipmaps = reader.ReadInt32();

                    reader.BaseStream.Seek(81, SeekOrigin.Current);

                    // topmip 1
                    TopMip mip1 = new TopMip
                    {
                        Width = reader.ReadInt32(),
                        Height = reader.ReadInt32()
                    };
                    reader.BaseStream.Seek(8, SeekOrigin.Current);
                    mip1.DXTType = DXTExtensions.GetDXT(reader.ReadInt32());
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mip1.Mipmaps = reader.ReadInt32();

                    // locate the two topmips, if they exist
                    if (forge.FileEntries.Where(x => x.NameTable.Name.Contains(Path.GetFileName(fileName) + "_TopMip")).Count() == 2)
                    {
                        Forge.FileEntry topMipEntry = forge.FileEntries.Where(x => x.NameTable.Name == Path.GetFileName(fileName) + "_TopMip_0").First();

                        // extract, read, and create DDS images with the first topmips
                        byte[] rawData = forge.GetRawData(topMipEntry);
                        Helpers.WriteToTempFile(topMipEntry.NameTable.Name, rawData);

                        ReadFile(Helpers.GetTempPath(topMipEntry.NameTable.Name));

                        ExtractTopMip(Helpers.GetTempPath(topMipEntry.NameTable.Name), mip0);
                    }
                    else // topmips do not exist. fear not! there is still image data found here. let us use that.
                    {
                        reader.BaseStream.Seek(25, SeekOrigin.Current);
                        TextureMap map = new TextureMap
                        {
                            DataSize = reader.ReadInt32()
                        };

                        byte[] mipmapData = reader.ReadBytes(map.DataSize);

                        // write DDS file
                        Helpers.WriteTempDDS(fileName, mipmapData, mip0.Width, mip0.Height, mip0.Mipmaps, mip0.DXTType);

                        // convert DDS to PNG
                        Helpers.ConvertDDSToPNG($"{Helpers.GetTempPath(fileName)}.dds");
                    }
                }
            }

            Thread.Sleep(1000);
            conversionCompleted();
        }

        private static void ExtractTopMip(string fileName, TopMip topMip)
        {
            /*
             * Diffuse maps = DXT1
             * Normal maps = DX10 w/ BC7_UNORM
             */
            using (Stream stream = new FileStream($"{fileName}.dec", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // this is omnipresent
                    DatafileHeader header = new DatafileHeader
                    {
                        ResourceType = reader.ReadInt32(),
                        FileSize = reader.ReadInt32(),
                        FileNameSize = reader.ReadInt32()
                    };
                    header.FileName = reader.ReadChars(header.FileNameSize);
                    
                    reader.BaseStream.Seek(18, SeekOrigin.Current);
                    byte[] data = reader.ReadBytes(header.FileSize - 18);

                    // write DDS file
                    Helpers.WriteTempDDS(fileName, data, topMip.Width, topMip.Height, topMip.Mipmaps, topMip.DXTType);

                    // convert DDS to PNG
                    Helpers.ConvertDDSToPNG(string.Format("{0}.dds", Helpers.GetTempPath(fileName)));
                }
            }
        }
        #endregion

        #region Materials
        #endregion

        #region Models
        public static void ExtractModel(string fileName)
        {
            using (Stream stream = new FileStream($"{fileName}-.dec", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // this is omnipresent
                    DatafileHeader file = new DatafileHeader
                    {
                        ResourceType = reader.ReadInt32(),
                        FileSize = reader.ReadInt32(),
                        FileNameSize = reader.ReadInt32()
                    };
                    file.FileName = reader.ReadChars(file.FileNameSize);

                    // ToDo: implement the model stuff
                }
            }
        }
        #endregion
    }
}