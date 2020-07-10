// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Validation
{
    /// <summary>
    /// The default implementation of <see cref="IValidationStrategy"/> for a complex object.
    /// </summary>
    internal class DefaultComplexObjectValidationStrategy : IValidationStrategy
    {
        /// <summary>
        /// Gets an instance of <see cref="DefaultComplexObjectValidationStrategy"/>.
        /// </summary>
        public static readonly IValidationStrategy Instance = new DefaultComplexObjectValidationStrategy();

        private DefaultComplexObjectValidationStrategy()
        {
        }

        /// <inheritdoc />
        public IEnumerator<ValidationEntry> GetChildren(
            ModelMetadata metadata,
            string key,
            object model)
        {
            return new Enumerator(metadata, key, model);
        }

        private class Enumerator : IEnumerator<ValidationEntry>
        {
            private readonly string _key;
            private readonly object _model;
            private readonly int _count;
            private readonly ModelMetadata _modelMetadata;
            private readonly IReadOnlyList<ModelMetadata> _parameters;

            private ValidationEntry _entry;
            private int _index;

            public Enumerator(
                ModelMetadata modelMetadata,
                string key,
                object model)
            {
                _modelMetadata = modelMetadata;
                _key = key;
                _model = model;

                _parameters = _modelMetadata.BoundConstructor?.Parameters ?? Array.Empty<ModelMetadata>();
                _count = _modelMetadata.Properties.Count + _parameters.Count;

                _index = -1;
            }

            public ValidationEntry Current => _entry;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index++;
                
                if (_index >= _count)
                {
                    return false;
                }

                if (_index < _parameters.Count)
                {
                    var parameter = _parameters[_index];
                    var parameterName = parameter.BinderModelName ?? parameter.ParameterName;
                    var key = ModelNames.CreatePropertyModelName(_key, parameterName);

                    if (_model is null)
                    {
                        _entry = new ValidationEntry(parameter, key, model: null);
                    }
                    else
                    {
                        if (!_modelMetadata.ParameterMapping.TryGetValue(parameter, out var property))
                        {
                            throw new InvalidOperationException($"No property found that maps to constructor parameter '{parameter}' for type '{_modelMetadata.ModelType}'. " +
                                $"Validation requires that each bound parameter must have a property to read the value.");
                        }

                        _entry = new ValidationEntry(parameter, key, () => GetModel(_model, property));
                    }
                }
                else
                {
                    var property = _modelMetadata.Properties[_index - _parameters.Count];
                    var propertyName = property.BinderModelName ?? property.PropertyName;
                    var key = ModelNames.CreatePropertyModelName(_key, propertyName);

                    if (_model == null)
                    {
                        // Performance: Never create a delegate when container is null.
                        _entry = new ValidationEntry(property, key, model: null);
                    }
                    else
                    {
                        _entry = new ValidationEntry(property, key, () => GetModel(_model, property));
                    }
                }

                return true;
            }

            public void Dispose()
            {
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            private static object GetModel(object container, ModelMetadata property)
            {
                return property.PropertyGetter(container);
            }
        }
    }
}
