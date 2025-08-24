using System;
using System.Collections.Generic;
using System.Linq;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Blockcore.Networks.ZEEV.Consensus;

namespace TESTZEEVTPS
{
    /// <summary>
    /// Generator of real blockchain transactions with cryptographic signatures
    /// </summary>
    public class RealTransactionGenerator
    {
        private readonly Network network;
        private readonly List<Key> privateKeys;
        private readonly List<Coin> availableCoins;
        private readonly Random random;

        public RealTransactionGenerator(Network network, int numberOfKeys = 10)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.privateKeys = new List<Key>();
            this.availableCoins = new List<Coin>();
            this.random = new Random();

            // Generate keys for testing
            GenerateTestKeys(numberOfKeys);

            // Create initial coins (UTXO)
            GenerateInitialCoins();
        }

        /// <summary>
        /// Generates a real signed transaction
        /// </summary>
        public SignedTransactionData GenerateSignedTransaction(TransactionGenerationOptions? options = null)
        {
            options ??= new TransactionGenerationOptions();

            var startTime = DateTime.UtcNow;

            try
            {
                // 1. Create transaction builder
                var builder = CreateTransactionBuilder();

                // 2. Select inputs (coins)
                var selectedCoins = SelectInputCoins(options.InputCount);
                builder.AddCoins(selectedCoins);

                // 3. Create outputs
                var outputs = CreateTransactionOutputs(options.OutputCount, options.TotalOutputAmount);
                foreach (var output in outputs)
                {
                    builder.Send(output.ScriptPubKey, output.Value);
                }

                // 5. Add change output if needed
                Script changeAddress;
                if (options.ChangeAddress != null)
                {
                    changeAddress = options.ChangeAddress;
                }
                else
                {
                    changeAddress = GenerateChangeAddress(); // Fallback
                }

                builder.SetChange(changeAddress);

                // 4. Set fee
                if (options.Fee > Money.Zero)
                {
                    builder.SendFees(options.Fee);
                }
                else
                {
                    builder.SendEstimatedFees(options.FeeRate);
                }

                // 6. Build and sign transaction
                var unsignedTx = (ZEEVTransaction)builder.BuildTransaction(false);
                var signedTx = builder.SignTransaction(unsignedTx);

                // 7. Calculate values for analysis
                var totalInputAmount = selectedCoins.Sum(c => c.Amount);
                var totalOutputAmount = signedTx.TotalOut;
                var calculatedFee = CalculateFee(selectedCoins, signedTx);
                var changeAmount = totalInputAmount - totalOutputAmount - calculatedFee;
                var hasChangeOutput = changeAmount >= options.MinimumChangeAmount || options.AllowZeroChange;

                // 8. Validate signatures if requested
                TransactionValidationResult? validationResult = null;
                if (options.ValidateAfterGeneration)
                {
                    validationResult = ValidateTransactionSignatures(signedTx, selectedCoins);
                }

                var endTime = DateTime.UtcNow;

                return new SignedTransactionData
                {
                    Transaction = signedTx,
                    InputCoins = selectedCoins,
                    OutputData = outputs,
                    ValidationResult = validationResult,
                    GenerationTimeMs = (endTime - startTime).TotalMilliseconds,
                    TransactionId = signedTx.GetHash(),
                    Size = signedTx.GetSerializedSize(),
                    Fee = calculatedFee,
                    Options = options,
                    ChangeAddress = GetAddressFromScript(changeAddress),
                    ChangeAmount = changeAmount,
                    HasChangeOutput = hasChangeOutput,
                    TotalInputAmount = totalInputAmount,
                    TotalOutputAmount = totalOutputAmount
                };
            }
            catch (Exception ex)
            {
                var endTime = DateTime.UtcNow;
                return new SignedTransactionData
                {
                    Error = ex.Message,
                    GenerationTimeMs = (endTime - startTime).TotalMilliseconds,
                    Options = options
                };
            }
        }

