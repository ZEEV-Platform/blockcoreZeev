using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Consensus;
using Org.BouncyCastle.Math;

namespace Blockcore.Networks.ZEEV.Components
{
    /// <summary>
    /// LWMA v3 with advanced multipool attack protection
    /// </summary>
    public class ZEEVLWMA
    {
        // Core parameters
        private const int DIFFICULTY_WINDOW = 60;
        private const int BLOCK_WINDOW = 24;
        private const decimal MAX_ADJUSTMENT_UP = 1.06m;    // +6%
        private const decimal MAX_ADJUSTMENT_DOWN = 0.94m;  // -6%
        private const decimal BURST_ADJUSTMENT_UP = 1.15m;  // +15% during burst
        private const decimal ASYMMETRY_FACTOR = 1.5m;      // Faster decrease

        // Multipool protection
        private const int BURST_DETECTION_WINDOW = 10;
        private const decimal BURST_THRESHOLD = 0.5m;       // 50% faster blocks
        private const int TIMESTAMP_MEDIAN_WINDOW = 11;     // Time-warp protection

        private readonly int _targetSeconds;
        private readonly bool _enableBurstProtection;
        private readonly bool _enableAsymmetricAdjustment;

        public ZEEVLWMA(
            int targetSeconds = 60,
            bool enableBurstProtection = true,
            bool enableAsymmetricAdjustment = true)
        {
            this._targetSeconds = targetSeconds;
            this._enableBurstProtection = enableBurstProtection;
            this._enableAsymmetricAdjustment = enableAsymmetricAdjustment;
        }

        public class BlockDifficultyInfo
        {
            public int Height { get; set; }
            public uint256 Hash { get; set; }
            public BigInteger Bits { get; set; }
            public long BlockTimeUnixUtc { get; set; }
            public Target Target { get; set; }
        }

        public Target GetWorkRequired(ChainedHeader chainedHeaderToValidate, ZEEVConsensus consensus)
        {
            var chainedHeader = chainedHeaderToValidate;
            var blockData = new List<BlockDifficultyInfo>();
            Target proofOfWorkLimit = consensus.PowLimit;

            for (int i = 0; i < DIFFICULTY_WINDOW && chainedHeader != null; i++, chainedHeader = chainedHeader.Previous)
            {
                if (chainedHeader == null) break;

                var target = chainedHeader.Header.Bits;
                var bitsBigInteger = target.ToBigInteger();

                blockData.Add(new BlockDifficultyInfo
                {
                    Height = chainedHeader.Height,
                    Hash = chainedHeader.HashBlock,
                    Bits = bitsBigInteger,
                    BlockTimeUnixUtc = chainedHeader.Header.BlockTime.ToUnixTimeSeconds(),
                    Target = target
                });
            }

            blockData.Reverse();

            //if (blockData.Count() < BLOCK_WINDOW) return proofOfWorkLimit;

            var newTarget = CalculateNextDifficulty(blockData);

            var oldTarget = new Target(chainedHeaderToValidate.Header.Bits);
            var finalTarget = new Target(newTarget);
            var finalTargetxx = ConvertToNBitcoinTarget(newTarget);
            var finalTargeTESTt = new Target(new BigInteger("32763547380948627296949453116101827902561883714452422263419321035862538"));
            var finalTargeTESTt2 = new Target(new BigInteger("49131841303777435290677601435980379858174820122309346313085912667245328"));

            var testss = ConvertToNBitcoinTarget(new BigInteger("49131841303777435290677601435980379858174820122309346313085912667245328"));
            var testsss = ConvertToNBitcoinTarget(new BigInteger("8577107408471988062745864925407430816171552662888613931306039482777"));
            Console.WriteLine("before");
            Console.WriteLine(finalTarget.Difficulty);

            if (finalTargeTESTt2 > proofOfWorkLimit)
                finalTarget = proofOfWorkLimit;

            Console.WriteLine(finalTarget.Difficulty);

            return finalTarget;
        }

