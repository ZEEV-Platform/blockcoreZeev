using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.NBitcoin;
using Blockcore.P2P.Peer;
using Blockcore.P2P.Protocol.Payloads;
using Microsoft.Extensions.Logging;

namespace Blockcore.P2P.Protocol.Compression
{
    public class CompressionProcessor
    {
        private readonly ILogger logger;

        private ConsensusFactory ConsensusFactory { get; set; }

        public CompressionProcessor(ILogger logger, ConsensusFactory consensusFactory)
        {
            this.logger = logger;
            this.ConsensusFactory = consensusFactory;
        }

        public async Task DecompressDataAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                var compressed = (CompressedPayload)message.Message.Payload;

                // Decompress the data
                var decompressedData = compressed.DecompressData();

                this.logger.LogDebug("Decompressed {Command} message: {OriginalSize} -> {CompressedSize} bytes",
                    compressed.OriginalCommand,
                    compressed.OriginalSize,
                    compressed.CompressedData.Length);

                // Try to parse back to original payload type if possible
                Payload reconstructedPayload = this.TryReconstructPayload(compressed.OriginalCommand, decompressedData);

                if (reconstructedPayload != null)
                {
                    // Successfully reconstructed original payload
                    var newMessage = new Message
                    {
                        Command = compressed.OriginalCommand,
                        Payload = reconstructedPayload
                    };
                    message.Message = newMessage;

                    this.logger.LogDebug("Successfully reconstructed {Command} payload", compressed.OriginalCommand);
                }
                else
                {
                    // Fallback to raw data payload
                    var rawPayload = new DecompressedRawPayload(compressed.OriginalCommand, decompressedData);
                    var newMessage = new Message
                    {
                        Command = compressed.OriginalCommand,
                        Payload = rawPayload
                    };
                    message.Message = newMessage;

                    this.logger.LogDebug("Using raw data payload for {Command}", compressed.OriginalCommand);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to decompress message from peer {Peer}", peer.RemoteSocketEndpoint);
            }


            await Task.CompletedTask;
        }

        /// <summary>
        /// Attempts to reconstruct the original payload from decompressed data.
        /// Returns null if reconstruction fails.
        /// </summary>
        private Payload TryReconstructPayload(string command, byte[] data)
        {
            try
            {
                using (var memoryStream = new MemoryStream(data))
                {
                    var stream = new BitcoinStream(memoryStream, false, this.ConsensusFactory);

                    // Try to reconstruct known payload types
                    return command.ToLower() switch
                    {
                        // Standard P2P payloads
                        "addr" => this.ReadPayload<AddrPayload>(stream),
                        "block" => this.ReadPayload<BlockPayload>(stream),
                        "getaddr" => this.ReadPayload<GetAddrPayload>(stream),
                        "getblocks" => this.ReadPayload<GetBlocksPayload>(stream),
                        "getdata" => this.ReadPayload<GetDataPayload>(stream),
                        "getheaders" => this.ReadPayload<GetHeadersPayload>(stream),
                        "getprovhdr" => this.ReadPayload<GetProvenHeadersPayload>(stream),
                        "havewitness" => this.ReadPayload<HaveWitnessPayload>(stream),
                        "headers" => this.ReadPayload<HeadersPayload>(stream),
                        "inv" => this.ReadPayload<InvPayload>(stream),
                        "mempool" => this.ReadPayload<MempoolPayload>(stream),
                        "notfound" => this.ReadPayload<NotFoundPayload>(stream),
                        "ping" => this.ReadPayload<PingPayload>(stream),
                        "pong" => this.ReadPayload<PongPayload>(stream),
                        "provhdr" => this.ReadPayload<ProvenHeadersPayload>(stream),
                        "reject" => this.ReadPayload<RejectPayload>(stream),
                        "sendheaders" => this.ReadPayload<SendHeadersPayload>(stream),
                        "tx" => this.ReadPayload<TxPayload>(stream),
                        "verack" => this.ReadPayload<VerAckPayload>(stream),
                        "version" => this.ReadPayload<VersionPayload>(stream),
                        _ => new UnknowPayload(command, data)
                    };
                }
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to reconstruct payload for command {Command}", command);
                return null;
            }
        }

        private T ReadPayload<T>(BitcoinStream stream) where T : Payload, new()
        {
            var payload = new T();
            payload.ReadWrite(stream);
            return payload;
        }
    }
}
