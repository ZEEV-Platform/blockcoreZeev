using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Utilities;
using Microsoft.Extensions.Primitives;
using Org.BouncyCastle.Math;

namespace Blockcore.Networks.ZEEV.Components
{
    /// <summary>
    /// LWMA (Linear Weighted Moving Average) difficulty adjustment algorithm implementation.
    /// This is a C# port of the original C++ LWMA algorithm with LWMA-3 jump rule support.
    /// Enhanced with burst protection mechanisms to prevent rapid hash rate attacks.
    /// </summary>
    public class ZEEVLWMA
    {
        private const int DIFFICULTY_WINDOW = 60;

        // Burst protection constants
        private const double MAX_DIFFICULTY_DECREASE_FACTOR = 8.0; // Maximum 4x difficulty decrease
        private const double MAX_DIFFICULTY_INCREASE_FACTOR = 4.0; // Maximum 2x difficulty increase
        private const long MIN_BLOCK_INTERVAL_SECONDS = 15; // Minimum 10 seconds between blocks
        private const int BURST_DETECTION_WINDOW = 6; // Number of recent blocks to check for burst
        private const double BURST_THRESHOLD_FACTOR = 0.25; // If average time < 25% of target, it's a burst

        public ZEEVLWMA()
        {
        }

        public Target GetWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            var chainedHeader = chainedHeaderToValidate;
            Target proofOfWorkLimit = consensus.PowLimit;
            var s = new StringBuilder();
            long lastTime = 0;

            for (int i = 0; i < DIFFICULTY_WINDOW && chainedHeader != null; i++, chainedHeader = chainedHeader.Previous)
            {
                if (chainedHeader == null) break;

                var target = chainedHeader.Header.Bits;
                var bitsBigInteger = target.ToBigInteger();

                if (lastTime != 0)
                {
                    var resu = lastTime - chainedHeader.Header.BlockTime.ToUnixTimeSeconds();
                    s.Append(resu + " - ");
                }


                lastTime = chainedHeader.Header.BlockTime.ToUnixTimeSeconds();
            }

            var newTarget = LwmaCalculateNextWorkRequired(chainedHeaderToValidate, consensus);

            var oldTarget = new Target(chainedHeaderToValidate.Header.Bits);
            var finalTarget = newTarget;
            Console.WriteLine("before");
            Console.WriteLine(finalTarget.Difficulty);

            Console.WriteLine(finalTarget.Difficulty);
            Console.WriteLine(s);

            return finalTarget;
        }

        /// <summary>
        /// Calculates the next work required using the LWMA algorithm with burst protection.
        /// Enhanced with burst mining detection and difficulty change limits to prevent
        /// rapid hash rate attacks and maintain network stability.
        /// </summary>
        /// <param name="chainedHeaderToValidate">The chained header for which to calculate the next work required.</param>
        /// <param name="consensus">The consensus parameters containing LWMA configuration.</param>
        /// <returns>The compact representation of the next target difficulty.</returns>
        public Target LwmaCalculateNextWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            Guard.NotNull(chainedHeaderToValidate, nameof(chainedHeaderToValidate));
            Guard.NotNull(consensus, nameof(consensus));

            // Get the last block header (equivalent to pindexLast in C++)
            ChainedHeader pindexLast = chainedHeaderToValidate.Previous;
            if (pindexLast == null)
            {
                return consensus.PowLimit.ToCompact();
            }

            // LWMA parameters - these should ideally come from consensus parameters
            // For T=120, 240, 600 use approx N=100, 75, 50
            long T = (long)consensus.TargetSpacing.TotalSeconds; // Target spacing in seconds
            long height = pindexLast.Height;
            long N = GetLwmaAveragingWindow(height);
            long k = N * (N + 1) * T / 2; // LWMA constant

            // Convert PowLimit to arithmetic uint256 for calculations
            var powLimit = consensus.PowLimit.ToBigInteger();

            // If we don't have enough blocks, return the proof-of-work limit
            if (height < N)
            {
                return consensus.PowLimit.ToCompact();
            }

