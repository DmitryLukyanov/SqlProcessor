using Microsoft.Extensions.DependencyInjection;

namespace UnitTestsGenerator.AI.Agent.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddRangeSingleton(this IServiceCollection serviceCollection, IServiceCollection values)
        {
            foreach (var descriptor in values)
            {
                // TODO: is it safe?
                if (descriptor.ImplementationInstance != null)
                {
                    serviceCollection.AddSingleton(descriptor.ServiceType, descriptor.ImplementationInstance);
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    serviceCollection.AddSingleton(descriptor.ServiceType, descriptor.ImplementationFactory);
                }
                else if (descriptor.ImplementationType != null)
                {
                    serviceCollection.AddSingleton(descriptor.ServiceType, descriptor.ImplementationType);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}
