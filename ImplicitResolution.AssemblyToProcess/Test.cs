using System;
using System.Collections.Generic;
using System.Linq;
using ImplicitResolution.Fody;

namespace ImplicitResolution.AssemblyToProcess
{
    [Typeclass]
    public interface IShowable<T>
    {
        string Show(T that);
    }

    public struct StrShowable : IShowable<string>
    {
        public string Show(string that) => $"\"{that}\"";
    }
    
    public struct IntShowable : IShowable<int>
    {
        public string Show(int that) => that.ToString();
    }

    public struct ListShowable<T> : IShowable<List<T>>
    {
        // public override string Show() => $"[{string.Join(", ", That.Select(x => Implicitly.Resolve<Showable<T>>(x).Show()))}]";
        public string Show(List<T> that) => "[\"foo\", \"bar\", \"baz\"]";
    }
        
    public class Test
    {
        public bool Run()
        {
            var strv = "Hello!";
            var intv = 1337;

            return "\"Hello!\"" == Implicitly.Resolve<IShowable<string>>().Show(strv) &&
                   "1337" == Implicitly.Resolve<IShowable<int>>().Show(intv);
        }
        
        public bool RunNested()
        {
            var listv = new List<string> {"foo", "bar", "baz"};

            return "[\"foo\", \"bar\", \"baz\"]" == Implicitly.Resolve<IShowable<List<string>>>().Show(listv);
        }
    }
}