﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlTypes;

using Composite.Functions;

using Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Foundation;
using Composite.Core.ResourceSystem;
using System.Linq.Expressions;

namespace Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Utils.Predicates
{
    internal sealed class StringEndsWithPredicateFunction : StandardFunctionBase
    {
        public StringEndsWithPredicateFunction(EntityTokenFactory entityTokenFactory)
            : base("StringEndsWith", "Composite.Utils.Predicates", typeof(Expression<Func<string, bool>>), entityTokenFactory)
        {
        }


        protected override IEnumerable<StandardFunctionParameterProfile> StandardFunctionParameterProfiles
        {
            get
            {
                WidgetFunctionProvider textboxWidget = StandardWidgetFunctions.TextBoxWidget;

                yield return new StandardFunctionParameterProfile(
                                    "Value", typeof(string), true, new ConstantValueProvider(""), textboxWidget);
            }
        }


        public override object Execute(ParameterList parameters, FunctionContextContainer context)
        {
            string valueToFind = parameters.GetParameter<string>("Value");
            Expression<Func<string,bool>> predicate = f=>f.EndsWith(valueToFind);
            return predicate;
        }
    }
}
