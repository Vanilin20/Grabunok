using System;
using System.IO;
using System.IO.Compression;

namespace Banda.ArtGen
{
    /// <summary>
    /// Мінімальний PNG-кодек (RGBA8, без чересстрокової розгортки) на чистому .NET:
    /// генератори арту працюють і в редакторі Unity, і з консолі (dotnet) без рушія.
    /// </summary>
    public static class Png
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static uint[] _crcTable;

        public static void Write(string path, int width, int height, Rgba[] pixels)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using FileStream file = File.Create(path);
            file.Write(Signature, 0, Signature.Length);
            byte[] ihdr = new byte[13];
            WriteBigEndian(ihdr, 0, (uint)width);
            WriteBigEndian(ihdr, 4, (uint)height);
            ihdr[8] = 8; // біт на канал
            ihdr[9] = 6; // RGBA
            WriteChunk(file, "IHDR", ihdr);

            byte[] raw = new byte[(width * 4 + 1) * height];
            int o = 0;
            for (int y = 0; y < height; y++)
            {
                raw[o++] = 0; // фільтр None
                for (int x = 0; x < width; x++)
                {
                    Rgba c = pixels[y * width + x];
                    raw[o++] = c.R;
                    raw[o++] = c.G;
                    raw[o++] = c.B;
                    raw[o++] = c.A;
                }
            }

            WriteChunk(file, "IDAT", ZlibCompress(raw));
            WriteChunk(file, "IEND", Array.Empty<byte>());
        }

        /// <summary>Читає PNG RGBA8/RGB8 (кольоровий тип 6 або 2, 8 біт, без interlace) — для повторного використання арту.</summary>
        public static Rgba[] Read(string path, out int width, out int height)
        {
            byte[] data = File.ReadAllBytes(path);
            int pos = 8;
            width = 0;
            height = 0;
            int colorType = 6;
            MemoryStream idat = new MemoryStream();
            byte[] palette = null;
            byte[] paletteAlpha = null;
            while (pos < data.Length)
            {
                int length = (int)ReadBigEndian(data, pos);
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                int start = pos + 8;
                if (type == "IHDR")
                {
                    width = (int)ReadBigEndian(data, start);
                    height = (int)ReadBigEndian(data, start + 4);
                    if (data[start + 8] != 8 || data[start + 12] != 0)
                    {
                        throw new NotSupportedException($"{path}: потрібен PNG 8 біт без interlace");
                    }

                    colorType = data[start + 9];
                }
                else if (type == "PLTE")
                {
                    palette = new byte[length];
                    Array.Copy(data, start, palette, 0, length);
                }
                else if (type == "tRNS")
                {
                    paletteAlpha = new byte[length];
                    Array.Copy(data, start, paletteAlpha, 0, length);
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, start, length);
                }
                else if (type == "IEND")
                {
                    break;
                }

                pos = start + length + 4;
            }

            int bpp = colorType == 6 ? 4 : colorType == 2 ? 3 : colorType == 3 ? 1 : colorType == 4 ? 2 : colorType == 0 ? 1 : 0;
            if (bpp == 0)
            {
                throw new NotSupportedException($"{path}: тип кольору {colorType} не підтримано");
            }

            byte[] raw = ZlibDecompress(idat.ToArray());
            int stride = width * bpp;
            byte[] cur = new byte[stride];
            byte[] prev = new byte[stride];
            Rgba[] result = new Rgba[width * height];
            int r = 0;
            for (int y = 0; y < height; y++)
            {
                byte filter = raw[r++];
                Array.Copy(raw, r, cur, 0, stride);
                r += stride;
                Unfilter(filter, cur, prev, bpp);
                for (int x = 0; x < width; x++)
                {
                    int i = x * bpp;
                    switch (colorType)
                    {
                        case 6:
                            result[y * width + x] = new Rgba(cur[i], cur[i + 1], cur[i + 2], cur[i + 3]);
                            break;
                        case 2:
                            result[y * width + x] = new Rgba(cur[i], cur[i + 1], cur[i + 2], 255);
                            break;
                        case 4:
                            result[y * width + x] = new Rgba(cur[i], cur[i], cur[i], cur[i + 1]);
                            break;
                        case 0:
                            result[y * width + x] = new Rgba(cur[i], cur[i], cur[i], 255);
                            break;
                        default:
                            int p = cur[i];
                            byte a = paletteAlpha != null && p < paletteAlpha.Length ? paletteAlpha[p] : (byte)255;
                            result[y * width + x] = new Rgba(palette[p * 3], palette[p * 3 + 1], palette[p * 3 + 2], a);
                            break;
                    }
                }

                byte[] swap = prev;
                prev = cur;
                cur = swap;
            }

            return result;
        }

        private static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
        {
            for (int i = 0; i < cur.Length; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0;
                int b = prev[i];
                int c = i >= bpp ? prev[i - bpp] : 0;
                int add;
                switch (filter)
                {
                    case 1:
                        add = a;
                        break;
                    case 2:
                        add = b;
                        break;
                    case 3:
                        add = (a + b) / 2;
                        break;
                    case 4:
                        int pa = Math.Abs(b - c);
                        int pb = Math.Abs(a - c);
                        int pc = Math.Abs(a + b - 2 * c);
                        add = pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                        break;
                    default:
                        add = 0;
                        break;
                }

                cur[i] = (byte)(cur[i] + add);
            }
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using MemoryStream output = new MemoryStream();
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            using (DeflateStream deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
            {
                deflate.Write(data, 0, data.Length);
            }

            uint adler = Adler32(data);
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
            return output.ToArray();
        }

        private static byte[] ZlibDecompress(byte[] data)
        {
            using MemoryStream input = new MemoryStream(data, 2, data.Length - 2);
            using DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress);
            using MemoryStream output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] header = new byte[8];
            WriteBigEndian(header, 0, (uint)data.Length);
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            Array.Copy(typeBytes, 0, header, 4, 4);
            stream.Write(header, 0, 8);
            stream.Write(data, 0, data.Length);
            uint crc = Crc(typeBytes, data);
            byte[] crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            stream.Write(crcBytes, 0, 4);
        }

        private static uint Crc(byte[] type, byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++)
                    {
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    }

                    _crcTable[n] = c;
                }
            }

            uint crc = 0xFFFFFFFFu;
            foreach (byte b in type)
            {
                crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            foreach (byte b in data)
            {
                crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1;
            uint b = 0;
            foreach (byte d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        private static void WriteBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint ReadBigEndian(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
        }
    }
}
