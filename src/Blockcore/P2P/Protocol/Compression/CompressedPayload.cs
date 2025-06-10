using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.P2P.Protocol.Payloads;
using K4os.Compression.LZ4;

namespace Blockcore.P2P.Protocol.Compression
{
    /// <summary>
    /// Simple compressed payload with manual serialization.
    /// </summary>
    [Payload("compressed")]
    public class CompressedPayload : Payload
    {
        public string OriginalCommand { get; set; } = string.Empty;

        private byte[] compressedData;

        public byte[] CompressedData
        {
            get => this.compressedData;
            set => this.compressedData = value;
        }

        public uint OriginalSize { get; set; }

        public CompressedPayload() { }

        public CompressedPayload(string command, byte[] data)
        {
            this.OriginalCommand = command ?? string.Empty;
            this.OriginalSize = (uint)data.Length;
            this.CompressedData = CompressData(data);
        }

        public static byte[] CompressData(byte[] data)
        {
            try
            {
                int maxCompressedSize = LZ4Codec.MaximumOutputSize(data.Length);
                byte[] compressedBuffer = new byte[maxCompressedSize];

                int compressedSize = LZ4Codec.Encode(
                    data, 0, data.Length,
                    compressedBuffer, 0, maxCompressedSize);

                if (compressedSize <= 0)
                    return data;

                byte[] result = new byte[compressedSize];
                Array.Copy(compressedBuffer, 0, result, 0, compressedSize);

                return result.Length < data.Length ? result : data;
            }
            catch
            {
                return data;
            }
        }

        public byte[] DecompressData()
        {
            try
            {
                byte[] decompressed = new byte[this.OriginalSize];

                int decompressedSize = LZ4Codec.Decode(
                    this.CompressedData, 0, this.CompressedData.Length,
                    decompressed, 0, (int)this.OriginalSize);

                if (decompressedSize != this.OriginalSize)
                    return this.CompressedData;

                return decompressed;
            }
            catch
            {
                return this.CompressedData;
            }
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            // Manual serialization to avoid ref issues
            if (stream.Serializing)
            {
                // Write command length and command
                var commandBytes = System.Text.Encoding.UTF8.GetBytes(this.OriginalCommand ?? string.Empty);
                stream.ReadWrite((byte)commandBytes.Length);
                if (commandBytes.Length > 0)
                    stream.ReadWrite(ref commandBytes);

                // Write original size
                stream.ReadWrite(this.OriginalSize);

                // Write compressed data length and data
                stream.ReadWrite((uint)(this.CompressedData?.Length ?? 0));
                if (this.CompressedData != null && this.CompressedData.Length > 0)
                    stream.ReadWrite(ref this.compressedData);
            }
            else
            {
                // Read command
                byte commandLength = 0;
                stream.ReadWrite(ref commandLength);
                if (commandLength > 0)
                {
                    byte[] commandBytes = new byte[commandLength];
                    stream.ReadWrite(ref commandBytes);
                    this.OriginalCommand = System.Text.Encoding.UTF8.GetString(commandBytes);
                }
                else
                {
                    this.OriginalCommand = string.Empty;
                }

                // Read original size
                uint tempOriginalSize = 0;
                stream.ReadWrite(ref tempOriginalSize);
                this.OriginalSize = tempOriginalSize;

                // Read compressed data
                uint compressedLength = 0;
                stream.ReadWrite(ref compressedLength);
                if (compressedLength > 0)
                {
                    this.CompressedData = new byte[compressedLength];
                    stream.ReadWriteBytes(ref this.compressedData);
                }
                else
                {
                    this.CompressedData = new byte[0];
                }
            }
        }

        public override string ToString()
        {
            return $"CompressedPayload: {this.OriginalCommand}, Original: {this.OriginalSize}, Compressed: {this.CompressedData?.Length ?? 0}";
        }
    }
}
