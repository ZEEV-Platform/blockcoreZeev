using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace TESTZEEVTPS
{
    /// <summary>
    /// Test for measuring actual TPS (Transactions Per Second) performance
    /// </summary>
    public class TPSPerformanceTests
    {
        private readonly ITestOutputHelper output;
        private readonly ILogger<TPSPerformanceTests> logger;

        public TPSPerformanceTests(ITestOutputHelper output)
        {
            this.output = output;
            
            var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            this.logger = loggerFactory.CreateLogger<TPSPerformanceTests>();
        }

        [Fact]
        public async Task MeasureActualTPS_SingleThread()
        {
            output.WriteLine("=== TPS Performance Test - Single Thread ===");
            
            const int numberOfTransactions = 10000;
            const int warmupTransactions = 1000;
            
            // Warmup
            await ProcessTransactionBatch(warmupTransactions, "Warmup", false);
            
            // Actual test
            var result = await ProcessTransactionBatch(numberOfTransactions, "Single Thread Test", true);
            
            output.WriteLine($"Single Thread Result: {result.TPS:F2} TPS");
            output.WriteLine($"Total time: {result.TotalTimeMs} ms");
            output.WriteLine($"Average time per transaction: {result.AvgTimePerTransaction:F4} ms");
            
            Assert.True(result.TPS > 0, "TPS must be greater than 0");
        }

        [Fact]
        public async Task MeasureActualTPS_MultiThread()
        {
            output.WriteLine("=== TPS Performance Test - Multi Thread ===");
            
            const int numberOfTransactions = 10000;
            int numberOfThreads = Environment.ProcessorCount;
            int transactionsPerThread = numberOfTransactions / numberOfThreads;
            
            output.WriteLine($"Number of threads: {numberOfThreads}");
            output.WriteLine($"Transactions per thread: {transactionsPerThread}");
            
            var tasks = new List<Task<TPSResult>>();
            var totalStopwatch = Stopwatch.StartNew();
            
            // Start multiple threads simultaneously
            for (int i = 0; i < numberOfThreads; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(async () => 
                    await ProcessTransactionBatch(transactionsPerThread, $"Thread-{threadId}", false)));
            }
            
            var results = await Task.WhenAll(tasks);
            totalStopwatch.Stop();
            
            // Calculate total TPS
            var totalTransactions = numberOfThreads * transactionsPerThread;
            var totalTPS = totalTransactions / (totalStopwatch.ElapsedMilliseconds / 1000.0);
            
            output.WriteLine($"=== Multi Thread Results ===");
            output.WriteLine($"Total TPS: {totalTPS:F2} TPS");
            output.WriteLine($"Total time: {totalStopwatch.ElapsedMilliseconds} ms");
            output.WriteLine($"Total transactions: {totalTransactions}");
            
            // Details of individual threads
            for (int i = 0; i < results.Length; i++)
            {
                output.WriteLine($"Thread-{i}: {results[i].TPS:F2} TPS ({results[i].TotalTimeMs} ms)");
            }
            
            Assert.True(totalTPS > 0, "Total TPS must be greater than 0");
        }

        [Fact]
        public async Task MeasureTPSWithDifferentBatchSizes()
        {
            output.WriteLine("=== TPS Test with different batch sizes ===");
            
            var batchSizes = new[] { 100, 500, 1000, 2000, 5000 };
            var results = new Dictionary<int, TPSResult>();
            
            foreach (var batchSize in batchSizes)
            {
                output.WriteLine($"\nTesting batch size: {batchSize}");
                var result = await ProcessTransactionBatch(batchSize, $"Batch-{batchSize}", true);
                results[batchSize] = result;
                
                output.WriteLine($"Batch {batchSize}: {result.TPS:F2} TPS");
            }
            
            output.WriteLine("\n=== Results Summary ===");
            foreach (var kvp in results)
            {
                output.WriteLine($"Batch {kvp.Key}: {kvp.Value.TPS:F2} TPS, " +
                    $"Total {kvp.Value.TotalTimeMs} ms, " +
                    $"Avg {kvp.Value.AvgTimePerTransaction:F4} ms/tx");
            }
            
            Assert.All(results.Values, result => Assert.True(result.TPS > 0, "TPS must be greater than 0"));
        }

        [Fact]
        public async Task MeasureTPSUnderLoad()
        {
            output.WriteLine("=== TPS Test under load ===");

            const int numberOfRounds = 5;
            const int transactionsPerRound = 2000;
            var results = new List<TPSResult>();

            for (int round = 1; round <= numberOfRounds; round++)
            {
                output.WriteLine($"\nRound {round}/{numberOfRounds}");

                var result = await ProcessTransactionBatch(transactionsPerRound, $"Round-{round}", true);
                results.Add(result);

                output.WriteLine($"Round {round}: {result.TPS:F2} TPS");

                // Short pause between rounds
                await Task.Delay(100);
            }

            // Statistics
            var avgTPS = results.Average(r => r.TPS);
            var minTPS = results.Min(r => r.TPS);
            var maxTPS = results.Max(r => r.TPS);

            output.WriteLine("\n=== Statistics under load ===");
            output.WriteLine($"Average TPS: {avgTPS:F2}");
            output.WriteLine($"Minimum TPS: {minTPS:F2}");
            output.WriteLine($"Maximum TPS: {maxTPS:F2}");
            output.WriteLine($"Difference (Max-Min): {maxTPS - minTPS:F2}");
            output.WriteLine($"Variability: {((maxTPS - minTPS) / avgTPS * 100):F2}%");

            Assert.True(avgTPS > 0, "Average TPS must be greater than 0");
            Assert.True(minTPS > 0, "Minimum TPS must be greater than 0");
        }

        [Fact]
        public async Task MeasureTPSUnderBlockConstraints()
        {
            output.WriteLine("=== TPS Test under blockchain constraints ===");
            output.WriteLine("Block parameters: 30s blocks, 2.5MB block size\n");

            const int BLOCK_TIME_SECONDS = 30;
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            const int AVERAGE_TX_SIZE = 500; // bytes

            var theoreticalMaxTPS = BLOCK_SIZE_BYTES / (double)(AVERAGE_TX_SIZE * BLOCK_TIME_SECONDS);
            output.WriteLine($"Theoretical maximum TPS: {theoreticalMaxTPS:F2}");

            // Test different transaction batch sizes
            var batchSizes = new[] { 100, 300, 500, 1000, 2000 };
            var results = new Dictionary<int, TPSResult>();

            foreach (var batchSize in batchSizes)
            {
                output.WriteLine($"\nTesting batch size: {batchSize} transactions");

                var result = await ProcessTransactionBatch(batchSize, $"BlockConstraint-{batchSize}", true);
                results[batchSize] = result;

                // Calculate transactions per block at this TPS
                var transactionsPerBlock = result.TPS * BLOCK_TIME_SECONDS;
                var blockUtilization = (transactionsPerBlock * AVERAGE_TX_SIZE) / (double)BLOCK_SIZE_BYTES;
                var efficiency = result.TPS / theoreticalMaxTPS;

                output.WriteLine($"  TPS: {result.TPS:F2}");
                output.WriteLine($"  Transactions per block: {transactionsPerBlock:F0}");
                output.WriteLine($"  Block utilization: {blockUtilization:P1}");
                output.WriteLine($"  Efficiency vs theoretical max: {efficiency:P1}");

                if (result.TPS > theoreticalMaxTPS)
                {
                    output.WriteLine($"  ⚠️  WARNING: TPS exceeds theoretical maximum!");
                }
                else if (efficiency > 0.8)
                {
                    output.WriteLine($"  ✅ Good efficiency");
                }
                else if (efficiency > 0.5)
                {
                    output.WriteLine($"  🔄 Moderate efficiency");
                }
                else
                {
                    output.WriteLine($"  ⚠️  Low efficiency");
                }
            }

            output.WriteLine("\n=== Block Constraint Analysis ===");
            var bestResult = results.Values.OrderByDescending(r => r.TPS).First();
            var optimalBatchSize = results.Where(kvp => kvp.Value.TPS == bestResult.TPS).First().Key;

            output.WriteLine($"Best performance: {bestResult.TPS:F2} TPS with batch size {optimalBatchSize}");
            output.WriteLine($"Theoretical limit utilization: {(bestResult.TPS / theoreticalMaxTPS):P1}");

            // Test sustained performance
            output.WriteLine("\n=== Sustained Performance Test ===");
            await TestSustainedBlockPerformance(optimalBatchSize);

            Assert.All(results.Values, result => Assert.True(result.TPS > 0, "TPS must be greater than 0"));
        }

        [Fact]
        public async Task MeasureBlockFillSimulation()
        {
            output.WriteLine("=== Block Fill Simulation ===");

            const int BLOCK_TIME_SECONDS = 30;
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            const int AVERAGE_TX_SIZE = 500; // bytes

            var maxTransactionsPerBlock = BLOCK_SIZE_BYTES / AVERAGE_TX_SIZE;
            var targetTPS = maxTransactionsPerBlock / (double)BLOCK_TIME_SECONDS;

            output.WriteLine($"Target: Fill {BLOCK_SIZE_BYTES / 1_048_576.0:F1}MB block in {BLOCK_TIME_SECONDS}s");
            output.WriteLine($"Max transactions per block: {maxTransactionsPerBlock}");
            output.WriteLine($"Target TPS: {targetTPS:F2}\n");

            // Simulate different fill strategies
            var strategies = new[]
            {
                ("Sequential", 1),
                ("Small Parallel", 2),
                ("Medium Parallel", 4),
                ("High Parallel", Environment.ProcessorCount)
            };

            var results = new Dictionary<string, BlockFillResult>();

            foreach (var (strategyName, threadCount) in strategies)
            {
                output.WriteLine($"Testing {strategyName} strategy ({threadCount} threads):");

                var fillResult = await SimulateBlockFill(maxTransactionsPerBlock, threadCount, BLOCK_TIME_SECONDS);
                results[strategyName] = fillResult;

                var efficiency = fillResult.ActualTPS / targetTPS;
                var blockUtilization = fillResult.TransactionsProcessed / (double)maxTransactionsPerBlock;

                output.WriteLine($"  Actual TPS: {fillResult.ActualTPS:F2}");
                output.WriteLine($"  Efficiency: {efficiency:P1}");
                output.WriteLine($"  Block utilization: {blockUtilization:P1}");
                output.WriteLine($"  Time to fill: {fillResult.TimeToFill.TotalSeconds:F1}s");

                if (fillResult.TimeToFill.TotalSeconds <= BLOCK_TIME_SECONDS)
                    output.WriteLine($"  ✅ Block filled within time limit");
                else
                    output.WriteLine($"  ⚠️  Block fill exceeded time limit");
            }

            // Find optimal strategy
            var optimalStrategy = results.OrderByDescending(kvp => kvp.Value.ActualTPS).First();
            output.WriteLine($"=== Optimal Strategy ===");
            output.WriteLine($"Best strategy: {optimalStrategy.Key}");
            output.WriteLine($"Performance: {optimalStrategy.Value.ActualTPS:F2} TPS");
            output.WriteLine($"Block fill time: {optimalStrategy.Value.TimeToFill.TotalSeconds:F1}s");

            Assert.True(results.Values.Any(r => r.ActualTPS > 0), "At least one strategy must have positive TPS");
        }

        [Fact]
        public async Task MeasureTPSWithVariableTransactionSizes()
        {
            output.WriteLine("=== TPS with variable transaction sizes ===");

            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            const int BLOCK_TIME_SECONDS = 30;

            var transactionSizes = new[] { 250, 500, 750, 1000, 1500 };
            var results = new Dictionary<int, TPSResult>();

            foreach (var txSize in transactionSizes)
            {
                output.WriteLine($"\nSimulating {txSize}-byte transactions:");

                var maxTxPerBlock = BLOCK_SIZE_BYTES / txSize;
                var theoreticalTPS = maxTxPerBlock / (double)BLOCK_TIME_SECONDS;

                output.WriteLine($"  Max transactions per block: {maxTxPerBlock}");
                output.WriteLine($"  Theoretical max TPS: {theoreticalTPS:F2}");

                // Adjust test size based on theoretical limits
                var testTransactionCount = Math.Min(1000, maxTxPerBlock / 5);

                var result = await ProcessTransactionBatchWithSize(testTransactionCount, txSize, $"TxSize-{txSize}");
                results[txSize] = result;

                var efficiency = result.TPS / theoreticalTPS;

                output.WriteLine($"  Actual TPS: {result.TPS:F2}");
                output.WriteLine($"  Efficiency: {efficiency:P1}");

                if (efficiency > 0.8)
                    output.WriteLine($"  ✅ High efficiency");
                else if (efficiency > 0.5)
                    output.WriteLine($"  🔄 Moderate efficiency");
                else
                    output.WriteLine($"  ⚠️  Low efficiency");
            }

            output.WriteLine("\n=== Transaction Size Impact Analysis ===");
            var baseline = results[500]; // 500 byte baseline

            foreach (var kvp in results)
            {
                if (kvp.Key == 500) continue;

                var impact = (kvp.Value.TPS - baseline.TPS) / baseline.TPS * 100;
                output.WriteLine($"{kvp.Key}-byte transactions: {impact:+0.0;-0.0;±0.0}% TPS change vs 500-byte");
            }

            Assert.All(results.Values, result => Assert.True(result.TPS > 0, "TPS must be positive"));
        }

        private async Task TestSustainedBlockPerformance(int optimalBatchSize)
        {
            const int numberOfBlocks = 5;
            const int BLOCK_TIME_SECONDS = 30;

            output.WriteLine($"Testing sustained performance over {numberOfBlocks} simulated blocks:");

            var blockResults = new List<TPSResult>();

            for (int block = 1; block <= numberOfBlocks; block++)
            {
                output.WriteLine($"  Block {block}/{numberOfBlocks}... ");

                var blockResult = await ProcessTransactionBatch(optimalBatchSize, $"SustainedBlock-{block}", false);
                blockResults.Add(blockResult);

                var transactionsPerBlock = blockResult.TPS * BLOCK_TIME_SECONDS;
                output.WriteLine($"{blockResult.TPS:F2} TPS ({transactionsPerBlock:F0} tx/block)");

                // Simulate inter-block delay
                await Task.Delay(100);
            }

            var avgTPS = blockResults.Average(r => r.TPS);
            var minTPS = blockResults.Min(r => r.TPS);
            var maxTPS = blockResults.Max(r => r.TPS);
            var stdDev = Math.Sqrt(blockResults.Average(r => Math.Pow(r.TPS - avgTPS, 2)));
            var stability = 1.0 - (stdDev / avgTPS);

            output.WriteLine($"\nSustained Performance Analysis:");
            output.WriteLine($"  Average TPS: {avgTPS:F2}");
            output.WriteLine($"  Range: {minTPS:F2} - {maxTPS:F2} TPS");
            output.WriteLine($"  Standard deviation: {stdDev:F2}");
            output.WriteLine($"  Stability coefficient: {stability:P1}");

            if (stability > 0.9)
                output.WriteLine($"  ✅ Very stable performance");
            else if (stability > 0.8)
                output.WriteLine($"  🔄 Good stability");
            else
                output.WriteLine($"  ⚠️  Performance variability detected");
        }

        private async Task<BlockFillResult> SimulateBlockFill(int maxTransactions, int threadCount, int timeLimit)
        {
            var stopwatch = Stopwatch.StartNew();
            var processedTransactions = 0;

            if (threadCount == 1)
            {
                // Sequential processing
                for (int i = 0; i < maxTransactions && stopwatch.ElapsedMilliseconds < timeLimit * 1000; i++)
                {
                    await SimulateTransactionProcessing();
                    processedTransactions++;
                }
            }
            else
            {
                // Parallel processing
                var tasks = new List<Task<int>>();
                var transactionsPerThread = maxTransactions / threadCount;

                for (int t = 0; t < threadCount; t++)
                {
                    tasks.Add(ProcessTransactionBatchWithTimeout(transactionsPerThread, timeLimit, stopwatch));
                }

                var threadResults = await Task.WhenAll(tasks);
                processedTransactions = threadResults.Sum();
            }

            stopwatch.Stop();

            var actualTPS = processedTransactions / (stopwatch.ElapsedMilliseconds / 1000.0);

            return new BlockFillResult
            {
                TransactionsProcessed = processedTransactions,
                ActualTPS = actualTPS,
                TimeToFill = stopwatch.Elapsed,
                ThreadCount = threadCount
            };
        }

        private async Task<int> ProcessTransactionBatchWithTimeout(int maxTransactions, int timeoutSeconds, Stopwatch globalStopwatch)
        {
            var processed = 0;

            for (int i = 0; i < maxTransactions && globalStopwatch.ElapsedMilliseconds < timeoutSeconds * 1000; i++)
            {
                await SimulateTransactionProcessing();
                processed++;
            }

            return processed;
        }

        private async Task<TPSResult> ProcessTransactionBatchWithSize(int count, int simulatedTxSize, string testName)
        {
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                // Simulate processing time proportional to transaction size
                var baseProcessingTime = TimeSpan.FromMicroseconds(350);
                var sizeMultiplier = simulatedTxSize / 500.0; // 500 bytes as baseline
                var adjustedProcessingTime = TimeSpan.FromTicks((long)(baseProcessingTime.Ticks * sizeMultiplier));

                await Task.Delay(adjustedProcessingTime);

                // Additional CPU work for larger transactions
                var iterations = (int)(100 * sizeMultiplier);
                var hash = 0;
                for (int j = 0; j < iterations; j++)
                {
                    hash = hash * 17 + j;
                }
            }

            stopwatch.Stop();

            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = count / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / count;

            return new TPSResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = count,
                AvgTimePerTransaction = avgTimePerTransaction,
                TestName = testName
            };
        }

        private async Task<TPSResult> ProcessTransactionBatch(int count, string testName, bool showProgress)
        {
            var stopwatch = Stopwatch.StartNew();
            var processedCount = 0;
            var progressInterval = Math.Max(count / 10, 1);
            
            for (int i = 0; i < count; i++)
            {
                // Simulate transaction processing
                await SimulateTransactionProcessing();
                
                processedCount++;
                
                if (showProgress && processedCount % progressInterval == 0)
                {
                    var currentTPS = processedCount / (stopwatch.ElapsedMilliseconds / 1000.0);
                    output.WriteLine($"{testName}: {processedCount}/{count} ({currentTPS:F2} TPS)");
                }
            }
            
            stopwatch.Stop();
            
            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = count / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / count;
            
            return new TPSResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = count,
                AvgTimePerTransaction = avgTimePerTransaction,
                TestName = testName
            };
        }

        private async Task SimulateTransactionProcessing()
        {
            // Simulate various operations during transaction processing
            // 1. Transaction validation (fast operation)
            await ValidateTransactionAsync();
            
            // 2. Memory pool processing (medium speed)
            await ProcessInMemoryPoolAsync();
            
            // 3. Consensus operations (slower)
            await ProcessConsensusAsync();
        }

        private async Task ValidateTransactionAsync()
        {
            // Simulate validation - cryptographic operations, signature verification
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
    }

    public class TPSResult
    {
        public double TPS { get; set; }
        public long TotalTimeMs { get; set; }
        public int TransactionCount { get; set; }
        public double AvgTimePerTransaction { get; set; }
        public string TestName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of block fill simulation test
    /// </summary>
    public class BlockFillResult
    {
        public int TransactionsProcessed { get; set; }
        public double ActualTPS { get; set; }
        public TimeSpan TimeToFill { get; set; }
        public int ThreadCount { get; set; }
    }
}