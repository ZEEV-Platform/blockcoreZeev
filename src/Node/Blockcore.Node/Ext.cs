using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Blockcore.Node
{
    internal static class FullNodeExtensions
    {
        public static T NodeController<T>(this IFullNode fullNode, bool failWithDefault = false)
        {
            foreach (ConstructorInfo ci in typeof(T).GetConstructors())
            {
                ParameterInfo[] paramInfo = ci.GetParameters();

                var parameters = new object[paramInfo.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = fullNode.Services.ServiceProvider.GetService(paramInfo[i].ParameterType) ?? paramInfo[i].DefaultValue;
                    if (parameters[i] == null && !paramInfo[i].HasDefaultValue)
                    {
                        if (failWithDefault)
                            return default(T);

                        throw new InvalidOperationException($"The {typeof(T).ToString()} controller constructor can't resolve {paramInfo[i].ParameterType.ToString()}");
                    }
                }

                return (T)ci.Invoke(parameters);
            }

            if (failWithDefault)
                return default(T);

            throw new InvalidOperationException($"The {typeof(T).ToString()} controller has no constructor");
        }
    }
}
