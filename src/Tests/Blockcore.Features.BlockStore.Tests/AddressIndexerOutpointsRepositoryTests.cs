﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Blockcore.Configuration.Logging;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.BlockStore.AddressIndexing;
using Blockcore.NBitcoin;
using LiteDB;
using Xunit;

namespace Blockcore.Features.BlockStore.Tests
{
    public class AddressIndexerOutpointsRepositoryTests
    {
        private readonly AddressIndexerOutpointsRepository repository;

        private readonly Random random = new Random();

        private readonly int maxItems = 10;

        public AddressIndexerOutpointsRepositoryTests()
        {
            ConnectionType connectionType = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ConnectionType.Direct : ConnectionType.Shared;
            var db = new LiteDatabase(new ConnectionString() { Filename = this.RandomString(20) + ".db", Upgrade = true, Connection = connectionType });

            this.repository = new AddressIndexerOutpointsRepository(db, new ExtendedLoggerFactory(), this.maxItems);
        }

        [Fact]
        public void LoadPercentageCalculatedCorrectly()
        {
            for (int i = 0; i < this.maxItems / 2; i++)
                this.repository.AddOutPointData(new OutPointData() { Outpoint = this.RandomString(20) });

            Assert.Equal(50, this.repository.GetLoadPercentage());
        }

        [Fact]
        public void CanAddAndRemoveOutpointData()
        {
            var outPoint = new OutPoint(new uint256(RandomUtils.GetUInt64()), 1);

            var data = new OutPointData() { Outpoint = outPoint.ToString(), Money = 1, ScriptPubKeyBytes = RandomUtils.GetBytes(20) };
            this.repository.AddOutPointData(data);

            // Add more to trigger eviction.
            for (int i = 0; i < this.maxItems * 2; i++)
                this.repository.AddOutPointData(new OutPointData() { Outpoint = this.RandomString(20) });

            Assert.True(this.repository.TryGetOutPointData(outPoint, out OutPointData dataOut));
            Assert.True(data.ScriptPubKeyBytes.SequenceEqual(dataOut.ScriptPubKeyBytes));
        }

        [Fact]
        public void CanRewind()
        {
            var rewindDataBlockHash = new uint256(RandomUtils.GetUInt64());

            var outPoint = new OutPoint(new uint256(RandomUtils.GetUInt64()), 1);
            var data = new OutPointData() { Outpoint = outPoint.ToString(), Money = 1, ScriptPubKeyBytes = RandomUtils.GetBytes(20) };

            var rewindData = new AddressIndexerRewindData()
            {
                BlockHash = rewindDataBlockHash.ToString(),
                BlockHeight = 100,
                SpentOutputs = new List<OutPointData>() { data }
            };

            this.repository.RecordRewindData(rewindData);

            Assert.False(this.repository.TryGetOutPointData(outPoint, out OutPointData dataOut));

            this.repository.RewindDataAboveHeight(rewindData.BlockHeight - 1);

            Assert.True(this.repository.TryGetOutPointData(outPoint, out dataOut));

            // Now record and purge rewind data.
            this.repository.RecordRewindData(rewindData);

            this.repository.RemoveOutPointData(outPoint);
            Assert.False(this.repository.TryGetOutPointData(outPoint, out dataOut));

            this.repository.PurgeOldRewindData(rewindData.BlockHeight + 1);

            Assert.False(this.repository.TryGetOutPointData(outPoint, out dataOut));
        }

        private string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[this.random.Next(s.Length)]).ToArray());
        }
    }
}