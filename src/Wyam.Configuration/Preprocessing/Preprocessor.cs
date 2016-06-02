﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wyam.Configuration.Assemblies;
using Wyam.Configuration.Directives;
using Wyam.Configuration.NuGet;

namespace Wyam.Configuration.Preprocessing
{
    public class Preprocessor : IPreprocessor
    {
        private static readonly ConcurrentDictionary<string, IDirective> AllDirectives 
            = new ConcurrentDictionary<string, IDirective>(StringComparer.OrdinalIgnoreCase);

        private readonly List<DirectiveValue> _values = new List<DirectiveValue>();

        static Preprocessor()
        {
            AddDirective(new NuGetDirective());
            AddDirective(new NuGetSourceDirective());
            AddDirective(new AssemblyDirective());
            AddDirective(new AssemblyNameDirective());
            AddDirective(new RecipeDirective());
        }

        private static void AddDirective(IDirective directive)
        {
            if (string.IsNullOrEmpty(directive.Name))
            {
                throw new ArgumentException($"The directive {directive.GetType().Name} must have a name");
            }
            if (string.IsNullOrEmpty(directive.Description))
            {
                throw new ArgumentException($"The directive {directive.GetType().Name} must have a description");
            }

            AllDirectives.TryAdd(directive.Name, directive);
            if (!string.IsNullOrEmpty(directive.ShortName))
            {
                AllDirectives.TryAdd(directive.ShortName, directive);
            }
        }
        
        public bool ContainsDirective(string name) => AllDirectives.ContainsKey(name);

        public IEnumerable<IDirective> Directives => AllDirectives.Values.Distinct();

        /// <summary>
        /// Adds values that will be persistent from one configuration to the next.
        /// </summary>
        public void AddValue(DirectiveValue value) => _values.Add(value);

        /// <summary>
        /// Processes both directives that were added to the preprocessor plus any additional ones passed in.
        /// </summary>
        internal void ProcessDirectives(Configurator configurator, IEnumerable<DirectiveValue> additionalValues)
        {
            HashSet<string> singleValueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DirectiveValue value in _values.Concat(additionalValues))
            {
                IDirective directive;
                if (AllDirectives.TryGetValue(value.Name, out directive))
                {
                    // Make sure this isn't an extra value for a single-value directive
                    if (!directive.SupportsMultiple && !singleValueNames.Add(value.Name))
                    {
                        string line = value.Line.HasValue ? (" on line " + value.Line.Value) : string.Empty;
                        throw new Exception($"Error while processing directive{line}: #{value.Name} {value.Value}{Environment.NewLine}"
                            + "Directive was previously specified and only one value is allowed");
                    }

                    // Process the directive
                    try
                    {
                        directive.Process(configurator, value.Value);
                    }
                    catch (Exception ex)
                    {
                        string line = value.Line.HasValue ? (" on line " + value.Line.Value) : string.Empty;
                        throw new Exception($"Error while processing directive{line}: #{value.Name} {value.Value}{Environment.NewLine}{ex}");
                    }
                }
            }
        }
    }
}
