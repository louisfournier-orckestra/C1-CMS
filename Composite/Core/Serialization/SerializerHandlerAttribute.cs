﻿using System;


namespace Composite.Core.Serialization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    internal sealed class SerializerHandlerAttribute : Attribute
    {
        public SerializerHandlerAttribute(Type serializerHandlerType)
        {
            this.SerializerHandlerType = serializerHandlerType;
        }


        public Type SerializerHandlerType { get; private set; }
    }
}