            // Initialize variables for LWMA calculation
            var sumTarget = BigInteger.Zero;
            long thisTimestamp, previousTimestamp;
            long t = 0, j = 0;

            // LWMA-3 jump rule variables
            var previousTarget = BigInteger.Zero;
            long sumLast3Solvetimes = 0;

            // Get the timestamp of the block N positions back
            ChainedHeader blockPreviousTimestamp = GetAncestor(pindexLast, height - N);
            if (blockPreviousTimestamp == null)
            {
                return consensus.PowLimit.ToCompact();
            }

            previousTimestamp = blockPreviousTimestamp.Header.BlockTime.ToUnixTimeSeconds();

            // Loop through N most recent blocks
            for (long i = height - N + 1; i <= height; i++)
            {
                ChainedHeader block = GetAncestor(pindexLast, (int)i);
                if (block == null)
                {
                    break;
                }

                thisTimestamp = block.Header.BlockTime.ToUnixTimeSeconds();

                // Ensure timestamp is monotonic (prevent timestamp manipulation)
                if (thisTimestamp <= previousTimestamp)
                {
                    thisTimestamp = previousTimestamp + 1;
                }

                // Calculate solve time with maximum limit of 6*T
                // BURST PROTECTION: Apply minimum block interval
                long solvetime = Math.Min(6 * T, thisTimestamp - previousTimestamp);
                solvetime = Math.Max(solvetime, MIN_BLOCK_INTERVAL_SECONDS);
                previousTimestamp = thisTimestamp;

                j++;
                t += solvetime * j; // Weighted solvetime sum

                // Get target from block's nBits and add to sum
                var target = new Target(block.Header.Bits);
                var targetBigInt = target.ToBigInteger();

                // Add weighted target to sum (equivalent to target / (k * N))
                var toAdd = targetBigInt.Divide(new BigInteger((k * N).ToString()));
                sumTarget = sumTarget.Add(toAdd);

                // LWMA-3 jump rule: collect data from last 3 blocks
                if (i > height - 3)
                {
                    sumLast3Solvetimes += solvetime;
                }

                if (i == height)
                {
                    previousTarget = targetBigInt;
                }
            }

            // Calculate next target using LWMA formula
            var nextTargetBigInt = sumTarget.Multiply(new BigInteger(t.ToString()));

            // LWMA-3 jump rule: Apply "memory-less" jump in difficulty
            // This provides approximately 2x normal adjustment during rapid hashrate changes
            if (sumLast3Solvetimes < (8 * T) / 10)
            {
                // Increase difficulty more aggressively when blocks are coming too fast
                var divideValue = (100 + (N * 26) / 200);
                nextTargetBigInt = previousTarget.Multiply(new BigInteger((100).ToString()))
                    .Divide(new BigInteger(divideValue.ToString()));
            }

            // BURST PROTECTION: Detect burst mining and apply protection
            bool isBurstDetected = DetectBurstMining(pindexLast, T);
            nextTargetBigInt = ApplyBurstProtection(nextTargetBigInt, previousTarget, isBurstDetected);

            // Ensure the target doesn't exceed the proof-of-work limit
            if (nextTargetBigInt.CompareTo(powLimit) > 0)
            {
                nextTargetBigInt = powLimit;
            }

            // Ensure the target is not zero or negative
            if (nextTargetBigInt.CompareTo(BigInteger.Zero) <= 0)
            {
                nextTargetBigInt = powLimit;
            }

