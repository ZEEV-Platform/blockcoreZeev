using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.Networks;
using Blockcore.Networks.ZEEV;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace TESTZEEVTPS
{
    /// <summary>
    /// Extended TPS tests using real blockchain transactions with signatures
    /// </summary>
    public class RealTransactionTPSTests
    {
        private readonly ITestOutputHelper output;
        private readonly ILogger<RealTransactionTPSTests> logger;
        private readonly Network network;
        private readonly RealTransactionGenerator transactionGenerator;

        public RealTransactionTPSTests(ITestOutputHelper output)
        {
            this.output = output;
            
            var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            this.logger = loggerFactory.CreateLogger<RealTransactionTPSTests>();
            
            // Using test network (you can change to a different network)
            this.network = CreateTestNetwork();
            this.transactionGenerator = new RealTransactionGenerator(this.network, 20);
        }

        [Fact]
        public async Task MeasureRealTransactionTPS_SingleThread()
        {
            output.WriteLine("=== Real TPS Test - Single Thread ===");
            
            const int numberOfTransactions = 1000;
            const int warmupTransactions = 100;
            
            // Warmup
            output.WriteLine("Running warmup...");
            await ProcessRealTransactionBatch(warmupTransactions, "Warmup", false);
            
            // Actual test
            var result = await ProcessRealTransactionBatch(numberOfTransactions, "Single Thread Real TX", true);
            
            output.WriteLine($"Single Thread Result (Real TX): {result.TPS:F2} TPS");
            output.WriteLine($"Total time: {result.TotalTimeMs} ms");
            output.WriteLine($"Average time per transaction: {result.AvgTimePerTransaction:F4} ms");
            output.WriteLine($"Successfully processed: {result.SuccessfulTransactions}/{result.TransactionCount}");
            output.WriteLine($"Average transaction size: {result.AverageTransactionSize} bytes");
            output.WriteLine($"Total fees: {result.TotalFees} plancks");
            
            Assert.True(result.TPS > 0, "TPS must be greater than 0");
            Assert.True(result.SuccessfulTransactions > numberOfTransactions * 0.8, "At least 80% of transactions must be successful");
        }

        [Fact]
        public async Task MeasureRealTransactionTPS_MultiThread()
        {
            output.WriteLine("=== Real TPS Test - Multi Thread ===");
            
            const int numberOfTransactions = 1000;
            int numberOfThreads = Math.Min(Environment.ProcessorCount, 4); // Limited for stability
            int transactionsPerThread = numberOfTransactions / numberOfThreads;
            
            output.WriteLine($"Number of threads: {numberOfThreads}");
            output.WriteLine($"Transactions per thread: {transactionsPerThread}");
            
            var tasks = new List<Task<RealTransactionTPSResult>>();
            var totalStopwatch = Stopwatch.StartNew();
            
            // Start multiple threads simultaneously
            for (int i = 0; i < numberOfThreads; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(async () => 
                    await ProcessRealTransactionBatch(transactionsPerThread, $"Thread-{threadId}", false)));
            }
            
            var results = await Task.WhenAll(tasks);
            totalStopwatch.Stop();
            
            // Analyze results
            var combinedResult = CombineResults(results, totalStopwatch.ElapsedMilliseconds);
            
            output.WriteLine($"=== Multi Thread Results (Real TX) ===");
            output.WriteLine($"Total TPS: {combinedResult.TPS:F2} TPS");
            output.WriteLine($"Total time: {combinedResult.TotalTimeMs} ms");
            output.WriteLine($"Successful transactions: {combinedResult.SuccessfulTransactions}/{combinedResult.TransactionCount}");
            output.WriteLine($"Average latency: {combinedResult.AvgTimePerTransaction:F4} ms");
            
            // Details of individual threads
            for (int i = 0; i < results.Length; i++)
            {
                output.WriteLine($"Thread-{i}: {results[i].TPS:F2} TPS, " +
                    $"Success: {results[i].SuccessfulTransactions}/{results[i].TransactionCount} " +
                    $"({results[i].SuccessfulTransactions * 100.0 / results[i].TransactionCount:F1}%)");
            }
            
            Assert.True(combinedResult.TPS > 0, "Total TPS must be greater than 0");
            Assert.True(combinedResult.SuccessfulTransactions > numberOfTransactions * 0.7, "At least 70% of transactions must be successful");
        }

        [Fact]
        public async Task CompareSimulatedVsRealTransactionTPS()
        {
            output.WriteLine("=== Comparison of simulated vs. real transactions ===");
            
            const int transactionCount = 500;
            
            // Test simulated transactions
            var simulatedStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < transactionCount; i++)
            {
                await SimulateTransactionProcessing(); // Original simulation
            }
            simulatedStopwatch.Stop();
            var simulatedTPS = transactionCount / (simulatedStopwatch.ElapsedMilliseconds / 1000.0);
            
            // Test real transactions
            var realResult = await ProcessRealTransactionBatch(transactionCount, "Real vs Simulated", false);
            
            output.WriteLine("=== Comparison Results ===");
            output.WriteLine($"Simulated transactions: {simulatedTPS:F2} TPS ({simulatedStopwatch.ElapsedMilliseconds} ms)");
            output.WriteLine($"Real transactions: {realResult.TPS:F2} TPS ({realResult.TotalTimeMs} ms)");
            output.WriteLine($"Ratio (Real/Simulated): {realResult.TPS / simulatedTPS:F3}x");
            output.WriteLine($"Time difference: {realResult.TotalTimeMs - simulatedStopwatch.ElapsedMilliseconds} ms");
            
            var overhead = (realResult.AvgTimePerTransaction - (simulatedStopwatch.ElapsedMilliseconds / (double)transactionCount));
            output.WriteLine($"Overhead per transaction: {overhead:F4} ms");
            
            Assert.True(realResult.TPS > 0, "Real transaction TPS must be greater than 0");
            Assert.True(realResult.SuccessfulTransactions > transactionCount * 0.8, "Most transactions must be successful");
        }

        [Fact]
        public async Task MeasureTransactionComplexityImpact()
        {
            output.WriteLine("=== Transaction complexity impact on TPS ===");
            
            const int transactionsPerComplexity = 200;
            var complexityLevels = new[]
            {
                TransactionComplexity.Simple,
                TransactionComplexity.Medium,
                TransactionComplexity.Complex
            };
            
            var results = new Dictionary<TransactionComplexity, RealTransactionTPSResult>();
            
            foreach (var complexity in complexityLevels)
            {
                output.WriteLine($"\nTesting complexity: {complexity}");
                
                var options = new TransactionGenerationOptions
                {
                    Complexity = complexity,
                    InputCount = complexity == TransactionComplexity.Simple ? 1 : 
                                complexity == TransactionComplexity.Medium ? 2 : 3,
                    OutputCount = complexity == TransactionComplexity.Simple ? 2 : 
                                 complexity == TransactionComplexity.Medium ? 3 : 4
                };
                
                var result = await ProcessRealTransactionBatchWithOptions(transactionsPerComplexity, 
                    $"Complexity-{complexity}", options, false);
                results[complexity] = result;
                
                output.WriteLine($"{complexity}: {result.TPS:F2} TPS, " +
                    $"Avg size: {result.AverageTransactionSize} bytes, " +
                    $"Success: {result.SuccessfulTransactions}/{result.TransactionCount}");
            }
            
            output.WriteLine("\n=== Complexity Impact Analysis ===");
            var baselineTPS = results[TransactionComplexity.Simple].TPS;
            foreach (var kvp in results)
            {
                var impact = (kvp.Value.TPS / baselineTPS - 1) * 100;
                output.WriteLine($"{kvp.Key}: {impact:+0.0;-0.0}% TPS change compared to Simple");
            }
            
            Assert.All(results.Values, result => Assert.True(result.TPS > 0, "TPS must be positive"));
        }

        [Fact]
        public async Task MeasureTransactionValidationOverhead()
        {
            output.WriteLine("=== Transaction validation overhead ===");
            
            const int transactionCount = 300;
            
            // Test without validation
            var optionsNoValidation = new TransactionGenerationOptions
            {
                ValidateAfterGeneration = false
            };
            var resultNoValidation = await ProcessRealTransactionBatchWithOptions(
                transactionCount, "No Validation", optionsNoValidation, false);
            
            // Test with validation
            var optionsWithValidation = new TransactionGenerationOptions
            {
                ValidateAfterGeneration = true
            };
            var resultWithValidation = await ProcessRealTransactionBatchWithOptions(
                transactionCount, "With Validation", optionsWithValidation, false);
            
            var validationOverhead = resultWithValidation.AvgTimePerTransaction - resultNoValidation.AvgTimePerTransaction;
            var validationImpact = (1 - resultWithValidation.TPS / resultNoValidation.TPS) * 100;
            
            output.WriteLine("=== Validation overhead analysis ===");
            output.WriteLine($"Without validation: {resultNoValidation.TPS:F2} TPS ({resultNoValidation.AvgTimePerTransaction:F4} ms/tx)");
            output.WriteLine($"With validation: {resultWithValidation.TPS:F2} TPS ({resultWithValidation.AvgTimePerTransaction:F4} ms/tx)");
            output.WriteLine($"Validation overhead: {validationOverhead:F4} ms per transaction");
            output.WriteLine($"TPS decrease: {validationImpact:F1}%");
            
            Assert.True(resultNoValidation.TPS >= resultWithValidation.TPS, "TPS without validation should be higher or equal");
            Assert.True(validationOverhead >= 0, "Validation overhead should be non-negative");
        }

        [Fact]
        public async Task MeasureBlockConstraintCompliance()
        {
            output.WriteLine("=== Block Constraint Compliance Test ===");

            const int BLOCK_TIME_SECONDS = 30;
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB

            output.WriteLine($"Blockchain Parameters:");
            output.WriteLine($"  Block time: {BLOCK_TIME_SECONDS} seconds");
            output.WriteLine($"  Block size: {BLOCK_SIZE_BYTES / 1_048_576.0:F1} MB");

            // Test block generation simulation
            var blockResult = this.transactionGenerator.SimulateBlockGeneration(5000, BLOCK_TIME_SECONDS);

            output.WriteLine($"Block Generation Results:");
            output.WriteLine($"  Transactions included: {blockResult.TransactionCount}");
            output.WriteLine($"  Rejected transactions: {blockResult.RejectedTransactions}");
            output.WriteLine($"  Block utilization: {blockResult.BlockUtilization:P1}");
            output.WriteLine($"  Total block size: {blockResult.TotalSize:N0} bytes");
            output.WriteLine($"  Average transaction size: {blockResult.AverageTransactionSize} bytes");
            output.WriteLine($"  Total fees: {blockResult.TotalFees.Satoshi:N0} plancks");
            output.WriteLine($"  Generation TPS: {blockResult.TransactionsPerSecond:F2}");
            output.WriteLine($"  Block fill status: {(blockResult.BlockFull ? "FULL" : "Partial")}");

            // Calculate theoretical limits
            var theoreticalMaxTx = BLOCK_SIZE_BYTES / blockResult.AverageTransactionSize;
            var theoreticalMaxTPS = theoreticalMaxTx / (double)BLOCK_TIME_SECONDS;
            var actualEfficiency = blockResult.TransactionsPerSecond / theoreticalMaxTPS;

            output.WriteLine($"Theoretical Analysis:");
            output.WriteLine($"  Max transactions per block: {theoreticalMaxTx}");
            output.WriteLine($"  Theoretical max TPS: {theoreticalMaxTPS:F2}");
            output.WriteLine($"  Actual efficiency: {actualEfficiency:P1}");

            if (blockResult.BlockFull && blockResult.BlockUtilization > 0.95)
                output.WriteLine($"  ✅ Excellent block utilization");
            else if (blockResult.BlockUtilization > 0.80)
                output.WriteLine($"  🔄 Good block utilization");
            else
                output.WriteLine($"  ⚠️  Low block utilization");

            Assert.True(blockResult.TransactionCount > 0, "Block must contain at least one transaction");
            Assert.True(blockResult.BlockUtilization <= 1.0, "Block utilization cannot exceed 100%");
            Assert.True(blockResult.TotalSize <= BLOCK_SIZE_BYTES, "Block size cannot exceed limit");
        }

        [Fact]
        public async Task MeasureOptimizedBlockGeneration()
        {
            output.WriteLine("=== Optimized Block Generation Test ===");

            var utilizationTargets = new[] { 50, 70, 85, 95 };

            foreach (var target in utilizationTargets)
            {
                output.WriteLine($"\nTesting {target}% block utilization target:");

                var optimizedTransactions = this.transactionGenerator
                    .GenerateOptimizedTransactionBatch(target, Money.Satoshis(750))
                    .ToList();

                var totalSize = optimizedTransactions.Sum(t => t.Size);
                var actualUtilization = totalSize / 2_621_440.0; // 2.5 MB
                var avgTxSize = optimizedTransactions.Count > 0 ? totalSize / optimizedTransactions.Count : 0;
                var totalFees = optimizedTransactions.Sum(t => t.Fee.Satoshi);

                output.WriteLine($"  Generated transactions: {optimizedTransactions.Count}");
                output.WriteLine($"  Total size: {totalSize:N0} bytes");
                output.WriteLine($"  Actual utilization: {actualUtilization:P1}");
                output.WriteLine($"  Average transaction size: {avgTxSize} bytes");
                output.WriteLine($"  Total fees: {totalFees:N0} plancks");

                var targetUtilizationDecimal = target / 100.0;
                var utilizationAccuracy = Math.Abs(actualUtilization - targetUtilizationDecimal) / targetUtilizationDecimal;

                output.WriteLine($"  Target accuracy: {(1 - utilizationAccuracy):P1}");

                if (utilizationAccuracy < 0.05) // Within 5%
                    output.WriteLine($"  ✅ Excellent targeting accuracy");
                else if (utilizationAccuracy < 0.15) // Within 15%
                    output.WriteLine($"  🔄 Good targeting accuracy");
                else
                    output.WriteLine($"  ⚠️  Poor targeting accuracy");

                Assert.True(optimizedTransactions.Count > 0, "Must generate at least one transaction");
                Assert.True(actualUtilization <= 1.0, "Cannot exceed 100% utilization");
            }
        }

        [Fact]
        public async Task MeasureRealTransactionTPS_WithBlockAnalysis()
        {
            output.WriteLine("=== Real Transaction TPS with Block Analysis ===");

            const int testTransactionCount = 500;
            const int BLOCK_TIME_SECONDS = 30;
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB

            var result = await ProcessRealTransactionBatch(testTransactionCount, "BlockAnalysis", true);

            // Calculate block metrics
            var transactionsPerBlock = result.TPS * BLOCK_TIME_SECONDS;
            var estimatedBlockSize = result.AverageTransactionSize * transactionsPerBlock;
            var blockUtilization = Math.Min(1.0, estimatedBlockSize / BLOCK_SIZE_BYTES);
            var theoreticalMaxTPS = BLOCK_SIZE_BYTES / (double)(result.AverageTransactionSize * BLOCK_TIME_SECONDS);
            var tpsEfficiency = result.TPS / theoreticalMaxTPS;

            output.WriteLine($"\n=== Block Constraint Analysis ===");
            output.WriteLine($"Actual TPS: {result.TPS:F2}");
            output.WriteLine($"Theoretical max TPS: {theoreticalMaxTPS:F2}");
            output.WriteLine($"TPS efficiency: {tpsEfficiency:P1}");
            output.WriteLine($"Transactions per block: {transactionsPerBlock:F0}");
            output.WriteLine($"Estimated block size: {estimatedBlockSize:N0} bytes ({estimatedBlockSize / 1_048_576.0:F2} MB)");
            output.WriteLine($"Block utilization: {blockUtilization:P1}");
            output.WriteLine($"Average transaction size: {result.AverageTransactionSize} bytes");
            output.WriteLine($"Success rate: {result.SuccessRate:P1}");

            // Performance classification
            if (tpsEfficiency > 0.8 && blockUtilization > 0.8)
                output.WriteLine($"✅ Excellent: High TPS efficiency and block utilization");
            else if (tpsEfficiency > 0.6 && blockUtilization > 0.6)
                output.WriteLine($"🔄 Good: Reasonable performance metrics");
            else if (tpsEfficiency > 0.4 || blockUtilization > 0.4)
                output.WriteLine($"⚠️  Moderate: Some optimization needed");
            else
                output.WriteLine($"❌ Poor: Significant optimization required");

            // Block time analysis
            var timeToFillBlock = transactionsPerBlock / result.TPS;
            output.WriteLine($"\nBlock Time Analysis:");
            output.WriteLine($"Time to fill block at current TPS: {timeToFillBlock:F1} seconds");

            if (timeToFillBlock <= BLOCK_TIME_SECONDS)
                output.WriteLine($"✅ Block can be filled within {BLOCK_TIME_SECONDS}s block time");
            else
                output.WriteLine($"⚠️  Block fill time exceeds {BLOCK_TIME_SECONDS}s block time limit");

            // Memory pool implications
            var pendingTransactions = Math.Max(0, (result.TPS * BLOCK_TIME_SECONDS) - transactionsPerBlock);
            if (pendingTransactions > 0)
            {
                output.WriteLine($"\nMemory Pool Analysis:");
                output.WriteLine($"Estimated pending transactions: {pendingTransactions:F0}");
                output.WriteLine($"Memory pool growth rate: {pendingTransactions / BLOCK_TIME_SECONDS:F2} tx/s");
            }

            Assert.True(result.TPS > 0, "TPS must be positive");
            Assert.True(result.SuccessRate > 0.5, "Success rate must be at least 50%");
            Assert.True(blockUtilization <= 1.0, "Block utilization cannot exceed 100%");
        }

        [Fact]
        public async Task CompareTPSWithDifferentFeeRates()
        {
            output.WriteLine("=== TPS Comparison with Different Fee Rates ===");

            var feeRates = new[]
            {
                (Name: "Low", Satoshis: 100L),
                (Name: "Standard", Satoshis: 500L),
                (Name: "High", Satoshis: 1000L),
                (Name: "Priority", Satoshis: 2000L)
            };

            const int transactionsPerTest = 200;
            var results = new Dictionary<string, RealTransactionTPSResult>();

            foreach (var (name, satoshis) in feeRates)
            {
                output.WriteLine($"\nTesting {name} fee rate ({satoshis} plancks/tx):");

                var options = new TransactionGenerationOptions
                {
                    Fee = Money.Satoshis(satoshis),
                    ValidateAfterGeneration = true,
                    InputCount = 1,
                    OutputCount = 2 // One output + change
                };

                var result = await ProcessRealTransactionBatchWithOptions(
                    transactionsPerTest, $"FeeRate-{name}", options, false);
                results[name] = result;

                // Block analysis
                var transactionsPerBlock = result.TPS * 30; // 30-second blocks
                var estimatedBlockSize = result.AverageTransactionSize * transactionsPerBlock;
                var blockUtilization = Math.Min(1.0, estimatedBlockSize / 2_621_440.0); // 2.5 MB

                output.WriteLine($"  TPS: {result.TPS:F2}");
                output.WriteLine($"  Success rate: {result.SuccessRate:P1}");
                output.WriteLine($"  Avg transaction size: {result.AverageTransactionSize} bytes");
                output.WriteLine($"  Transactions per block: {transactionsPerBlock:F0}");
                output.WriteLine($"  Block utilization: {blockUtilization:P1}");
                output.WriteLine($"  Average fee paid: {result.FeesPerTransaction:F0} sat/tx");
            }

            output.WriteLine("\n=== Fee Rate Impact Analysis ===");
            var baselineResult = results["Standard"];

            foreach (var kvp in results)
            {
                if (kvp.Key == "Standard") continue;

                var tpsChange = (kvp.Value.TPS - baselineResult.TPS) / baselineResult.TPS * 100;
                var sizeChange = (kvp.Value.AverageTransactionSize - baselineResult.AverageTransactionSize) / (double)baselineResult.AverageTransactionSize * 100;

                output.WriteLine($"{kvp.Key} vs Standard:");
                output.WriteLine($"  TPS change: {tpsChange:+0.0;-0.0;±0.0}%");
                output.WriteLine($"  Size change: {sizeChange:+0.0;-0.0;±0.0}%");
                output.WriteLine($"  Fee difference: {kvp.Value.FeesPerTransaction - baselineResult.FeesPerTransaction:+0;-0;±0} sat/tx");
            }

            Assert.All(results.Values, result => Assert.True(result.TPS > 0, "All fee rates must produce positive TPS"));
        }

        [Fact]
        public async Task MeasureNetworkCongestionSimulation()
        {
            output.WriteLine("=== Network Congestion Simulation ===");

            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            const int BLOCK_TIME_SECONDS = 30;

            var congestionLevels = new[]
            {
                (Name: "Low", MemPoolMultiplier: 1.2),
                (Name: "Medium", MemPoolMultiplier: 2.0),
                (Name: "High", MemPoolMultiplier: 3.0),
                (Name: "Extreme", MemPoolMultiplier: 5.0)
            };

            foreach (var (name, multiplier) in congestionLevels)
            {
                output.WriteLine($"\nSimulating {name} congestion (Memory pool: {multiplier:F1}x block size):");

                // Calculate target transactions for this congestion level
                var baseTransactionSize = 500; // bytes
                var baseTransactionsPerBlock = BLOCK_SIZE_BYTES / baseTransactionSize;
                var targetMemPoolSize = (int)(baseTransactionsPerBlock * multiplier);

                output.WriteLine($"  Target memory pool size: {targetMemPoolSize} transactions");
                output.WriteLine($"  Expected competition: {(multiplier - 1) * 100:F0}% more transactions than block capacity");

                // Simulate higher fee pressure during congestion
                var baseFee = 500; // plancks
                var congestionFee = (int)(baseFee * Math.Max(1.0, multiplier - 0.5));

                var options = new TransactionGenerationOptions
                {
                    Fee = Money.Satoshis(congestionFee),
                    ValidateAfterGeneration = true,
                    Complexity = multiplier > 3 ? TransactionComplexity.Simple : TransactionComplexity.Medium // Simpler txs during high congestion
                };

                // Test smaller batch to simulate competitive environment
                var testCount = Math.Min(300, targetMemPoolSize / 5);
                var result = await ProcessRealTransactionBatchWithOptions(
                    testCount, $"Congestion-{name}", options, false);

                var transactionsPerBlock = result.TPS * BLOCK_TIME_SECONDS;
                var competitionRatio = targetMemPoolSize / Math.Max(1, transactionsPerBlock);
                var confirmationBlocks = Math.Ceiling(competitionRatio);
                var estimatedConfirmationTime = confirmationBlocks * BLOCK_TIME_SECONDS;

                output.WriteLine($"  Actual TPS: {result.TPS:F2}");
                output.WriteLine($"  Success rate: {result.SuccessRate:P1}");
                output.WriteLine($"  Transactions per block: {transactionsPerBlock:F0}");
                output.WriteLine($"  Competition ratio: {competitionRatio:F1}:1");
                output.WriteLine($"  Estimated confirmation time: {estimatedConfirmationTime:F0} seconds ({confirmationBlocks:F0} blocks)");
                output.WriteLine($"  Average fee: {result.FeesPerTransaction:F0} sat/tx");

                // Performance assessment
                if (result.SuccessRate > 0.9 && result.TPS > 50)
                    output.WriteLine($"  ✅ Network handling congestion well");
                else if (result.SuccessRate > 0.7 && result.TPS > 25)
                    output.WriteLine($"  🔄 Network experiencing some pressure");
                else if (result.SuccessRate > 0.5)
                    output.WriteLine($"  ⚠️  Network under significant stress");
                else
                    output.WriteLine($"  ❌ Network severely congested");
            }

            output.WriteLine("\n=== Congestion Resistance Analysis ===");
            output.WriteLine("Higher success rates and stable TPS under congestion indicate");
            output.WriteLine("better network resilience and transaction processing efficiency.");
        }

        private async Task<RealTransactionTPSResult> ProcessRealTransactionBatch(int count, string testName, bool showProgress)
        {
            return await ProcessRealTransactionBatchWithOptions(count, testName, null, showProgress);
        }

        private async Task<RealTransactionTPSResult> ProcessRealTransactionBatchWithOptions(
            int count, string testName, TransactionGenerationOptions? options, bool showProgress)
        {
            var stopwatch = Stopwatch.StartNew();
            var processedCount = 0;
            var successfulCount = 0;
            var totalSize = 0;
            var totalFees = Money.Zero;
            var progressInterval = Math.Max(count / 10, 1);
            var errors = new List<string>();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    // Generate real transaction with signature
                    var transactionData = this.transactionGenerator.GenerateSignedTransaction(options);

                    if (transactionData.IsValid && transactionData.HasTransaction)
                    {
                        successfulCount++;
                        totalSize += transactionData.Size;
                        totalFees += transactionData.Fee;
                    }
                    else if (!string.IsNullOrEmpty(transactionData.Error))
                    {
                        errors.Add(transactionData.Error);
                    }

                    processedCount++;

                    if (showProgress && processedCount % progressInterval == 0)
                    {
                        var currentTPS = processedCount / (stopwatch.ElapsedMilliseconds / 1000.0);
                        output.WriteLine($"{testName}: {processedCount}/{count} " +
                            $"({currentTPS:F2} TPS, Success: {successfulCount}/{processedCount})");
                    }

                    // Small delay to simulate network latency
                    await Task.Delay(1);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    processedCount++;
                }
            }

            stopwatch.Stop();

            var totalTimeMs = stopwatch.ElapsedMilliseconds;
            var tps = count / (totalTimeMs / 1000.0);
            var avgTimePerTransaction = (double)totalTimeMs / count;
            var avgTransactionSize = successfulCount > 0 ? totalSize / successfulCount : 0;

            return new RealTransactionTPSResult
            {
                TPS = tps,
                TotalTimeMs = totalTimeMs,
                TransactionCount = count,
                SuccessfulTransactions = successfulCount,
                AvgTimePerTransaction = avgTimePerTransaction,
                AverageTransactionSize = avgTransactionSize,
                TotalFees = totalFees,
                TestName = testName,
                Errors = errors
            };
        }

        private RealTransactionTPSResult CombineResults(RealTransactionTPSResult[] results, long totalTimeMs)
        {
            var totalTransactions = results.Sum(r => r.TransactionCount);
            var totalSuccessful = results.Sum(r => r.SuccessfulTransactions);
            var totalSize = results.Sum(r => r.AverageTransactionSize * r.SuccessfulTransactions);
            var totalFees = results.Aggregate(Money.Zero, (sum, r) => sum + r.TotalFees);
            var allErrors = results.SelectMany(r => r.Errors).ToList();

            return new RealTransactionTPSResult
            {
                TPS = totalTransactions / (totalTimeMs / 1000.0),
                TotalTimeMs = totalTimeMs,
                TransactionCount = totalTransactions,
                SuccessfulTransactions = totalSuccessful,
                AvgTimePerTransaction = (double)totalTimeMs / totalTransactions,
                AverageTransactionSize = totalSuccessful > 0 ? (int)(totalSize / totalSuccessful) : 0,
                TotalFees = totalFees,
                TestName = "Combined",
                Errors = allErrors
            };
        }

        private async Task SimulateTransactionProcessing()
        {
            // Original simulated operations for comparison
            await Task.Delay(TimeSpan.FromMicroseconds(350));

            var hash = 0;
            for (int i = 0; i < 200; i++)
            {
                hash = hash * 17 + i;
            }
        }

        private Network CreateTestNetwork()
        {
            return new ZEEVTest();
        }
    }

    /// <summary>
    /// TPS test results with real transactions
    /// </summary>
    public class RealTransactionTPSResult
    {
        public double TPS { get; set; }
        public long TotalTimeMs { get; set; }
        public int TransactionCount { get; set; }
        public int SuccessfulTransactions { get; set; }
        public double AvgTimePerTransaction { get; set; }
        public int AverageTransactionSize { get; set; }
        public Money TotalFees { get; set; } = Money.Zero;
        public string TestName { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        
        public double SuccessRate => TransactionCount > 0 ? (double)SuccessfulTransactions / TransactionCount : 0;
        public double FeesPerTransaction => SuccessfulTransactions > 0 ? TotalFees.Satoshi / (double)SuccessfulTransactions : 0;
    }
}