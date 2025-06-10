using System;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.NBitcoin;
using Blockcore.P2P.Peer;
using Blockcore.P2P.Protocol.Payloads;

namespace Blockcore.P2P.Protocol.Compression
{
    public static class CompressionExtensions
    {
        /// <summary>
        /// Send a message with automatic LZ4 compression if it's large enough.
        /// </summary>
        public static async Task SendWithLZ4CompressionAsync(this INetworkPeer peer, Payload payload, ConsensusFactory consensusFactory)
        {
            try
            {
                var data = payload.ToBytes(consensusFactory);
                var compressedPayload = new CompressedPayload(payload.Command, data);

                await peer.SendMessageAsync(compressedPayload);
                return;
            }
            catch (Exception ex)
            {
                // Fallback to original payload on any error
                await peer.SendMessageAsync(payload);
            }
        }
    }
}
