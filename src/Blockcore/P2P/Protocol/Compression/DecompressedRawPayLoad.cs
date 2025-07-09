using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.P2P.Protocol.Payloads;

namespace Blockcore.P2P.Protocol.Compression
{
    /// <summary>
    /// Decompressed raw data payload.
    /// </summary>
    [Payload("decompressed")]
    public class DecompressedRawPayload : Payload
    {
        private byte[] data;
        private string originalCommand; // Store original command separately

        public byte[] Data
        {
            get
            {
                return this.data;
            }

            set
            {
                this.data = value;
            }
        }

        /// <summary>
        /// Gets the original command name before compression.
        /// </summary>
        public string OriginalCommand
        {
            get
            {
                return this.originalCommand;
            }

            private set
            {
                this.originalCommand = value;
            }
        }

        public DecompressedRawPayload(string originalCommand = null, byte[] data = null)
        {
            this.originalCommand = originalCommand ?? string.Empty;
            this.data = data ?? new byte[0];
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            // Manual serialization to avoid ref issues
            if (stream.Serializing)
            {
                // Write original command length and command
                var commandBytes = Encoding.UTF8.GetBytes(this.originalCommand ?? string.Empty);
                stream.ReadWrite((byte)commandBytes.Length);
                if (commandBytes.Length > 0)
                    stream.ReadWrite(ref commandBytes);

                // Write data length and data
                stream.ReadWrite((uint)(this.data?.Length ?? 0));
                if (this.data != null && this.data.Length > 0)
                    stream.ReadWrite(ref this.data);
            }
            else
            {
                // Read original command
                byte commandLength = 0;
                stream.ReadWrite(ref commandLength);
                if (commandLength > 0)
                {
                    byte[] commandBytes = new byte[commandLength];
                    stream.ReadWrite(ref commandBytes);
                    this.originalCommand = Encoding.UTF8.GetString(commandBytes);
                }
                else
                {
                    this.originalCommand = string.Empty;
                }

                // Read data
                uint dataLength = 0;
                stream.ReadWrite(ref dataLength);
                if (dataLength > 0)
                {
                    this.data = new byte[dataLength];
                    stream.ReadWrite(ref this.data);
                }
                else
                {
                    this.data = new byte[0];
                }
            }
        }

        public override string ToString()
        {
            return $"DecompressedRawPayload: OriginalCommand={this.OriginalCommand}, Data={this.Data?.Length ?? 0} bytes";
        }
    }
}
