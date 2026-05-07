using System;
using System.Collections.Generic;

namespace KingCardsSpire.Core
{
    /// <summary>
    /// 全局服务定位器，由各个 Manager 在 Awake 中注册。
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                return;
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class
        {
            Services.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            return Services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        public static void Clear()
        {
            Services.Clear();
        }
    }
}
