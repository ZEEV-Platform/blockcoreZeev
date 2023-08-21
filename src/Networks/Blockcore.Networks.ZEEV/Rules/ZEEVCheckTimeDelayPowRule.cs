using Blockcore.Consensus.Chain;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus;
using Microsoft.Extensions.Logging;
using Blockcore.Networks.ZEEV.Consensus;

namespace Blockcore.Networks.ZEEV.Rules
{
    public class ZEEVCheckTimeDelayPowRule : HeaderValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block's timestamp is mining attack.</exception>
        public override void Run(RuleContext context)
        {
            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;
            ZEEVConsensus consensus = (ZEEVConsensus)this.Parent.Network.Consensus;

            // Mining attack protection.
            if (chainedHeader.Header.BlockTime < (chainedHeader.Previous.Header.BlockTime + consensus.PowTimeDelay))
            {
                this.Logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }
        }
    }
}
