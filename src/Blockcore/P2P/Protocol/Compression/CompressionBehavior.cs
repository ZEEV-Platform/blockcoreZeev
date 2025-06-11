using System;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.NBitcoin;
using Blockcore.P2P.Peer;
using Blockcore.P2P.Protocol.Behaviors;
using Blockcore.P2P.Protocol.Payloads;
using Microsoft.Extensions.Logging;

namespace Blockcore.P2P.Protocol.Compression
{
    public class CompressionBehavior : NetworkPeerBehavior
    {
        private readonly ILogger logger;

        private ConsensusFactory ConsensusFactory { get; set; }

        public CompressionBehavior(ILogger logger, ConsensusFactory consensusFactory)
        {
            this.logger = logger;
            this.ConsensusFactory = consensusFactory;
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        public override object Clone()
        {
            return new CompressionBehavior(this.logger, this.ConsensusFactory);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (message.Message.Payload is CompressedPayload compressed)
            {
                var compressionProcessor = new CompressionProcessor(this.logger, this.ConsensusFactory);
                await compressionProcessor.DecompressDataAsync(peer, message);
            }

            await Task.CompletedTask;
        }
    }
}
