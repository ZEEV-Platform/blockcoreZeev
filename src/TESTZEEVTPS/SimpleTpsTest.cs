using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Blockcore.Networks.ZEEV;

namespace TESTZEEVTPS
{
    /// <summary>
    /// Simple TPS test with real transactions and blockchain constraints
    /// Block parameters: 30s blocks, 2.5MB block size
    /// </summary>
    public class SimpleTpsTest
    {
        private readonly bool useRealTransactions;
        private readonly RealTransactionGenerator? realTransactionGenerator;

        // Blockchain constants from Program
        public const int BLOCK_TIME_SECONDS = 30;
        public const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
        public const int AVERAGE_TX_SIZE_BYTES = 500; // Conservative estimate
        public const double THEORETICAL_MAX_TPS = BLOCK_SIZE_BYTES / (double)(AVERAGE_TX_SIZE_BYTES * BLOCK_TIME_SECONDS);

        public SimpleTpsTest(bool useRealTransactions = false)
        {
            this.useRealTransactions = useRealTransactions;
            
            if (useRealTransactions)
            {
                var network = new ZEEVTest();
                this.realTransactionGenerator = new RealTransactionGenerator(network, 15);
            }
        }

        /// <summary>
        /// Measures TPS for sequential transaction processing with blockchain constraints
        /// </summary>
        public async Task<TPSTestResult> MeasureTPS(int transactionCount, string testName)
        {
            var stopwatch = Stopwatch.StartNew();
            var processedCount = 0;
            
            for (int i = 0; i < transactionCount; i++)
            {
                await SimulateTransactionProcessing();
                processedCount++;
            }
            
            stopwatch.Stop();
            
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = transactionCount / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / transactionCount;
            
            // Calculate block-related metrics
            var transactionsPerBlock = CalculateTransactionsPerBlock(tps);
            var blockUtilization = CalculateBlockUtilization(transactionsPerBlock, AVERAGE_TX_SIZE_BYTES);
            
            return new TPSTestResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = transactionCount,
                AvgTimePerTransaction = avgTimePerTransaction,
                TestName = testName,
                TransactionsPerBlock = transactionsPerBlock,
                BlockUtilization = blockUtilization,
                AverageTransactionSize = AVERAGE_TX_SIZE_BYTES,
                TheoreticalMaxTPS = THEORETICAL_MAX_TPS,
                BlockConstraintCompliance = tps <= THEORETICAL_MAX_TPS
            };
        }