        /// <summary>
        /// Main method for difficulty calculation
        /// </summary>
        public BigInteger CalculateNextDifficulty(List<BlockDifficultyInfo> blockInfo)
        {
            if (blockInfo.Count <= 1) return BigInteger.One;

            int N = Math.Min(DIFFICULTY_WINDOW, blockInfo.Count - 1);

            // 1. Timestamp validation and protection
            var validatedTimestamps = ValidateTimestamps(blockInfo, N);

            // 2. Multipool burst detection
            bool isBurstDetected = false;
            if (this._enableBurstProtection)
            {
                isBurstDetected = DetectHashrateBurst(validatedTimestamps, blockInfo);
            }

            // 3. LWMA v3 calculation with harmonic mean
            var baseDifficulty = CalculateLWMAv3(validatedTimestamps, blockInfo, N);

            // 4. Apply protection mechanisms
            var adjustedDifficulty = baseDifficulty;

            if (isBurstDetected)
            {
                adjustedDifficulty = ApplyBurstProtection(adjustedDifficulty, blockInfo[N].Bits);
            }

            if (this._enableAsymmetricAdjustment)
            {
                adjustedDifficulty = ApplyAsymmetricAdjustment(
                    adjustedDifficulty,
                    validatedTimestamps,
                    blockInfo[N].Bits
                );
            }

            // 5. Apply safety limits
            return ApplySafetyLimits(adjustedDifficulty, blockInfo[N].Bits, isBurstDetected);
        }

        /// <summary>
        /// LWMA v3 with harmonic mean
        /// </summary>
        private BigInteger CalculateLWMAv3(
            List<long> timestamps,
            List<BlockDifficultyInfo> blockInfo,
            int N)
        {
            long T = this._targetSeconds;
            long k = N * (N + 1) / 2;  // Sum of weights

            // Use BigDecimal for precise calculations
            BigInteger sumNumerator = BigInteger.Zero;
            BigInteger sumDenominator = BigInteger.Zero;

            // Calculate weighted harmonic mean
            for (int i = 1; i <= N; i++)
            {
                long solveTime = timestamps[i] - timestamps[i - 1];

                // Protection against extreme values
                solveTime = Math.Max(solveTime, 1);
                solveTime = Math.Min(solveTime, T * 10);

                if (solveTime <= 0)
                {
                    solveTime = 1;
                }

                BigInteger difficulty = blockInfo[i].Bits;

                // Calculate weight * T * T
                BigInteger weightedTSquared = new BigInteger((i * T * T).ToString());

                // Calculate difficulty * solveTime * solveTime
                BigInteger denominator = difficulty
                    .Multiply(new BigInteger(solveTime.ToString()))
                    .Multiply(new BigInteger(solveTime.ToString()));

                // For harmonic mean, we need to sum the inverses
                // Instead of dividing, we'll cross-multiply later
                sumNumerator = sumNumerator.Add(weightedTSquared);
                sumDenominator = sumDenominator.Add(denominator.Divide(new BigInteger(i.ToString())));
            }

            // Calculate harmonic mean: (N * k) / sum(1/weighted_difficulties)
            // Which is: (N * k * sumDenominator) / sumNumerator
            BigInteger nk = new BigInteger((N * k).ToString());
            BigInteger harmonicMeanD = nk.Multiply(sumDenominator).Divide(sumNumerator);

            // Ensure minimum difficulty
            if (harmonicMeanD.CompareTo(BigInteger.One) < 0)
            {
                harmonicMeanD = BigInteger.One;
            }

            return harmonicMeanD;
        }