            // Convert back to compact representation
            var nextTarget = new Target(nextTargetBigInt);
            return nextTarget;
        }

        private static long GetLwmaAveragingWindow(long height)
        {
            if (height < 10)
            {
                return DIFFICULTY_WINDOW; //for default PowLimit
            }
            else if (height >= 10 && height < DIFFICULTY_WINDOW)
            {
                return height;
            }
            else
            {
                return DIFFICULTY_WINDOW;
            }
        }

        /// <summary>
        /// Detects burst mining by analyzing recent block intervals.
        /// </summary>
        /// <param name="pindexLast">The last block header.</param>
        /// <param name="targetSpacing">Target block spacing in seconds.</param>
        /// <returns>True if burst mining is detected, false otherwise.</returns>
        private bool DetectBurstMining(ChainedHeader pindexLast, long targetSpacing)
        {
            if (pindexLast == null || pindexLast.Height < BURST_DETECTION_WINDOW)
            {
                return false;
            }

            long totalTime = 0;
            int validIntervals = 0;

            // Check the last BURST_DETECTION_WINDOW blocks
            for (int i = 0; i < BURST_DETECTION_WINDOW; i++)
            {
                ChainedHeader currentBlock = GetAncestor(pindexLast, pindexLast.Height - i);
                ChainedHeader previousBlock = GetAncestor(pindexLast, pindexLast.Height - i - 1);

                if (currentBlock == null || previousBlock == null)
                {
                    break;
                }

                long interval = currentBlock.Header.BlockTime.ToUnixTimeSeconds() -
                               previousBlock.Header.BlockTime.ToUnixTimeSeconds();

                // Ensure minimum block interval for burst protection
                interval = Math.Max(interval, MIN_BLOCK_INTERVAL_SECONDS);

                totalTime += interval;
                validIntervals++;
            }

            if (validIntervals == 0)
            {
                return false;
            }

            double averageTime = (double)totalTime / validIntervals;
            double burstThreshold = targetSpacing * BURST_THRESHOLD_FACTOR;

            return averageTime < burstThreshold;
        }

        /// <summary>
        /// Applies burst protection by limiting difficulty changes.
        /// </summary>
        /// <param name="nextTarget">The calculated next target.</param>
        /// <param name="previousTarget">The previous target.</param>
        /// <param name="isBurstDetected">Whether burst mining was detected.</param>
        /// <returns>The burst-protected target.</returns>
        private BigInteger ApplyBurstProtection(BigInteger nextTarget, BigInteger previousTarget, bool isBurstDetected)
        {
            if (previousTarget.CompareTo(BigInteger.Zero) <= 0)
            {
                return nextTarget;
            }

            // Calculate the ratio of difficulty change
            // Lower target = higher difficulty, so we need to invert the ratio
            if (nextTarget.CompareTo(previousTarget) > 0)
            {
                // Difficulty is decreasing (target increasing)
                // Check if the increase is too large
                var maxAllowedTarget = previousTarget.Multiply(new BigInteger(((int)MAX_DIFFICULTY_DECREASE_FACTOR).ToString()));
                var s = new Target(maxAllowedTarget);
                var s2 = new Target(nextTarget);
                if (nextTarget.CompareTo(maxAllowedTarget) > 0)
                {
                    nextTarget = maxAllowedTarget;
                }
            }
            else if (nextTarget.CompareTo(previousTarget) < 0)
            {
                // Difficulty is increasing (target decreasing)
                // Limit difficulty increase, but be more aggressive during burst
                double maxIncrease = isBurstDetected ? MAX_DIFFICULTY_INCREASE_FACTOR * 1.5 : MAX_DIFFICULTY_INCREASE_FACTOR;

                var minAllowedTarget = previousTarget.Divide(new BigInteger(((int)maxIncrease).ToString()));
                if (nextTarget.CompareTo(minAllowedTarget) < 0)
                {
                    nextTarget = minAllowedTarget;
                }
            }

            return nextTarget;
        }

        /// <summary>
        /// Gets an ancestor block at the specified height.
        /// This is equivalent to the GetAncestor method in the C++ implementation.
        /// </summary>
        /// <param name="chainedHeader">The starting chained header.</param>
        /// <param name="height">The target height.</param>
        /// <returns>The chained header at the specified height, or null if not found.</returns>
        private ChainedHeader GetAncestor(ChainedHeader chainedHeader, long height)
        {
            if (chainedHeader == null || height < 0 || height > chainedHeader.Height)
            {
                return null;
            }

            // Walk backwards to find the block at the specified height
            ChainedHeader current = chainedHeader;
            while (current != null && current.Height > height)
            {
                current = current.Previous;
            }

            return current?.Height == height ? current : null;
        }

        /// <summary>
        /// Validates LWMA parameters for consistency and security.
        /// </summary>
        /// <param name="targetSpacing">Target block spacing in seconds.</param>
        /// <param name="averagingWindow">LWMA averaging window size.</param>
        /// <returns>True if parameters are valid, false otherwise.</returns>
        public bool ValidateLwmaParameters(long targetSpacing, long averagingWindow)
        {
            // Basic sanity checks
            if (targetSpacing <= 0 || averagingWindow <= 0)
            {
                return false;
            }

            // Ensure reasonable bounds
            if (targetSpacing < 10 || targetSpacing > 3600) // 10 seconds to 1 hour
            {
                return false;
            }

            if (averagingWindow < 10 || averagingWindow > 500) // 10 to 500 blocks
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the expected difficulty adjustment ratio for monitoring purposes.
        /// </summary>
        /// <param name="actualTime">Actual time taken for the averaging window.</param>
        /// <param name="targetTime">Expected time for the averaging window.</param>
        /// <returns>The difficulty adjustment ratio.</returns>
        public double CalculateAdjustmentRatio(long actualTime, long targetTime)
        {
            if (targetTime <= 0)
            {
                return 1.0;
            }

            return (double)targetTime / actualTime;
        }

        /// <summary>
        /// Gets burst protection statistics for monitoring and debugging.
        /// </summary>
        /// <param name="pindexLast">The last block header.</param>
        /// <param name="targetSpacing">Target block spacing in seconds.</param>
        /// <returns>Burst protection statistics.</returns>
        public BurstProtectionStats GetBurstProtectionStats(ChainedHeader pindexLast, long targetSpacing)
        {
            var stats = new BurstProtectionStats();

            if (pindexLast == null || pindexLast.Height < BURST_DETECTION_WINDOW)
            {
                return stats;
            }

            long totalTime = 0;
            int validIntervals = 0;
            long minInterval = long.MaxValue;
            long maxInterval = 0;

            // Check the last BURST_DETECTION_WINDOW blocks
            for (int i = 0; i < BURST_DETECTION_WINDOW; i++)
            {
                ChainedHeader currentBlock = GetAncestor(pindexLast, pindexLast.Height - i);
                ChainedHeader previousBlock = GetAncestor(pindexLast, pindexLast.Height - i - 1);

                if (currentBlock == null || previousBlock == null)
                {
                    break;
                }

                long interval = currentBlock.Header.BlockTime.ToUnixTimeSeconds() -
                               previousBlock.Header.BlockTime.ToUnixTimeSeconds();

                totalTime += interval;
                validIntervals++;
                minInterval = Math.Min(minInterval, interval);
                maxInterval = Math.Max(maxInterval, interval);
            }

            if (validIntervals > 0)
            {
                stats.AverageBlockTime = (double)totalTime / validIntervals;
                stats.MinBlockTime = minInterval;
                stats.MaxBlockTime = maxInterval;
                stats.TargetBlockTime = targetSpacing;
                stats.IsBurstDetected = stats.AverageBlockTime < (targetSpacing * BURST_THRESHOLD_FACTOR);
                stats.BurstThreshold = targetSpacing * BURST_THRESHOLD_FACTOR;
                stats.BlocksAnalyzed = validIntervals;
            }

            return stats;
        }
    }

    /// <summary>
    /// Statistics for burst protection monitoring.
    /// </summary>
    public class BurstProtectionStats
    {
        public double AverageBlockTime { get; set; }
        public long MinBlockTime { get; set; }
        public long MaxBlockTime { get; set; }
        public long TargetBlockTime { get; set; }
        public bool IsBurstDetected { get; set; }
        public double BurstThreshold { get; set; }
        public int BlocksAnalyzed { get; set; }
    }
}