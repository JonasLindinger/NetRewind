using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NetRewind.Utils.Simulation.State
{
    public static class StateTypeRegistry
    {
        private static bool _initialized;
        private static readonly Dictionary<Type, ushort> TypeToId = new();
        private static readonly List<Func<IState>> FactoriesById = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            InitializeIfNeeded();
        }

        public static void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            _initialized = true;

            var allMarkedTypes = new List<Type>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    var hasAttr = type.GetCustomAttribute<StateTypeAttribute>() != null;
                    var isIState = typeof(IState).IsAssignableFrom(type);

                    if (hasAttr)
                    {
                        // Rule 1: attribute only allowed on struct + IState
                        if (!type.IsValueType || !isIState)
                        {
                            throw new InvalidOperationException(
                                $"[StateType] can only be applied to structs implementing IState. " +
                                $"Type '{type.FullName}' violates this rule.");
                        }

                        allMarkedTypes.Add(type);
                    }

                    // Rule 2 is handled after the scan
                }
            }

            // Rule 2: every IState must have [StateType]
            var missingAttributeTypes = assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
                })
                .Where(t => t != null
                            && typeof(IState).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.IsValueType
                            && t.GetCustomAttribute<StateTypeAttribute>() == null)
                .ToList();

            if (missingAttributeTypes.Count > 0)
            {
                var list = string.Join("\n - ", missingAttributeTypes.Select(t => t.FullName));
                throw new InvalidOperationException(
                    "The following IState structs are missing the [StateType] attribute:\n - " + list);
            }

            // Now we know:
            // - Any [StateType] is on a struct that implements IState
            // - Every struct IState has [StateType]

            // Deterministic ID assignment
            allMarkedTypes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

            for (ushort i = 0; i < allMarkedTypes.Count; i++)
            {
                var type = allMarkedTypes[i];
                ushort id = i;

                TypeToId[type] = id;
                FactoriesById.Add(() => (IState)Activator.CreateInstance(type));
            }
        }

        public static ushort GetId<T>() where T : IState
        {
            InitializeIfNeeded();

            var type = typeof(T);
            if (!TypeToId.TryGetValue(type, out var id))
                throw new InvalidOperationException(
                    $"Type {type.FullName} is not registered as an IState with [StateType].");
            return id;
        }

        public static IState Create(ushort id)
        {
            InitializeIfNeeded();

            if (id < 0 || id >= FactoriesById.Count)
                return null;

            return FactoriesById[id]();
        }
        
        public static ushort GetId(Type type)
        {
            InitializeIfNeeded();
      
            if (!TypeToId.TryGetValue(type, out var id))
                throw new InvalidOperationException($"Type {type.FullName} not registered.");
      
            return id;
        }
    }
    
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class StateTypeAttribute : Attribute
    {
        
    }
}