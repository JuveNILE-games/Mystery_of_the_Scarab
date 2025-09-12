using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.ServiceLocator{
    public class ServiceManager{
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        public IEnumerator<object> RegisteredServices => _services.Values.GetEnumerator();

        public bool TryGet<T>(out T service) where T : class{
            Type type = typeof(T);
            if (_services.TryGetValue(type, out var foundService))
            {
                service = foundService as T;
                return service != null;
            }
            service = null;
            return false;
        }
        
        public T Get<T>() where T : class{
            Type type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            throw new ArgumentException($"ServiceManager.Get: Service of type {type.FullName} is not registered.", nameof(T));
        }
        
        public ServiceManager Register<T>(T service){
            Type type = typeof(T);
            if (!_services.TryAdd(type, service))
            {
                Debug.LogError($"ServiceManager.Register: Service of type {type.FullName} is already registered.");
            }
            return this;
        }

        public ServiceManager Register(Type type, object service){
            if (!type.IsInstanceOfType(service))
            {
                throw new ArgumentException(
                    "ServiceManager.Register: type of service does not match type of service interface.",
                    nameof(service));
            }

            if (!_services.TryAdd(type, service))
            {
                Debug.LogError($"ServiceManager.Register: Service of type {type.FullName} is already registered.");
            }

            return this;
        }
    }
}