        /// <summary>
        /// Timestamp validation with median-time-past protection
        /// </summary>
        private List<long> ValidateTimestamps(List<BlockDifficultyInfo> blockInfo, int N)
        {
            var validated = new List<long>();

            for (int i = 0; i <= N; i++)
            {
                if (i < TIMESTAMP_MEDIAN_WINDOW)
                {
                    validated.Add(blockInfo[i].BlockTimeUnixUtc);
                }
                else
                {
                    // Use median for time-warp protection
                    var window = new List<long>();
                    for (int j = Math.Max(0, i - TIMESTAMP_MEDIAN_WINDOW + 1); j <= i; j++)
                    {
                        window.Add(blockInfo[j].BlockTimeUnixUtc);
                    }
                    window.Sort();
                    validated.Add(window[window.Count / 2]);
                }
            }

            return validated;
        }

        /// <summary>
        /// Detect sudden hashrate increase (multipool arrival)
        /// </summary>
        private bool DetectHashrateBurst(
            List<long> timestamps,
            List<BlockDifficultyInfo> blockInfo)
        {
            if (timestamps.Count < BURST_DETECTION_WINDOW + 1) return false;

            // Average time of last N blocks
            decimal recentAvgTime = 0;
            int startIdx = timestamps.Count - BURST_DETECTION_WINDOW - 1;

            for (int i = startIdx + 1; i < timestamps.Count; i++)
            {
                recentAvgTime += timestamps[i] - timestamps[i - 1];
            }
            recentAvgTime /= BURST_DETECTION_WINDOW;

            // Compare with target time
            if (recentAvgTime < this._targetSeconds * BURST_THRESHOLD)
            {
                // Additional check - growing difficulty but still fast blocks
                var difficultyGrowth = blockInfo[timestamps.Count - 1].Bits
                    .Divide(blockInfo[startIdx].Bits);

                if (difficultyGrowth.CompareTo(BigInteger.One) > 0 &&
                    recentAvgTime < this._targetSeconds * (decimal)0.7)
                {
                    return true; // Definitely a burst
                }
            }

            return false;
        }

        /// <summary>
        /// Apply burst protection (sudden multipool arrival)
        /// </summary>
        private BigInteger ApplyBurstProtection(
            BigInteger baseDifficulty,
            BigInteger previousDifficulty)
        {
            // More aggressive increase during burst detection
            // Convert decimal to integer percentage (115 for 1.15)
            int burstMultiplier = (int)(BURST_ADJUSTMENT_UP * 100);
            var burstAdjusted = baseDifficulty
                .Multiply(new BigInteger(burstMultiplier.ToString()))
                .Divide(new BigInteger("100"));

            // Ensure minimum increase (110%)
            var minIncrease = previousDifficulty
                .Multiply(new BigInteger("110"))
                .Divide(new BigInteger("100"));

            if (burstAdjusted.CompareTo(minIncrease) < 0)
            {
                burstAdjusted = minIncrease;
            }

            return burstAdjusted;
        }

        /// <summary>
        /// Asymmetric adjustment - faster decrease, slower increase
        /// </summary>
        private BigInteger ApplyAsymmetricAdjustment(
            BigInteger baseDifficulty,
            List<long> timestamps,
            BigInteger previousDifficulty)
        {
            if (timestamps.Count < 6) return baseDifficulty;

            // Average time of last 5 blocks
            double avgTime = 0;
            for (int i = timestamps.Count - 5; i < timestamps.Count; i++)
            {
                avgTime += timestamps[i] - timestamps[i - 1];
            }
            avgTime /= 5;

            BigInteger adjusted = baseDifficulty;

            if (avgTime > this._targetSeconds * 1.2) // Blocks are slow
            {
                // Faster difficulty decrease
                var decrease = previousDifficulty.Subtract(baseDifficulty);

                // Multiply by asymmetry factor (1.5 = 150/100)
                int asymmetryMultiplier = (int)(ASYMMETRY_FACTOR * 100);
                decrease = decrease
                    .Multiply(new BigInteger(asymmetryMultiplier.ToString()))
                    .Divide(new BigInteger("100"));

                adjusted = previousDifficulty.Subtract(decrease);

                // Ensure minimum decrease (96%)
                var minDecrease = previousDifficulty
                    .Multiply(new BigInteger("96"))
                    .Divide(new BigInteger("100"));

                if (adjusted.CompareTo(minDecrease) > 0)
                {
                    adjusted = minDecrease;
                }
            }
            else if (avgTime < this._targetSeconds * 0.8) // Blocks are fast
            {
                // Slower difficulty increase
                var increase = baseDifficulty.Subtract(previousDifficulty);

                // Divide by asymmetry factor
                int asymmetryDivisor = (int)(ASYMMETRY_FACTOR * 100);
                increase = increase
                    .Multiply(new BigInteger("100"))
                    .Divide(new BigInteger(asymmetryDivisor.ToString()));

                adjusted = previousDifficulty.Add(increase);
            }

            return adjusted;
        }

