﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Composite.Functions;
using Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Foundation;


namespace Composite.Plugins.Functions.FunctionProviders.StandardFunctionProvider.Utils.Predicates
{
    internal sealed class StringNoValuePredicateFunction : StandardFunctionBase
    {
        public StringNoValuePredicateFunction(EntityTokenFactory entityTokenFactory)
            : base("StringNoValue", "Composite.Utils.Predicates", typeof(Expression<Func<string, bool>>), entityTokenFactory)
        {
        }


        protected override IEnumerable<StandardFunctionParameterProfile> StandardFunctionParameterProfiles
        {
            get
            {
                yield break;
            }
        }


        public override object Execute(ParameterList parameters, FunctionContextContainer context)
        {
            Expression<Func<string, bool>> predicate = f => f == null;
            return predicate;
        }
    }
}
