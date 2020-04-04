using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register ProtoActor in the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="registerAction">Actor factory props registration</param>
        public static void AddProtoActor(this IServiceCollection services, Action<ActorPropsRegistry> registerAction = null)
        {
            services.AddSingleton<IActorFactory, ActorFactory>();

            var registry = new ActorPropsRegistry();
            registerAction?.Invoke(registry);
            services.AddSingleton(registry);
            services.AddSingleton<ActorSystem>();
        }
    }
}