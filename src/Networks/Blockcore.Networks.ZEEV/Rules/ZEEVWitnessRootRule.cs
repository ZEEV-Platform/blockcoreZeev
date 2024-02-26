using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Networks.ZEEV.Crypto.Blake2b;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using Polly;

namespace Blockcore.Networks.ZEEV.Rules
{
    /// <summary>
    /// This rule will validate that the calculated merkle tree matches the merkle root in the header.
    /// </summary>
    /// <remarks>
    /// Transactions in a block are hashed together using SHA256 in to a merkle tree,
    /// the root of that tree is included in the block header.
    /// </remarks>
    /// <remarks>
    /// Check for merkle tree malleability (CVE-2012-2459): repeating sequences
    /// of transactions in a block without affecting the merkle root of a block,
    /// while still invalidating it.
    /// Validation cannot be skipped for this rule, someone might have been able to create a mutated
    /// block (block with a duplicate transaction) with a valid hash, but we don't want to accept these
    /// kind of blocks.
    /// <seealso cref="https://bitcointalk.org/index.php?topic=102395.0"/>
    /// </remarks>
    public class ZEEVWitnessRootRule : PartialValidationConsensusRule //IntegrityValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadMerkleRoot">The block merkle root is different from the computed merkle root.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicate">One of the leaf nodes of the merkle tree has a duplicate hash within the subtree.</exception>
        public override void Run(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            uint256 hashWitnessRoot2 = BlockWitnessRoot(block, out bool mutated);
            if (((ZEEVBlockHeader)block.Header).HashWitnessRoot != hashWitnessRoot2)
            {
                this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
                ConsensusErrors.BadMerkleRoot.Throw();
            }

            if (mutated)
            {
                this.Logger.LogTrace("(-)[BAD_TX_DUP]");
                ConsensusErrors.BadTransactionDuplicate.Throw();
            }
        }

        /// <summary>
        /// Calculates merkle root for block's transactions.
        /// </summary>
        /// <param name="block">Block which transactions are used for calculation.</param>
        /// <param name="mutated"><c>true</c> if block contains repeating sequences of transactions without affecting the merkle root of a block. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public static uint256 BlockWitnessRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>(block.Transactions.Count);
            foreach (ZEEVTransaction tx in block.Transactions)
            {
                if (tx.IsCoinBase)
                {
                    leaves.Add(new uint256());
                }
                else
                {
                    var hash = new uint256(tx.GetWitHash().ToBytes());
                    leaves.Add(hash);
                }
            }

            return ZEEVBlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            uint256 hashWitnessRoot2 = BlockWitnessRoot(block, out bool mutated);
            if (((ZEEVBlockHeader)block.Header).HashWitnessRoot != hashWitnessRoot2)
            {
                this.Logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
                ConsensusErrors.BadMerkleRoot.Throw();
            }

            if (mutated)
            {
                this.Logger.LogTrace("(-)[BAD_TX_DUP]");
                ConsensusErrors.BadTransactionDuplicate.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
