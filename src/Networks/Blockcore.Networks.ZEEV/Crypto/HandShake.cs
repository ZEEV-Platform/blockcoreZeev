﻿using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.Networks.ZEEV.Crypto.Blake2b;
using Blockcore.Networks.ZEEV.Crypto.SHA3;
using static HashLib.HashFactory.Crypto;

namespace Blockcore.Networks.ZEEV.Crypto
{
    public sealed class HandShake
    {

        private readonly object hashLock;

        private static readonly Lazy<HandShake> SingletonInstance = new Lazy<HandShake>(LazyThreadSafetyMode.PublicationOnly);

        public HandShake()
        {
            this.hashLock = new object();
        }

        /// <summary>
        /// using the instance method is not thread safe.
        /// to calling the hashing method in a multi threaded environment use the create() method
        /// </summary>
        public static HandShake Instance => SingletonInstance.Value;

        public static HandShake Create()
        {
            return new HandShake();
        }

        public uint256 Hash(byte[] input)
        {
            var buffer = input;

            lock (this.hashLock)
            {
                //fromMiner(data) {
                //    const br = bio.read(data);

                //    // Preheader.
                //    this.nonce = br.readU32();
                //    this.time = br.readU64();

                //    const padding = br.readBytes(20);

                //    this.prevBlock = br.readHash();
                //    this.treeRoot = br.readHash();

                //    assert(padding.equals(this.padding(20)));

                //    // Note: mask _hash_.
                //    this._maskHash = br.readHash();

                //    // Subheader.
                //    this.extraNonce = br.readBytes(consensus.NONCE_SIZE);
                //    this.reservedRoot = br.readHash();
                //    this.witnessRoot = br.readHash();
                //    this.merkleRoot = br.readHash();
                //    this.version = br.readU32();
                //    this.bits = br.readU32();

                //    // Mask (unknown).
                //    this.mask = Buffer.alloc(32, 0x00);

                //    return this;
                //}

                //toPrehead() {
                //    const bw = bio.write(128);


                //    bw.writeU32(this.nonce);
                //    bw.writeU64(this.time);
                //    bw.writeBytes(this.padding(20));
                //    bw.writeHash(this.prevBlock);
                //    bw.writeHash(this.treeRoot);
                //    bw.writeHash(this.commitHash());

                //    // Exactly one blake2b block (128 bytes).
                //    assert(bw.offset === BLAKE2b.blockSize);

                //    return bw.render();
                //}
                //var subHeader = buffer.Skip(128).Take(128).ToArray();
                var blake2bConfig = new Blake2BConfig();
                blake2bConfig.OutputSizeInBytes = 32;
                //var subHeaderHash = Blake2B.ComputeHash(subHeader, blake2bConfig);

                //var maskHash = buffer.Skip(96).Take(32).ToArray();
                var prevBlock = buffer.Skip(32).Take(32).ToArray();
                //var commithash = Blake2B.ComputeHash(subHeaderHash.Concat(maskHash).ToArray(), blake2bConfig);

                var data = buffer.Take(128).ToArray();

                StringBuilder hex2 = new StringBuilder(data.Length * 2);
                foreach (byte b in data)
                    hex2.AppendFormat("{0:x2}", b);
                var sfff2 = hex2.ToString();

                //padding(size) {
                //    assert((size >>> 0) === size);

                //    const pad = Buffer.alloc(size);

                //    for (let i = 0; i < size; i++)
                //        pad[i] = this.prevBlock[i % 32] ^ this.treeRoot[i % 32];

                //    return pad;
                //}

                var treeRoot = buffer.Skip(64).Take(32).ToArray();
                var pad8 = new byte[8];
                var pad32 = new byte[32];

                for (int i = 0; i < pad8.Length; i++)
                {
                    pad8[i] = (byte)(prevBlock[i % 32] ^ treeRoot[i % 32]);
                }

                StringBuilder hex2xx = new StringBuilder(pad8.Length * 2);
                foreach (byte b in pad8)
                    hex2xx.AppendFormat("{0:x2}", b);
                var sfff2xxx = hex2xx.ToString();

                for (int i = 0; i < pad32.Length; i++)
                {
                    pad32[i] = (byte)(prevBlock[i % 32] ^ treeRoot[i % 32]);
                }

                StringBuilder hex2xxccc = new StringBuilder(pad32.Length * 2);
                foreach (byte b in pad32)
                    hex2xxccc.AppendFormat("{0:x2}", b);
                var sfff2xxxccc = hex2xxccc.ToString();

                var left = Blake2B.ComputeHash(data);

                StringBuilder hex2x = new StringBuilder(left.Length * 2);
                foreach (byte b in left)
                    hex2x.AppendFormat("{0:x2}", b);
                var sfff2x = hex2x.ToString();

                var right = Sha3.Sha3256().ComputeHash(data.Concat(pad8).ToArray());
                buffer = Blake2B.ComputeHash(left.Concat(pad32).Concat(right).ToArray(), blake2bConfig);
            }

            return new uint256(buffer.Take(32).Reverse().ToArray());
        }
    }
}