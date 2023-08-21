using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.Consensus;
using Blockcore.Features.Consensus.Rules.UtxosetRules;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Consensus;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;

namespace Blockcore.Networks.ZEEV.Rules
{
    /// <inheritdoc />
    public sealed class ZEEVCheckPowUtxosetPowRule : CheckUtxosetRule
    {
        /// <summary>Consensus parameters.</summary>
        private ZEEVConsensus consensus;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        this.consensus = (ZEEVConsensus)this.Parent.Network.Consensus;
    }

    /// <inheritdoc/>
    public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
    {
        Money blockReward = fees + this.GetProofOfWorkReward(height);
        if (block.Transactions[0].TotalOut > blockReward)
        {
            this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
            ConsensusErrors.BadCoinbaseAmount.Throw();
        }
    }

    /// <inheritdoc/>
    public override Money GetProofOfWorkReward(int height)
    {
        if (this.IsPremine(height))
            return this.consensus.PremineReward;

        if (this.consensus.ProofOfWorkReward == 0)
            return 0;

        int halvings = height / this.consensus.SubsidyHalvingInterval;

        // Force block reward to zero when right shift is undefined.
        if (halvings >= 101)
            return 0;

        Money subsidy = this.consensus.ProofOfWorkReward;
        Money subsidityDecrease = this.consensus.SubsidityDecrease;

        subsidy = subsidy - (subsidityDecrease * halvings);

        return subsidy;
    }

    protected override Money GetTransactionFee(UnspentOutputSet view, Transaction tx)
    {
        return view.GetValueIn(tx) - tx.TotalOut;
    }

    /// <inheritdoc />
    protected override bool IsTxFinal(Transaction transaction, RuleContext context)
    {
        if (transaction.IsCoinBase)
            return true;

        ChainedHeader index = context.ValidationContext.ChainedHeaderToValidate;

        UnspentOutputSet view = (context as UtxoRuleContext).UnspentOutputSet;

        var prevheights = new int[transaction.Inputs.Count];
        // Check that transaction is BIP68 final.
        // BIP68 lock checks (as opposed to nLockTime checks) must
        // be in ConnectBlock because they require the UTXO set.
        for (int i = 0; i < transaction.Inputs.Count; i++)
        {
            prevheights[i] = (int)view.AccessCoins(transaction.Inputs[i].PrevOut).Coins.Height;
        }

        return transaction.CheckSequenceLocks(prevheights, index, context.Flags.LockTimeFlags);
    }

    /// <inheritdoc/>
    public override void CheckMaturity(UnspentOutput coins, int spendHeight)
    {
        base.CheckCoinbaseMaturity(coins, spendHeight);
    }

    /// <inheritdoc/>
    public override void UpdateCoinView(RuleContext context, Transaction transaction)
    {
        base.UpdateUTXOSet(context, transaction);
    }

    /// <inheritdoc />
    public override Task RunAsync(RuleContext context)
    {
        return base.RunAsync(context);
    }
}
}