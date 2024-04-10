using Blockcore.NBitcoin;

namespace Blockcore.Features.Wallet.Api.Models
{
    public class CoinKey
    {
        public CoinKey(ScriptCoin scriptCoin, ISecret secret)
        {
            this.ScriptCoin = scriptCoin;
            this.Secret = secret;
        }

        public ScriptCoin ScriptCoin { get; }
        public ISecret Secret { get; }
    }
}
