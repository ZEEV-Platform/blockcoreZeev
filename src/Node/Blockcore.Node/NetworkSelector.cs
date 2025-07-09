using Blockcore.Configuration;

namespace Blockcore.Node
{
    public static class NetworkSelector
    {
        public static NodeSettings Create(string chain, string[] args)
        {
            chain = chain.ToUpperInvariant();

            NodeSettings nodeSettings = null;

            switch (chain)
            {
                case "ZEEV":
                    nodeSettings = new NodeSettings(networksSelector: Blockcore.Networks.ZEEV.Networks.ZEEV, args: args);
                    break;
            }

            return nodeSettings;
        }
    }
}