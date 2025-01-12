﻿using System.Reflection;
using Autofac.Core.Registration;
using Autofac.Core;
using Autofac;
using Smartstore.ComponentModel;
using Smartstore.Data;
using Autofac.Core.Resolving.Pipeline;

namespace Smartstore.Core.Bootstrapping
{
    internal class CommonServicesModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CommonServices>().As<ICommonServices>().InstancePerLifetimeScope();
        }

        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistry, IComponentRegistration registration)
        {
            // Look for first settable property of type "ICommonServices" and inject
            var servicesProperty = FindCommonServicesProperty(registration.Activator.LimitType);

            if (servicesProperty == null)
                return;

            registration.Metadata.Add("Property.ICommonServices", FastProperty.Create(servicesProperty));

            registration.PipelineBuilding += (sender, pipeline) =>
            {
                // Add our CommonServices middleware to the pipeline.
                pipeline.Use(PipelinePhase.ParameterSelection, (context, next) =>
                {
                    next(context);

                    if (!DataSettings.DatabaseIsInstalled())
                    {
                        return;
                    }

                    if (!context.NewInstanceActivated || context.Registration.Metadata.Get("Property.ICommonServices") is not FastProperty prop)
                    {
                        return;
                    }

                    try
                    {
                        var services = context.Resolve<ICommonServices>();
                        prop.SetValue(context.Instance, services);
                    }
                    catch { }
                });
            };
        }

        private static PropertyInfo FindCommonServicesProperty(Type type)
        {
            var prop = type
                .GetProperties(BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new
                {
                    PropertyInfo = p,
                    p.PropertyType,
                    IndexParameters = p.GetIndexParameters(),
                    Accessors = p.GetAccessors(false)
                })
                .Where(x => x.PropertyType == typeof(ICommonServices)) // must be ICommonServices
                .Where(x => x.IndexParameters.Count() == 0) // must not be an indexer
                .Where(x => x.Accessors.Length != 1 || x.Accessors[0].ReturnType == typeof(void)) //must have get/set, or only set
                .Select(x => x.PropertyInfo)
                .FirstOrDefault();

            return prop;
        }
    }
}