        /// <summary>
        /// Apply final safety limits
        /// </summary>
        private BigInteger ApplySafetyLimits(
            BigInteger newDifficulty,
            BigInteger previousDifficulty,
            bool isBurstDetected)
        {
            // Different limits for normal situation and burst
            int maxUpPercent = isBurstDetected ?
                (int)(BURST_ADJUSTMENT_UP * 100) :
                (int)(MAX_ADJUSTMENT_UP * 100);
            int maxDownPercent = (int)(MAX_ADJUSTMENT_DOWN * 100);

            var maxIncrease = previousDifficulty
                .Multiply(new BigInteger(maxUpPercent.ToString()))
                .Divide(new BigInteger("100"));
            var maxDecrease = previousDifficulty
                .Multiply(new BigInteger(maxDownPercent.ToString()))
                .Divide(new BigInteger("100"));

            // Apply limits
            if (newDifficulty.CompareTo(maxIncrease) > 0)
            {
                newDifficulty = maxIncrease;
            }
            else if (newDifficulty.CompareTo(maxDecrease) < 0)
            {
                newDifficulty = maxDecrease;
            }

            // Absolute minimum
            if (newDifficulty.CompareTo(BigInteger.One) < 0)
            {
                newDifficulty = BigInteger.One;
            }

            return newDifficulty;
        }

        /// <summary>
        /// Helper method for network statistics analysis
        /// </summary>
        public NetworkStats AnalyzeNetwork(
            List<long> timestamps,
            List<BigInteger> difficulties,
            int blockCount = 100)
        {
            if (timestamps.Count < blockCount + 1)
                blockCount = timestamps.Count - 1;

            var stats = new NetworkStats();
            var blockTimes = new List<long>();

            for (int i = timestamps.Count - blockCount; i < timestamps.Count; i++)
            {
                blockTimes.Add(timestamps[i] - timestamps[i - 1]);
            }

            stats.AverageBlockTime = blockTimes.Average();
            stats.MedianBlockTime = blockTimes.OrderBy(x => x).ElementAt(blockTimes.Count / 2);
            stats.BlockTimeVariance = CalculateVariance(blockTimes);
            stats.CurrentDifficulty = difficulties.Last();

            // Calculate growth rate
            var oldDiff = difficulties[difficulties.Count - blockCount];
            var newDiff = difficulties.Last();
            // Convert to double for growth rate calculation
            double growth = double.Parse(newDiff.ToString()) / double.Parse(oldDiff.ToString());
            stats.DifficultyGrowthRate = growth;

            return stats;
        }

        private double CalculateVariance(List<long> values)
        {
            double avg = values.Average();
            double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return sumSquares / values.Count;
        }
    }

    /// <summary>
    /// Network statistics for monitoring
    /// </summary>
    public class NetworkStats
    {
        public double AverageBlockTime { get; set; }
        public long MedianBlockTime { get; set; }
        public double BlockTimeVariance { get; set; }
        public BigInteger CurrentDifficulty { get; set; }
        public double DifficultyGrowthRate { get; set; }

        public bool IsUnderAttack()
        {
            // Detect possible attack based on statistics
            return this.BlockTimeVariance > 1000 || // High variance
                   this.AverageBlockTime < 60 ||     // Blocks too fast
                   this.DifficultyGrowthRate > 2;    // Growth too rapid
        }
    }
}