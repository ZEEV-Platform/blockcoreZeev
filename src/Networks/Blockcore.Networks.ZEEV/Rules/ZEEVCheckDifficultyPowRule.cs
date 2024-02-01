using Blockcore.Consensus;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.Rules;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BouncyCastle.math;
using Blockcore.Networks.ZEEV.Components;
using Blockcore.Networks.ZEEV.Consensus;
using Microsoft.Extensions.Logging;

namespace Blockcore.Networks.ZEEV.Rules
{
    public class ZEEVCheckDifficultyPowRule : HeaderValidationConsensusRule
    {
        private static BigInteger pow256 = BigInteger.ValueOf(2).Pow(256);

        public override void Run(RuleContext context)
        {
            if (!((ZEEVBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Header).CheckProofOfWork())
                ConsensusErrors.HighHash.Throw();

            Target nextWorkRequired = GetWorkRequired(
                context.ValidationContext.ChainedHeaderToValidate,
                (ZEEVConsensus)this.Parent.Network.Consensus);

            ZEEVBlockHeader header = (ZEEVBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Header;

            // Check proof of work.
            if (header.Bits != nextWorkRequired)
            {
                this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }
        }

        public Target GetWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            ZEEVDigiShield digiShield = new ZEEVDigiShield();

            return digiShield.GetWorkRequired(chainedHeaderToValidate, consensus);
        }
    }
}