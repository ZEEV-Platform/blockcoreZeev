using System;
using System.Collections.Generic;
using Blockcore.Features.Wallet.Database;
using Blockcore.Features.Wallet.Types;
using Blockcore.Networks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;

namespace Blockcore.Features.Wallet.Interfaces
{
    public interface IAccountRoot
    {
        ICollection<IHdAccount> Accounts { get; set; }
        int CoinType { get; set; }
        uint256 LastBlockSyncedHash { get; set; }
        int? LastBlockSyncedHeight { get; set; }

        IHdAccount AddNewAccount(ExtPubKey accountExtPubKey, int accountIndex, Network network, DateTimeOffset accountCreationTime, int purpose);
        IHdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime, int purpose, int? accountIndex = null, string accountName = null);
        IHdAccount CreateAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime, int purpose, int newAccountIndex, string newAccountName = null);
        IHdAccount GetAccountByName(string accountName);
        IHdAccount GetFirstUnusedAccount(IWalletStore walletStore);
        IHdAccount GetAccountByIndex(int index);
    }
}