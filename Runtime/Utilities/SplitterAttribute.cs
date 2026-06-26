using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Utilities
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class SplitterAttribute : PropertyAttribute
    {
        public readonly string Title;

        public SplitterAttribute(string title = "")
        {
            Title = title;
        }
    }
}