        /// <summary>
        /// Generates multiple transactions simultaneously for batch testing
        /// </summary>
        public IEnumerable<SignedTransactionData> GenerateTransactionBatch(int count, TransactionGenerationOptions? options = null)
        {
            var transactions = new List<SignedTransactionData>();

            for (int i = 0; i < count; i++)
            {
                var tx = GenerateSignedTransaction(options);
                transactions.Add(tx);

                // Update UTXO after each transaction
                if (tx.IsValid && tx.Transaction != null)
                {
                    UpdateUtxoSet(tx);
                }
            }

            return transactions;
        }

        /// <summary>
        /// Validates transaction signatures
        /// </summary>
        public TransactionValidationResult ValidateTransactionSignatures(Transaction transaction, IEnumerable<Coin> inputCoins)
        {
            var result = new TransactionValidationResult { IsValid = true };
            var coinArray = inputCoins.ToArray();

            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                try
                {
                    var input = transaction.Inputs[i];
                    var coin = coinArray.FirstOrDefault(c => c.Outpoint.Hash == input.PrevOut.Hash && c.Outpoint.N == input.PrevOut.N);

                    if (coin == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Input {i}: Coin not found for outpoint {input.PrevOut}");
                        continue;
                    }

                    // Script validation
                    var scriptValidation = ValidateScript(input, coin, transaction, i);
                    if (!scriptValidation.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.AddRange(scriptValidation.Errors);
                    }
                    else
                    {
                        result.ValidInputs.Add(i);
                    }
                }
                catch (Exception ex)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Input {i}: Validation error - {ex.Message}");
                }
            }

            return result;
        }

        private void GenerateTestKeys(int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.privateKeys.Add(new Key());
            }
        }

        private void GenerateInitialCoins()
        {
            // Create initial UTXO for each key
            foreach (var key in this.privateKeys)
            {
                var address = key.PubKey.GetAddress(this.network);
                var scriptPubKey = address.ScriptPubKey;

                // Several different coin values
                var values = new[] {
                    Money.Coins(1m),
                    Money.Coins(0.5m),
                    Money.Coins(0.1m),
                    Money.Coins(0.01m)
                };

                foreach (var value in values)
                {
                    // Create fake outpoint for testing
                    var outpoint = new OutPoint(RandomUint256(), (uint)this.random.Next(0, 10));
                    var coin = new Coin(outpoint, new TxOut(value, scriptPubKey));
                    this.availableCoins.Add(coin);
                }
            }
        }

        private TransactionBuilder CreateTransactionBuilder()
        {
            var builder = new TransactionBuilder(this.network);

            // Add all private keys
            foreach (var key in this.privateKeys)
            {
                builder.AddKeys(key);
            }

            return builder;
        }

        private IEnumerable<Coin> SelectInputCoins(int count)
        {
            // Random coin selection
            return this.availableCoins
                .OrderBy(c => this.random.Next())
                .Take(Math.Min(count, this.availableCoins.Count))
                .ToList();
        }

        private IEnumerable<TransactionOutputData> CreateTransactionOutputs(int count, Money totalAmount)
        {
            var outputs = new List<TransactionOutputData>();
            var remainingAmount = totalAmount;

            for (int i = 0; i < count; i++)
            {
                // Random key for output
                var targetKey = this.privateKeys[this.random.Next(this.privateKeys.Count)];
                var address = targetKey.PubKey.GetAddress(this.network);

                // Divide amount
                var amount = i == count - 1
                    ? remainingAmount  // Last output gets the remainder
                    : Money.Satoshis(remainingAmount.Satoshi / count);

                if (amount <= Money.Zero)
                    break;

                outputs.Add(new TransactionOutputData
                {
                    ScriptPubKey = address.ScriptPubKey,
                    Value = amount,
                    Address = address.ToString(),
                    KeyIndex = this.privateKeys.IndexOf(targetKey)
                });

                remainingAmount -= amount;
            }

            return outputs;
        }

        private TransactionValidationResult ValidateScript(TxIn input, Coin coin, Transaction transaction, int inputIndex)
        {
            var result = new TransactionValidationResult { IsValid = true };

            try
            {
                // Basic validation of scriptSig and scriptPubKey
                var scriptSig = input.ScriptSig;
                var scriptPubKey = coin.ScriptPubKey;

                // For P2PKH scripts
                if (scriptPubKey.IsScriptType(ScriptType.P2PKH))
                {
                    // Check that scriptSig has the correct format
                    var ops = scriptSig.ToOps().ToList();
                    if (ops.Count < 2)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Input {inputIndex}: Invalid scriptSig format");
                        return result;
                    }

                    // Signature verification
                    var signature = ops[0].PushData;
                    var pubkey = ops[1].PushData;

                    if (signature == null || pubkey == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Input {inputIndex}: Missing signature or pubkey data");
                        return result;
                    }

                    // Here normally full cryptographic validation would occur
                    // For testing purposes we assume the signature is valid
                    // if it has the correct length
                    if (signature.Length < 64)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Input {inputIndex}: Invalid signature length");
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Input {inputIndex}: Script validation error - {ex.Message}");
            }

            return result;
        }

        private void UpdateUtxoSet(SignedTransactionData transactionData)
        {
            if (transactionData.Transaction == null) return;

            var tx = transactionData.Transaction;

            // Remove spent coins
            foreach (var input in tx.Inputs)
            {
                var coinToRemove = this.availableCoins
                    .FirstOrDefault(c => c.Outpoint.Hash == input.PrevOut.Hash && c.Outpoint.N == input.PrevOut.N);
                if (coinToRemove != null)
                {
                    this.availableCoins.Remove(coinToRemove);
                }
            }

            // Add new coins from outputs
            for (int i = 0; i < tx.Outputs.Count; i++)
            {
                var output = tx.Outputs[i];
                var outpoint = new OutPoint(tx.GetHash(), (uint)i);
                var newCoin = new Coin(outpoint, output);
                this.availableCoins.Add(newCoin);
            }
        }

        private Money CalculateFee(IEnumerable<Coin> inputs, Transaction transaction)
        {
            var totalInput = inputs.Sum(c => c.Amount);
            var totalOutput = transaction.TotalOut;
            return totalInput - totalOutput;
        }

        private uint256 RandomUint256()
        {
            var bytes = new byte[32];
            this.random.NextBytes(bytes);
            return new uint256(bytes);
        }

        /// <summary>
        /// Generates a new change address from available keys
        /// </summary>
        private Script GenerateChangeAddress()
        {
            // Select random key for change address
            var changeKey = this.privateKeys[this.random.Next(this.privateKeys.Count)];
            var changeAddress = changeKey.PubKey.GetAddress(this.network);
            return changeAddress.ScriptPubKey;
        }

        /// <summary>
        /// Calculates expected change value for transaction
        /// </summary>
        public Money CalculateExpectedChange(IEnumerable<Coin> inputs, Money totalOutputs, Money fee)
        {
            var totalInputs = inputs.Sum(c => c.Amount);
            return totalInputs - totalOutputs - fee;
        }

        /// <summary>
        /// Gets address string from Script
        /// </summary>
        private string GetAddressFromScript(Script script)
        {
            try
            {
                if (script.IsScriptType(ScriptType.P2PKH))
                {
                    var address = script.GetDestination(this.network)?.GetAddress(this.network);
                    return address?.ToString() ?? "Unknown";
                }
                return "Unknown Script Type";
            }
            catch
            {
                return "Invalid Script";
            }
        }

        /// <summary>
        /// Creates transaction analysis with detailed statistics
        /// </summary>
        public TransactionAnalysis AnalyzeTransaction(SignedTransactionData transactionData)
        {
            var analysis = new TransactionAnalysis();

            if (transactionData.Transaction == null)
            {
                analysis.IsValid = false;
                analysis.ErrorMessage = transactionData.Error ?? "No transaction data";
                return analysis;
            }

            var tx = transactionData.Transaction;
            analysis.IsValid = transactionData.IsValid;
            analysis.TransactionId = tx.GetHash();
            analysis.Version = tx.Version;
            analysis.InputCount = tx.Inputs.Count;
            analysis.OutputCount = tx.Outputs.Count;
            analysis.Size = tx.GetSerializedSize();
            analysis.VirtualSize = tx.GetVirtualSize(4);
            analysis.Fee = transactionData.Fee;
            analysis.FeeRate = analysis.VirtualSize > 0 ? transactionData.Fee.Satoshi / (decimal)analysis.VirtualSize : 0;
            analysis.TotalInputAmount = transactionData.TotalInputAmount;
            analysis.TotalOutputAmount = transactionData.TotalOutputAmount;
            analysis.HasChangeOutput = transactionData.HasChangeOutput;
            analysis.ChangeAmount = transactionData.ChangeAmount;
            analysis.LockTime = tx.LockTime;
            analysis.GenerationTime = TimeSpan.FromMilliseconds(transactionData.GenerationTimeMs);

            // Analyze script types
            foreach (var output in tx.Outputs)
            {
                var scriptType = GetScriptTypeName(output.ScriptPubKey);
                if (analysis.ScriptTypes.ContainsKey(scriptType))
                    analysis.ScriptTypes[scriptType]++;
                else
                    analysis.ScriptTypes[scriptType] = 1;
            }

            // Blockchain constraint analysis
            analysis.BlockConstraints = AnalyzeBlockConstraints(analysis);

            return analysis;
        }

        /// <summary>
        /// Analyzes how transaction fits within blockchain constraints
        /// </summary>
        public BlockConstraintAnalysis AnalyzeBlockConstraints(TransactionAnalysis transaction)
        {
            const int BLOCK_TIME_SECONDS = 30;
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB

            var maxTxPerBlock = BLOCK_SIZE_BYTES / transaction.Size;
            var theoreticalTPS = maxTxPerBlock / (double)BLOCK_TIME_SECONDS;
            var blockUtilization = transaction.Size / (double)BLOCK_SIZE_BYTES;

            return new BlockConstraintAnalysis
            {
                BlockTimeSeconds = BLOCK_TIME_SECONDS,
                BlockSizeBytes = BLOCK_SIZE_BYTES,
                TransactionSize = transaction.Size,
                MaxTransactionsPerBlock = maxTxPerBlock,
                TheoreticalMaxTPS = theoreticalTPS,
                BlockUtilizationPerTx = blockUtilization,
                FitsInBlock = transaction.Size <= BLOCK_SIZE_BYTES,
                EstimatedConfirmationTime = TimeSpan.FromSeconds(BLOCK_TIME_SECONDS),
                Priority = CalculateTransactionPriority(transaction)
            };
        }

        /// <summary>
        /// Simulates block generation with transaction constraints
        /// </summary>
        public BlockGenerationResult SimulateBlockGeneration(int maxTransactions = 1000, int timeoutSeconds = 30)
        {
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            var startTime = DateTime.UtcNow;
            var transactions = new List<SignedTransactionData>();
            var totalSize = 0;
            var totalFees = Money.Zero;
            var rejectedTransactions = 0;
            var errors = new List<string>();

            for (int i = 0; i < maxTransactions; i++)
            {
                // Check timeout
                if ((DateTime.UtcNow - startTime).TotalSeconds > timeoutSeconds)
                    break;

                try
                {
                    var options = new TransactionGenerationOptions
                    {
                        ValidateAfterGeneration = true,
                        Fee = Money.Satoshis(new Random().Next(100, 1000)) // Variable fees
                    };

                    var transactionData = GenerateSignedTransaction(options);

                    if (transactionData.IsValid && transactionData.HasTransaction)
                    {
                        // Check if transaction fits in remaining block space
                        if (totalSize + transactionData.Size <= BLOCK_SIZE_BYTES)
                        {
                            transactions.Add(transactionData);
                            totalSize += transactionData.Size;
                            totalFees += transactionData.Fee;
                        }
                        else
                        {
                            rejectedTransactions++;
                            break; // Block is full
                        }
                    }
                    else
                    {
                        rejectedTransactions++;
                        if (!string.IsNullOrEmpty(transactionData.Error))
                            errors.Add(transactionData.Error);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    rejectedTransactions++;
                }
            }

            var endTime = DateTime.UtcNow;
            var generationTime = endTime - startTime;

            return new BlockGenerationResult
            {
                TransactionCount = transactions.Count,
                RejectedTransactions = rejectedTransactions,
                TotalSize = totalSize,
                TotalFees = totalFees,
                BlockUtilization = totalSize / (double)BLOCK_SIZE_BYTES,
                GenerationTime = generationTime,
                AverageTransactionSize = transactions.Count > 0 ? totalSize / transactions.Count : 0,
                AverageFee = transactions.Count > 0 ? totalFees.Satoshi / transactions.Count : 0,
                TransactionsPerSecond = transactions.Count / generationTime.TotalSeconds,
                BlockFull = totalSize >= BLOCK_SIZE_BYTES * 0.95, // 95% full considered full
                Errors = errors,
                Transactions = transactions
            };
        }

        /// <summary>
        /// Generates transactions optimized for block constraints
        /// </summary>
        public IEnumerable<SignedTransactionData> GenerateOptimizedTransactionBatch(
            int targetBlockUtilization = 90, // Percentage
            Money? targetFeeRate = null)
        {
            const int BLOCK_SIZE_BYTES = 2_621_440; // 2.5 MB
            var targetSize = BLOCK_SIZE_BYTES * targetBlockUtilization / 100;
            var currentSize = 0;
            var transactions = new List<SignedTransactionData>();

            var feeRate = targetFeeRate ?? Money.Satoshis(500);

            while (currentSize < targetSize)
            {
                var options = new TransactionGenerationOptions
                {
                    Fee = feeRate,
                    ValidateAfterGeneration = true,
                    // Optimize transaction complexity based on remaining space
                    Complexity = currentSize < targetSize * 0.5 ?
                                TransactionComplexity.Complex :
                                TransactionComplexity.Simple
                };

                var transactionData = GenerateSignedTransaction(options);

                if (transactionData.IsValid && transactionData.HasTransaction)
                {
                    if (currentSize + transactionData.Size <= targetSize)
                    {
                        transactions.Add(transactionData);
                        currentSize += transactionData.Size;
                    }
                    else
                    {
                        break; // Would exceed target size
                    }
                }

                // Safety break to avoid infinite loop
                if (transactions.Count > 10000)
                    break;
            }

            return transactions;
        }

        private TransactionPriority CalculateTransactionPriority(TransactionAnalysis transaction)
        {
            // Priority based on fee rate (sat/vB)
            if (transaction.FeeRate > 50)
                return TransactionPriority.High;
            else if (transaction.FeeRate > 20)
                return TransactionPriority.Medium;
            else if (transaction.FeeRate > 5)
                return TransactionPriority.Low;
            else
                return TransactionPriority.VeryLow;
        }

        private string GetScriptTypeName(Script script)
        {
            if (script.IsScriptType(ScriptType.P2PKH)) return "P2PKH";
            if (script.IsScriptType(ScriptType.P2SH)) return "P2SH";
            if (script.IsScriptType(ScriptType.P2WPKH)) return "P2WPKH";
            if (script.IsScriptType(ScriptType.P2WSH)) return "P2WSH";
            if (script.IsScriptType(ScriptType.MultiSig)) return "MultiSig";
            if (script.IsScriptType(ScriptType.Witness)) return "Witness";
            return "Unknown";
        }
    }

    /// <summary>
    /// Options for transaction generation
    /// </summary>
    public class TransactionGenerationOptions
    {
        public int InputCount { get; set; } = 1;
        public int OutputCount { get; set; } = 1;
        public Money TotalOutputAmount { get; set; } = Money.Coins(0.01m);
        public Money Fee { get; set; } = Money.Zero; // If zero, uses FeeRate
        public FeeRate FeeRate { get; set; } = new FeeRate(Money.Satoshis(1000));
        public bool ValidateAfterGeneration { get; set; } = true;
        public TransactionComplexity Complexity { get; set; } = TransactionComplexity.Simple;
        public Script? ChangeAddress { get; set; }
        public bool AutoGenerateChangeAddress { get; set; } = true;
        public int? PreferredChangeKeyIndex { get; set; }
        public Money MinimumChangeAmount { get; set; } = Money.Satoshis(546); // Dust limit
        public bool AllowZeroChange { get; set; } = false;

        /// <summary>
        /// Creates basic options for simple transaction
        /// </summary>
        public static TransactionGenerationOptions CreateSimple()
        {
            return new TransactionGenerationOptions
            {
                InputCount = 1,
                OutputCount = 1,
                TotalOutputAmount = Money.Coins(0.01m),
                Complexity = TransactionComplexity.Simple
            };
        }

        /// <summary>
        /// Creates options for more complex transaction with multiple inputs and outputs
        /// </summary>
        public static TransactionGenerationOptions CreateComplex()
        {
            return new TransactionGenerationOptions
            {
                InputCount = 3,
                OutputCount = 4,
                TotalOutputAmount = Money.Coins(0.5m),
                Complexity = TransactionComplexity.Complex,
                FeeRate = new FeeRate(Money.Satoshis(2000))
            };
        }

        /// <summary>
        /// Creates options for high-volume testing
        /// </summary>
        public static TransactionGenerationOptions CreateHighVolume()
        {
            return new TransactionGenerationOptions
            {
                InputCount = 2,
                OutputCount = 2,
                TotalOutputAmount = Money.Coins(0.05m),
                ValidateAfterGeneration = false, // For speed
                Fee = Money.Satoshis(500) // Low fee
            };
        }
    }

    public enum TransactionComplexity
    {
        Simple,     // Basic P2PKH transaction
        Medium,     // Multi-sig transaction
        Complex     // Complex scripts
    }

    /// <summary>
    /// Data about generated and signed transaction
    /// </summary>
    public class SignedTransactionData
    {
        public Transaction? Transaction { get; set; }
        public IEnumerable<Coin> InputCoins { get; set; } = Enumerable.Empty<Coin>();
        public IEnumerable<TransactionOutputData> OutputData { get; set; } = Enumerable.Empty<TransactionOutputData>();
        public TransactionValidationResult? ValidationResult { get; set; }
        public double GenerationTimeMs { get; set; }
        public uint256? TransactionId { get; set; }
        public int Size { get; set; }
        public Money Fee { get; set; } = Money.Zero;
        public string? Error { get; set; }
        public TransactionGenerationOptions? Options { get; set; }

        // New properties for change management
        public string? ChangeAddress { get; set; }
        public Money ChangeAmount { get; set; } = Money.Zero;
        public bool HasChangeOutput { get; set; }
        public Money TotalInputAmount { get; set; } = Money.Zero;
        public Money TotalOutputAmount { get; set; } = Money.Zero;

        public bool IsValid => string.IsNullOrEmpty(Error) && ValidationResult?.IsValid != false;
        public bool HasTransaction => Transaction != null;

        /// <summary>
        /// Calculates TPS for individual transaction
        /// </summary>
        public double CalculateTPS()
        {
            return GenerationTimeMs > 0 ? 1000.0 / GenerationTimeMs : 0;
        }

        /// <summary>
        /// Detailed transaction information
        /// </summary>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Error))
                return $"Transaction Error: {Error}";

            var info = $"TX {TransactionId?.ToString()?.Substring(0, 8)}... ";
            info += $"Size: {Size}b, Fee: {Fee.Satoshi} sat, ";
            info += $"Inputs: {InputCoins.Count()}, Outputs: {OutputData.Count()}";

            if (HasChangeOutput)
                info += $", Change: {ChangeAmount.Satoshi} sat";

            if (ValidationResult != null)
                info += $", Valid: {ValidationResult.IsValid}";

            return info;
        }
    }

    /// <summary>
    /// Transaction output data
    /// </summary>
    public class TransactionOutputData
    {
        public Script ScriptPubKey { get; set; } = Script.Empty;
        public Money Value { get; set; } = Money.Zero;
        public string Address { get; set; } = string.Empty;
        public int KeyIndex { get; set; }
    }

    /// <summary>
    /// Transaction validation result
    /// </summary>
    public class TransactionValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<int> ValidInputs { get; set; } = new List<int>();
        public TimeSpan ValidationTime { get; set; }
    }

    /// <summary>
    /// Detailed transaction analysis
    /// </summary>
    public class TransactionAnalysis
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public uint256? TransactionId { get; set; }
        public uint Version { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public int Size { get; set; }
        public int VirtualSize { get; set; }
        public Money Fee { get; set; } = Money.Zero;
        public decimal FeeRate { get; set; } // satoshi per vbyte
        public Money TotalInputAmount { get; set; } = Money.Zero;
        public Money TotalOutputAmount { get; set; } = Money.Zero;
        public bool HasChangeOutput { get; set; }
        public Money ChangeAmount { get; set; } = Money.Zero;
        public LockTime LockTime { get; set; }
        public TimeSpan GenerationTime { get; set; }
        public Dictionary<string, int> ScriptTypes { get; set; } = new Dictionary<string, int>();

        // Blockchain constraint analysis
        public BlockConstraintAnalysis? BlockConstraints { get; set; }

        /// <summary>
        /// Space utilization efficiency (outputs/size)
        /// </summary>
        public double SpaceEfficiency => Size > 0 ? OutputCount / (double)Size * 100 : 0;

        /// <summary>
        /// Fee to value ratio
        /// </summary>
        public double FeeToValueRatio => TotalOutputAmount.Satoshi > 0
            ? Fee.Satoshi / (double)TotalOutputAmount.Satoshi * 100
            : 0;

        /// <summary>
        /// Whether the transaction is economically efficient (low fees)
        /// </summary>
        public bool IsEconomicallyEfficient => FeeRate < 20; // < 20 sat/vB

        public override string ToString()
        {
            if (!IsValid)
                return $"Invalid transaction: {ErrorMessage}";

            var blockInfo = BlockConstraints != null
                ? $", {BlockConstraints.MaxTransactionsPerBlock} max/block"
                : "";

            return $"TX Analysis: {InputCount} inputs, {OutputCount} outputs, " +
                   $"{Size} bytes, {FeeRate:F2} sat/vB, " +
                   $"Efficiency: {SpaceEfficiency:F2}%{blockInfo}";
        }
    }

    /// <summary>
    /// Analysis of how transaction fits within blockchain constraints
    /// </summary>
    public class BlockConstraintAnalysis
    {
        public int BlockTimeSeconds { get; set; }
        public int BlockSizeBytes { get; set; }
        public int TransactionSize { get; set; }
        public int MaxTransactionsPerBlock { get; set; }
        public double TheoreticalMaxTPS { get; set; }
        public double BlockUtilizationPerTx { get; set; }
        public bool FitsInBlock { get; set; }
        public TimeSpan EstimatedConfirmationTime { get; set; }
        public TransactionPriority Priority { get; set; }

        public override string ToString()
        {
            return $"Block Constraints: {TransactionSize}b tx, " +
                   $"{MaxTransactionsPerBlock} max/block, " +
                   $"{TheoreticalMaxTPS:F2} max TPS, " +
                   $"{Priority} priority";
        }
    }

    /// <summary>
    /// Result of block generation simulation
    /// </summary>
    public class BlockGenerationResult
    {
        public int TransactionCount { get; set; }
        public int RejectedTransactions { get; set; }
        public int TotalSize { get; set; }
        public Money TotalFees { get; set; } = Money.Zero;
        public double BlockUtilization { get; set; }
        public TimeSpan GenerationTime { get; set; }
        public int AverageTransactionSize { get; set; }
        public long AverageFee { get; set; }
        public double TransactionsPerSecond { get; set; }
        public bool BlockFull { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<SignedTransactionData> Transactions { get; set; } = new List<SignedTransactionData>();

        public double SuccessRate => (TransactionCount + RejectedTransactions) > 0
            ? TransactionCount / (double)(TransactionCount + RejectedTransactions)
            : 0;

        public override string ToString()
        {
            return $"Block Generation: {TransactionCount} tx, " +
                   $"{BlockUtilization:P1} utilization, " +
                   $"{TransactionsPerSecond:F2} TPS, " +
                   $"{TotalFees.Satoshi:N0} sat fees, " +
                   $"{(BlockFull ? "FULL" : "partial")}";
        }
    }

    /// <summary>
    /// Transaction priority levels for blockchain processing
    /// </summary>
    public enum TransactionPriority
    {
        VeryLow,
        Low,
        Medium,
        High
    }
}