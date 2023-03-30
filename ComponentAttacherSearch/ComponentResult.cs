using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComponentAttacherSearch
{
    internal readonly struct ComponentResult
    {
        public CategoryNode<Type> Category { get; }
        public Type Type { get; }

        public ComponentResult(CategoryNode<Type> category, Type type)
        {
            Type = type;
            Category = category;
        }
    }
}