        /// <summary>
        /// Measures TPS for parallel transaction processing
        /// </summary>
        public async Task<TPSTestResult> MeasureParallelTPS(int transactionCount, int threadCount, string testName)
        {
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            var transactionsPerThread = transactionCount / threadCount;
            var remainingTransactions = transactionCount % threadCount;
            
            for (int i = 0; i < threadCount; i++)
            {
                var transactionsForThisThread = transactionsPerThread + (i < remainingTransactions ? 1 : 0);
                tasks.Add(ProcessTransactionBatch(transactionsForThisThread));
            }
            
            await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = transactionCount / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / transactionCount;
            
            // Block metrics
            var transactionsPerBlock = CalculateTransactionsPerBlock(tps);
            var blockUtilization = CalculateBlockUtilization(transactionsPerBlock, AVERAGE_TX_SIZE_BYTES);
            
            return new TPSTestResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = transactionCount,
                AvgTimePerTransaction = avgTimePerTransaction,
                TestName = testName,
                ThreadCount = threadCount,
                TransactionsPerBlock = transactionsPerBlock,
                BlockUtilization = blockUtilization,
                AverageTransactionSize = AVERAGE_TX_SIZE_BYTES,
                TheoreticalMaxTPS = THEORETICAL_MAX_TPS,
                BlockConstraintCompliance = tps <= THEORETICAL_MAX_TPS
            };
        }

        /// <summary>
        /// Measures TPS using real blockchain transactions
        /// </summary>
        public async Task<RealTPSTestResult> MeasureRealTPS(int transactionCount, string testName)
        {
            if (!useRealTransactions || realTransactionGenerator == null)
                throw new InvalidOperationException("Real transactions not enabled. Create SimpleTpsTest with useRealTransactions=true");

            var stopwatch = Stopwatch.StartNew();
            var processedCount = 0;
            var successfulCount = 0;
            var totalSize = 0;
            var totalFees = 0L;
            var errors = new List<string>();
            
            for (int i = 0; i < transactionCount; i++)
            {
                try
                {
                    var transactionData = realTransactionGenerator.GenerateSignedTransaction();
                    
                    if (transactionData.IsValid && transactionData.HasTransaction)
                    {
                        successfulCount++;
                        totalSize += transactionData.Size;
                        totalFees += transactionData.Fee.Satoshi;
                        
                        // Simulate network delay
                        await Task.Delay(1);
                    }
                    else if (!string.IsNullOrEmpty(transactionData.Error))
                    {
                        errors.Add(transactionData.Error);
                    }
                    
                    processedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    processedCount++;
                }
            }
            
            stopwatch.Stop();
            
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = successfulCount / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / transactionCount;
            var avgTransactionSize = successfulCount > 0 ? totalSize / successfulCount : 0;
            
            // Block analysis with real transaction sizes
            var transactionsPerBlock = CalculateTransactionsPerBlock(tps, avgTransactionSize);
            var blockUtilization = CalculateBlockUtilization(transactionsPerBlock, avgTransactionSize);
            var theoreticalMaxTPS = avgTransactionSize > 0 ? BLOCK_SIZE_BYTES / (double)(avgTransactionSize * BLOCK_TIME_SECONDS) : THEORETICAL_MAX_TPS;
            
            return new RealTPSTestResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = transactionCount,
                SuccessfulTransactions = successfulCount,
                AvgTimePerTransaction = avgTimePerTransaction,
                AverageTransactionSize = avgTransactionSize,
                TotalFees = totalFees,
                TestName = testName,
                Errors = errors,
                SuccessRate = transactionCount > 0 ? (double)successfulCount / transactionCount : 0,
                FeesPerTransaction = successfulCount > 0 ? totalFees / (double)successfulCount : 0,
                TransactionsPerBlock = transactionsPerBlock,
                BlockUtilization = blockUtilization,
                TheoreticalMaxTPS = theoreticalMaxTPS,
                BlockConstraintCompliance = tps <= theoreticalMaxTPS
            };
        }

        /// <summary>
        /// Measures parallel TPS using real transactions
        /// </summary>
        public async Task<RealTPSTestResult> MeasureParallelRealTPS(int transactionCount, int threadCount, string testName)
        {
            if (!useRealTransactions || realTransactionGenerator == null)
                throw new InvalidOperationException("Real transactions not enabled");

            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<RealTPSTestResult>>();
            var transactionsPerThread = transactionCount / threadCount;
            var remainingTransactions = transactionCount % threadCount;
            
            for (int i = 0; i < threadCount; i++)
            {
                var transactionsForThisThread = transactionsPerThread + (i < remainingTransactions ? 1 : 0);
                tasks.Add(MeasureRealTPS(transactionsForThisThread, $"{testName}-T{i}"));
            }
            
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            // Aggregate results
            var totalProcessed = results.Sum(r => r.TransactionCount);
            var totalSuccessful = results.Sum(r => r.SuccessfulTransactions);
            var totalSize = results.Sum(r => r.AverageTransactionSize * r.SuccessfulTransactions);
            var totalFees = results.Sum(r => r.TotalFees);
            var allErrors = results.SelectMany(r => r.Errors).ToList();
            
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = totalSuccessful / (totalTimeMs / 1000.0);
            var avgTransactionSize = totalSuccessful > 0 ? (int)(totalSize / totalSuccessful) : 0;
            
            // Block analysis
            var transactionsPerBlock = CalculateTransactionsPerBlock(tps, avgTransactionSize);
            var blockUtilization = CalculateBlockUtilization(transactionsPerBlock, avgTransactionSize);
            var theoreticalMaxTPS = avgTransactionSize > 0 ? BLOCK_SIZE_BYTES / (double)(avgTransactionSize * BLOCK_TIME_SECONDS) : THEORETICAL_MAX_TPS;
            
            return new RealTPSTestResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = totalProcessed,
                SuccessfulTransactions = totalSuccessful,
                AvgTimePerTransaction = (double)totalTimeMs / totalProcessed,
                AverageTransactionSize = avgTransactionSize,
                TotalFees = totalFees,
                TestName = testName,
                Errors = allErrors,
                SuccessRate = totalProcessed > 0 ? (double)totalSuccessful / totalProcessed : 0,
                FeesPerTransaction = totalSuccessful > 0 ? totalFees / (double)totalSuccessful : 0,
                ThreadCount = threadCount,
                TransactionsPerBlock = transactionsPerBlock,
                BlockUtilization = blockUtilization,
                TheoreticalMaxTPS = theoreticalMaxTPS,
                BlockConstraintCompliance = tps <= theoreticalMaxTPS
            };
        }

        /// <summary>
        /// Compares simulated vs real transaction performance
        /// </summary>
        public async Task<ComparisonResult> CompareSimulatedVsReal(int transactionCount, string testName)
        {
            // Test simulated transactions
            var simulatedResult = await MeasureTPS(transactionCount, $"{testName}-Simulated");
            
            // Test real transactions
            var realResult = await MeasureRealTPS(transactionCount, $"{testName}-Real");
            
            var speedup = simulatedResult.TPS / realResult.TPS;
            var overhead = realResult.AvgTimePerTransaction - simulatedResult.AvgTimePerTransaction;
            
            return new ComparisonResult
            {
                SimulatedResult = simulatedResult,
                RealResult = realResult,
                Speedup = speedup,
                Overhead = overhead,
                TestName = testName
            };
        }

        /// <summary>
        /// Tests block capacity constraints
        /// </summary>
        public async Task<BlockAnalysisResult> AnalyzeBlockCapacity(int testTransactionCount = 1000)
        {
            if (!useRealTransactions || realTransactionGenerator == null)
                throw new InvalidOperationException("Block analysis requires real transactions");

            // Generate sample transactions to get average size
            var sampleResult = await MeasureRealTPS(Math.Min(testTransactionCount, 100), "BlockAnalysis-Sample");
            
            var avgTxSize = sampleResult.AverageTransactionSize > 0 ? sampleResult.AverageTransactionSize : AVERAGE_TX_SIZE_BYTES;
            var maxTransactionsPerBlock = BLOCK_SIZE_BYTES / avgTxSize;
            var theoreticalMaxTPS = maxTransactionsPerBlock / (double)BLOCK_TIME_SECONDS;
            
            // Test actual performance at theoretical maximum
            var targetTransactions = Math.Min((int)(theoreticalMaxTPS * 5), testTransactionCount); // 5-second test
            var performanceResult = await MeasureRealTPS(targetTransactions, "BlockAnalysis-Performance");
            
            return new BlockAnalysisResult
            {
                AverageTransactionSize = avgTxSize,
                MaxTransactionsPerBlock = maxTransactionsPerBlock,
                TheoreticalMaxTPS = theoreticalMaxTPS,
                ActualTPS = performanceResult.TPS,
                Efficiency = performanceResult.TPS / theoreticalMaxTPS,
                BlockTimeSeconds = BLOCK_TIME_SECONDS,
                BlockSizeBytes = BLOCK_SIZE_BYTES,
                SampleResult = sampleResult,
                PerformanceResult = performanceResult
            };
        }

        private async Task ProcessTransactionBatch(int count)
        {
            for (int i = 0; i < count; i++)
            {
                await SimulateTransactionProcessing();
            }
        }

        private async Task SimulateTransactionProcessing()
        {
            // Simulation of transaction processing operations
            await ValidateTransactionAsync();
            await ProcessInMemoryPoolAsync();
            await ProcessConsensusAsync();
        }

        private async Task ValidateTransactionAsync()
        {
            // Simulate validation - cryptographic operations, signature checking
            await Task.Delay(TimeSpan.FromMicroseconds(50)); // 0.05 ms
            
            // Simulate CPU intensive operation
            var hash = 0;
            for (int i = 0; i < 100; i++)
            {
                hash = hash * 17 + i;
            }
        }

        private async Task ProcessInMemoryPoolAsync()
        {
            // Simulate adding to memory pool
            await Task.Delay(TimeSpan.FromMicroseconds(100)); // 0.1 ms
            
            // Simulate working with collections
            var temp = new List<int>(50);
            for (int i = 0; i < 50; i++)
            {
                temp.Add(i);
            }
        }

        private async Task ProcessConsensusAsync()
        {
            // Simulate consensus operations
            await Task.Delay(TimeSpan.FromMicroseconds(200)); // 0.2 ms
            
            // Simulate more CPU operations
            var result = 1.0;
            for (int i = 0; i < 100; i++)
            {
                result = Math.Sqrt(result + i) * 1.1;
            }
        }

        private int CalculateTransactionsPerBlock(double tps, int? avgTxSize = null)
        {
            var txSize = avgTxSize ?? AVERAGE_TX_SIZE_BYTES;
            var maxBySize = BLOCK_SIZE_BYTES / txSize;
            var maxByTPS = (int)(tps * BLOCK_TIME_SECONDS);
            
            return Math.Min(maxBySize, maxByTPS);
        }

        private double CalculateBlockUtilization(int transactionsPerBlock, int avgTxSize)
        {
            var usedSpace = transactionsPerBlock * avgTxSize;
            return Math.Min(1.0, usedSpace / (double)BLOCK_SIZE_BYTES);
        }
    }

    /// <summary>
    /// TPS test result with blockchain constraints
    /// </summary>
    public class TPSTestResult
    {
        public double TPS { get; set; }
        public long TotalTimeMs { get; set; }
        public int TransactionCount { get; set; }
        public double AvgTimePerTransaction { get; set; }
        public string TestName { get; set; } = string.Empty;
        public int ThreadCount { get; set; } = 1;
        
        // Blockchain-related properties
        public int TransactionsPerBlock { get; set; }
        public double BlockUtilization { get; set; }
        public int AverageTransactionSize { get; set; }
        public double TheoreticalMaxTPS { get; set; }
        public bool BlockConstraintCompliance { get; set; }

        public override string ToString()
        {
            var compliance = BlockConstraintCompliance ? "✅" : "⚠️";
            return $"{TestName}: {TPS:F2} TPS, {TransactionsPerBlock} tx/block, {BlockUtilization:P1} block usage {compliance}";
        }
    }

    /// <summary>
    /// Real transaction TPS test result with blockchain analysis
    /// </summary>
    public class RealTPSTestResult
    {
        public double TPS { get; set; }
        public long TotalTimeMs { get; set; }
        public int TransactionCount { get; set; }
        public int SuccessfulTransactions { get; set; }
        public double AvgTimePerTransaction { get; set; }
        public int AverageTransactionSize { get; set; }
        public long TotalFees { get; set; }
        public string TestName { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public int ThreadCount { get; set; } = 1;

        // Derived properties
        public double SuccessRate { get; set; }
        public double FeesPerTransaction { get; set; }
        
        // Blockchain constraints
        public int TransactionsPerBlock { get; set; }
        public double BlockUtilization { get; set; }
        public double TheoreticalMaxTPS { get; set; }
        public bool BlockConstraintCompliance { get; set; }

        public override string ToString()
        {
            var compliance = BlockConstraintCompliance ? "✅" : "⚠️";
            return $"{TestName}: {TPS:F2} TPS, Success: {SuccessRate:P1}, " +
                   $"{TransactionsPerBlock} tx/block, {BlockUtilization:P1} block usage {compliance}";
        }
    }

    /// <summary>
    /// Comparison between simulated and real transactions
    /// </summary>
    public class ComparisonResult
    {
        public TPSTestResult SimulatedResult { get; set; } = new TPSTestResult();
        public RealTPSTestResult RealResult { get; set; } = new RealTPSTestResult();
        public double Speedup { get; set; }
        public double Overhead { get; set; }
        public string TestName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Block capacity and constraint analysis
    /// </summary>
    public class BlockAnalysisResult
    {
        public int AverageTransactionSize { get; set; }
        public int MaxTransactionsPerBlock { get; set; }
        public double TheoreticalMaxTPS { get; set; }
        public double ActualTPS { get; set; }
        public double Efficiency { get; set; }
        public int BlockTimeSeconds { get; set; }
        public int BlockSizeBytes { get; set; }
        public RealTPSTestResult SampleResult { get; set; } = new RealTPSTestResult();
        public RealTPSTestResult PerformanceResult { get; set; } = new RealTPSTestResult();

        public override string ToString()
        {
            return $"Block Analysis: {AverageTransactionSize}b avg tx size, " +
                   $"{MaxTransactionsPerBlock} max tx/block, " +
                   $"{TheoreticalMaxTPS:F2} theoretical TPS, " +
                   $"{ActualTPS:F2} actual TPS, " +
                   $"{Efficiency:P1} efficiency";
        }
    }
}