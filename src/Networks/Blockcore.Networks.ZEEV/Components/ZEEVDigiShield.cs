using System;
using System.Collections.Generic;
using System.Linq;
using Blockcore.Consensus.Chain;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BouncyCastle.math;
using Blockcore.Networks.ZEEV.Consensus;

namespace Blockcore.Networks.ZEEV.Components
{
    public class ZEEVDigiShield
    {
        int MedianTimeSpan = 11;

        public Target GetWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            var nAveragingInterval = 16; // block
            var multiAlgoTargetSpacingV4 = consensus.TargetSpacing.TotalSeconds; // seconds
            var nAveragingTargetTimespanV4 = nAveragingInterval * multiAlgoTargetSpacingV4;
            var nMaxAdjustDownV4 = 32;
            var nMaxAdjustUpV4 = 24;
            var nMinActualTimespanV4 = TimeSpan.FromSeconds(nAveragingTargetTimespanV4 * (100 - nMaxAdjustUpV4) / 100);
            var nMaxActualTimespanV4 = TimeSpan.FromSeconds(nAveragingTargetTimespanV4 * (100 + nMaxAdjustDownV4) / 100);

            var height = chainedHeaderToValidate.Height;
            Target proofOfWorkLimit = consensus.PowLimit;
            ChainedHeader lastBlock = chainedHeaderToValidate.Previous;
            if (nAveragingInterval > height) nAveragingInterval = height;
            ChainedHeader firstBlock = chainedHeaderToValidate.GetAncestor(height - nAveragingInterval);

            // Limit adjustment step
            // Use medians to prevent time-warp attacks
            TimeSpan nActualTimespan = GetAverageTimePast(lastBlock, this.MedianTimeSpan) - GetAverageTimePast(firstBlock, this.MedianTimeSpan);
            nActualTimespan = TimeSpan.FromSeconds(nAveragingTargetTimespanV4
                                    + (nActualTimespan.TotalSeconds - nAveragingTargetTimespanV4) / 4);

            if (nActualTimespan < nMinActualTimespanV4)
                nActualTimespan = nMinActualTimespanV4;
            if (nActualTimespan > nMaxActualTimespanV4)
                nActualTimespan = nMaxActualTimespanV4;

            // Retarget.
            BigInteger newTarget = lastBlock.Header.Bits.ToBigInteger();

            newTarget = newTarget.Multiply(BigInteger.ValueOf((long)nActualTimespan.TotalSeconds));
            newTarget = newTarget.Divide(BigInteger.ValueOf((long)nAveragingTargetTimespanV4));

            var finalTarget = new Target(newTarget);
            if (finalTarget > proofOfWorkLimit)
                finalTarget = proofOfWorkLimit;

            return finalTarget;
        }

        public DateTimeOffset GetAverageTimePast(ChainedHeader chainedHeaderToValidate, int medianTimeSpan)
        {
            var median = new List<DateTimeOffset>();

            ChainedHeader chainedHeader = chainedHeaderToValidate;
            for (int i = 0; i < medianTimeSpan && chainedHeader != null; i++, chainedHeader = chainedHeader.Previous)
            {
                if (chainedHeader == null) break;
                median.Add(chainedHeader.Header.BlockTime);
            }

            if (median.Count() == 0) return new DateTimeOffset();

            median.Sort();

            DateTimeOffset firstTimespan = median.First();
            DateTimeOffset lastTimespan = median.Last();
            TimeSpan differenceTimespan = lastTimespan - firstTimespan;
            var timespan = differenceTimespan.TotalSeconds / 2;
            DateTimeOffset averageDateTime = firstTimespan.AddSeconds((long)timespan);

            return averageDateTime;
        }
    }
}
