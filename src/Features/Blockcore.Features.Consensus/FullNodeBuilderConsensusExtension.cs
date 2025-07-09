using Blockcore.Base;
using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Blockcore.Consensus;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.Features.Consensus.CoinViews.Coindb;
using Blockcore.Features.Consensus.ProvenBlockHeaders;
using Blockcore.Features.Consensus.Rules;
using Blockcore.Interfaces;
using Blockcore.Utilities.Store;
using Microsoft.Extensions.DependencyInjection;

namespace Blockcore.Features.Consensus
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderConsensusExtension
    {
        public static IFullNodeBuilder UsePowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<PowConsensusFeature>("powconsensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PowConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        fullNodeBuilder.PersistenceProviderManager.RequirePersistence<PowConsensusFeature>(services);

                        services.AddSingleton<ConsensusOptions, ConsensusOptions>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<IConsensusRuleEngine, PowConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                    });
            });

            return fullNodeBuilder;
        }
    }
}