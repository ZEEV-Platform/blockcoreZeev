using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using Blockcore.Configuration;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.Wallet.Database;
using Blockcore.Features.Wallet.Exceptions;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Blockcore.Utilities;
using Blockcore.Utilities.JsonConverters;
using Dapper;
using DBreeze.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace Blockcore.Features.Wallet.Database
{
    public class WalletStore : IWalletStore, IDisposable
    {
        private const int WalletVersion = 1;

        /// <summary>
        /// A connection used only when the data store is in memory only.
        /// SQLite in-memory mode will keep the db as long as there is one connection open.
        /// https://github.com/dotnet/docs/blob/master/samples/snippets/standard/data/sqlite/InMemorySample/Program.cs
        /// /// </summary>
        private readonly SqliteConnection inmemorySqliteConnection;
        private readonly Network network;

        private readonly string dbPath;
        private readonly string dbConnection;

        public WalletData WalletData { get; private set; }
        private SqliteConnection sqliteConnection;

        public WalletStore(Network network, DataFolder dataFolder, Types.Wallet wallet)
        {
            this.dbPath = Path.Combine(dataFolder.WalletFolderPath, $"{wallet.Name}.db");
            this.dbConnection = "Data Source=" + this.dbPath;

            if (!Directory.Exists(dataFolder.WalletFolderPath))
            {
                Directory.CreateDirectory(dataFolder.WalletFolderPath);
            }

            if (!File.Exists(this.dbPath))
            {
                this.CreateDatabase();
            }

            this.network = network;

            this.Init(wallet);
        }

        protected SqliteConnection GetDbConnection()
        {
            if (this.sqliteConnection != null)
            {
                return this.sqliteConnection;
            }

            this.sqliteConnection = new SqliteConnection(this.dbConnection);
            this.sqliteConnection.Open();

            using (var command = this.sqliteConnection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode = WAL;";
                command.ExecuteNonQuery();

                //128 MB
                command.CommandText = "PRAGMA cache_size = -131072;";
                command.ExecuteNonQuery();

                //NORMAL WAL mode
                command.CommandText = "PRAGMA synchronous = NORMAL;";
                command.ExecuteNonQuery();
            }

            return this.sqliteConnection;
        }

        private void Init(Types.Wallet wallet)
        {
            SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
            SqlMapper.AddTypeHandler(new HashHeightPairHandler());
            SqlMapper.AddTypeHandler(new CollectionOfuint256Handler());
            SqlMapper.AddTypeHandler(new Uint256Handler());
            SqlMapper.AddTypeHandler(new MoneyHandler());
            SqlMapper.AddTypeHandler(new OutPointHandler());
            SqlMapper.AddTypeHandler(new ScriptHandler());
            SqlMapper.AddTypeHandler(new CollectionOfPaymentDetailsHandler());
            SqlMapper.AddTypeHandler(new PartialMerkleTreeHandler());

            this.WalletData = this.GetData();

            if (this.WalletData != null)
            {
                if (this.WalletData.EncryptedSeed != wallet.EncryptedSeed)
                {
                    throw new WalletException("Invalid Wallet seed");
                }
            }
            else
            {
                this.SetData(new WalletData
                {
                    Key = "Key",
                    EncryptedSeed = wallet.EncryptedSeed,
                    WalletName = wallet.Name,
                    WalletTip = new HashHeightPair(this.network.GenesisHash, 0),
                    WalletVersion = WalletVersion
                });
            }
        }

        public WalletData GetData()
        {
            if (this.WalletData == null)
            {
                var conn = this.GetDbConnection();
                this.WalletData = conn.QueryFirstOrDefault<WalletData>("SELECT *, Id AS Key FROM WalletData WHERE Id = 'Key'");
            }

            return this.WalletData;
        }

        public void SetData(WalletData data)
        {
            var sql = @$"INSERT INTO WalletData
                      (Id, EncryptedSeed, WalletName, WalletTip, WalletVersion, BlockLocator)
                      VALUES (@Key, @EncryptedSeed, @WalletName, @WalletTip, @WalletVersion, @BlockLocator)
                      ON CONFLICT(Id) DO UPDATE SET
                      EncryptedSeed = @EncryptedSeed, WalletTip = @WalletTip, BlockLocator = @BlockLocator;";

            var conn = this.GetDbConnection();
            conn.Execute(sql, data);

            this.WalletData = data;
        }

        public void InsertOrUpdate(TransactionOutputData item)
        {
            TransactionData insert = this.Convert(item);

            var sql = @$"INSERT INTO TransactionData
                      (OutPoint, OutPointHash, Address, AddressHash, Id, Amount, IndexInTransaction, BlockHeight, BlockHash, BlockIndex, CreationTime, ScriptPubKey, IsPropagated, IsCoinBase, IsCoinStake, IsColdCoinStake, AccountIndex, MerkleProof, Hex, SpendingDetailsTransactionId, SpendingDetailsBlockHeight, SpendingDetailsBlockIndex, SpendingDetailsIsCoinStake, SpendingDetailsCreationTime, SpendingDetailsPayments, SpendingDetailsHex)
                      VALUES (@OutPoint, @OutPointHash, @Address, @AddressHash, @Id, @Amount, @IndexInTransaction, @BlockHeight, @BlockHash, @BlockIndex, @CreationTime, @ScriptPubKey, @IsPropagated, @IsCoinBase, @IsCoinStake, @IsColdCoinStake, @AccountIndex, @MerkleProof, @Hex, @SpendingDetailsTransactionId, @SpendingDetailsBlockHeight, @SpendingDetailsBlockIndex, @SpendingDetailsIsCoinStake, @SpendingDetailsCreationTime, @SpendingDetailsPayments, @SpendingDetailsHex)
                      ON CONFLICT(OutPointHash) DO UPDATE SET
                      IndexInTransaction = @IndexInTransaction, BlockHeight = @BlockHeight, BlockHash = @BlockHash, BlockIndex = @BlockIndex, CreationTime = @CreationTime, IsPropagated = @IsPropagated, IsColdCoinStake = @IsColdCoinStake, AccountIndex = @AccountIndex, MerkleProof = @MerkleProof, Hex = @Hex, SpendingDetailsTransactionId = @SpendingDetailsTransactionId, SpendingDetailsBlockHeight = @SpendingDetailsBlockHeight, SpendingDetailsBlockIndex = @SpendingDetailsBlockIndex, SpendingDetailsIsCoinStake = @SpendingDetailsIsCoinStake, SpendingDetailsCreationTime = @SpendingDetailsCreationTime, SpendingDetailsPayments = @SpendingDetailsPayments, SpendingDetailsHex = @SpendingDetailsHex;";

            var conn = this.GetDbConnection();
            conn.Execute(sql, insert);
        }

        public int CountForAddress(string address)
        {
            var conn = this.GetDbConnection();
            var addressHash = this.GetMurmurHash(address);

            var count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM TransactionData WHERE AddressHash = @addressHash", new { addressHash });

            return count;
        }

        public Dictionary<string, int> CountForAddresses(List<string> addresses)
        {
            if (addresses == null || !addresses.Any())
                return new Dictionary<string, int>();

            var conn = this.GetDbConnection();
            var addressHashes = string.Join(", ", addresses.Select(n => this.GetMurmurHash(n)).ToArray());

            var resultSQL = conn.Query("SELECT Address, COUNT(*) as AddressCount FROM TransactionData WHERE AddressHash IN (" + addressHashes + ") GROUP BY Address");

            var result = new Dictionary<string, int>();
            foreach (dynamic item in resultSQL)
            {
                result.Add(item.Address, (int)item.AddressCount);
            }

            return result;
        }

        public IEnumerable<WalletHistoryData> GetAccountHistory(int accountIndex, bool excludeColdStake, int skip = 0, int take = 100)
        {
            // The result of this method is not guaranteed to be the length
            //  of the 'take' param. In case some of the inputs we have are
            // in the same trx they will be grouped in to a single entry.
            var conn = this.GetDbConnection();

            var sql = @$"SELECT * FROM TransactionData
                      WHERE AccountIndex == @accountIndex
                      AND SpendingDetailsTransactionId IS NOT NULL
                      {(excludeColdStake ? "AND (IsColdCoinStake = false OR IsColdCoinStake is null) " : "")}
                      ORDER BY SpendingDetailsCreationTime DESC
                      LIMIT @take OFFSET @skip ";

            var historySpentResult = conn.Query<TransactionData>(sql, new { accountIndex, skip, take }).ToList();
            var historySpent = historySpentResult.Select(this.Convert);

            sql = @$"SELECT * FROM TransactionData
                  WHERE AccountIndex == @accountIndex
                  {(excludeColdStake ? "AND (IsColdCoinStake = false OR IsColdCoinStake is null) " : "")}
                  ORDER BY CreationTime DESC
                  LIMIT @take OFFSET @skip";

            var historyUnspentResult = conn.Query<TransactionData>(sql, new { accountIndex, skip, take }).ToList();

            var historyUnspent = historyUnspentResult.Select(this.Convert);

            var items = new List<WalletHistoryData>();

            items.AddRange(historySpent
                .GroupBy(g => g.SpendingDetails.TransactionId)
                       .Select(s =>
                       {
                           var x = s.First();

                           return new WalletHistoryData
                           {
                               IsSent = true,
                               SentTo = x.SpendingDetails.TransactionId,
                               IsCoinStake = x.SpendingDetails.IsCoinStake,
                               CreationTime = x.SpendingDetails.CreationTime,
                               BlockHeight = x.SpendingDetails.BlockHeight,
                               BlockIndex = x.SpendingDetails.BlockIndex,
                               SentPayments = x.SpendingDetails.Payments?.Select(p => new WalletHistoryPaymentData
                               {
                                   Amount = p.Amount,
                                   PayToSelf = p.PayToSelf,
                                   DestinationAddress = p.DestinationAddress
                               }).ToList(),

                               // when spent the amount represents the
                               // input that was spent not the output
                               Amount = x.Amount
                           };
                       }));

            items.AddRange(historyUnspent
                .GroupBy(g => g.Id)
                .Select(s =>
                {
                    var x = s.First();

                    var ret = new WalletHistoryData
                    {
                        IsSent = false,
                        OutPoint = x.OutPoint,
                        BlockHeight = x.BlockHeight,
                        BlockIndex = x.BlockIndex,
                        IsCoinStake = x.IsCoinStake,
                        CreationTime = x.CreationTime,
                        ScriptPubKey = x.ScriptPubKey,
                        Address = x.Address,
                        Amount = x.Amount,
                        IsCoinBase = x.IsCoinBase,
                        IsColdCoinStake = x.IsColdCoinStake,
                    };

                    if (s.Count() > 1)
                    {
                        ret.Amount = s.Sum(b => b.Amount);
                        ret.ReceivedOutputs = s.Select(b => new WalletHistoryData
                        {
                            IsSent = false,
                            OutPoint = b.OutPoint,
                            BlockHeight = b.BlockHeight,
                            BlockIndex = b.BlockIndex,
                            IsCoinStake = b.IsCoinStake,
                            CreationTime = b.CreationTime,
                            ScriptPubKey = b.ScriptPubKey,
                            Address = b.Address,
                            Amount = b.Amount,
                            IsCoinBase = b.IsCoinBase,
                            IsColdCoinStake = b.IsColdCoinStake,
                        }).ToList();
                    }

                    return ret;
                }));

            return items.OrderByDescending(x => x.CreationTime).ThenBy(x => x.BlockIndex);
        }

        public IEnumerable<TransactionOutputData> GetForAddress(string address)
        {
            var conn = this.GetDbConnection();
            var addressHash = this.GetMurmurHash(address);

            var trxs = conn.Query<TransactionData>(
                "SELECT * FROM TransactionData " +
                "WHERE AddressHash = @addressHash",
                new { addressHash });

            return trxs.Select(this.Convert);
        }

        public IEnumerable<TransactionOutputData> GetForTransaction(string txId)
        {
            var conn = this.GetDbConnection();
            var trxs = conn.Query<TransactionData>(
                "SELECT * FROM TransactionData " +
                "WHERE Id = @txId OR SpendingDetailsTransactionId = @txId",
                new { txId });

            return trxs.Select(this.Convert);
        }

        public IEnumerable<TransactionOutputData> GetUnspentForAddress(string address)
        {
            var conn = this.GetDbConnection();
            var addressHash = this.GetMurmurHash(address);

            var trxs = conn.Query<TransactionData>(
                "SELECT * FROM TransactionData " +
                "WHERE AddressHash = @addressHash " +
                "AND SpendingDetailsTransactionId IS NULL",
                new { addressHash });

            return trxs.Select(this.Convert);
        }

        public WalletBalanceResult GetBalanceForAddress(string address, bool excludeColdStake)
        {
            string excludeColdStakeSql = excludeColdStake && this.network.Consensus.IsProofOfStake ? "AND (IsColdCoinStake = false OR IsColdCoinStake IS NULL) " : string.Empty;

            var addressHash = this.GetMurmurHash(address);

            var sql = @$"SELECT
                        BlockHeight as Confirmed,
                        SUM(Amount) as Total
                        FROM TransactionData
                        WHERE SpendingDetailsTransactionId IS NULL AND AddressHash = @addressHash
                        {excludeColdStakeSql}
                        GROUP BY BlockHeight IS NOT NULL";

            var conn = this.GetDbConnection();
            var result = conn.Query(sql, new { address });

            var walletBalanceResult = new WalletBalanceResult();

            foreach (dynamic item in result)
            {
                if (item.Confirmed == null) walletBalanceResult.AmountUnconfirmed = (long)item.Total;
                else walletBalanceResult.AmountConfirmed = (long)item.Total;
            }

            return walletBalanceResult;
        }

        public WalletBalanceResult GetBalanceForAccount(int accountIndex, bool excludeColdStake)
        {
            string excludeColdStakeSql = excludeColdStake && this.network.Consensus.IsProofOfStake ? "AND (IsColdCoinStake = false OR IsColdCoinStake IS NULL) " : string.Empty;

            var sql = @$"SELECT
                      BlockHeight as Confirmed,
                      SUM(Amount) as Total
                      FROM TransactionData
                      WHERE SpendingDetailsTransactionId IS NULL AND AccountIndex = @accountIndex
                      {excludeColdStakeSql}
                      GROUP BY BlockHeight IS NOT NULL";

            var conn = this.GetDbConnection();
            var result = conn.Query(sql, new { accountIndex });

            var walletBalanceResult = new WalletBalanceResult();

            foreach (dynamic item in result)
            {
                if (item.Confirmed == null) walletBalanceResult.AmountUnconfirmed = (long)item.Total;
                else walletBalanceResult.AmountConfirmed = (long)item.Total;
            }

            return walletBalanceResult;
        }

        public TransactionOutputData GetForOutput(OutPoint outPoint)
        {
            TransactionData trx = null;
            var outPointHash = this.GetMurmurHash(outPoint.Hash.ToString());

            var conn = this.GetDbConnection();
            trx = conn.QueryFirstOrDefault<TransactionData>("SELECT * FROM TransactionData WHERE OutPointHash = @outPointHash", new { outPointHash });

            if (trx == null)
            {
                return null;
            }

            TransactionOutputData ret = this.Convert(trx);

            return ret;
        }

        public bool Remove(OutPoint outPoint)
        {
            var conn = this.GetDbConnection();
            var outPointHash = this.GetMurmurHash(outPoint.Hash.ToString());

            var ret = conn.ExecuteScalar<int>("DELETE FROM TransactionData WHERE OutPointHash = @outPointHash", new { outPointHash });
            return ret > 0;
        }

        private void UpgradeDatabase(int oldVersion)
        {
            // Here can come code to upgrade the db from old to current version.
        }

        private long GetMurmurHash(string text)
        {
            const ulong m = 0xc6a4a7935bd1e995UL;
            const int r = 47;

            byte[] data = Encoding.UTF8.GetBytes(text);
            ulong h = 0xc70f6907UL ^ ((ulong)data.Length * m);

            int length8 = data.Length / 8;

            for (int i = 0; i < length8; i++)
            {
                int i8 = i * 8;
                ulong k = BitConverter.ToUInt64(data, i8);

                k *= m;
                k ^= k >> r;
                k *= m;

                h ^= k;
                h *= m;
            }

            return (long)h;
        }

        private void CreateDatabase()
        {
            var conn = GetDbConnection();

            conn.Execute(
               @$"CREATE TABLE WalletData(
               Id            VARCHAR(3) NOT NULL PRIMARY KEY,
               EncryptedSeed VARCHAR(500) NULL,
               WalletName    VARCHAR(100) NOT NULL,
               WalletTip     VARCHAR(75) NOT NULL,
               WalletVersion INTEGER NOT NULL,
               BlockLocator  TEXT NULL)");

            conn.Execute(
                @$"CREATE TABLE TransactionData(
                OutPoint                                           VARCHAR(66) NOT NULL,
                OutPointHash                                       INTEGER NOT NULL PRIMARY KEY,
                Address                                            VARCHAR(34) NOT NULL,
                AddressHash                                        INTEGER NOT NULL,
                Id                                                 VARCHAR(64) NOT NULL,
                Amount                                             INTEGER  NOT NULL,
                IndexInTransaction                                 INTEGER  NOT NULL,
                BlockHeight                                        INTEGER  NULL,
                BlockHash                                          VARCHAR(64) NULL,
                BlockIndex                                         INTEGER NULL,
                CreationTime                                       INTEGER  NOT NULL,
                ScriptPubKey                                       VARCHAR(100) NOT NULL,
                IsPropagated                                       INTEGER  NULL,
                IsCoinBase                                         INTEGER  NULL,
                IsCoinStake                                        INTEGER  NULL,
                IsColdCoinStake                                    INTEGER  NULL,
                AccountIndex                                       INTEGER  NOT NULL,
                MerkleProof                                        TEXT NULL,
                Hex                                                TEXT NULL,
                SpendingDetailsTransactionId                       VARCHAR(64) NULL,
                SpendingDetailsBlockHeight                         INTEGER  NULL,
                SpendingDetailsBlockIndex                          INTEGER  NULL,
                SpendingDetailsIsCoinStake                         INTEGER  NULL,
                SpendingDetailsCreationTime                        INTEGER  NULL,
                SpendingDetailsPayments                            TEXT NULL,
                SpendingDetailsHex                                 TEXT NULL)");

            conn.Execute("CREATE INDEX 'address_index' ON 'TransactionData' ('Address')");
            conn.Execute("CREATE INDEX 'address_index_hash' ON 'TransactionData' ('AddressHash')");
            conn.Execute("CREATE INDEX 'blockheight_index' ON 'TransactionData' ('BlockHeight')");
            conn.Execute("CREATE UNIQUE INDEX 'outpoint_index' ON 'TransactionData' ('OutPoint')");
            conn.Execute("CREATE UNIQUE INDEX 'outpoint_index_hash' ON 'TransactionData' ('OutPointHash')");
            conn.Execute("CREATE UNIQUE INDEX 'key_index' ON 'WalletData' ('Id')");
        }

        public void Dispose()
        {
            this.inmemorySqliteConnection?.Dispose();

            this.sqliteConnection.Close();
            this.sqliteConnection?.Dispose();
        }

        private TransactionData Convert(TransactionOutputData source)
        {
            var target = new TransactionData
            {
                OutPoint = source.OutPoint,
                OutPointHash = this.GetMurmurHash(source.OutPoint.Hash.ToString()),
                Address = source.Address,
                AddressHash = this.GetMurmurHash(source.Address),
                Id = source.Id ?? source.OutPoint.Hash,
                Amount = source.Amount ?? 0,
                IndexInTransaction = source.Index,
                BlockHeight = source.BlockHeight,
                BlockHash = source.BlockHash,
                BlockIndex = source.BlockIndex,
                CreationTime = source.CreationTime,
                ScriptPubKey = source.ScriptPubKey ?? Script.Empty,
                IsPropagated = source.IsPropagated,
                IsCoinBase = source.IsCoinBase,
                IsCoinStake = source.IsCoinStake,
                IsColdCoinStake = source.IsColdCoinStake,
                AccountIndex = source.AccountIndex,
                MerkleProof = source.MerkleProof,
                Hex = source.Hex,
                SpendingDetailsTransactionId = source.SpendingDetails?.TransactionId,
                SpendingDetailsBlockHeight = source.SpendingDetails?.BlockHeight,
                SpendingDetailsBlockIndex = source.SpendingDetails?.BlockIndex,
                SpendingDetailsIsCoinStake = source.SpendingDetails?.IsCoinStake,
                SpendingDetailsCreationTime = source.SpendingDetails?.CreationTime,
                SpendingDetailsPayments = source.SpendingDetails?.Payments,
                SpendingDetailsHex = source.SpendingDetails?.Hex
            };

            return target;
        }

        private TransactionOutputData Convert(TransactionData source)
        {
            var target = new TransactionOutputData
            {
                OutPoint = source.OutPoint,
                Address = source.Address,
                Id = source.Id,
                Amount = source.Amount,
                Index = source.IndexInTransaction,
                BlockHeight = source.BlockHeight,
                BlockIndex = source.BlockIndex,
                BlockHash = source.BlockHash,
                CreationTime = source.CreationTime,
                ScriptPubKey = source.ScriptPubKey,
                IsPropagated = source.IsPropagated,
                IsCoinBase = source.IsCoinBase,
                IsCoinStake = source.IsCoinStake,
                IsColdCoinStake = source.IsColdCoinStake,
                AccountIndex = source.AccountIndex,
                MerkleProof = source.MerkleProof,
                Hex = source.Hex
            };

            if (source.SpendingDetailsTransactionId != null)
            {
                target.SpendingDetails = new SpendingDetails
                {
                    TransactionId = source.SpendingDetailsTransactionId,
                    BlockHeight = source.SpendingDetailsBlockHeight,
                    BlockIndex = source.SpendingDetailsBlockIndex,
                    IsCoinStake = source.SpendingDetailsIsCoinStake,
                    CreationTime = source.SpendingDetailsCreationTime.Value,
                    Payments = source.SpendingDetailsPayments,
                    Hex = source.SpendingDetailsHex
                };
            }

            return target;
        }
    }

    internal abstract class SqliteTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        // Parameters are converted by Microsoft.Data.Sqlite
        public override void SetValue(IDbDataParameter parameter, T value)
            => parameter.Value = value;
    }

    internal class DateTimeOffsetHandler : SqliteTypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value)
            => DateTimeOffset.Parse((string)value);
    }

    internal class ScriptHandler : SqliteTypeHandler<Script>
    {
        public override Script Parse(object value)
        {
            return Script.FromBytesUnsafe(Encoders.Hex.DecodeData((string)value));
        }

        public override void SetValue(IDbDataParameter parameter, Script value)
        {
            parameter.Value = Encoders.Hex.EncodeData(value.ToBytes(false));
        }
    }

    internal class MoneyHandler : SqliteTypeHandler<Money>
    {
        public override Money Parse(object value)
        {
            return Money.Satoshis((long)value);
        }

        public override void SetValue(IDbDataParameter parameter, Money value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value.Satoshi;
        }
    }

    internal class Uint256Handler : SqliteTypeHandler<uint256>
    {
        public override uint256 Parse(object value)
        {
            return uint256.Parse((string)value);
        }

        public override void SetValue(IDbDataParameter parameter, uint256 value)
        {
            parameter.Value = value.ToString();
        }
    }

    internal class OutPointHandler : SqliteTypeHandler<OutPoint>
    {
        public override OutPoint Parse(object value)
        {
            return OutPoint.Parse((string)value);
        }

        public override void SetValue(IDbDataParameter parameter, OutPoint value)
        {
            parameter.Value = value.ToString();
        }
    }

    internal class HashHeightPairHandler : SqliteTypeHandler<HashHeightPair>
    {
        public override HashHeightPair Parse(object value)
        {
            return HashHeightPair.Parse((string)value);
        }

        public override void SetValue(IDbDataParameter parameter, HashHeightPair value)
        {
            parameter.Value = value.ToString();
        }
    }

    internal class PartialMerkleTreeHandler : SqliteTypeHandler<PartialMerkleTree>
    {
        public override PartialMerkleTree Parse(object value)
        {
            if (value == null)
            {
                return null;
            }

            var ret = new PartialMerkleTree();
            var bytes = Encoders.Hex.DecodeData((string)value);
            ret.ReadWrite(bytes);

            return ret;
        }

        public override void SetValue(IDbDataParameter parameter, PartialMerkleTree value)
        {
            string values = string.Empty;

            if (value != null)
            {
                values = Encoders.Hex.EncodeData(value.ToBytes());
            }

            parameter.Value = values;
        }
    }

    internal class CollectionOfuint256Handler : SqliteTypeHandler<ICollection<uint256>>
    {
        private static readonly JsonSerializerSettings Converters = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new UInt256JsonConverter() }
        };

        public override ICollection<uint256> Parse(object value)
        {
            if (value == null)
            {
                return null;
            }

            var res = JsonConvert.DeserializeObject<ICollection<uint256>>((string)value, Converters);

            return res;
        }

        public override void SetValue(IDbDataParameter parameter, ICollection<uint256> value)
        {
            string values = string.Empty;

            if (value != null)
            {
                values = JsonConvert.SerializeObject(value, Converters);
            }

            parameter.Value = values;
        }
    }

    internal class CollectionOfPaymentDetailsHandler : SqliteTypeHandler<ICollection<PaymentDetails>>
    {
        private static readonly JsonSerializerSettings Converters = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new MoneyJsonConverter(), new ScriptJsonConverter() }
        };

        public override ICollection<PaymentDetails> Parse(object value)
        {
            if (value == null)
            {
                return null;
            }

            var res = JsonConvert.DeserializeObject<ICollection<PaymentDetails>>((string)value, Converters);

            return res;
        }

        public override void SetValue(IDbDataParameter parameter, ICollection<PaymentDetails> value)
        {
            string values = string.Empty;

            if (value != null)
            {
                values = JsonConvert.SerializeObject(value, Converters);
            }

            parameter.Value = values;
        }
    }
}