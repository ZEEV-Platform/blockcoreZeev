using Blockcore.Consensus.Rules;
using Blockcore.Features.Consensus.Rules.CommonRules;

namespace Blockcore.Networks.ZEEV.Rules
{
    /// <summary>
    /// Checks if <see cref="ZEEVMain"/> network block's header has a valid block version.
    /// </summary>
    public class ZEEVHeaderVersionRule : HeaderVersionRule
    {
        /// <inheritdoc />
        public override void Run(RuleContext context)
        {
        }
    }
}