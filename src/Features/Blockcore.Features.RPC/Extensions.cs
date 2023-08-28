using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Blockcore.NBitcoin;

namespace Blockcore.Features.RPC
{
    public static class Extensions
    {
        public static IApplicationBuilder UseRPC(this IApplicationBuilder app)
        {
            return app.UseMvc(o =>
            {
                var actionDescriptor = app.ApplicationServices.GetService(typeof(IActionDescriptorCollectionProvider)) as IActionDescriptorCollectionProvider;
                o.Routes.Add(new RPCRouteHandler(o.DefaultHandler, actionDescriptor));
            });
        }

        public static double DifficultySafe(this Target target)
        {
            double difficulty = 0;

            try
            {
                difficulty = target.Difficulty;
            }
            catch (ArithmeticException)
            {
                //Division by zero error
            }
            catch (Exception)
            {
                //Another exception
            }

            return difficulty;
        }
    }
}
