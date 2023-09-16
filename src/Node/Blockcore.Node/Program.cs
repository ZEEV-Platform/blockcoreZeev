using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.Miner;
using Blockcore.Features.Miner.Api.Controllers;
using Blockcore.Features.Miner.Interfaces;
using Blockcore.Features.RPC.Exceptions;
using Blockcore.Features.RPC;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Features.Wallet.Types;
using Blockcore.Utilities;
using Microsoft.AspNetCore.Mvc;
using Blockcore.NBitcoin;

namespace Blockcore.Node
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                string chain = args
                   .DefaultIfEmpty("--chain=BTC")
                   .Where(arg => arg.StartsWith("--chain", ignoreCase: true, CultureInfo.InvariantCulture))
                   .Select(arg => arg.Replace("--chain=", string.Empty, ignoreCase: true, CultureInfo.InvariantCulture))
                   .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(chain))
                {
                    chain = "BTC";
                }

                NodeSettings nodeSettings = NetworkSelector.Create(chain, args);
                IFullNodeBuilder nodeBuilder = NodeBuilder.Create(chain, nodeSettings);

                IFullNode node = nodeBuilder.Build();
     
                var f = new Target(new uint256("0000000000ffff00000000000000000000000000000000000000000000000000")).ToBigInteger();
                var b = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")).ToBigInteger();
                var a = new Target(new uint256("00000000ffff0000000000000000000000000000000000000000000000000000")).ToBigInteger();
                var s = new Target(new uint256("7fffff0000000000000000000000000000000000000000000000000000000000")).ToBigInteger();

                Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith((t) => { TestFee(node); }).GetAwaiter();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex);
            }
        }

        public static void TestFee(IFullNode fullNode)
        {
            var mining = fullNode.NodeService<IPowMining>();
            var wallet = fullNode.NodeService<IWalletManager>();

            try
            {
                string walletName = wallet.GetWalletsNames().FirstOrDefault();

                if (string.IsNullOrEmpty(walletName))
                {
                    var password = "testtest";

                    wallet.CreateWallet(password, "default", "resource");
                }

                walletName = wallet.GetWalletsNames().FirstOrDefault();
                IHdAccount account = wallet.GetAccounts(walletName).FirstOrDefault();

                var accountReference = new WalletAccountReference(walletName, account.Name);

                HdAddress address = wallet.GetUnusedAddress(accountReference);
                var addresses = wallet.GetUnusedAddresses(accountReference, 10);
                var addresses2 = wallet.GetUnusedChangeAddress(accountReference);

                foreach (var item in addresses)
                {
                    Console.WriteLine(item.Address);
                }

                // var res = mining.GenerateBlocks(new ReserveScript(address.Pubkey), 100, int.MaxValue);

            }
            catch (Exception e)
            {
                //exist then nothing
            }
        }
    }
}
